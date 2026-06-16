// typewriter-template: v1
// output: generated/signalr-chat.service.ts
${
    using System.Collections.Generic;
    using System.Linq;

    string HubName(string className) => className.EndsWith("Hub") ? className.Substring(0, className.Length - 3) : className;

    string CleanAttributeValue(string value) => value.Trim().Trim('"').Trim('\'').Trim().TrimStart('/');

    bool IncludeClass(Class c)
    {
        var attr = c.Attributes.FirstOrDefault(a => a.Name == "GenerateSignalRFrontendType");
        var parent = c.BaseClass;
        return attr != null
            && parent != null
            && (parent.Name == "Hub" || parent.FullName == "Microsoft.AspNetCore.SignalR.Hub");
    }

    bool IncludeHubMethod(Method m)
    {
        return !m.Attributes.Any(a => a.Name == "NonHubMethod")
            && m.Name != "OnConnectedAsync"
            && m.Name != "OnDisconnectedAsync";
    }

    Type EffectiveType(Type t)
    {
        return t.IsTask && t.TypeArguments.Any()
            ? t.TypeArguments.FirstOrDefault()
            : t;
    }

    bool IsStreamLike(Type t)
    {
        var effectiveType = EffectiveType(t);
        return effectiveType.OriginalName == "IAsyncEnumerable"
            || effectiveType.OriginalName == "ChannelReader"
            || effectiveType.OriginalName == "IObservable";
    }

    bool IsStreamingMethod(Method m) => IncludeHubMethod(m) && IsStreamLike(m.Type);

    bool IsInvokeMethod(Method m) => IncludeHubMethod(m) && !IsStreamLike(m.Type);

    string MethodName(Method m) => IsStreamingMethod(m) ? $"stream{m.Name}" : $"invoke{m.Name}";

    string SignalRMethodName(Method m)
    {
        var attr = m.Attributes.FirstOrDefault(a => a.Name == "HubMethodName");
        return attr == null ? m.Name : CleanAttributeValue(attr.Value);
    }

    bool IsParameterSkipped(Parameter parameter)
    {
        return parameter.Type.OriginalName == "CancellationToken"
            || parameter.Type.OriginalName == "ClaimsPrincipal";
    }

    List<Parameter> SkipParameters(Method m)
    {
        return m.Parameters.Where(parameter => !IsParameterSkipped(parameter)).ToList();
    }

    string InvocationArguments(Method m)
    {
        var names = SkipParameters(m).Select(p => p.name).ToList();
        return names.Count == 0 ? string.Empty : ", " + string.Join(", ", names);
    }

    string GetHubRouteValue(Class c)
    {
        var route = c.Attributes.FirstOrDefault(a => a.Name == "HubRoute");
        var routeValue = route == null ? $"hubs/{HubName(c.Name)}" : CleanAttributeValue(route.Value);
        return routeValue.Replace("[Hub]", HubName(c.Name));
    }

    string ToTypeScriptType(Type t)
    {
        if (t.IsTask)
        {
            return t.TypeArguments.Any() ? ToTypeScriptType(t.TypeArguments.FirstOrDefault()) : "void";
        }

        if (IsStreamLike(t))
        {
            return t.TypeArguments.Any() ? ToTypeScriptType(t.TypeArguments.FirstOrDefault()) : "unknown";
        }

        return t.Name;
    }

    string InvokeReturnType(Method m) => ToTypeScriptType(m.Type);

    string StreamReturnType(Method m) => ToTypeScriptType(m.Type);

    string ServiceName(Class c) => $"SignalR{HubName(c.Name)}Service";
}
$Classes($IncludeClass)[
export class $ServiceName {
  readonly url = '$GetHubRouteValue';
$Methods($IncludeHubMethod)[
  $MethodName$IsStreamingMethod[$$]($SkipParameters[$name: $Type][, ]): $IsStreamingMethod[Observable<$StreamReturnType>]$IsInvokeMethod[Promise<$InvokeReturnType>] {
    return '$SignalRMethodName'$InvocationArguments;
  }]
}]
