namespace WebApiServices.Infrastructure;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class GenerateFrontendTypeAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RouteAttribute(string template) : Attribute
{
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class HttpGetAttribute(string template) : Attribute
{
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class HttpPostAttribute(string template) : Attribute
{
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class ProducesResponseTypeAttribute(Type type, int statusCode) : Attribute
{
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromBodyAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromRouteAttribute : Attribute
{
}
