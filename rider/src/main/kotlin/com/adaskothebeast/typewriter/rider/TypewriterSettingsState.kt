package com.adaskothebeast.typewriter.rider

import com.intellij.openapi.components.PersistentStateComponent
import com.intellij.openapi.components.State
import com.intellij.openapi.components.Storage
import com.intellij.openapi.components.StoragePathMacros

@State(name = "TypewriterSettings", storages = [Storage(StoragePathMacros.WORKSPACE_FILE)])
class TypewriterSettingsState : PersistentStateComponent<TypewriterSettingsState.Settings> {
    var settings: Settings = Settings()

    override fun getState(): Settings = settings

    override fun loadState(state: Settings) {
        settings = state
    }

    data class Settings(
        var cliPath: String = "",
        var cliArguments: String = "",
        var workspacePath: String = "",
        var projectPath: String = "",
        var templatePath: String = "",
        var framework: String = "",
        var allProjects: Boolean = false,
        var generateOnSave: Boolean = true,
        var validateOnSave: Boolean = true,
    )
}
