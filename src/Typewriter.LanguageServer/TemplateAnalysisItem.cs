using Typewriter.Abstractions;

namespace Typewriter.LanguageServer;

internal sealed record TemplateAnalysisItem(
    string Label,
    int CompletionKind,
    string Detail,
    string Documentation,
    string? InsertText = null,
    int? InsertTextFormat = null,
    SourceLocation? Location = null,
    TemplateAnalysisTargetKind TargetKind = TemplateAnalysisTargetKind.TemplateMember);
