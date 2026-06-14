package com.adaskothebeast.typewriter.rider

import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.CommonDataKeys
import com.intellij.openapi.components.service
import com.intellij.openapi.project.DumbAwareAction
import com.intellij.openapi.vfs.VirtualFile

class TypewriterGenerateCurrentAction : DumbAwareAction() {
    override fun update(event: AnActionEvent) {
        event.presentation.isEnabledAndVisible = event.project != null && hasCurrentTemplateOrFallback(event)
    }

    override fun actionPerformed(event: AnActionEvent) {
        event.project
            ?.service<TypewriterCliService>()
            ?.generateCurrentTemplate(currentTypewriterFile(event))
    }
}

class TypewriterGenerateAllAction : DumbAwareAction() {
    override fun update(event: AnActionEvent) {
        event.presentation.isEnabledAndVisible = event.project != null
    }

    override fun actionPerformed(event: AnActionEvent) {
        event.project
            ?.service<TypewriterCliService>()
            ?.generateAllTemplates()
    }
}

class TypewriterValidateCurrentAction : DumbAwareAction() {
    override fun update(event: AnActionEvent) {
        event.presentation.isEnabledAndVisible = event.project != null && hasCurrentTemplateOrFallback(event)
    }

    override fun actionPerformed(event: AnActionEvent) {
        event.project
            ?.service<TypewriterCliService>()
            ?.validateCurrentTemplate(currentTypewriterFile(event))
    }
}

private fun hasCurrentTemplateOrFallback(event: AnActionEvent): Boolean {
    val project = event.project ?: return false
    return currentTypewriterFile(event) != null ||
        project.service<TypewriterSettingsState>().settings.templatePath.isNotBlank()
}

private fun currentTypewriterFile(event: AnActionEvent): VirtualFile? {
    val file = event.getData(CommonDataKeys.VIRTUAL_FILE)
        ?: event.getData(CommonDataKeys.PSI_FILE)?.virtualFile
    return file?.takeIf { it.extension.equals("tst", ignoreCase = true) }
}
