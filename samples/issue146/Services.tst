// output: Services.ts
${
    private void CollectTypeProperties(Type t, List<string> props)
    {
        Break();
        props.AddRange(t.Fields.Select(p => "'" + p.Name + "'").ToList());
        props.AddRange(t.Properties.Select(p => "'" + p.Name + "'").ToList());
        if (t.BaseClass != null)
        {
            CollectTypeProperties(t.BaseClass, props);
        }
    }

    string CollectParameterMembers(Parameter parameter)
    {
        var props = new List<string>();
        CollectTypeProperties(parameter.Type, props);
        return string.Join(", ", props);
    }
}
$Classes(*Controller)[export const $name = {$Methods[
    $name: {$Parameters[
        $name: {
            members: [$CollectParameterMembers],
            directProperties: [$Type[$Properties['$Name: $Type'][, ]]]
        }][,
]
    }]
};]
