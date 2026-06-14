package com.adaskothebeast.typewriter.rider

import com.intellij.icons.AllIcons
import com.intellij.openapi.fileTypes.LanguageFileType
import javax.swing.Icon

class TypewriterFileType private constructor() : LanguageFileType(TypewriterLanguage) {
    override fun getName(): String = "Typewriter"

    override fun getDescription(): String = "Typewriter template"

    override fun getDefaultExtension(): String = "tst"

    override fun getIcon(): Icon = AllIcons.FileTypes.Text

    companion object {
        @JvmField
        val INSTANCE = TypewriterFileType()
    }
}
