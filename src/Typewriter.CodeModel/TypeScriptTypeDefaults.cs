namespace Typewriter.Engine;

internal static class TypeScriptTypeDefaults
{
    public const string DefaultDateType = "Date";
    public const string DefaultDateInitializer = "new Date()";
    public const string DefaultDateOnlyType = DefaultDateType;
    public const string DefaultDateOnlyInitializer = DefaultDateInitializer;
    public const string DefaultTimeOnlyType = "string";
    public const string DefaultTimeOnlyInitializer = "\"00:00:00\"";
    public const string DefaultGuidType = "string";
    public const string DefaultGuidInitializer = "auto";
    public const string DefaultDecimalType = "number";
    public const string DefaultDecimalInitializer = "auto";
}
