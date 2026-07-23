using System.Diagnostics.CodeAnalysis;

namespace AdaskoTheBeAsT.Typewriter.Annotations;

/// <summary>
/// Overrides the generated frontend name for a single C# method. Typewriter
/// recipe templates that compute method names (for example
/// <c>MethodName(Method)</c>) check for this attribute and use its value
/// instead of the C# method name.
/// </summary>
/// <param name="name">The custom frontend name for the method.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[ExcludeFromCodeCoverage]
public sealed class CustomNameAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the custom name specified in the constructor.
    /// </summary>
    public string Name { get; } = name;
}
