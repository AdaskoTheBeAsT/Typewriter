using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal sealed class TypeArgumentSubstitution(IReadOnlyDictionary<string, TypeMetadataReference> substitutions)
{
#pragma warning disable IDE0028
    private readonly Dictionary<TypeMetadataReference, TypeMetadataReference> _completed = new(comparer: ReferenceEqualityComparer.Instance);
#pragma warning restore IDE0028
    private readonly Lock _sync = new();

    public static TypeArgumentSubstitution? Create(
        TypeMetadata type,
        TypeMetadataReference? reference)
    {
        if (reference is null
            || type.TypeParameters.Count == 0
            || type.TypeParameters.Count != reference.TypeArguments.Count)
        {
            return null;
        }

#pragma warning disable IDE0028
        var map = new Dictionary<string, TypeMetadataReference>(comparer: StringComparer.Ordinal);
#pragma warning restore IDE0028
        for (var index = 0; index < type.TypeParameters.Count; index++)
        {
            map[type.TypeParameters[index: index].Name] = reference.TypeArguments[index: index];
        }

        return new TypeArgumentSubstitution(substitutions: map);
    }

    public TypeMetadataReference Substitute(TypeMetadataReference target)
    {
        lock (_sync)
        {
            return SubstituteCore(target: target);
        }
    }

    public PropertyMetadata SubstituteProperty(PropertyMetadata property)
    {
        var substitutedType = Substitute(target: property.Type);
        var substitutedParameters = SubstituteParameters(parameters: property.Parameters);
        return ReferenceEquals(objA: substitutedType, objB: property.Type)
            && ReferenceEquals(objA: substitutedParameters, objB: property.Parameters)
                ? property
                : property with
                {
                    Type = substitutedType,
                    Parameters = substitutedParameters,
                };
    }

    public FieldMetadata SubstituteField(FieldMetadata field)
    {
        var substitutedType = Substitute(target: field.Type);
        return ReferenceEquals(objA: substitutedType, objB: field.Type)
            ? field
            : field with { Type = substitutedType };
    }

    public ConstantMetadata SubstituteConstant(ConstantMetadata constant)
    {
        var substitutedType = Substitute(target: constant.Type);
        return ReferenceEquals(objA: substitutedType, objB: constant.Type)
            ? constant
            : constant with { Type = substitutedType };
    }

    public StaticReadOnlyFieldMetadata SubstituteStaticReadOnlyField(StaticReadOnlyFieldMetadata field)
    {
        var substitutedType = Substitute(target: field.Type);
        return ReferenceEquals(objA: substitutedType, objB: field.Type)
            ? field
            : field with { Type = substitutedType };
    }

    public EventMetadata SubstituteEvent(EventMetadata @event)
    {
        var substitutedType = Substitute(target: @event.Type);
        return ReferenceEquals(objA: substitutedType, objB: @event.Type)
            ? @event
            : @event with { Type = substitutedType };
    }

    public ParameterMetadata SubstituteParameter(ParameterMetadata parameter)
    {
        var substitutedType = Substitute(target: parameter.Type);
        return ReferenceEquals(objA: substitutedType, objB: parameter.Type)
            ? parameter
            : parameter with { Type = substitutedType };
    }

    public MethodMetadata SubstituteMethod(MethodMetadata method)
    {
        var effective = WithoutShadowedTypeParameters(typeParameters: method.TypeParameters);
        if (effective is null)
        {
            return method;
        }

        var substitutedReturnType = effective.Substitute(target: method.ReturnType);
        var substitutedParameters = effective.SubstituteParameters(parameters: method.Parameters);
        return ReferenceEquals(objA: substitutedReturnType, objB: method.ReturnType)
            && ReferenceEquals(objA: substitutedParameters, objB: method.Parameters)
                ? method
                : method with
                {
                    ReturnType = substitutedReturnType,
                    Parameters = substitutedParameters,
                };
    }

    public DelegateMetadata SubstituteDelegate(DelegateMetadata @delegate)
    {
        var effective = WithoutShadowedTypeParameters(typeParameters: @delegate.TypeParameters);
        if (effective is null)
        {
            return @delegate;
        }

        var substitutedReturnType = effective.Substitute(target: @delegate.ReturnType);
        var substitutedParameters = effective.SubstituteParameters(parameters: @delegate.Parameters);
        return ReferenceEquals(objA: substitutedReturnType, objB: @delegate.ReturnType)
            && ReferenceEquals(objA: substitutedParameters, objB: @delegate.Parameters)
                ? @delegate
                : @delegate with
                {
                    ReturnType = substitutedReturnType,
                    Parameters = substitutedParameters,
                };
    }

    private IReadOnlyList<ParameterMetadata> SubstituteParameters(IReadOnlyList<ParameterMetadata> parameters)
    {
        if (parameters.Count == 0)
        {
            return parameters;
        }

        ParameterMetadata[]? substituted = null;
        for (var index = 0; index < parameters.Count; index++)
        {
            var original = parameters[index: index];
            var replaced = SubstituteParameter(parameter: original);
            if (!ReferenceEquals(objA: original, objB: replaced) && substituted is null)
            {
                substituted = new ParameterMetadata[parameters.Count];
                for (var copyIndex = 0; copyIndex < index; copyIndex++)
                {
                    substituted[copyIndex] = parameters[index: copyIndex];
                }
            }

            if (substituted is not null)
            {
                substituted[index] = replaced;
            }
        }

        return substituted ?? parameters;
    }

    private TypeArgumentSubstitution? WithoutShadowedTypeParameters(IReadOnlyList<TypeParameterMetadata> typeParameters)
    {
        if (typeParameters.Count == 0)
        {
            return this;
        }

        var hasShadow = typeParameters.Any(predicate: parameter =>
            substitutions.ContainsKey(key: parameter.Name)
            || substitutions.ContainsKey(key: parameter.FullName));
        if (!hasShadow)
        {
            return this;
        }

#pragma warning disable IDE0028
        var filtered = new Dictionary<string, TypeMetadataReference>(comparer: StringComparer.Ordinal);
#pragma warning restore IDE0028
        foreach (var pair in substitutions.Where(predicate: pair =>
                     !typeParameters.Any(predicate: parameter =>
                         parameter.Name.Equals(value: pair.Key, comparisonType: StringComparison.Ordinal)
                         || parameter.FullName.Equals(value: pair.Key, comparisonType: StringComparison.Ordinal))))
        {
            filtered[pair.Key] = pair.Value;
        }

        return filtered.Count == 0 ? null : new TypeArgumentSubstitution(substitutions: filtered);
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
        var typeArguments = SubstituteTypeArguments(arguments: target.TypeArguments, hasChanges: ref hasChanges);
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

        var tupleElements = SubstituteTupleElements(tupleElements: target.TupleElements, hasChanges: ref hasChanges);

        return hasChanges
            ? target with
            {
                TypeArguments = typeArguments,
                ElementType = elementType,
                TupleElements = tupleElements,
                Interfaces = new LazySubstitutedTypeReferenceList(source: target.Interfaces, substitution: this),
            }
            : target;
    }

    private IReadOnlyList<TypeMetadataReference> SubstituteTypeArguments(
        IReadOnlyList<TypeMetadataReference> arguments,
        ref bool hasChanges)
    {
        if (arguments.Count == 0)
        {
            return arguments;
        }

        TypeMetadataReference[]? substitutedArguments = null;
        for (var index = 0; index < arguments.Count; index++)
        {
            var originalArgument = arguments[index: index];
            var substitutedArgument = SubstituteCore(target: originalArgument);
            if (!ReferenceEquals(objA: originalArgument, objB: substitutedArgument) && substitutedArguments is null)
            {
                substitutedArguments = new TypeMetadataReference[arguments.Count];
                for (var copyIndex = 0; copyIndex < index; copyIndex++)
                {
                    substitutedArguments[copyIndex] = arguments[index: copyIndex];
                }

                hasChanges = true;
            }

            if (substitutedArguments is not null)
            {
                substitutedArguments[index] = substitutedArgument;
            }
        }

        return substitutedArguments ?? arguments;
    }

    private IReadOnlyList<FieldMetadata> SubstituteTupleElements(
        IReadOnlyList<FieldMetadata> tupleElements,
        ref bool hasChanges)
    {
        if (tupleElements.Count == 0)
        {
            return tupleElements;
        }

        FieldMetadata[]? substitutedElements = null;
        for (var index = 0; index < tupleElements.Count; index++)
        {
            var originalField = tupleElements[index: index];
            var substitutedFieldType = SubstituteCore(target: originalField.Type);
            if (!ReferenceEquals(objA: originalField.Type, objB: substitutedFieldType) && substitutedElements is null)
            {
                substitutedElements = new FieldMetadata[tupleElements.Count];
                for (var copyIndex = 0; copyIndex < index; copyIndex++)
                {
                    substitutedElements[copyIndex] = tupleElements[index: copyIndex];
                }

                hasChanges = true;
            }

            if (substitutedElements is not null)
            {
                substitutedElements[index] = ReferenceEquals(objA: originalField.Type, objB: substitutedFieldType)
                    ? originalField
                    : originalField with { Type = substitutedFieldType };
            }
        }

        return substitutedElements ?? tupleElements;
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
