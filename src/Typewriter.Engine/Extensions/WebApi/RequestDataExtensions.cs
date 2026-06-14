using Typewriter.CodeModel;

namespace Typewriter.Extensions.WebApi;

public static class RequestDataExtensions
{
    public static string RequestData(this Method method)
    {
        return RequestData(method: method, route: UrlExtensions.DefaultRoute);
    }

    public static string RequestData(
        this Method method,
        string route)
    {
        ArgumentNullException.ThrowIfNull(argument: method);

        var url = method.Url(route: route);
        var dataParameters = method.Parameters
            .Where(predicate: parameter => !parameter.Type.Name.Equals(value: "CancellationToken", comparisonType: StringComparison.OrdinalIgnoreCase))
            .Where(predicate: parameter => !url.Contains(value: $"${{{UrlExtensions.GetParameterValue(method: method, name: parameter.Name)}}}", comparisonType: StringComparison.Ordinal))
            .ToArray();

        return dataParameters.Length switch
        {
            0 => "null",
            1 => dataParameters[0].Name,
            _ => $"{{ {string.Join(separator: ", ", values: dataParameters.Select(selector: parameter => $"{parameter.Name}: {parameter.Name}"))} }}",
        };
    }
}
