using Typewriter.Abstractions;

namespace Typewriter.Engine;

public sealed record TemplateRenderDefaults(
    bool StrictNullGeneration,
    bool Utf8BomGeneration,
    string SolutionFullName,
    char StringLiteralCharacter = '"',
    string DateTypeGeneration = TypeScriptTypeMapper.DefaultDateType,
    string DecimalTypeGeneration = TypeScriptTypeMapper.DefaultDecimalType)
{
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
            DecimalTypeGeneration: configuration.Output.DecimalType);
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
