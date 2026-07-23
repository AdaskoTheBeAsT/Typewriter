using System.Diagnostics.CodeAnalysis;

namespace AdaskoTheBeAsT.Typewriter.Annotations;

/// <summary>
/// Marks a property so that Typewriter templates emit it as a plain passthrough
/// value, skipping any runtime transformation even when the C# type would normally
/// trigger one (for example <see cref="global::System.Guid"/> or
/// <c>decimal</c>).
/// </summary>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
[ExcludeFromCodeCoverage]
public sealed class FrontendNoTransformAttribute : Attribute
{
}
