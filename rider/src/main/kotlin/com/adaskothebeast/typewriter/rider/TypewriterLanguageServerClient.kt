package com.adaskothebeast.typewriter.rider

import com.google.gson.Gson
import com.google.gson.JsonObject
import com.google.gson.JsonParser
import com.intellij.ide.plugins.PluginManagerCore
import com.intellij.openapi.Disposable
import com.intellij.openapi.components.Service
import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.extensions.PluginId
import com.intellij.openapi.project.Project
import java.io.BufferedInputStream
import java.io.BufferedOutputStream
import java.io.EOFException
import java.nio.charset.StandardCharsets
import java.nio.file.Files
import java.nio.file.Path
import java.nio.file.Paths
import java.util.concurrent.atomic.AtomicLong

@Service(Service.Level.PROJECT)
class TypewriterLanguageServerClient(private val project: Project) : Disposable {
    private val lock = Any()
    private val gson = Gson()
    private val nextRequestId = AtomicLong(1)
    private var process: Process? = null
    private var input: BufferedInputStream? = null
    private var output: BufferedOutputStream? = null

    internal fun tryGenerate(request: PersistentGenerationRequest, workingDirectory: String): JsonObject? =
        synchronized(lock) {
            try {
                ensureStarted(workingDirectory, request.workspacePath)
                sendRequest("typewriter/generate", gson.toJsonTree(request).asJsonObject)
            } catch (exception: Exception) {
                if (exception is InterruptedException) {
                    Thread.currentThread().interrupt()
                }

                logger.warn("Persistent Typewriter generation failed; falling back to the CLI.", exception)
                reset()
                null
            }
        }

    override fun dispose() {
        synchronized(lock) {
            try {
                if (process?.isAlive == true) {
                    sendRequest("shutdown", JsonObject(), RequestTimeoutMillis)
                    sendNotification("exit", JsonObject())
                }
            } catch (exception: Exception) {
                if (exception is InterruptedException) {
                    Thread.currentThread().interrupt()
                }

                logger.debug("Unable to shut down the Typewriter language server cleanly.", exception)
            } finally {
                reset()
            }
        }
    }

    private fun ensureStarted(workingDirectory: String, workspacePath: String) {
        if (process?.isAlive == true && input != null && output != null) {
            return
        }

        reset()
        val invocation = resolveLanguageServerInvocation(workingDirectory)
        val startedProcess = ProcessBuilder(listOf(invocation.command) + invocation.arguments)
            .directory(Paths.get(workingDirectory).toFile())
            .start()
        process = startedProcess
        input = BufferedInputStream(startedProcess.inputStream)
        output = BufferedOutputStream(startedProcess.outputStream)
        drainErrorStream(startedProcess)

        val initializationOptions = JsonObject().apply {
            addProperty("workspacePath", workspacePath)
        }
        val initializeParameters = JsonObject().apply {
            addProperty("rootUri", Paths.get(workspacePath).toUri().toString())
            add("initializationOptions", initializationOptions)
        }
        sendRequest("initialize", initializeParameters)
        sendNotification("initialized", JsonObject())
    }

    private fun sendRequest(
        method: String,
        parameters: JsonObject,
        timeoutMillis: Long = RequestTimeoutMillis,
    ): JsonObject {
        val id = nextRequestId.getAndIncrement()
        val request = JsonObject().apply {
            addProperty("jsonrpc", "2.0")
            addProperty("id", id)
            addProperty("method", method)
            add("params", parameters)
        }
        writeMessage(request)

        val deadline = System.currentTimeMillis() + timeoutMillis
        while (true) {
            val response = readMessage(deadline)
            if (!response.has("id") || response.get("id").asLong != id) {
                continue
            }

            if (response.has("error")) {
                throw IllegalStateException(response.getAsJsonObject("error").get("message")?.asString ?: "Language server request failed.")
            }

            val result = response.get("result")
            return if (result == null || result.isJsonNull) JsonObject() else result.asJsonObject
        }
    }

    private fun sendNotification(method: String, parameters: JsonObject) {
        writeMessage(
            JsonObject().apply {
                addProperty("jsonrpc", "2.0")
                addProperty("method", method)
                add("params", parameters)
            },
        )
    }

    private fun writeMessage(message: JsonObject) {
        val content = gson.toJson(message).toByteArray(StandardCharsets.UTF_8)
        val header = "Content-Length: ${content.size}\r\n\r\n".toByteArray(StandardCharsets.US_ASCII)
        val stream = output ?: throw IllegalStateException("Typewriter language server is not connected.")
        stream.write(header)
        stream.write(content)
        stream.flush()
    }

