using Typewriter.Engine;
using Typewriter.VisualStudio;
using CodeFile = Typewriter.CodeModel.File;

namespace Typewriter.Configuration;

public class Settings
{
    private static readonly IReadOnlyList<string> EmptyIncludedProjects = [];
    private char _stringLiteralCharacter = '"';
    private List<string>? _includedProjects;

    public string OutputExtension { get; set; } = ".ts";

    public Func<CodeFile, string>? OutputFilenameFactory { get; set; }

    public PartialRenderingMode PartialRenderingMode { get; set; } = PartialRenderingMode.Partial;

    public virtual string SolutionFullName { get; init; } = string.Empty;

    public string? OutputDirectory { get; set; }

    public bool SkipAddingGeneratedFilesToProject { get; set; }

    public virtual bool IsSingleFileMode { get; private set; }

    public virtual string SingleFileName { get; private set; } = string.Empty;

    public virtual char StringLiteralCharacter => _stringLiteralCharacter;

    public virtual string DateTypeGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultDateType;

    public virtual string DateInitializerGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultDateInitializer;

    public virtual string DateOnlyTypeGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultDateOnlyType;

    public virtual string DateOnlyInitializerGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultDateOnlyInitializer;

    public virtual string TimeOnlyTypeGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultTimeOnlyType;

    public virtual string TimeOnlyInitializerGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultTimeOnlyInitializer;

    public virtual DateLibrary DateLibraryGeneration { get; private set; } = DateLibrary.Legacy;

    public virtual string DateLibraryImportsGeneration { get; private set; } = string.Empty;

    public virtual string InstantTypeGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultDateType;

    public virtual string InstantInitializerGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultDateInitializer;

    public virtual string PlainDateTypeGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultDateOnlyType;

    public virtual string PlainDateInitializerGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultDateOnlyInitializer;

    public virtual string PlainTimeTypeGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultTimeOnlyType;

    public virtual string PlainTimeInitializerGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultTimeOnlyInitializer;

    public virtual string PlainDateTimeTypeGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultDateType;

    public virtual string PlainDateTimeInitializerGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultDateInitializer;

    public virtual string ZonedDateTimeTypeGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultDateType;

    public virtual string ZonedDateTimeInitializerGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultDateInitializer;

    public virtual string DurationTypeGeneration { get; private set; } = "string";

    public virtual string DurationInitializerGeneration { get; private set; } = "\"00:00:00\"";

    public virtual string PeriodTypeGeneration { get; private set; } = "string";

    public virtual string PeriodInitializerGeneration { get; private set; } = "\"P0D\"";

    public virtual string PlainYearMonthTypeGeneration { get; private set; } = "string";

    public virtual string PlainYearMonthInitializerGeneration { get; private set; } = "\"\"";

    public virtual string PlainMonthDayTypeGeneration { get; private set; } = "string";

    public virtual string PlainMonthDayInitializerGeneration { get; private set; } = "\"\"";

    public virtual string GuidTypeGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultGuidType;

    public virtual string GuidInitializerGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultGuidInitializer;

    public virtual string DecimalTypeGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultDecimalType;

    public virtual string DecimalInitializerGeneration { get; private set; } = TypeScriptTypeDefaults.DefaultDecimalInitializer;

    public virtual bool StrictNullGeneration { get; private set; } = true;

    public virtual bool Utf8BomGeneration { get; private set; } = true;

    public virtual string TemplatePath { get; init; } = string.Empty;

    public virtual ILog Log { get; init; } = NullLog.Instance;

    public virtual IReadOnlyList<string> IncludedProjects => _includedProjects ?? EmptyIncludedProjects;

    public virtual Settings IncludeProject(string projectName)
    {
        if (!string.IsNullOrWhiteSpace(value: projectName))
        {
            _includedProjects ??= [];
            _includedProjects.Add(item: projectName.Trim());
        }

        return this;
    }

    public virtual Settings SingleFileMode(string singleFilename)
    {
        IsSingleFileMode = true;
        SingleFileName = singleFilename;
        return this;
    }

    public virtual Settings IncludeCurrentProject()
    {
        return this;
    }

    public virtual Settings IncludeReferencedProjects()
    {
        return this;
    }

    public virtual Settings IncludeAllProjects()
    {
        return this;
    }

    public virtual Settings UseStringLiteralCharacter(char ch)
    {
        _stringLiteralCharacter = ch;
        return this;
    }

    public virtual Settings UseDateType(string dateType)
    {
        DateTypeGeneration = string.IsNullOrWhiteSpace(value: dateType)
            ? TypeScriptTypeDefaults.DefaultDateType
            : dateType.Trim();
        InstantTypeGeneration = DateTypeGeneration;
        PlainDateTimeTypeGeneration = DateTypeGeneration;
        ZonedDateTimeTypeGeneration = DateTypeGeneration;
        return this;
    }

