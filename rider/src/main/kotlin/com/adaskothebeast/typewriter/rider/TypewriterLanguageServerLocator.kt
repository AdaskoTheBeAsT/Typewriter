package com.adaskothebeast.typewriter.rider

import com.intellij.ide.plugins.PluginManagerCore
import com.intellij.openapi.extensions.PluginId
import java.nio.file.Files
import java.nio.file.Path
import java.nio.file.Paths

internal data class TypewriterServerInvocation(val command: String, val arguments: List<String>)

internal object TypewriterLanguageServerLocator {
    private const val PLUGIN_ID = "com.adaskothebeast.typewriter"

    fun resolveInvocation(projectBasePath: String?, workingDirectory: String): TypewriterServerInvocation {
        val localProject = findLocalLanguageServerProject(projectBasePath)
            ?: findLocalLanguageServerProject(workingDirectory)
        if (localProject != null) {
            val assembly = findBuiltLanguageServerAssembly(localProject)
            return if (assembly != null) {
                TypewriterServerInvocation("dotnet", listOf(assembly))
            } else {
                TypewriterServerInvocation("dotnet", listOf("run", "--project", localProject, "--no-launch-profile", "--"))
            }
        }

        val plugin = PluginManagerCore.getPlugin(PluginId.getId(PLUGIN_ID))
        val packagedAssembly = plugin?.pluginPath
            ?.resolve("tools")
            ?.resolve("typewriter-lsp")
            ?.resolve("Typewriter.LanguageServer.dll")
        return if (packagedAssembly != null && Files.isRegularFile(packagedAssembly)) {
            TypewriterServerInvocation("dotnet", listOf(packagedAssembly.toString()))
        } else {
            TypewriterServerInvocation("typewriter-lsp", emptyList())
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
}
