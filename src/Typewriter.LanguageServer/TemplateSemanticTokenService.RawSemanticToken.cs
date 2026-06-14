namespace Typewriter.LanguageServer;

internal sealed partial class TemplateSemanticTokenService
{
    private sealed record RawSemanticToken(
        int Line,
        int Start,
        int Length,
        int TokenTypeIndex,
        int TokenModifierMask);
}
