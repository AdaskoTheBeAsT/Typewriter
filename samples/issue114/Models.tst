// typewriter-template: v1
${
    string ModelName(Class model) => model.Name;

    string Foo(Class model)
    {
        var methodName = "foo";
        var sb = new StringBuilder();
        sb.AppendLine($"public {methodName}() {{");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
$Classes[export class $ModelName {
    $Foo
}]
