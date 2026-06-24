package com.adaskothebeast.typewriter.rider

import com.google.gson.JsonParseException
import com.google.gson.JsonParser
import com.intellij.execution.ExecutionException
import com.intellij.execution.configurations.GeneralCommandLine
import com.intellij.execution.process.CapturingProcessHandler
import com.intellij.ide.plugins.PluginManagerCore
import com.intellij.notification.NotificationGroupManager
import com.intellij.notification.NotificationType
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.extensions.PluginId
import com.intellij.openapi.progress.ProgressIndicator
import com.intellij.openapi.progress.Task
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.util.Alarm
import com.intellij.util.execution.ParametersListUtil
import java.io.IOException
import java.nio.charset.StandardCharsets
import java.nio.file.Files
import java.nio.file.Path
import java.nio.file.Paths

@Service(Service.Level.PROJECT)
class TypewriterCliService(private val project: Project) {
    private val saveLock = Any()
    private val saveAlarm = Alarm(Alarm.ThreadToUse.POOLED_THREAD, project)
    private var pendingSaveRequest: SaveRequest? = null
    private var saveRunning = false
    private var lastSaveAt = 0L

    fun generateCurrentTemplate(file: VirtualFile?) {
        executeCurrentTemplate("generate", "Generate current template", file)
    }

    fun validateCurrentTemplate(file: VirtualFile?) {
        executeCurrentTemplate("validate", "Validate current template", file)
    }

    fun generateAllTemplates() {
        execute("generate", "Generate all Typewriter templates", templatePath = null, allTemplates = true)
    }

    fun handleSavedFile(file: VirtualFile) {
        val request = createSaveRequest(file) ?: return
        scheduleSaveRequest(request)
    }

    private fun createSaveRequest(file: VirtualFile): SaveRequest? {
        val settings = project.service<TypewriterSettingsState>().settings
        if (!settings.generateOnSave && !settings.validateOnSave) {
            return null
        }

        val extension = normalizeExtension(file.extension ?: return null).lowercase()
        if (extension !in readConfiguredInputExtensions(file.path)) {
            return null
        }

        if (extension == ".tst") {
            return when {
                settings.generateOnSave -> SaveRequest("generate", "Generate saved Typewriter template", file.path, allTemplates = false, projectPath = null)
                settings.validateOnSave -> SaveRequest("validate", "Validate saved Typewriter template", file.path, allTemplates = false, projectPath = null)
                else -> null
            }
        }

        val projectPath = findNearestProjectPathForInput(file)
        return when {
            settings.generateOnSave -> SaveRequest("generate", "Generate Typewriter templates after save", templatePath = null, allTemplates = true, projectPath = projectPath)
            settings.validateOnSave -> SaveRequest("validate", "Validate Typewriter templates after save", templatePath = null, allTemplates = true, projectPath = projectPath)
            else -> null
        }
    }

    private fun scheduleSaveRequest(request: SaveRequest) {
        synchronized(saveLock) {
            pendingSaveRequest = SaveRequest.merge(pendingSaveRequest, request)
            lastSaveAt = System.currentTimeMillis()
        }

        saveAlarm.cancelAllRequests()
        saveAlarm.addRequest({ processSaveRequests() }, SaveDebounceMillis)
    }

    private fun processSaveRequests() {
        synchronized(saveLock) {
            if (saveRunning) {
                return
            }

            saveRunning = true
        }

        try {
            while (true) {
                waitForSaveQuietPeriod()
                val request = synchronized(saveLock) {
                    val current = pendingSaveRequest
                    pendingSaveRequest = null
                    current
                } ?: return

                runCli(request.command, request.title, request.templatePath, request.allTemplates, request.projectPath)
            }
        } finally {
            synchronized(saveLock) {
                saveRunning = false
                if (pendingSaveRequest != null) {
                    saveAlarm.cancelAllRequests()
                    saveAlarm.addRequest({ processSaveRequests() }, SaveDebounceMillis)
                }
            }
        }
    }

    private fun waitForSaveQuietPeriod() {
        while (true) {
            val remaining = synchronized(saveLock) {
                SaveDebounceMillis - (System.currentTimeMillis() - lastSaveAt)
            }
            if (remaining <= 0L) {
                return
            }

            Thread.sleep(remaining)
        }
    }

    private fun executeCurrentTemplate(command: String, title: String, file: VirtualFile?) {
        val settings = project.service<TypewriterSettingsState>().settings
        val workspacePath = resolveWorkspacePath(file?.path, settings.workspacePath)
        val configuredTemplatePath = workspacePath?.let {
            resolveOptionalPath(settings.templatePath, getPathBase(it).toString())
        }
        val templatePath = file?.path ?: configuredTemplatePath
        if (templatePath.isNullOrBlank()) {
            notify("Typewriter", "Open a .tst file or configure Typewriter template path.", NotificationType.WARNING)
            return
        }

        execute(command, title, templatePath, allTemplates = false)
    }

