namespace Typewriter.Engine;

internal sealed record TemplateInlinePredicate(
    string CollectionName,
    string Filter,
    string MethodName,
    string? ItemTypeName,
    int Line,
    int Column);
