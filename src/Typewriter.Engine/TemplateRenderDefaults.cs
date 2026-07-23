using Typewriter.Abstractions;
using Typewriter.Configuration;

namespace Typewriter.Engine;

public sealed record TemplateRenderDefaults(
    bool StrictNullGeneration,
    bool Utf8BomGeneration,
    string SolutionFullName,
    char StringLiteralCharacter = '"',
    string DateTypeGeneration = TypeScriptTypeMapper.DefaultDateType,
    string DateInitializerGeneration = TypeScriptTypeMapper.DefaultDateInitializer,
    string DateOnlyTypeGeneration = TypeScriptTypeMapper.DefaultDateOnlyType,
    string DateOnlyInitializerGeneration = TypeScriptTypeMapper.DefaultDateOnlyInitializer,
    string TimeOnlyTypeGeneration = TypeScriptTypeMapper.DefaultTimeOnlyType,
    string TimeOnlyInitializerGeneration = TypeScriptTypeMapper.DefaultTimeOnlyInitializer,
    string GuidTypeGeneration = TypeScriptTypeMapper.DefaultGuidType,
    string GuidInitializerGeneration = TypeScriptTypeMapper.DefaultGuidInitializer,
    string DecimalTypeGeneration = TypeScriptTypeMapper.DefaultDecimalType,
    string DecimalInitializerGeneration = TypeScriptTypeMapper.DefaultDecimalInitializer)
{
    public DateLibrary DateLibraryGeneration { get; init; } = DateLibrary.Legacy;

    // Matches the original Typewriter defaults: strict null unions and a UTF-8 BOM.
    public static TemplateRenderDefaults Default { get; } = new(
        StrictNullGeneration: true,
        Utf8BomGeneration: true,
        SolutionFullName: string.Empty);

    public static TemplateRenderDefaults FromConfiguration(
        TypewriterConfiguration configuration,
        string? solutionFullName = null)
    {
        ArgumentNullException.ThrowIfNull(argument: configuration);

        return new TemplateRenderDefaults(
            StrictNullGeneration: configuration.Output.StrictNull,
            Utf8BomGeneration: configuration.Output.Encoding.Equals(value: "utf-8-bom", comparisonType: StringComparison.OrdinalIgnoreCase),
            SolutionFullName: solutionFullName ?? string.Empty,
            StringLiteralCharacter: ToStringLiteralCharacter(quoteStyle: configuration.Output.QuoteStyle),
            DateTypeGeneration: configuration.Output.DateType,
            DateInitializerGeneration: configuration.Output.DateInitializer,
            DateOnlyTypeGeneration: configuration.Output.DateOnlyType,
            DateOnlyInitializerGeneration: configuration.Output.DateOnlyInitializer,
            TimeOnlyTypeGeneration: configuration.Output.TimeOnlyType,
            TimeOnlyInitializerGeneration: configuration.Output.TimeOnlyInitializer,
            GuidTypeGeneration: configuration.Output.GuidType,
            GuidInitializerGeneration: configuration.Output.GuidInitializer,
            DecimalTypeGeneration: configuration.Output.DecimalType,
            DecimalInitializerGeneration: configuration.Output.DecimalInitializer)
        {
            DateLibraryGeneration = configuration.Output.DateLibrary,
        };
    }

    private static char ToStringLiteralCharacter(QuoteStyle quoteStyle)
    {
        return quoteStyle switch
        {
            QuoteStyle.Single => '\'',
            QuoteStyle.Backtick => '`',
            _ => '"',
        };
    }
}
