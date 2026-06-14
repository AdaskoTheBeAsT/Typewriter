namespace SignalRHubs.Infrastructure;

[AttributeUsage(AttributeTargets.Class)]
public sealed class GenerateSignalRFrontendTypeAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class HubRouteAttribute(string template) : Attribute
{
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class HubMethodNameAttribute(string name) : Attribute
{
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class NonHubMethodAttribute : Attribute
{
}