    private fun execute(
        command: String,
        title: String,
        templatePath: String?,
        allTemplates: Boolean,
        projectPathOverride: String? = null,
    ) {
        object : Task.Backgroundable(project, "Typewriter: $title", false) {
            override fun run(indicator: ProgressIndicator) {
                indicator.text = "Running Typewriter CLI"
                runCli(command, title, templatePath, allTemplates, projectPathOverride)
            }
        }.queue()
    }

    private fun runCli(
        command: String,
        title: String,
        templatePath: String?,
        allTemplates: Boolean,
        projectPathOverride: String? = null,
    ) {
        val settings = project.service<TypewriterSettingsState>().settings
        val workspacePath = resolveWorkspacePath(templatePath ?: projectPathOverride, settings.workspacePath)
        if (workspacePath.isNullOrBlank()) {
            notify("Typewriter", "Typewriter requires an open project or configured workspace path.", NotificationType.ERROR)
            return
        }

        try {
            val workingDirectory = getPathBase(workspacePath).toString()
            val invocation = resolveCliInvocation(settings, workingDirectory)
            val arguments = invocation.arguments.toMutableList()
            arguments += command
            arguments += "--workspace"
            arguments += workspacePath
            arguments += "--output"
            arguments += "text"

            val projectPath = resolveOptionalPath(settings.projectPath, getPathBase(workspacePath).toString()) ?: projectPathOverride
            if (!projectPath.isNullOrBlank()) {
                arguments += "--project"
                arguments += projectPath
            }

            if (!templatePath.isNullOrBlank() && !allTemplates) {
                arguments += "--template"
                arguments += templatePath
            }

            if (settings.framework.isNotBlank()) {
                arguments += "--framework"
                arguments += settings.framework
            }

            if (allTemplates && settings.allProjects) {
                arguments += "--all-projects"
            }

            val commandLine = GeneralCommandLine(invocation.command)
                .withParameters(arguments)
                .withWorkDirectory(workingDirectory)
                .withCharset(StandardCharsets.UTF_8)
            val output = CapturingProcessHandler(commandLine).runProcess(ProcessTimeoutMillis)
            val message = summarize(output.stdout, output.stderr, output.isTimeout)
            when {
                output.isTimeout -> notify(title, message, NotificationType.ERROR)
                output.exitCode == 0 -> notify(title, message.ifBlank { "Typewriter completed." }, NotificationType.INFORMATION)
                else -> notify(title, message.ifBlank { "Typewriter CLI failed with exit code ${output.exitCode}." }, NotificationType.WARNING)
            }
        } catch (ex: ExecutionException) {
            notify(title, ex.message ?: "Unable to start Typewriter CLI.", NotificationType.ERROR)
        } catch (ex: RuntimeException) {
            notify(title, ex.message ?: "Typewriter CLI failed.", NotificationType.ERROR)
        }
    }

    private fun resolveCliInvocation(settings: TypewriterSettingsState.Settings, workingDirectory: String): CliInvocation {
        if (settings.cliPath.isNotBlank()) {
            return CliInvocation(settings.cliPath, parseArguments(settings.cliArguments))
        }

        val localCliProject = findLocalCliProject(project.basePath) ?: findLocalCliProject(workingDirectory)
        if (localCliProject != null) {
            return CliInvocation("dotnet", listOf("run", "--project", localCliProject, "--"))
        }

        val packagedCliInvocation = findPackagedCliInvocation()
        if (packagedCliInvocation != null) {
            return packagedCliInvocation
        }

        return CliInvocation("typewriter", emptyList())
    }

    private fun parseArguments(value: String): List<String> =
        if (value.isBlank()) {
            emptyList()
        } else {
            ParametersListUtil.parse(value)
        }

    private fun findLocalCliProject(startPath: String?): String? {
        if (startPath.isNullOrBlank()) {
            return null
        }

        val start = Paths.get(startPath)
        val candidates = listOf(
            start.resolve("src/Typewriter.Cli/Typewriter.Cli.csproj"),
            start.resolve("../src/Typewriter.Cli/Typewriter.Cli.csproj"),
        )

        return candidates
            .map { it.normalize() }
            .firstOrNull { Files.exists(it) }
            ?.toString()
    }

    private fun findPackagedCliInvocation(): CliInvocation? {
        val plugin = PluginManagerCore.getPlugin(PluginId.getId(PluginIdValue)) ?: return null
        val cliAssembly = plugin.pluginPath
            .resolve("tools")
            .resolve("typewriter-cli")
            .resolve("Typewriter.Cli.dll")

        return if (Files.isRegularFile(cliAssembly)) {
            CliInvocation("dotnet", listOf(cliAssembly.toString()))
        } else {
            null
        }
    }

    private fun resolveWorkspacePath(templatePath: String?, configuredWorkspacePath: String): String? {
        if (configuredWorkspacePath.isNotBlank()) {
            val base = project.basePath ?: templatePath?.let { Paths.get(it).parent?.toString() } ?: "."
            return resolveOptionalPath(configuredWorkspacePath, base)
        }

        return project.basePath ?: templatePath?.let { Paths.get(it).parent?.toString() }
    }

