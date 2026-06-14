using System.Text;
using System.Text.RegularExpressions;
using Typewriter.CodeModel;

namespace Typewriter.Extensions.WebApi;

public static class UrlExtensions
{
    internal const string DefaultRoute = "api/{controller}/{id?}";

    public static string Url(this Method method)
    {
        return Url(method: method, route: DefaultRoute);
    }

    public static string Url(
        this Method method,
        string route)
    {
        ArgumentNullException.ThrowIfNull(argument: method);

        route = Route(method: method, route: route);
        route = RemoveUnmatchedOptionalParameters(method: method, route: route);
        route = ReplaceSpecialParameters(method: method, route: route);
        route = ConvertRouteParameters(method: method, route: route);
        route = AppendQueryString(method: method, route: route);

        return route;
    }

    public static string Route(this Method method)
    {
        return Route(method: method, route: DefaultRoute);
    }

    public static string Route(
        this Method method,
        string route)
    {
        ArgumentNullException.ThrowIfNull(argument: method);

        var routeAttribute = method.Attributes.FirstOrDefault(
                predicate: attribute => attribute.Name.Equals(value: nameof(Route), comparisonType: StringComparison.OrdinalIgnoreCase))
            ?? method.Attributes.FirstOrDefault(predicate: attribute => attribute.Name.StartsWith(value: "Http", comparisonType: StringComparison.OrdinalIgnoreCase));
        var routePrefix = GetRoutePrefix(@class: method.Parent as Class);

        if (routeAttribute is not null)
        {
            var value = ParseAttributeValue(value: routeAttribute.Value);
#pragma warning disable CC0014 // Use ternary operator
            if (string.IsNullOrEmpty(value: value))
            {
                route = routePrefix ?? route;
            }
            else
            {
                route = value.StartsWith(value: "~/", comparisonType: StringComparison.OrdinalIgnoreCase)
                    ? value[2..]
                    : CombineRoute(routePrefix: routePrefix, route: value);
            }
#pragma warning restore CC0014 // Use ternary operator
        }
        else if (routePrefix is not null)
        {
            route = string.Concat(str0: routePrefix, str1: "/", str2: route);
        }

        return route;
    }

    internal static string GetParameterValue(
        Method method,
        string name)
    {
        var parameter = method.Parameters.FirstOrDefault(
            predicate: candidate => candidate.Name.Equals(value: name, comparisonType: StringComparison.OrdinalIgnoreCase));
        if (parameter is null)
        {
            return name;
        }

        if (parameter.Type.Name.Equals(value: "string", comparisonType: StringComparison.OrdinalIgnoreCase)
            || parameter.Type.Name.Equals(value: "string | null", comparisonType: StringComparison.OrdinalIgnoreCase)
            || parameter.Type.IsDate)
        {
            // Nullable values are not assignable to encodeURIComponent (issue 53).
            return parameter.Type.IsNullable
                || parameter.Type.Name.Equals(value: "string | null", comparisonType: StringComparison.OrdinalIgnoreCase)
                ? $"encodeURIComponent({name} ?? '')"
                : $"encodeURIComponent({name})";
        }

        return name;
    }

    private static string CombineRoute(
        string? routePrefix,
        string route)
    {
        return routePrefix is null ? route : string.Concat(str0: routePrefix, str1: "/", str2: route);
    }

    private static string? GetRoutePrefix(Class? @class)
    {
        if (@class is null)
        {
            return null;
        }

        var routePrefixValue = @class.Attributes
            .FirstOrDefault(predicate: attribute => attribute.Name.Equals(value: "RoutePrefix", comparisonType: StringComparison.OrdinalIgnoreCase))
            ?.Value;
        var routePrefix = routePrefixValue is null
            ? null
            : ParseAttributeValue(value: routePrefixValue).TrimEnd(trimChar: '/');

        if (string.IsNullOrEmpty(value: routePrefix))
        {
            var routeValue = @class.Attributes
                .FirstOrDefault(predicate: attribute => attribute.Name.Equals(value: nameof(Route), comparisonType: StringComparison.OrdinalIgnoreCase))
                ?.Value;
            routePrefix = routeValue is null
                ? null
                : ParseAttributeValue(value: routeValue).TrimEnd(trimChar: '/');
        }

        if (!string.IsNullOrEmpty(value: routePrefix))
        {
            return routePrefix;
        }

        return @class.BaseClass is not null ? GetRoutePrefix(@class: @class.BaseClass) : null;
    }

