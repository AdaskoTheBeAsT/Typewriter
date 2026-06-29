using Typewriter.VisualStudio;
using CodeFile = Typewriter.CodeModel.File;

namespace Typewriter.Configuration;

public class Settings
{
    private char _stringLiteralCharacter = '"';

    public string OutputExtension { get; set; } = ".ts";

    public Func<CodeFile, string>? OutputFilenameFactory { get; set; }

    public PartialRenderingMode PartialRenderingMode { get; set; } = PartialRenderingMode.Partial;

    public virtual string SolutionFullName { get; init; } = string.Empty;

    public string? OutputDirectory { get; set; }

    public bool SkipAddingGeneratedFilesToProject { get; set; }

    public virtual bool IsSingleFileMode { get; private set; }

    public virtual string SingleFileName { get; private set; } = string.Empty;

    public virtual char StringLiteralCharacter => _stringLiteralCharacter;

    public virtual string DateTypeGeneration { get; private set; } = "Date";

    public virtual string DecimalTypeGeneration { get; private set; } = "number";

    public virtual bool StrictNullGeneration { get; private set; } = true;

    public virtual bool Utf8BomGeneration { get; private set; } = true;

    public virtual string TemplatePath { get; init; } = string.Empty;

    public virtual ILog Log { get; init; } = NullLog.Instance;

    public virtual Settings IncludeProject(string projectName)
    {
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
            ? "Date"
            : dateType.Trim();
        return this;
    }

    public virtual Settings UseDecimalType(string decimalType)
    {
        DecimalTypeGeneration = string.IsNullOrWhiteSpace(value: decimalType)
            ? "number"
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
        string decimalTypeGeneration)
    {
        StrictNullGeneration = strictNullGeneration;
        Utf8BomGeneration = utf8BomGeneration;
        _stringLiteralCharacter = stringLiteralCharacter;
        UseDateType(dateType: dateTypeGeneration);
        UseDecimalType(decimalType: decimalTypeGeneration);
    }
}