    public virtual Settings UseDateInitializer(string dateInitializer)
    {
        DateInitializerGeneration = string.IsNullOrWhiteSpace(value: dateInitializer)
            ? TypeScriptTypeDefaults.DefaultDateInitializer
            : dateInitializer.Trim();
        InstantInitializerGeneration = DateInitializerGeneration;
        PlainDateTimeInitializerGeneration = DateInitializerGeneration;
        ZonedDateTimeInitializerGeneration = DateInitializerGeneration;
        return this;
    }

    public virtual Settings UseDateOnlyType(string dateOnlyType)
    {
        DateOnlyTypeGeneration = string.IsNullOrWhiteSpace(value: dateOnlyType)
            ? TypeScriptTypeDefaults.DefaultDateOnlyType
            : dateOnlyType.Trim();
        PlainDateTypeGeneration = DateOnlyTypeGeneration;
        return this;
    }

    public virtual Settings UseDateOnlyInitializer(string dateOnlyInitializer)
    {
        DateOnlyInitializerGeneration = string.IsNullOrWhiteSpace(value: dateOnlyInitializer)
            ? TypeScriptTypeDefaults.DefaultDateOnlyInitializer
            : dateOnlyInitializer.Trim();
        PlainDateInitializerGeneration = DateOnlyInitializerGeneration;
        return this;
    }

    public virtual Settings UseTimeOnlyType(string timeOnlyType)
    {
        TimeOnlyTypeGeneration = string.IsNullOrWhiteSpace(value: timeOnlyType)
            ? TypeScriptTypeDefaults.DefaultTimeOnlyType
            : timeOnlyType.Trim();
        PlainTimeTypeGeneration = TimeOnlyTypeGeneration;
        return this;
    }

    public virtual Settings UseTimeOnlyInitializer(string timeOnlyInitializer)
    {
        TimeOnlyInitializerGeneration = string.IsNullOrWhiteSpace(value: timeOnlyInitializer)
            ? TypeScriptTypeDefaults.DefaultTimeOnlyInitializer
            : timeOnlyInitializer.Trim();
        PlainTimeInitializerGeneration = TimeOnlyInitializerGeneration;
        return this;
    }

    public virtual Settings UseDateLibrary(DateLibrary library)
    {
        DateLibraryGeneration = library;
        if (library == DateLibrary.Legacy)
        {
            DateLibraryImportsGeneration = string.Empty;
            UseDateType(dateType: TypeScriptTypeDefaults.DefaultDateType);
            UseDateInitializer(dateInitializer: TypeScriptTypeDefaults.DefaultDateInitializer);
            UseDateOnlyType(dateOnlyType: TypeScriptTypeDefaults.DefaultDateOnlyType);
            UseDateOnlyInitializer(dateOnlyInitializer: TypeScriptTypeDefaults.DefaultDateOnlyInitializer);
            UseTimeOnlyType(timeOnlyType: TypeScriptTypeDefaults.DefaultTimeOnlyType);
            UseTimeOnlyInitializer(timeOnlyInitializer: TypeScriptTypeDefaults.DefaultTimeOnlyInitializer);
            DurationTypeGeneration = "string";
            DurationInitializerGeneration = "\"00:00:00\"";
            PeriodTypeGeneration = "string";
            PeriodInitializerGeneration = "\"P0D\"";
            PlainYearMonthTypeGeneration = "string";
            PlainYearMonthInitializerGeneration = "\"\"";
            PlainMonthDayTypeGeneration = "string";
            PlainMonthDayInitializerGeneration = "\"\"";
            return this;
        }

        var profile = DateLibraryProfiles.Get(library: library);
        ApplyDateLibraryProfile(profile: profile);
        return this;
    }

    public virtual Settings UseGuidType(string guidType)
    {
        GuidTypeGeneration = string.IsNullOrWhiteSpace(value: guidType)
            ? TypeScriptTypeDefaults.DefaultGuidType
            : guidType.Trim();
        return this;
    }

    public virtual Settings UseGuidInitializer(string guidInitializer)
    {
        GuidInitializerGeneration = ScalarInitializer.Normalize(
            initializer: guidInitializer,
            defaultInitializer: TypeScriptTypeDefaults.DefaultGuidInitializer);
        return this;
    }

    public virtual Settings UseDecimalType(string decimalType)
    {
        DecimalTypeGeneration = string.IsNullOrWhiteSpace(value: decimalType)
            ? TypeScriptTypeDefaults.DefaultDecimalType
            : decimalType.Trim();
        return this;
    }

    public virtual Settings UseDecimalInitializer(string decimalInitializer)
    {
        DecimalInitializerGeneration = ScalarInitializer.Normalize(
            initializer: decimalInitializer,
            defaultInitializer: TypeScriptTypeDefaults.DefaultDecimalInitializer);
        return this;
    }

    public virtual Settings DisableStrictNullGeneration()
    {
        StrictNullGeneration = false;
        return this;
    }

    public virtual Settings DisableUtf8BomGeneration()
    {
        Utf8BomGeneration = false;
        return this;
    }