    private static string ParseAttributeValue(string value)
    {
        if (string.IsNullOrEmpty(value: value))
        {
            return value;
        }

        if (value.StartsWith(value: '\"'))
        {
            return Regex.Match(
                input: value,
                pattern: @"(?<="")(?:\\.|[^""\\])*(?="")",
                options: RegexOptions.CultureInvariant,
                matchTimeout: TimeSpan.FromSeconds(seconds: 1)).Value;
        }

        // Named-argument-only values such as `Name = "ListFoo", Order = 5` carry no
        // route template and must not leak into the url/route (issue 52).
        if (Regex.IsMatch(
                input: value,
                pattern: @"^\s*\w+\s*=",
                options: RegexOptions.CultureInvariant,
                matchTimeout: TimeSpan.FromSeconds(seconds: 1)))
        {
            var templateArgument = Regex.Match(
                input: value,
                pattern: @"\bTemplate\s*=\s*""(?<routeTemplate>(?:\\.|[^""\\])*)""",
                options: RegexOptions.CultureInvariant,
                matchTimeout: TimeSpan.FromSeconds(seconds: 1));
            return templateArgument.Success ? templateArgument.Groups[groupname: "routeTemplate"].Value : string.Empty;
        }

        return value;
    }

    private static string RemoveUnmatchedOptionalParameters(
        Method method,
        string route)
    {
        var parameters = Regex.Matches(
                input: route,
                pattern: @"\{(?<parameter>\w+):*\w*\?\}",
                options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
                matchTimeout: TimeSpan.FromSeconds(seconds: 1))
            .Cast<Match>()
            .Select(selector: match => match.Groups[groupname: "parameter"].Value);
        var unmatchedParameters = parameters
            .Where(predicate: parameter => !method.Parameters.Any(
                predicate: candidate => candidate.Name.Equals(value: parameter, comparisonType: StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        foreach (var parameter in unmatchedParameters)
        {
            route = Regex.Replace(
                input: route,
                pattern: $"\\{{{parameter}:*\\w*\\?\\}}",
                replacement: string.Empty,
                options: RegexOptions.CultureInvariant,
                matchTimeout: TimeSpan.FromSeconds(seconds: 1));
        }

        return route;
    }

    private static string ReplaceSpecialParameters(
        Method method,
        string route)
    {
        if ((route.Contains(value: "{controller}", comparisonType: StringComparison.Ordinal) || route.Contains(value: "[controller]", comparisonType: StringComparison.Ordinal))
            && !method.Parameters.Any(predicate: parameter => parameter.name.Equals(value: "controller", comparisonType: StringComparison.OrdinalIgnoreCase))
            && method.Parent is Class parent)
        {
            var controller = parent.Name;
            if (controller.EndsWith(value: "Controller", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                controller = controller[..^10];
            }

            route = route
                .Replace(oldValue: "{controller}", newValue: controller, comparisonType: StringComparison.Ordinal)
                .Replace(oldValue: "[controller]", newValue: controller, comparisonType: StringComparison.Ordinal);
        }

        if ((route.Contains(value: "{action}", comparisonType: StringComparison.Ordinal) || route.Contains(value: "[action]", comparisonType: StringComparison.Ordinal))
            && !method.Parameters.Any(predicate: parameter => parameter.name.Equals(value: "action", comparisonType: StringComparison.OrdinalIgnoreCase)))
        {
            var action = method.Attributes
                .FirstOrDefault(predicate: attribute => attribute.Name.Equals(value: "ActionName", comparisonType: StringComparison.OrdinalIgnoreCase))
                ?.Value ?? method.name;
            route = route
                .Replace(oldValue: "{action}", newValue: action, comparisonType: StringComparison.Ordinal)
                .Replace(oldValue: "[action]", newValue: action, comparisonType: StringComparison.Ordinal);
        }

        return route;
    }

    private static string ConvertRouteParameters(
        Method method,
        string route)
    {
        return Regex.Replace(
            input: route,
            pattern: @"\{\*?(?<parameter>\w+):?\w*\??\}",
            evaluator: match => $"${{{GetParameterValue(method: method, name: match.Groups[groupname: "parameter"].Value)}}}",
            options: RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
            matchTimeout: TimeSpan.FromSeconds(seconds: 1));
    }

    private static string AppendQueryString(
        Method method,
        string route)
    {
        var prefix = route.Contains(value: '?', comparisonType: StringComparison.Ordinal) ? "&" : "?";
        var builder = new StringBuilder(value: route);
        foreach (var parameterName in method.Parameters
                     .Where(predicate: parameter => parameter.Type.IsPrimitive
                                                    && !parameter.Attributes.Any(
                                                        predicate: attribute => attribute.Name.Equals(value: "FromBody", comparisonType: StringComparison.OrdinalIgnoreCase)))
                     .Select(selector: parameter => parameter.Name))
        {
            if (route.Contains(value: $"${{{GetParameterValue(method: method, name: parameterName)}}}", comparisonType: StringComparison.Ordinal))
            {
                continue;
            }

            builder.Append(value: prefix)
                .Append(value: parameterName)
                .Append(value: "=${")
                .Append(value: GetParameterValue(method: method, name: parameterName))
                .Append(value: '}');
            prefix = "&";
        }

        return builder.ToString();
    }
}
