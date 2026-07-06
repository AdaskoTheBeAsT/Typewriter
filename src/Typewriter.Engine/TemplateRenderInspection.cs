namespace Typewriter.Engine;

internal sealed record TemplateRenderInspection(
    bool IsSingleFileMode,
    bool UsesOutputFilenameFactory,
    IReadOnlyList<string> IncludedProjects);
