using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal sealed class TypeArgumentSubstitution(IReadOnlyDictionary<string, TypeMetadataReference> substitutions)
{
    private readonly Dictionary<TypeMetadataReference, TypeMetadataReference> _completed = new(comparer: ReferenceEqualityComparer.Instance);
    private readonly Lock _sync = new();

    public TypeMetadataReference Substitute(TypeMetadataReference target)
    {
        lock (_sync)
        {
            return SubstituteCore(target: target);
        }
    }

    private TypeMetadataReference SubstituteCore(TypeMetadataReference target)
    {
        if (_completed.TryGetValue(key: target, value: out var completed))
        {
            return completed;
        }

        var result = SubstituteUncached(target: target);
        _completed.Add(key: target, value: result);
        return result;
    }

    private TypeMetadataReference SubstituteUncached(TypeMetadataReference target)
    {
        if (target.Arity == 0
            && target.TypeArguments.Count == 0
            && (substitutions.TryGetValue(key: target.Name, value: out var replacement)
                || substitutions.TryGetValue(key: target.FullName, value: out replacement)))
        {
            return target.IsNullable && !replacement.IsNullable
                ? replacement with { IsNullable = true }
                : replacement;
        }

        var hasChanges = false;
        var typeArguments = target.TypeArguments;
        if (target.TypeArguments.Count > 0)
        {
            var substitutedArguments = new TypeMetadataReference[target.TypeArguments.Count];
            for (var index = 0; index < target.TypeArguments.Count; index++)
            {
                var originalArgument = target.TypeArguments[index: index];
                var substitutedArgument = SubstituteCore(target: originalArgument);
                substitutedArguments[index] = substitutedArgument;
                if (!ReferenceEquals(objA: originalArgument, objB: substitutedArgument))
                {
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                typeArguments = substitutedArguments;
            }
        }

        var elementType = target.ElementType;
        if (elementType is not null)
        {
            var substitutedElement = SubstituteCore(target: elementType);
            if (!ReferenceEquals(objA: elementType, objB: substitutedElement))
            {
                elementType = substitutedElement;
                hasChanges = true;
            }
        }

        return hasChanges
            ? target with
            {
                TypeArguments = typeArguments,
                ElementType = elementType,
                Interfaces = new LazySubstitutedTypeReferenceList(source: target.Interfaces, substitution: this),
            }
            : target;
    }

    private sealed class LazySubstitutedTypeReferenceList(
        IReadOnlyList<TypeMetadataReference> source,
        TypeArgumentSubstitution substitution)
        : IReadOnlyList<TypeMetadataReference>
    {
        private readonly Lazy<IReadOnlyList<TypeMetadataReference>> _items = new(
            valueFactory: () => source.Count == 0
                ? []
                : source.Select(selector: item => substitution.Substitute(target: item)).ToArray());

        public int Count => _items.Value.Count;

        public TypeMetadataReference this[int index] => _items.Value[index];

        public IEnumerator<TypeMetadataReference> GetEnumerator() => _items.Value.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
