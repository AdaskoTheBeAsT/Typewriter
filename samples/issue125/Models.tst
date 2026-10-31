${
    using System.Globalization;

    string InjectedType(Property property) =>
        (string)property.Attributes.First(a => a.Name.StartsWith("JsonInject")).Arguments[0].Value!;

    string Includes(Class c)
    {
        var fileIncludes = new List<string>();
        fileIncludes.AddRange(c.Properties
            .Where(p => p.Attributes.Any(a => a.Name.StartsWith("JsonInject")))
            .Select(p => p.Attributes.First(a => a.Name.StartsWith("JsonInject")).Arguments)
            .Where(argz => (bool)argz[1].Value!)
            .Select(argz => (string)argz[0].Value!)
            .Select(s => $"import {{ {s} }} from '../Special/{s}';"));
        return string.Join("\n", fileIncludes);
    }

    string PrimitiveValues(Class model)
    {
        var arguments = model.Attributes.Single(a => a.Name == "ValueKinds").Arguments;
        return $"{(int)arguments[0].Value!}|{((double)arguments[1].Value!).ToString(CultureInfo.InvariantCulture)}|{(char)arguments[2].Value!}|{(string)arguments[3].Value!}|{arguments[4].Value is null}|{(bool)arguments[5].Value!}";
    }

    bool IsInclude(AttributeArgument argument) => argument.Name == "Include";
}
$Classes(Model)[$Includes

export interface $Name {$Properties[
    $name: $InjectedType;]
}

// primitive values: $PrimitiveValues
// named flag: $Attributes(ValueKinds)[$Arguments($IsInclude)[$Value[enabled][disabled]]]]
