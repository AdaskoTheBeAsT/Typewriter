package com.adaskothebeast.typewriter.rider

import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.openapi.fileTypes.SyntaxHighlighter
import com.intellij.openapi.options.colors.AttributesDescriptor
import com.intellij.openapi.options.colors.ColorDescriptor
import com.intellij.openapi.options.colors.ColorSettingsPage
import javax.swing.Icon

class TypewriterColorSettingsPage : ColorSettingsPage {
    override fun getIcon(): Icon = TypewriterFileType.INSTANCE.getIcon()

    override fun getHighlighter(): SyntaxHighlighter = TypewriterSyntaxHighlighter()

    override fun getDemoText(): String = """
        // typewriter-template: v1
        ${'$'}{
            using Typewriter.VisualStudio;

            Template(Settings settings)
            {
                settings.SingleFileMode("models.ts");
            }
        }
        ${'$'}Classes(*Model)[
        export interface ${'$'}Name {
            ${'$'}Properties[${'$'}name: ${'$'}Type;]
        }
        ]
    """.trimIndent()

    override fun getAdditionalHighlightingTagToDescriptorMap(): MutableMap<String, TextAttributesKey>? = null

    override fun getAttributeDescriptors(): Array<AttributesDescriptor> = DESCRIPTORS

    override fun getColorDescriptors(): Array<ColorDescriptor> = ColorDescriptor.EMPTY_ARRAY

    override fun getDisplayName(): String = "Typewriter"

    private companion object {
        val DESCRIPTORS = arrayOf(
            AttributesDescriptor("Directive", TypewriterSyntaxHighlighter.DIRECTIVE),
            AttributesDescriptor("Template expression", TypewriterSyntaxHighlighter.TEMPLATE_TOKEN),
            AttributesDescriptor("C# helper block", TypewriterSyntaxHighlighter.HELPER_BLOCK),
            AttributesDescriptor("String", TypewriterSyntaxHighlighter.STRING),
            AttributesDescriptor("Number", TypewriterSyntaxHighlighter.NUMBER),
            AttributesDescriptor("Comment", TypewriterSyntaxHighlighter.COMMENT),
            AttributesDescriptor("Delimiter", TypewriterSyntaxHighlighter.DELIMITER),
        )
    }
}
