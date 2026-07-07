using System.Text.Json.Serialization;

namespace Typewriter.Abstractions;

public sealed record GenerationConfiguration(string Incremental)
{
    public const string IncrementalAuto = "auto";
    public const string IncrementalOff = "off";

    public static GenerationConfiguration Default { get; } = new(Incremental: IncrementalAuto);

    [JsonIgnore]
    public bool IsIncrementalEnabled =>
        !IncrementalOff.Equals(value: Incremental, comparisonType: StringComparison.OrdinalIgnoreCase);
}
