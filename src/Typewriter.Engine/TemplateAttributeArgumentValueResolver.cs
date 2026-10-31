using System.Globalization;
using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal static class TemplateAttributeArgumentValueResolver
{
    public static object? Resolve(AttributeArgumentMetadata argument)
    {
        ArgumentNullException.ThrowIfNull(argument: argument);

        if (argument.Value is null)
        {
            return null;
        }

        var typeCode = argument.Type?.FullName switch
        {
            "System.Boolean" => TypeCode.Boolean,
            "System.Byte" => TypeCode.Byte,
            "System.SByte" => TypeCode.SByte,
            "System.Int16" => TypeCode.Int16,
            "System.UInt16" => TypeCode.UInt16,
            "System.Int32" => TypeCode.Int32,
            "System.UInt32" => TypeCode.UInt32,
            "System.Int64" => TypeCode.Int64,
            "System.UInt64" => TypeCode.UInt64,
            "System.Single" => TypeCode.Single,
            "System.Double" => TypeCode.Double,
            "System.Char" => TypeCode.Char,
            _ => TypeCode.String,
        };

        try
        {
            return Convert.ChangeType(value: argument.Value, typeCode: typeCode, provider: CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            return argument.Value;
        }
    }
}
