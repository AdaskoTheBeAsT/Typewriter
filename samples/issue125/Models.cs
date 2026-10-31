namespace Sample;

[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonInjectAttribute(string type, bool import = true) : Attribute
{
    public string Type { get; } = type;

    public bool Import { get; } = import;
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class ValueKindsAttribute(int number, double ratio, char marker, string text, string? optional) : Attribute
{
    public int Number { get; } = number;

    public double Ratio { get; } = ratio;

    public char Marker { get; } = marker;

    public string Text { get; } = text;

    public string? Optional { get; } = optional;

    public bool Include { get; set; }
}

[ValueKinds(42, 1.25d, 'x', "false", null, Include = false)]
public sealed class Model
{
    [JsonInject("string[]", false)]
    public required string Choices { get; set; }

    [JsonInject("Widget", true)]
    public required string ExplicitValue { get; set; }

    [JsonInject("DefaultWidget")]
    public required string DefaultValue { get; set; }
}
