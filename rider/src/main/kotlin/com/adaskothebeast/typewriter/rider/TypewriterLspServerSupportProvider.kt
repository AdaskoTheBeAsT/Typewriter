package com.adaskothebeast.typewriter.rider

import com.google.gson.JsonObject
import com.intellij.execution.configurations.GeneralCommandLine
import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.platform.lsp.api.LspServerSupportProvider
import com.intellij.platform.lsp.api.ProjectWideLspServerDescriptor

class TypewriterLspServerSupportProvider : LspServerSupportProvider {
    override fun fileOpened(
        project: Project,
        file: VirtualFile,
        serverStarter: LspServerSupportProvider.LspServerStarter,
    ) {
        if (!isTypewriterTemplate(file)) {
            return
        }

        if (!project.service<TypewriterSettingsState>().settings.languageServerEnabled) {
            return
        }

        serverStarter.ensureServerStarted(TypewriterLspServerDescriptor(project))
    }

    internal companion object {
        fun isTypewriterTemplate(file: VirtualFile): Boolean =
            file.extension?.equals("tst", ignoreCase = true) == true
    }
}

private class TypewriterLspServerDescriptor(project: Project) : ProjectWideLspServerDescriptor(project, "Typewriter") {
    override fun isSupportedFile(file: VirtualFile): Boolean =
        TypewriterLspServerSupportProvider.isTypewriterTemplate(file)

    override fun createCommandLine(): GeneralCommandLine {
        val workingDirectory = resolveWorkingDirectory()
        val invocation = TypewriterLanguageServerLocator.resolveInvocation(project.basePath, workingDirectory)
        return GeneralCommandLine(listOf(invocation.command) + invocation.arguments)
            .withWorkDirectory(workingDirectory)
    }

    override fun createInitializationOptions(): Any {
        val settings = project.service<TypewriterSettingsState>().settings
        val options = JsonObject()
        val workspacePath = settings.workspacePath.ifBlank { project.basePath.orEmpty() }
        if (workspacePath.isNotBlank()) {
            options.addProperty("workspacePath", workspacePath)
        }

        if (settings.projectPath.isNotBlank()) {
            options.addProperty("projectPath", settings.projectPath)
        }

        if (settings.framework.isNotBlank()) {
            options.addProperty("framework", settings.framework)
        }

        if (settings.allProjects) {
            options.addProperty("allProjects", true)
        }

        return options
    }

    private fun resolveWorkingDirectory(): String {
        val settings = project.service<TypewriterSettingsState>().settings
        return settings.workspacePath.ifBlank { project.basePath ?: System.getProperty("user.dir") }
    }
}
