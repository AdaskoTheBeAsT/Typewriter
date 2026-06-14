package com.adaskothebeast.typewriter.rider

import com.intellij.lexer.Lexer
import com.intellij.openapi.editor.DefaultLanguageHighlighterColors
import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.openapi.fileTypes.SyntaxHighlighter
import com.intellij.openapi.fileTypes.SyntaxHighlighterFactory
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.psi.tree.IElementType

class TypewriterSyntaxHighlighter : SyntaxHighlighter {
    override fun getHighlightingLexer(): Lexer = TypewriterHighlightingLexer()

    override fun getTokenHighlights(tokenType: IElementType): Array<TextAttributesKey> =
        when (tokenType) {
            TypewriterTokenTypes.DIRECTIVE -> DIRECTIVE_KEYS
            TypewriterTokenTypes.TEMPLATE_TOKEN -> TEMPLATE_TOKEN_KEYS
            TypewriterTokenTypes.HELPER_BLOCK -> HELPER_BLOCK_KEYS
            TypewriterTokenTypes.STRING -> STRING_KEYS
            TypewriterTokenTypes.NUMBER -> NUMBER_KEYS
            TypewriterTokenTypes.COMMENT -> COMMENT_KEYS
            TypewriterTokenTypes.DELIMITER -> DELIMITER_KEYS
            else -> EMPTY_KEYS
        }

    companion object {
        val DIRECTIVE: TextAttributesKey = TextAttributesKey.createTextAttributesKey(
            "TYPEWRITER_DIRECTIVE",
            DefaultLanguageHighlighterColors.KEYWORD,
        )
        val TEMPLATE_TOKEN: TextAttributesKey = TextAttributesKey.createTextAttributesKey(
            "TYPEWRITER_TEMPLATE_TOKEN",
            DefaultLanguageHighlighterColors.INSTANCE_METHOD,
        )
        val HELPER_BLOCK: TextAttributesKey = TextAttributesKey.createTextAttributesKey(
            "TYPEWRITER_HELPER_BLOCK",
            DefaultLanguageHighlighterColors.BLOCK_COMMENT,
        )
        val STRING: TextAttributesKey = TextAttributesKey.createTextAttributesKey(
            "TYPEWRITER_STRING",
            DefaultLanguageHighlighterColors.STRING,
        )
        val NUMBER: TextAttributesKey = TextAttributesKey.createTextAttributesKey(
            "TYPEWRITER_NUMBER",
            DefaultLanguageHighlighterColors.NUMBER,
        )
        val COMMENT: TextAttributesKey = TextAttributesKey.createTextAttributesKey(
            "TYPEWRITER_COMMENT",
            DefaultLanguageHighlighterColors.LINE_COMMENT,
        )
        val DELIMITER: TextAttributesKey = TextAttributesKey.createTextAttributesKey(
            "TYPEWRITER_DELIMITER",
            DefaultLanguageHighlighterColors.BRACES,
        )

        private val EMPTY_KEYS = emptyArray<TextAttributesKey>()
        private val DIRECTIVE_KEYS = arrayOf(DIRECTIVE)
        private val TEMPLATE_TOKEN_KEYS = arrayOf(TEMPLATE_TOKEN)
        private val HELPER_BLOCK_KEYS = arrayOf(HELPER_BLOCK)
        private val STRING_KEYS = arrayOf(STRING)
        private val NUMBER_KEYS = arrayOf(NUMBER)
        private val COMMENT_KEYS = arrayOf(COMMENT)
        private val DELIMITER_KEYS = arrayOf(DELIMITER)
    }
}

class TypewriterSyntaxHighlighterFactory : SyntaxHighlighterFactory() {
    override fun getSyntaxHighlighter(project: Project?, virtualFile: VirtualFile?): SyntaxHighlighter =
        TypewriterSyntaxHighlighter()
}
