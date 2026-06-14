// typewriter-template: v1
// output: generated/users.service.ts
${
    using Typewriter.Extensions.WebApi;
    using System.Text.RegularExpressions;

    bool IncludeController(Class c)
    {
        return c.Attributes.Any(a => a.Name == "GenerateFrontendType")
            && c.BaseClass != null
            && c.BaseClass.Name == "ControllerBase";
    }

    string ServiceName(Class c) => $"{c.Name.Replace("Controller", "Service")}";

    string MethodName(Method m)
    {
        return m.Name.EndsWith("Async")
            ? m.name.Substring(0, m.name.Length - 5)
            : m.name;
    }

    string Parameters(Method m)
    {
        return string.Join(", ", m.Parameters
            .Where(p => p.Type.OriginalName != "CancellationToken")
            .Select(p => $"{p.name}: {p.Type.Name}"));
    }

    string ReturnType(Method m)
    {
        var attr = m.Attributes.FirstOrDefault(a => a.Name == "ProducesResponseType");
        if (attr == null)
        {
            return m.Type.Name;
        }

        var regex = new Regex(".*typeof[(]([^.<]*[.])*([^)<]*)(([<])([^.<]*[.])*([^)>]*)([>]))?[)].*");
        return regex.Replace(attr.Value, "$2$4$6$7");
    }

    string HttpVerb(Method m) => m.HttpMethod().ToUpperInvariant();

    string Route(Method m) => m.Url("api/[controller]");

    string Body(Method m) => m.RequestData("api/[controller]");
}
$Classes($IncludeController)[
export class $ServiceName {
$Methods[
  $MethodName($Parameters): Promise<$ReturnType> {
    return this.request('$HttpVerb', `$Route`, $Body);
  }]
}]
