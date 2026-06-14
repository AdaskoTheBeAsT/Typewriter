package com.adaskothebeast.typewriter.rider

import com.intellij.psi.tree.IElementType

object TypewriterTokenTypes {
    @JvmField
    val DIRECTIVE = IElementType("TYPEWRITER_DIRECTIVE", TypewriterLanguage)

    @JvmField
    val TEMPLATE_TOKEN = IElementType("TYPEWRITER_TEMPLATE_TOKEN", TypewriterLanguage)

    @JvmField
    val HELPER_BLOCK = IElementType("TYPEWRITER_HELPER_BLOCK", TypewriterLanguage)

    @JvmField
    val STRING = IElementType("TYPEWRITER_STRING", TypewriterLanguage)

    @JvmField
    val NUMBER = IElementType("TYPEWRITER_NUMBER", TypewriterLanguage)

    @JvmField
    val COMMENT = IElementType("TYPEWRITER_COMMENT", TypewriterLanguage)

    @JvmField
    val DELIMITER = IElementType("TYPEWRITER_DELIMITER", TypewriterLanguage)

    @JvmField
    val TEXT = IElementType("TYPEWRITER_TEXT", TypewriterLanguage)
}
