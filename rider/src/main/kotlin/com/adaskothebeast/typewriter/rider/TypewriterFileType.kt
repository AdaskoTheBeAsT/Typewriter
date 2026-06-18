package com.adaskothebeast.typewriter.rider

import com.intellij.openapi.fileTypes.LanguageFileType
import com.intellij.openapi.util.IconLoader
import javax.swing.Icon

class TypewriterFileType private constructor() : LanguageFileType(TypewriterLanguage) {
    override fun getName(): String = "Typewriter"

    override fun getDescription(): String = "Typewriter template"

    override fun getDefaultExtension(): String = "tst"

    override fun getIcon(): Icon = IconLoader.getIcon("/icons/typewriter.svg", TypewriterFileType::class.java)

    companion object {
        @JvmField
        val INSTANCE = TypewriterFileType()
    }
}
