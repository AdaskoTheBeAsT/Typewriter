// typewriter-template: v1
${
    string DescribeHierarchy(Class model)
    {
        var baseChain = new System.Collections.Generic.List<string>();
        for (var current = model.BaseClass; current != null; current = current.BaseClass)
        {
            var args = current.TypeArguments.Any()
                ? $"<{string.Join(", ", current.TypeArguments.Select(a => a.Name))}>"
                : string.Empty;
            var interfaces = current.Interfaces.Any()
                ? $"[{string.Join(", ", current.Interfaces.Select(FormatInterface))}]"
                : "[]";
            baseChain.Add($"{current.Name}{args}{interfaces}");
        }

        return baseChain.Count == 0 ? "none" : string.Join(" -> ", baseChain);
    }

    string DescribePropertyInterfaces(Property property)
    {
        return string.Join(
            ", ",
            property.Type.Interfaces.Select(i =>
            {
                var inherited = i.Interfaces.Any()
                    ? $" : {string.Join(", ", i.Interfaces.Select(FormatInterface))}"
                    : string.Empty;
                return $"{FormatInterface(i)}{inherited}";
            }));
    }

    string FormatInterface(Interface iface)
    {
        return iface.TypeArguments.Any()
            ? $"{iface.Name}<{string.Join(", ", iface.TypeArguments.Select(a => a.Name))}>"
            : iface.Name;
    }
}
$Classes[
// Class: $Name$TypeParameters
// Direct Interfaces Token: $Interfaces[$Name$TypeArguments][, ]
// Base Hierarchy: $DescribeHierarchy
export interface $Name$TypeParameters {
$Properties[
    // Property Interfaces Token: $Type[$Interfaces[$Name$TypeArguments][, ]]
    // Property Interfaces Helper: $DescribePropertyInterfaces
    $name: $Type;
]}
]
