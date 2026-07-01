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
    QuoteStyle QuoteStyle,
    string DateType,
    string DateInitializer,
    string DateOnlyType,
    string DateOnlyInitializer,
    string TimeOnlyType,
    string TimeOnlyInitializer,
    string GuidType,
    string DecimalType)
{
    public const string DefaultDateType = "Date";
    public const string DefaultDateInitializer = "new Date()";
    public const string DefaultDateOnlyType = DefaultDateType;
    public const string DefaultDateOnlyInitializer = DefaultDateInitializer;
    public const string DefaultTimeOnlyType = "string";
    public const string DefaultTimeOnlyInitializer = "\"00:00:00\"";
    public const string DefaultGuidType = "string";
    public const string DefaultDecimalType = "number";

#pragma warning disable SA1313
    public OutputConfiguration(
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
        QuoteStyle QuoteStyle,
        string DateType,
        string DecimalType)
        : this(
            Newline: Newline,
            Encoding: Encoding,
            WriteOnlyWhenChanged: WriteOnlyWhenChanged,
            DryRun: DryRun,
            FileNameConvention: FileNameConvention,
            StrictNull: StrictNull,
            IndentStyle: IndentStyle,
            IndentSize: IndentSize,
            InsertFinalNewline: InsertFinalNewline,
            TrimTrailingWhitespace: TrimTrailingWhitespace,
            QuoteStyle: QuoteStyle,
            DateType: DateType,
            DateInitializer: DefaultDateInitializer,
            DateOnlyType: DateType,
            DateOnlyInitializer: DefaultDateOnlyInitializer,
            TimeOnlyType: DefaultTimeOnlyType,
            TimeOnlyInitializer: DefaultTimeOnlyInitializer,
            GuidType: DefaultGuidType,
            DecimalType: DecimalType)
    {
    }
#pragma warning restore SA1313

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
        QuoteStyle: QuoteStyle.Double,
        DateType: DefaultDateType,
        DateInitializer: DefaultDateInitializer,
        DateOnlyType: DefaultDateOnlyType,
        DateOnlyInitializer: DefaultDateOnlyInitializer,
        TimeOnlyType: DefaultTimeOnlyType,
        TimeOnlyInitializer: DefaultTimeOnlyInitializer,
        GuidType: DefaultGuidType,
        DecimalType: DefaultDecimalType);

    public void Deconstruct(
        out string newline,
        out string encoding,
        out bool writeOnlyWhenChanged,
        out bool dryRun,
        out FileNameConvention fileNameConvention,
        out bool strictNull,
        out IndentStyle indentStyle,
        out int indentSize,
        out bool insertFinalNewline,
        out bool trimTrailingWhitespace,
        out QuoteStyle quoteStyle,
        out string dateType,
        out string decimalType)
    {
        newline = Newline;
        encoding = Encoding;
        writeOnlyWhenChanged = WriteOnlyWhenChanged;
        dryRun = DryRun;
        fileNameConvention = FileNameConvention;
        strictNull = StrictNull;
        indentStyle = IndentStyle;
        indentSize = IndentSize;
        insertFinalNewline = InsertFinalNewline;
        trimTrailingWhitespace = TrimTrailingWhitespace;
        quoteStyle = QuoteStyle;
        dateType = DateType;
        decimalType = DecimalType;
    }
}
