namespace Typewriter.Abstractions;

public sealed record OutputConfiguration(
    string Newline,
    string Encoding,
    bool WriteOnlyWhenChanged,
    bool DryRun,
    FileNameConvention FileNameConvention,
    bool StrictNull,
    IndentStyle IndentStyle,
    int IndentSize,
    bool InsertFinalNewline,
    bool TrimTrailingWhitespace,
    QuoteStyle QuoteStyle)
{
    public static OutputConfiguration Default { get; } = new(
        Newline: "lf",
        Encoding: "utf-8",
        WriteOnlyWhenChanged: true,
        DryRun: false,
        FileNameConvention: FileNameConvention.Preserve,
        StrictNull: true,
        IndentStyle: IndentStyle.Preserve,
        IndentSize: 4,
        InsertFinalNewline: false,
        TrimTrailingWhitespace: false,
        QuoteStyle: QuoteStyle.Double);
}
