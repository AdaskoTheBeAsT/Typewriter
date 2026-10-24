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
        val closeIndex = scanCSharpBalancedBraces(index + 1)
        return if (closeIndex < 0) endOffset else closeIndex + 1
    }

    private fun scanCSharpBalancedBraces(openIndex: Int): Int {
        if (openIndex >= endOffset || buffer[openIndex] != '{') {
            return -1
        }

        var current = openIndex
        var depth = 0
        while (current < endOffset) {
            val skipped = trySkipCSharpNonCode(current)
            if (skipped > current) {
                current = skipped
                continue
            }

            when (buffer[current]) {
                '{' -> {
                    depth++
                    current++
                }
                '}' -> {
                    depth--
                    if (depth == 0) {
                        return current
                    }
                    current++
                }
                else -> current++
            }
        }

        return -1
    }

    private fun trySkipCSharpNonCode(index: Int): Int {
        val ch = buffer[index]
        if (ch == '/' && peek(index + 1) == '/') {
            return lineEnd(index + 2)
        }
        if (ch == '/' && peek(index + 1) == '*') {
            var current = index + 2
            while (current < endOffset) {
                if (buffer[current] == '*' && peek(current + 1) == '/') {
                    return current + 2
                }
                current++
            }
            return endOffset
        }
        if (ch == '\'') {
            return scanString(index)
        }
        return trySkipCSharpString(index)
    }

    private fun trySkipCSharpString(index: Int): Int {
        val ch = buffer[index]
        if (ch == '@') {
            if (peek(index + 1) == '$' && peek(index + 2) == '"') {
                return skipVerbatimString(index + 3, interpolated = true)
            }
            if (peek(index + 1) == '"') {
                return skipVerbatimString(index + 2, interpolated = false)
            }
            return index
        }

        if (ch == '$') {
            val dollarCount = countConsecutive(index, '$')
            val afterDollars = index + dollarCount
            if (peek(afterDollars) == '@' && dollarCount == 1 && peek(afterDollars + 1) == '"') {
                return skipVerbatimString(afterDollars + 2, interpolated = true)
            }
            if (peek(afterDollars) == '"') {
                val quoteCount = countConsecutive(afterDollars, '"')
                if (quoteCount >= 3) {
                    return skipInterpolatedRawString(afterDollars + quoteCount, quoteCount, dollarCount)
                }
                if (dollarCount == 1) {
                    return skipRegularString(afterDollars + 1, interpolated = true)
                }
            }
            return index
        }

        if (ch == '"') {
            val quoteCount = countConsecutive(index, '"')
            return if (quoteCount >= 3) {
                skipRawString(index + quoteCount, quoteCount)
            } else {
                skipRegularString(index + 1, interpolated = false)
            }
        }

        return index
    }

    private fun skipRegularString(startIndex: Int, interpolated: Boolean): Int {
        var current = startIndex
        while (current < endOffset) {
            val ch = buffer[current]
            if (ch == '\\') {
                current += if (current + 1 < endOffset) 2 else 1
                continue
            }
            if (ch == '"' || ch == '\r' || ch == '\n') {
                return current + 1
            }
            if (interpolated && (ch == '{' || ch == '}')) {
                if (peek(current + 1) == ch) {
                    current += 2
                    continue
                }
                if (ch == '{') {
                    val holeEnd = scanCSharpBalancedBraces(current)
                    if (holeEnd < 0) {
                        return endOffset
                    }
                    current = holeEnd + 1
                    continue
                }
            }
            current++
        }

        return endOffset
    }

    private fun skipVerbatimString(startIndex: Int, interpolated: Boolean): Int {
        var current = startIndex
        while (current < endOffset) {
            val ch = buffer[current]
            if (ch == '"') {
                if (peek(current + 1) == '"') {
                    current += 2
                    continue
                }
                return current + 1
            }
            if (interpolated && (ch == '{' || ch == '}')) {
                if (peek(current + 1) == ch) {
                    current += 2
                    continue
                }
                if (ch == '{') {
                    val holeEnd = scanCSharpBalancedBraces(current)
                    if (holeEnd < 0) {
                        return endOffset
                    }
                    current = holeEnd + 1
                    continue
                }
            }
            current++
        }

        return endOffset
    }

    private fun skipRawString(startIndex: Int, quoteCount: Int): Int {
        var current = startIndex
        while (current < endOffset) {
            if (buffer[current] == '"') {
                val run = countConsecutive(current, '"')
                if (run >= quoteCount) {
                    return current + run
                }
                current += run
                continue
            }
            current++
        }

        return endOffset
    }

    private fun skipInterpolatedRawString(startIndex: Int, quoteCount: Int, dollarCount: Int): Int {
        var current = startIndex
        while (current < endOffset) {
            val ch = buffer[current]
            if (ch == '"') {
                val run = countConsecutive(current, '"')
                if (run >= quoteCount) {
                    return current + run
                }
                current += run
                continue
            }
            if (ch == '{') {
                val braceRun = countConsecutive(current, '{')
                if (braceRun < dollarCount) {
                    current += braceRun
                    continue
                }
                val holeOpenIndex = current + braceRun - 1
                val holeEnd = scanCSharpBalancedBraces(holeOpenIndex)
                if (holeEnd < 0) {
                    return endOffset
                }
                val consumedClose = minOf(dollarCount - 1, countConsecutive(holeEnd + 1, '}'))
                current = holeEnd + 1 + consumedClose
                continue
            }
            if (ch == '}') {
                current += countConsecutive(current, '}')
                continue
            }
            current++
        }

        return endOffset
    }

    private fun countConsecutive(startIndex: Int, target: Char): Int {
        var current = startIndex
        while (current < endOffset && buffer[current] == target) {
            current++
        }
        return current - startIndex
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
