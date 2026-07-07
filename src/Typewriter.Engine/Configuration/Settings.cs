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

    public virtual string DateTypeGeneration { get; private set; } = TypeScriptTypeMapper.DefaultDateType;

    public virtual string DateInitializerGeneration { get; private set; } = TypeScriptTypeMapper.DefaultDateInitializer;

    public virtual string DateOnlyTypeGeneration { get; private set; } = TypeScriptTypeMapper.DefaultDateOnlyType;

    public virtual string DateOnlyInitializerGeneration { get; private set; } = TypeScriptTypeMapper.DefaultDateOnlyInitializer;

    public virtual string TimeOnlyTypeGeneration { get; private set; } = TypeScriptTypeMapper.DefaultTimeOnlyType;

    public virtual string TimeOnlyInitializerGeneration { get; private set; } = TypeScriptTypeMapper.DefaultTimeOnlyInitializer;

    public virtual string GuidTypeGeneration { get; private set; } = TypeScriptTypeMapper.DefaultGuidType;

    public virtual string DecimalTypeGeneration { get; private set; } = TypeScriptTypeMapper.DefaultDecimalType;

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
            ? TypeScriptTypeMapper.DefaultDateType
            : dateType.Trim();
        return this;
    }

    public virtual Settings UseDateInitializer(string dateInitializer)
    {
        DateInitializerGeneration = string.IsNullOrWhiteSpace(value: dateInitializer)
            ? TypeScriptTypeMapper.DefaultDateInitializer
            : dateInitializer.Trim();
        return this;
    }

    public virtual Settings UseDateOnlyType(string dateOnlyType)
    {
        DateOnlyTypeGeneration = string.IsNullOrWhiteSpace(value: dateOnlyType)
            ? TypeScriptTypeMapper.DefaultDateOnlyType
            : dateOnlyType.Trim();
        return this;
    }

    public virtual Settings UseDateOnlyInitializer(string dateOnlyInitializer)
    {
        DateOnlyInitializerGeneration = string.IsNullOrWhiteSpace(value: dateOnlyInitializer)
            ? TypeScriptTypeMapper.DefaultDateOnlyInitializer
            : dateOnlyInitializer.Trim();
        return this;
    }

    public virtual Settings UseTimeOnlyType(string timeOnlyType)
    {
        TimeOnlyTypeGeneration = string.IsNullOrWhiteSpace(value: timeOnlyType)
            ? TypeScriptTypeMapper.DefaultTimeOnlyType
            : timeOnlyType.Trim();
        return this;
    }

    public virtual Settings UseTimeOnlyInitializer(string timeOnlyInitializer)
    {
        TimeOnlyInitializerGeneration = string.IsNullOrWhiteSpace(value: timeOnlyInitializer)
            ? TypeScriptTypeMapper.DefaultTimeOnlyInitializer
            : timeOnlyInitializer.Trim();
        return this;
    }

    public virtual Settings UseGuidType(string guidType)
    {
        GuidTypeGeneration = string.IsNullOrWhiteSpace(value: guidType)
            ? TypeScriptTypeMapper.DefaultGuidType
            : guidType.Trim();
        return this;
    }

    public virtual Settings UseDecimalType(string decimalType)
    {
        DecimalTypeGeneration = string.IsNullOrWhiteSpace(value: decimalType)
            ? TypeScriptTypeMapper.DefaultDecimalType
            : decimalType.Trim();
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
        string decimalTypeGeneration)
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
        UseDecimalType(decimalType: decimalTypeGeneration);
    }
}
