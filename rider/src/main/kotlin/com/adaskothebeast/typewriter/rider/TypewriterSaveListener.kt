package com.adaskothebeast.typewriter.rider

import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project
import com.intellij.openapi.roots.ProjectFileIndex
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.openapi.vfs.newvfs.BulkFileListener
import com.intellij.openapi.vfs.newvfs.events.VFileContentChangeEvent
import com.intellij.openapi.vfs.newvfs.events.VFileEvent

class TypewriterSaveListener(private val project: Project) : BulkFileListener {
    override fun after(events: List<VFileEvent>) {
        if (project.isDisposed) {
            return
        }

        val settings = project.service<TypewriterSettingsState>().settings
        if (!settings.generateOnSave && !settings.validateOnSave) {
            return
        }

        events
            .filterIsInstance<VFileContentChangeEvent>()
            .mapNotNull { it.file }
            .filter(::isGenerationInputFile)
            .filter { ProjectFileIndex.getInstance(project).isInContent(it) }
            .distinctBy { it.path }
            .forEach { project.service<TypewriterCliService>().handleSavedFile(it) }
    }

    private fun isGenerationInputFile(file: VirtualFile): Boolean {
        if (file.path.split('/', '\\').any { it.lowercase() in ignoredDirectories }) {
            return false
        }

        return !file.extension.isNullOrBlank()
    }

    private companion object {
        val ignoredDirectories = setOf("bin", "obj", "node_modules", "generated")
    }
}