    internal void ApplyConfigurationDefaults(
        bool strictNullGeneration,
        bool utf8BomGeneration,
        char stringLiteralCharacter,
        string dateTypeGeneration,
        string dateInitializerGeneration,
        string dateOnlyTypeGeneration,
        string dateOnlyInitializerGeneration,
        string timeOnlyTypeGeneration,
        string timeOnlyInitializerGeneration,
        string guidTypeGeneration,
        string guidInitializerGeneration,
        string decimalTypeGeneration,
        string decimalInitializerGeneration,
        DateLibrary dateLibraryGeneration)
    {
        StrictNullGeneration = strictNullGeneration;
        Utf8BomGeneration = utf8BomGeneration;
        _stringLiteralCharacter = stringLiteralCharacter;
        UseDateType(dateType: dateTypeGeneration);
        UseDateInitializer(dateInitializer: dateInitializerGeneration);
        UseDateOnlyType(dateOnlyType: dateOnlyTypeGeneration);
        UseDateOnlyInitializer(dateOnlyInitializer: dateOnlyInitializerGeneration);
        UseTimeOnlyType(timeOnlyType: timeOnlyTypeGeneration);
        UseTimeOnlyInitializer(timeOnlyInitializer: timeOnlyInitializerGeneration);
        UseGuidType(guidType: guidTypeGeneration);
        UseGuidInitializer(guidInitializer: guidInitializerGeneration);
        UseDecimalType(decimalType: decimalTypeGeneration);
        UseDecimalInitializer(decimalInitializer: decimalInitializerGeneration);
        if (dateLibraryGeneration != DateLibrary.Legacy)
        {
            UseDateLibrary(library: dateLibraryGeneration);
        }
    }

    internal TypeScriptDateMapping GetDateMapping() =>
        new(
            Library: DateLibraryGeneration,
            DateType: DateTypeGeneration,
            DateOnlyType: DateOnlyTypeGeneration,
            TimeOnlyType: TimeOnlyTypeGeneration,
            InstantType: InstantTypeGeneration,
            PlainDateType: PlainDateTypeGeneration,
            PlainTimeType: PlainTimeTypeGeneration,
            PlainDateTimeType: PlainDateTimeTypeGeneration,
            ZonedDateTimeType: ZonedDateTimeTypeGeneration,
            DurationType: DurationTypeGeneration,
            PeriodType: PeriodTypeGeneration,
            PlainYearMonthType: PlainYearMonthTypeGeneration,
            PlainMonthDayType: PlainMonthDayTypeGeneration);

    internal string GetDateInitializer(DateSemanticKind kind) =>
        kind switch
        {
            DateSemanticKind.Instant => InstantInitializerGeneration,
            DateSemanticKind.PlainDate => PlainDateInitializerGeneration,
            DateSemanticKind.PlainTime => PlainTimeInitializerGeneration,
            DateSemanticKind.PlainDateTime => PlainDateTimeInitializerGeneration,
            DateSemanticKind.ZonedDateTime => ZonedDateTimeInitializerGeneration,
            DateSemanticKind.Duration => DurationInitializerGeneration,
            DateSemanticKind.Period => PeriodInitializerGeneration,
            DateSemanticKind.PlainYearMonth => PlainYearMonthInitializerGeneration,
            DateSemanticKind.PlainMonthDay => PlainMonthDayInitializerGeneration,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(kind), actualValue: kind, message: null),
        };

    private void ApplyDateLibraryProfile(DateLibraryProfile profile)
    {
        DateLibraryImportsGeneration = profile.Imports;
        InstantTypeGeneration = profile.InstantType;
        InstantInitializerGeneration = profile.InstantInitializer;
        PlainDateTypeGeneration = profile.PlainDateType;
        PlainDateInitializerGeneration = profile.PlainDateInitializer;
        PlainTimeTypeGeneration = profile.PlainTimeType;
        PlainTimeInitializerGeneration = profile.PlainTimeInitializer;
        PlainDateTimeTypeGeneration = profile.PlainDateTimeType;
        PlainDateTimeInitializerGeneration = profile.PlainDateTimeInitializer;
        ZonedDateTimeTypeGeneration = profile.ZonedDateTimeType;
        ZonedDateTimeInitializerGeneration = profile.ZonedDateTimeInitializer;
        DurationTypeGeneration = profile.DurationType;
        DurationInitializerGeneration = profile.DurationInitializer;
        PeriodTypeGeneration = profile.PeriodType;
        PeriodInitializerGeneration = profile.PeriodInitializer;
        PlainYearMonthTypeGeneration = profile.PlainYearMonthType;
        PlainYearMonthInitializerGeneration = profile.PlainYearMonthInitializer;
        PlainMonthDayTypeGeneration = profile.PlainMonthDayType;
        PlainMonthDayInitializerGeneration = profile.PlainMonthDayInitializer;
        DateTypeGeneration = profile.PlainDateTimeType;
        DateInitializerGeneration = profile.PlainDateTimeInitializer;
        DateOnlyTypeGeneration = profile.PlainDateType;
        DateOnlyInitializerGeneration = profile.PlainDateInitializer;
        TimeOnlyTypeGeneration = profile.PlainTimeType;
        TimeOnlyInitializerGeneration = profile.PlainTimeInitializer;
    }
}
