namespace Typewriter.Engine;

internal sealed record TemplateCompatibilityMethod(
    string Name,
    TemplateCompatibilityMethodKind Kind,
    string ParameterName,
    string? Expression,
    string Body);
