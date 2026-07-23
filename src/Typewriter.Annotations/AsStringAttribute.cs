using System.Diagnostics.CodeAnalysis;

namespace AdaskoTheBeAsT.Typewriter.Annotations;

/// <summary>
/// Marks an enum so that Typewriter templates emit its values as strings
/// instead of numbers. When this attribute is present the
/// <c>IsEnumAsNumber</c> template predicate returns <see langword="false"/>
/// and enum values are rendered as their string representation.
/// </summary>
/// <remarks>
/// Typewriter recognizes this attribute by simple name, so user-defined
/// attributes named <c>AsStringAttribute</c> in any namespace
/// continue to work without referencing this package.
/// </remarks>
[AttributeUsage(AttributeTargets.Enum)]
[ExcludeFromCodeCoverage]
public sealed class AsStringAttribute : Attribute
{
}