    private fun readMessage(deadline: Long): JsonObject {
        val stream = input ?: throw IllegalStateException("Typewriter language server is not connected.")
        waitForInput(stream, deadline)
        var contentLength: Int? = null
        while (true) {
            val line = readHeaderLine(stream)
            if (line.isEmpty()) {
                break
            }

            val separator = line.indexOf(':')
            if (separator > 0 && line.substring(0, separator).equals("Content-Length", ignoreCase = true)) {
                contentLength = line.substring(separator + 1).trim().toInt()
            }
        }

        val length = contentLength ?: throw IllegalStateException("Language server response did not include Content-Length.")
        val content = stream.readNBytes(length)
        if (content.size != length) {
            throw EOFException("Language server response ended unexpectedly.")
        }

        return JsonParser.parseString(String(content, StandardCharsets.UTF_8)).asJsonObject
    }

    private fun waitForInput(stream: BufferedInputStream, deadline: Long) {
        while (stream.available() == 0) {
            val currentProcess = process
            if (currentProcess == null || !currentProcess.isAlive) {
                throw IllegalStateException("Typewriter language server exited unexpectedly.")
            }

            if (System.currentTimeMillis() >= deadline) {
                throw IllegalStateException("Typewriter language server request timed out.")
            }

            Thread.sleep(PollDelayMillis)
        }
    }

    private fun readHeaderLine(stream: BufferedInputStream): String {
        val bytes = mutableListOf<Byte>()
        while (true) {
            val value = stream.read()
            if (value < 0) {
                throw EOFException("Language server response ended while reading headers.")
            }

            if (value == '\n'.code) {
                return String(bytes.toByteArray(), StandardCharsets.US_ASCII)
            }

            if (value != '\r'.code) {
                bytes.add(value.toByte())
            }
        }
    }

    private fun drainErrorStream(startedProcess: Process) {
        Thread(
            {
                startedProcess.errorStream.bufferedReader(StandardCharsets.UTF_8).useLines { lines ->
                    lines.filter { it.isNotBlank() }.forEach { logger.info("Typewriter language server: $it") }
                }
            },
            "Typewriter language server stderr",
        ).apply {
            isDaemon = true
            start()
        }
    }

    private fun resolveLanguageServerInvocation(workingDirectory: String): ServerInvocation {
        val localProject = findLocalLanguageServerProject(project.basePath)
            ?: findLocalLanguageServerProject(workingDirectory)
        if (localProject != null) {
            val assembly = findBuiltLanguageServerAssembly(localProject)
            return if (assembly != null) {
                ServerInvocation("dotnet", listOf(assembly))
            } else {
                ServerInvocation("dotnet", listOf("run", "--project", localProject, "--no-launch-profile", "--"))
            }
        }

        val plugin = PluginManagerCore.getPlugin(PluginId.getId(PluginIdValue))
        val packagedAssembly = plugin?.pluginPath
            ?.resolve("tools")
            ?.resolve("typewriter-lsp")
            ?.resolve("Typewriter.LanguageServer.dll")
        return if (packagedAssembly != null && Files.isRegularFile(packagedAssembly)) {
            ServerInvocation("dotnet", listOf(packagedAssembly.toString()))
        } else {
            ServerInvocation("typewriter-lsp", emptyList())
        }
    }

    private fun findLocalLanguageServerProject(startPath: String?): String? {
        if (startPath.isNullOrBlank()) {
            return null
        }

        var directory: Path? = if (Files.isDirectory(Paths.get(startPath))) {
            Paths.get(startPath)
        } else {
            Paths.get(startPath).parent
        }
        while (directory != null) {
            val candidate = directory.resolve("src/Typewriter.LanguageServer/Typewriter.LanguageServer.csproj")
            if (Files.isRegularFile(candidate)) {
                return candidate.normalize().toString()
            }

            directory = directory.parent
        }

        return null
    }

    private fun findBuiltLanguageServerAssembly(projectPath: String): String? {
        val outputRoot = Paths.get(projectPath).parent?.resolve("bin") ?: return null
        if (!Files.isDirectory(outputRoot)) {
            return null
        }

        return Files.walk(outputRoot).use { paths ->
            paths
                .filter { Files.isRegularFile(it) && it.fileName.toString() == "Typewriter.LanguageServer.dll" }
                .max(compareBy { Files.getLastModifiedTime(it).toMillis() })
                .orElse(null)
                ?.toString()
        }
    }

    private fun reset() {
        input?.runCatching { close() }
        input = null
        output?.runCatching { close() }
        output = null
        process?.let { currentProcess ->
            if (currentProcess.isAlive) {
                currentProcess.destroy()
                if (currentProcess.isAlive) {
                    currentProcess.destroyForcibly()
                }
            }
        }
        process = null
    }

    internal data class PersistentGenerationRequest(
        val command: String,
        val workspacePath: String,
        val projectPath: String?,
        val templatePath: String?,
        val templateSearchPath: String?,
        val framework: String?,
        val allProjects: Boolean,
    )

    private data class ServerInvocation(val command: String, val arguments: List<String>)

    private companion object {
        const val PluginIdValue = "com.adaskothebeast.typewriter"
        const val PollDelayMillis = 10L
        const val RequestTimeoutMillis = 300_000L
        val logger: Logger = Logger.getInstance(TypewriterLanguageServerClient::class.java)
    }
}
