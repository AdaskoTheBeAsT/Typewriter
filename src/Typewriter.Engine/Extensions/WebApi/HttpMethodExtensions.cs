using Typewriter.CodeModel;

namespace Typewriter.Extensions.WebApi;

public static class HttpMethodExtensions
{
    private static readonly string[] ValidVerbs = ["get", "post", "put", "delete", "patch", "head", "options"];

    public static string HttpMethod(this Method method)
    {
        ArgumentNullException.ThrowIfNull(argument: method);

        var httpAttributes = method.Attributes
            .Where(predicate: attribute => attribute.Name.StartsWith(value: "Http", comparisonType: StringComparison.OrdinalIgnoreCase));
        var acceptAttribute = method.Attributes.FirstOrDefault(
            predicate: attribute => attribute.Name.Equals(value: "AcceptVerbs", comparisonType: StringComparison.OrdinalIgnoreCase));
        var verbs = httpAttributes
            .Select(selector: attribute => attribute.Name[4..].ToLowerInvariant())
            .Where(predicate: verb => verb.Length > 0)
            .ToList();

        if (acceptAttribute is not null)
        {
            verbs.AddRange(
                collection: acceptAttribute.Value
                    .Split(separator: ',', options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(selector: verb => verb.Trim(trimChar: '"').ToLowerInvariant()));
        }

        if (verbs.Contains(value: "post", comparer: StringComparer.OrdinalIgnoreCase))
        {
            return "post";
        }

        if (verbs.Count > 0)
        {
            return verbs[index: 0];
        }

        var methodName = method.Name.ToLowerInvariant();
        return ValidVerbs.FirstOrDefault(predicate: verb => methodName.StartsWith(value: verb, comparisonType: StringComparison.OrdinalIgnoreCase))
            ?? "post";
    }
}
