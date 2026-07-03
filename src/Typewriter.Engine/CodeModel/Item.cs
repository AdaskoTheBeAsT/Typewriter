namespace Typewriter.CodeModel;

/// <summary>
/// Base class for all Typewriter code model items. Every metadata object
/// available in a <c>.tst</c> template (classes, properties, methods, ...)
/// derives from this type.
/// </summary>
public class Item
{
    /// <summary>
    /// Gets the name of the assembly that declares this item, for example <c>MyApp.Contracts</c>.
    /// </summary>
    public virtual string AssemblyName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the fully qualified name of the item including its namespace,
    /// for example <c>MyApp.Models.UserModel</c>. Template shorthand: <c>$FullName</c>.
    /// </summary>
    public virtual string FullName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the declared C# name of the item, for example <c>UserModel</c> or <c>CreatedAt</c>.
    /// Template shorthand: <c>$Name</c>.
    /// </summary>
    public virtual string Name { get; init; } = string.Empty;

#pragma warning disable SA1300 // Element should begin with upper-case letter
    /// <summary>
    /// Gets the camel-cased name of the item, for example <c>userModel</c> or <c>createdAt</c>.
    /// Template shorthand: <c>$name</c>.
    /// </summary>
    public virtual string name => GetName(nameCase: NameCase.LegacyCamelCase);
#pragma warning restore SA1300 // Element should begin with upper-case letter

    /// <summary>
    /// Gets the parent code model item, for example the <see cref="Class"/> that declares
    /// the current <see cref="Property"/>. Template shorthand: <c>$Parent</c>.
    /// </summary>
    public virtual Item? Parent { get; init; }

    /// <summary>
    /// Formats <see cref="Name"/> with the requested casing convention
    /// (camel, pascal, kebab, snake, ...).
    /// </summary>
    /// <param name="nameCase">The casing convention to apply.</param>
    /// <returns>The formatted name.</returns>
    public virtual string GetName(NameCase nameCase) => NameCaseFormatter.Format(value: Name, nameCase: nameCase);

    /// <inheritdoc/>
    public override string ToString() => Name;
}