    private fun resolveOptionalPath(value: String, basePath: String): String? {
        if (value.isBlank()) {
            return null
        }

        val path = Paths.get(value)
        return if (path.isAbsolute) {
            path.normalize().toString()
        } else {
            Paths.get(basePath).resolve(path).normalize().toString()
        }
    }

    private fun getPathBase(value: String): Path {
        val path = Paths.get(value)
        return when {
            Files.isDirectory(path) -> path
            path.parent != null -> path.parent
            else -> path
        }
    }

    private fun findNearestProjectPathForInput(file: VirtualFile): String? {
        if (file.extension.equals("csproj", ignoreCase = true)) {
            return file.path
        }

        var directory = Paths.get(file.path).parent
        while (directory != null) {
            val projectFile = directory.toFile()
                .listFiles { candidate -> candidate.isFile && candidate.name.endsWith(".csproj", ignoreCase = true) }
                ?.sortedBy { it.name.lowercase() }
                ?.firstOrNull()
            if (projectFile != null) {
                return projectFile.toPath().toString()
            }

            directory = directory.parent
        }

        return null
    }

    private fun readConfiguredInputExtensions(filePath: String): Set<String> {
        var extensions = DefaultInputExtensions
        for (configurationPath in findConfigurationFiles(filePath)) {
            val configuredExtensions = readInputExtensions(configurationPath)
            if (configuredExtensions != null) {
                extensions = configuredExtensions
            }
        }

        return extensions.map { it.lowercase() }.toSet()
    }

    private fun findConfigurationFiles(filePath: String): List<Path> {
        var directory: Path? = Paths.get(filePath)
        if (directory != null && !Files.isDirectory(directory)) {
            directory = directory?.parent
        }

        val directories = mutableListOf<Path>()
        while (directory != null) {
            directories.add(directory)
            directory = directory.parent
        }

        return directories
            .asReversed()
            .flatMap { candidateDirectory -> ConfigurationFileNames.map { candidateDirectory.resolve(it) } }
            .filter { Files.exists(it) }
    }

    private fun readInputExtensions(configurationPath: Path): Set<String>? {
        return try {
            val root = Files.newBufferedReader(configurationPath).use(JsonParser::parseReader)
            if (!root.isJsonObject) {
                return null
            }

            val inputExtensions = root.asJsonObject
                .entrySet()
                .firstOrNull { it.key.equals("inputExtensions", ignoreCase = true) }
                ?.value
                ?.takeIf { it.isJsonArray }
                ?: return null
            val extensions = inputExtensions.asJsonArray
                .asSequence()
                .filter { it.isJsonPrimitive && it.asJsonPrimitive.isString }
                .map { normalizeExtension(it.asString) }
                .filter { it.isNotBlank() }
                .toSet()

            extensions.ifEmpty { DefaultInputExtensions }
        } catch (_: IOException) {
            null
        } catch (_: JsonParseException) {
            null
        }
    }

    private fun normalizeExtension(extension: String): String {
        val trimmed = extension.trim()
        return when {
            trimmed.isBlank() -> ""
            trimmed.startsWith(".") -> trimmed
            else -> ".$trimmed"
        }
    }

    private fun summarize(stdout: String, stderr: String, timeout: Boolean): String {
        val prefix = if (timeout) "Typewriter CLI timed out.\n" else ""
        val text = listOf(stdout.trim(), stderr.trim()).filter { it.isNotBlank() }.joinToString("\n")
        val summary = prefix + text
        return if (summary.length <= MaxNotificationLength) {
            summary
        } else {
            summary.take(MaxNotificationLength) + "..."
        }
    }

    private fun notify(title: String, message: String, type: NotificationType) {
        NotificationGroupManager.getInstance()
            .getNotificationGroup(NotificationGroupId)
            .createNotification(title, message, type)
            .notify(project)
    }

    private data class CliInvocation(val command: String, val arguments: List<String>)
    private data class SaveRequest(
        val command: String,
        val title: String,
        val templatePath: String?,
        val allTemplates: Boolean,
        val projectPath: String?,
    ) {
        companion object {
            fun merge(existing: SaveRequest?, incoming: SaveRequest): SaveRequest =
                if (existing?.allTemplates == true && !incoming.allTemplates) {
                    existing
                } else {
                    incoming
                }
        }
    }

    private companion object {
        const val PluginIdValue = "com.adaskothebeast.typewriter"
        const val NotificationGroupId = "Typewriter"
        const val ProcessTimeoutMillis = 300_000
        const val MaxNotificationLength = 1_000
        const val SaveDebounceMillis = 300
        val ConfigurationFileNames = listOf("typewriter.json", "typewriter.config.json", ".typewriterrc.json")
        val DefaultInputExtensions = setOf(".cs", ".csproj", ".json", ".props", ".sln", ".slnx", ".targets", ".tst")
    }
}
