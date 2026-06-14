package com.adaskothebeast.typewriter.rider

import com.intellij.lexer.LexerBase
import com.intellij.psi.tree.IElementType
import java.util.Locale

class TypewriterHighlightingLexer : LexerBase() {
    private var buffer: CharSequence = ""
    private var startOffset: Int = 0
    private var endOffset: Int = 0
    private var currentState: Int = 0
    private var tokenStart: Int = 0
    private var tokenEnd: Int = 0
    private var tokenType: IElementType? = null

    override fun start(buffer: CharSequence, startOffset: Int, endOffset: Int, initialState: Int) {
        this.buffer = buffer
        this.startOffset = startOffset
        this.endOffset = endOffset
        currentState = initialState
        tokenStart = startOffset
        locateToken()
    }

    override fun getState(): Int = currentState

    override fun getTokenType(): IElementType? = tokenType

    override fun getTokenStart(): Int = tokenStart

    override fun getTokenEnd(): Int = tokenEnd

    override fun advance() {
        tokenStart = tokenEnd
        locateToken()
    }

    override fun getBufferSequence(): CharSequence = buffer

    override fun getBufferEnd(): Int = endOffset

    private fun locateToken() {
        if (tokenStart >= endOffset) {
            tokenType = null
            tokenEnd = tokenStart
            return
        }

        val current = buffer[tokenStart]
        when {
            isDirectiveStart(tokenStart) -> {
                tokenType = TypewriterTokenTypes.DIRECTIVE
                tokenEnd = lineEnd(tokenStart)
            }
            current == '/' && peek(tokenStart + 1) == '/' -> {
                tokenType = TypewriterTokenTypes.COMMENT
                tokenEnd = lineEnd(tokenStart)
            }
            current == '$' -> {
                tokenType = if (peek(tokenStart + 1) == '{') {
                    tokenEnd = scanHelperBlock(tokenStart)
                    TypewriterTokenTypes.HELPER_BLOCK
                } else {
                    tokenEnd = scanTemplateToken(tokenStart)
                    TypewriterTokenTypes.TEMPLATE_TOKEN
                }
            }
            current == '\'' || current == '"' -> {
                tokenType = TypewriterTokenTypes.STRING
                tokenEnd = scanString(tokenStart)
            }
            current.isDigit() -> {
                tokenType = TypewriterTokenTypes.NUMBER
                tokenEnd = scanNumber(tokenStart)
            }
            isDelimiter(current) -> {
                tokenType = TypewriterTokenTypes.DELIMITER
                tokenEnd = tokenStart + 1
            }
            else -> {
                tokenType = TypewriterTokenTypes.TEXT
                tokenEnd = scanText(tokenStart)
            }
        }
    }

    private fun scanTemplateToken(index: Int): Int {
        var current = index + 1
        if (current >= endOffset || !isIdentifierStart(buffer[current])) {
            return index + 1
        }

        current++
        while (current < endOffset && isIdentifierPart(buffer[current])) {
            current++
        }

        return current
    }

    private fun scanHelperBlock(index: Int): Int {
        var current = index + 2
        var depth = 1
        while (current < endOffset) {
            when (buffer[current]) {
                '\'', '"' -> current = scanString(current)
                '{' -> {
                    depth++
                    current++
                }
                '}' -> {
                    depth--
                    current++
                    if (depth == 0) {
                        return current
                    }
                }
                else -> current++
            }
        }

        return endOffset
    }

    private fun scanString(index: Int): Int {
        val quote = buffer[index]
        var current = index + 1
        while (current < endOffset) {
            val ch = buffer[current]
            current++
            if (ch == '\\' && current < endOffset) {
                current++
                continue
            }

            if (ch == quote) {
                break
            }
        }

        return current
    }

    private fun scanNumber(index: Int): Int {
        var current = index + 1
        while (current < endOffset && (buffer[current].isDigit() || buffer[current] == '.')) {
            current++
        }

        return current
    }

    private fun scanText(index: Int): Int {
        var current = index
        while (current < endOffset) {
            val ch = buffer[current]
            if (ch == '$' ||
                ch == '/' ||
                ch == '\'' ||
                ch == '"' ||
                ch.isDigit() ||
                isDelimiter(ch)
            ) {
                break
            }

            current++
        }

        return if (current == index) index + 1 else current
    }

    private fun isDirectiveStart(index: Int): Boolean {
        if (peek(index) != '/' || peek(index + 1) != '/') {
            return false
        }

        var lineStart = index
        while (lineStart > startOffset && buffer[lineStart - 1] != '\n' && buffer[lineStart - 1] != '\r') {
            lineStart--
        }

        var current = lineStart
        while (current < index && buffer[current].isWhitespace() && buffer[current] != '\n' && buffer[current] != '\r') {
            current++
        }

        if (current != index) {
            return false
        }

        val end = minOf(index + DirectivePrefix.length, endOffset)
        return buffer.subSequence(index, end)
            .toString()
            .lowercase(Locale.ROOT)
            .startsWith(DirectivePrefix)
    }

    private fun lineEnd(index: Int): Int {
        var current = index
        while (current < endOffset && buffer[current] != '\n' && buffer[current] != '\r') {
            current++
        }

        return current
    }

    private fun peek(index: Int): Char? = if (index < endOffset) buffer[index] else null

    private fun isDelimiter(ch: Char): Boolean = ch == '[' ||
        ch == ']' ||
        ch == '(' ||
        ch == ')' ||
        ch == '{' ||
        ch == '}' ||
        ch == ',' ||
        ch == ';'

    private fun isIdentifierStart(ch: Char): Boolean = ch == '_' || ch.isLetter()

    private fun isIdentifierPart(ch: Char): Boolean = ch == '_' || ch.isLetterOrDigit()

    private companion object {
        const val DirectivePrefix = "// typewriter-"
    }
}
