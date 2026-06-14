package com.adaskothebeast.typewriter.rider

import com.intellij.openapi.components.service
import com.intellij.openapi.options.SearchableConfigurable
import com.intellij.openapi.project.Project
import java.awt.GridBagConstraints
import java.awt.GridBagLayout
import java.awt.Insets
import javax.swing.JCheckBox
import javax.swing.JComponent
import javax.swing.JLabel
import javax.swing.JPanel
import javax.swing.JTextField

class TypewriterConfigurable(private val project: Project) : SearchableConfigurable {
    private var panel: JPanel? = null
    private lateinit var cliPathField: JTextField
    private lateinit var cliArgumentsField: JTextField
    private lateinit var workspacePathField: JTextField
    private lateinit var projectPathField: JTextField
    private lateinit var templatePathField: JTextField
    private lateinit var frameworkField: JTextField
    private lateinit var allProjectsCheckBox: JCheckBox
    private lateinit var generateOnSaveCheckBox: JCheckBox
    private lateinit var validateOnSaveCheckBox: JCheckBox

    override fun getId(): String = "typewriter"

    override fun getDisplayName(): String = "Typewriter"

    override fun createComponent(): JComponent {
        cliPathField = JTextField()
        cliArgumentsField = JTextField()
        workspacePathField = JTextField()
        projectPathField = JTextField()
        templatePathField = JTextField()
        frameworkField = JTextField()
        allProjectsCheckBox = JCheckBox("Generate all projects")
        generateOnSaveCheckBox = JCheckBox("Generate current template on save")
        validateOnSaveCheckBox = JCheckBox("Validate current template on save")

        val createdPanel = JPanel(GridBagLayout())
        var row = 0
        addRow(createdPanel, row++, "CLI path:", cliPathField)
        addRow(createdPanel, row++, "CLI arguments:", cliArgumentsField)
        addRow(createdPanel, row++, "Workspace path:", workspacePathField)
        addRow(createdPanel, row++, "Project path:", projectPathField)
        addRow(createdPanel, row++, "Template path:", templatePathField)
        addRow(createdPanel, row++, "Target framework:", frameworkField)
        addCheckBox(createdPanel, row++, allProjectsCheckBox)
        addCheckBox(createdPanel, row++, generateOnSaveCheckBox)
        addCheckBox(createdPanel, row++, validateOnSaveCheckBox)
        addFiller(createdPanel, row)

        panel = createdPanel
        reset()
        return createdPanel
    }

    override fun isModified(): Boolean {
        val state = project.service<TypewriterSettingsState>().settings
        return cliPathField.text != state.cliPath ||
            cliArgumentsField.text != state.cliArguments ||
            workspacePathField.text != state.workspacePath ||
            projectPathField.text != state.projectPath ||
            templatePathField.text != state.templatePath ||
            frameworkField.text != state.framework ||
            allProjectsCheckBox.isSelected != state.allProjects ||
            generateOnSaveCheckBox.isSelected != state.generateOnSave ||
            validateOnSaveCheckBox.isSelected != state.validateOnSave
    }

    override fun apply() {
        val state = project.service<TypewriterSettingsState>().settings
        state.cliPath = cliPathField.text.trim()
        state.cliArguments = cliArgumentsField.text.trim()
        state.workspacePath = workspacePathField.text.trim()
        state.projectPath = projectPathField.text.trim()
        state.templatePath = templatePathField.text.trim()
        state.framework = frameworkField.text.trim()
        state.allProjects = allProjectsCheckBox.isSelected
        state.generateOnSave = generateOnSaveCheckBox.isSelected
        state.validateOnSave = validateOnSaveCheckBox.isSelected
    }

    override fun reset() {
        val state = project.service<TypewriterSettingsState>().settings
        cliPathField.text = state.cliPath
        cliArgumentsField.text = state.cliArguments
        workspacePathField.text = state.workspacePath
        projectPathField.text = state.projectPath
        templatePathField.text = state.templatePath
        frameworkField.text = state.framework
        allProjectsCheckBox.isSelected = state.allProjects
        generateOnSaveCheckBox.isSelected = state.generateOnSave
        validateOnSaveCheckBox.isSelected = state.validateOnSave
    }

    override fun disposeUIResources() {
        panel = null
    }

    private fun addRow(panel: JPanel, row: Int, label: String, component: JComponent) {
        panel.add(
            JLabel(label),
            GridBagConstraints().apply {
                gridx = 0
                gridy = row
                anchor = GridBagConstraints.WEST
                insets = Insets(4, 0, 4, 8)
            },
        )
        panel.add(
            component,
            GridBagConstraints().apply {
                gridx = 1
                gridy = row
                weightx = 1.0
                fill = GridBagConstraints.HORIZONTAL
                insets = Insets(4, 0, 4, 0)
            },
        )
    }

    private fun addCheckBox(panel: JPanel, row: Int, checkBox: JCheckBox) {
        panel.add(
            checkBox,
            GridBagConstraints().apply {
                gridx = 0
                gridy = row
                gridwidth = 2
                anchor = GridBagConstraints.WEST
                insets = Insets(4, 0, 4, 0)
            },
        )
    }

    private fun addFiller(panel: JPanel, row: Int) {
        panel.add(
            JPanel(),
            GridBagConstraints().apply {
                gridx = 0
                gridy = row
                gridwidth = 2
                weightx = 1.0
                weighty = 1.0
                fill = GridBagConstraints.BOTH
            },
        )
    }
}
