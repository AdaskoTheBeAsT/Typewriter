using Typewriter.Abstractions;
using Typewriter.Roslyn;
using Xunit;

namespace Typewriter.Roslyn.Tests;

public sealed class CSharpProjectMetadataProviderTests
{
    [Fact]
    public async Task GetMetadataExtractsTypesPropertiesCollectionsAndEnums()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            await File.WriteAllTextAsync(
                path: projectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                              <Nullable>enable</Nullable>
                              <ImplicitUsings>enable</ImplicitUsings>
                            </PropertyGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: directory, path2: "Models.cs"),
                contents: """
                          namespace Sample;

                          using System;
                          using System.Collections.Generic;
                          using System.Threading.Tasks;

                          [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Field)]
                          public sealed class GenerateFrontendTypeAttribute : Attribute
                          {
                          }

                          [AttributeUsage(AttributeTargets.Method)]
                          public sealed class HttpGetAttribute(string template) : Attribute
                          {
                          }

                          [AttributeUsage(AttributeTargets.Method)]
                          public sealed class ProducesResponseTypeAttribute(Type type, int statusCode) : Attribute
                          {
                          }

                          [AttributeUsage(AttributeTargets.Parameter)]
                          public sealed class FromRouteAttribute : Attribute
                          {
                          }

                          [AttributeUsage(AttributeTargets.Field)]
                          public sealed class EnumLabelAttribute(string label) : Attribute
                          {
                          }

                          public abstract class ControllerBase
                          {
                          }

                          /// <summary>Frontend user.</summary>
                          public sealed class User
                          {
                              /// <summary>Name shown to users.</summary>
                              public required string DisplayName { get; init; }

                              public string? Email { get; init; }

                              public IReadOnlyList<Order> Orders { get; init; } = [];

                              public IReadOnlyList<string?> Aliases { get; init; } = [];

                              public IReadOnlyDictionary<string, User?> RelatedUsers { get; init; } = new Dictionary<string, User?>();

                              public OrderStatus DefaultStatus { get; init; }

                              public string this[[FromRoute] int index, string? key]
                              {
                                  get => DisplayName;
                                  set { }
                              }
                          }

                          public sealed record Order(int Id, decimal Total);

                          public struct Money
                          {
                              public decimal Amount { get; set; }

                              public struct Token
                              {
                                  public int Value { get; set; }
                              }
                          }

                          [GenerateFrontendType]
                          public sealed class UsersController : ControllerBase
                          {
                              public const string ApiVersion = "v1";

                              [HttpGet("{id}")]
                              [ProducesResponseType(typeof(User), 200)]
                              public Task<User?> GetAsync([FromRoute] int id, string? filter = null) => Task.FromResult<User?>(null);
                          }

                          public static class AppConstants
                          {
                              public const int Answer = 42;
                          }

                          public enum OrderStatus
                          {
                              [EnumLabel("draft")]
                              Draft = 0,
                              Paid = 1
                          }
                          """);
            var provider = new CSharpProjectMetadataProvider();

            var metadata = await provider.GetMetadataAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory),
                cancellationToken: CancellationToken.None);

            Assert.Empty(collection: metadata.Diagnostics);
            var user = Assert.Single(collection: metadata.Types.Where(predicate: type => type.Name == "User"));
            Assert.Equal(expected: TypeMetadataKind.Class, actual: user.Kind);
            Assert.Equal(expected: "Frontend user.", actual: user.Documentation);
            Assert.NotNull(@object: user.Location);
            var email = Assert.Single(collection: user.Properties.Where(predicate: property => property.Name == "Email"));
            Assert.True(condition: email.Type.IsNullable);
            var displayName = Assert.Single(collection: user.Properties.Where(predicate: property => property.Name == "DisplayName"));
            Assert.Equal(expected: "Name shown to users.", actual: displayName.Documentation);
            Assert.NotNull(@object: displayName.Location);
            var indexer = Assert.Single(collection: user.Properties.Where(predicate: property => property.IsIndexer));
            Assert.Collection(
                collection: indexer.Parameters,
                parameter =>
                {
                    Assert.Equal(expected: "index", actual: parameter.Name);
                    Assert.Equal(expected: "Int32", actual: parameter.Type.Name);
                    Assert.Contains(collection: parameter.Attributes, filter: attribute => attribute.Name == "FromRoute");
                    Assert.Equal(expected: indexer.FullName, actual: parameter.ParentPropertyFullName);
                },
                parameter =>
                {
                    Assert.Equal(expected: "key", actual: parameter.Name);
                    Assert.True(condition: parameter.Type.IsNullable);
                    Assert.Equal(expected: indexer.FullName, actual: parameter.ParentPropertyFullName);
                });
            var orders = Assert.Single(collection: user.Properties.Where(predicate: property => property.Name == "Orders"));
            Assert.True(condition: orders.Type.IsCollection);
            Assert.Equal(expected: "Order", actual: orders.Type.ElementType?.Name);
            var aliases = Assert.Single(collection: user.Properties.Where(predicate: property => property.Name == "Aliases"));
            Assert.True(condition: aliases.Type.IsCollection);
            Assert.True(condition: aliases.Type.ElementType?.IsNullable);
            var relatedUsers = Assert.Single(collection: user.Properties.Where(predicate: property => property.Name == "RelatedUsers"));
            Assert.True(condition: relatedUsers.Type.IsDictionary);
            Assert.Equal(expected: "String", actual: relatedUsers.Type.TypeArguments[index: 0].Name);
            Assert.True(condition: relatedUsers.Type.TypeArguments[index: 1].IsNullable);
            var defaultStatus = Assert.Single(collection: user.Properties.Where(predicate: property => property.Name == "DefaultStatus"));
            Assert.True(condition: defaultStatus.Type.IsEnum);
            Assert.Collection(
                collection: defaultStatus.Type.EnumValues,
                value => Assert.Equal(expected: (string?)"Draft", actual: (string?)value.Name),
                value => Assert.Equal(expected: (string?)"Paid", actual: (string?)value.Name));
            var order = Assert.Single(collection: metadata.Types.Where(predicate: type => type.Name == "Order"));
            Assert.Equal(expected: TypeMetadataKind.Record, actual: order.Kind);
            var money = Assert.Single(collection: metadata.Types.Where(predicate: type => type.Name == "Money"));
            Assert.Equal(expected: TypeMetadataKind.Struct, actual: money.Kind);
            Assert.Contains(collection: money.Properties, filter: property => property.Name == "Amount");
            Assert.Contains(collection: money.NestedStructs, filter: nested => nested.Name == "Token");
            var controller = Assert.Single(collection: metadata.Types.Where(predicate: type => type.Name == "UsersController"));
            Assert.False(condition: controller.IsStatic);
            var apiVersion = Assert.Single(collection: controller.Constants.Where(predicate: constant => constant.Name == "ApiVersion"));
            Assert.Equal(expected: MetadataAccessibility.Public, actual: apiVersion.Accessibility);
            Assert.Equal(expected: "v1", actual: apiVersion.Value);
            Assert.Equal(expected: controller.FullName, actual: apiVersion.ParentTypeFullName);
            var getAsync = Assert.Single(collection: controller.Methods.Where(predicate: method => method.Name == "GetAsync"));
            Assert.Equal(expected: MetadataAccessibility.Public, actual: getAsync.Accessibility);
            Assert.Equal(expected: "Task", actual: getAsync.ReturnType.Name);
            Assert.Equal(expected: controller.FullName, actual: getAsync.ParentTypeFullName);
            Assert.Contains(collection: getAsync.Attributes, filter: attribute => attribute.Name == "HttpGet");
            Assert.Contains(
                collection: getAsync.Attributes,
                filter: attribute => attribute.Name == "ProducesResponseType"
                                     && attribute.Arguments.FirstOrDefault()?.Value == "typeof(Sample.User)");
            var id = Assert.Single(collection: getAsync.Parameters.Where(predicate: parameter => parameter.Name == "id"));
            Assert.False(condition: id.HasDefaultValue);
            Assert.Contains(collection: id.Attributes, filter: attribute => attribute.Name == "FromRoute");
            var filter = Assert.Single(collection: getAsync.Parameters.Where(predicate: parameter => parameter.Name == "filter"));
            Assert.True(condition: filter.Type.IsNullable);
            Assert.True(condition: filter.HasDefaultValue);
            Assert.Equal(expected: "null", actual: filter.DefaultValue);
            Assert.Equal(expected: getAsync.FullName, actual: filter.ParentMethodFullName);
            var constants = Assert.Single(collection: metadata.Types.Where(predicate: type => type.Name == "AppConstants"));
            Assert.True(condition: constants.IsStatic);
            Assert.Equal(expected: "42", actual: Assert.Single(collection: constants.Constants.Where(predicate: constant => constant.Name == "Answer")).Value);
            var status = Assert.Single(collection: metadata.Types.Where(predicate: type => type.Name == "OrderStatus"));
            Assert.Collection(
                collection: status.EnumValues,
                value =>
                {
                    Assert.Equal(expected: (string?)"Draft", actual: (string?)value.Name);
                    Assert.Equal<long?>(expected: 0, actual: value.Value);
                    Assert.Equal(expected: status.FullName, actual: (string?)value.ParentTypeFullName);
                    Assert.Contains(collection: value.Attributes, filter: attribute => attribute.Name == "EnumLabel");
                },
                value =>
                {
                    Assert.Equal(expected: (string?)"Paid", actual: (string?)value.Name);
                    Assert.Equal<long?>(expected: 1, actual: value.Value);
                });
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetMetadataUsesNullableContextAtDeclarationPosition()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            await File.WriteAllTextAsync(
                path: projectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                              <Nullable>enable</Nullable>
                              <ImplicitUsings>enable</ImplicitUsings>
                            </PropertyGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: directory, path2: "NullableRegions.cs"),
                contents: """
                          #nullable enable
                          using System.Collections.Generic;
                          using System.Threading.Tasks;

                          namespace Sample;

                          public sealed class EnabledRegion
                          {
                              public string? OptionalName { get; init; }

                              public IReadOnlyList<string?> OptionalAliases { get; init; } = [];

                              public IReadOnlyDictionary<string, EnabledRegion?> RelatedItems { get; init; } =
                                  new Dictionary<string, EnabledRegion?>();

                              public Task<EnabledRegion?> GetAsync() => Task.FromResult<EnabledRegion?>(null);
                          }

                          #nullable disable

                          public sealed class DisabledRegion
                          {
                              public string Name { get; init; }
                          }

                          #nullable restore

                          public sealed class RestoredRegion
                          {
                              public string? OptionalName { get; init; }
                          }
                          """);
            var provider = new CSharpProjectMetadataProvider();

            var metadata = await provider.GetMetadataAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory),
                cancellationToken: CancellationToken.None);

            Assert.Empty(collection: metadata.Diagnostics);
            var enabled = Assert.Single(collection: metadata.Types.Where(predicate: type => type.Name == "EnabledRegion"));
            Assert.True(condition: enabled.IsNullableAware);
            Assert.True(condition: Assert.Single(collection: enabled.Properties.Where(predicate: property => property.Name == "OptionalName")).Type.IsNullable);
            Assert.True(condition: Assert.Single(collection: enabled.Properties.Where(predicate: property => property.Name == "OptionalAliases")).Type.ElementType?.IsNullable);
            var relatedItems = Assert.Single(collection: enabled.Properties.Where(predicate: property => property.Name == "RelatedItems"));
            Assert.True(condition: relatedItems.Type.IsDictionary);
            Assert.True(condition: relatedItems.Type.TypeArguments[index: 1].IsNullable);
            var getAsync = Assert.Single(collection: enabled.Methods.Where(predicate: method => method.Name == "GetAsync"));
            Assert.True(condition: getAsync.ReturnType.TypeArguments[index: 0].IsNullable);

            var disabled = Assert.Single(collection: metadata.Types.Where(predicate: type => type.Name == "DisabledRegion"));
            Assert.False(condition: disabled.IsNullableAware);

            var restored = Assert.Single(collection: metadata.Types.Where(predicate: type => type.Name == "RestoredRegion"));
            Assert.True(condition: restored.IsNullableAware);
            Assert.True(condition: Assert.Single(collection: restored.Properties.Where(predicate: property => property.Name == "OptionalName")).Type.IsNullable);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetMetadataHandlesAllowedValuesAttributeWithNullAndConstants()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            await File.WriteAllTextAsync(
                path: projectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                              <Nullable>enable</Nullable>
                              <ImplicitUsings>enable</ImplicitUsings>
                            </PropertyGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: directory, path2: "Models.cs"),
                contents: """
                          namespace Sample;

                          using System.ComponentModel.DataAnnotations;

                          public static class TestPseudoEnum
                          {
                              public const string Value1 = "value1";
                              public const string Value2 = "value2";
                          }

                          public sealed record Test
                          {
                              [AllowedValues(null, TestPseudoEnum.Value1, TestPseudoEnum.Value2)]
                              public string? PseudoEnum { get; init; }
                          }
                          """);
            var provider = new CSharpProjectMetadataProvider();

            var metadata = await provider.GetMetadataAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory),
                cancellationToken: CancellationToken.None);

            Assert.Empty(collection: metadata.Diagnostics);
            var test = Assert.Single(collection: metadata.Types.Where(predicate: type => type.Name == "Test"));
            var pseudoEnum = Assert.Single(collection: test.Properties.Where(predicate: property => property.Name == "PseudoEnum"));
            var allowedValues = Assert.Single(collection: pseudoEnum.Attributes.Where(predicate: attribute => attribute.Name == "AllowedValues"));
            var argument = Assert.Single(collection: allowedValues.Arguments);
            Assert.Contains(expectedSubstring: "value1", actualString: argument.Value, comparisonType: StringComparison.Ordinal);
            Assert.Contains(expectedSubstring: "value2", actualString: argument.Value, comparisonType: StringComparison.Ordinal);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetMetadataPreservesExplicitNullableAnnotationsWhenProjectNullableIsNotEnabled()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            await File.WriteAllTextAsync(
                path: projectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                            </PropertyGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: directory, path2: "Model.cs"),
                contents: """
                          namespace Sample;

                          public sealed class CombinedQueryModel
                          {
                              public string FirstName { get; set; } = string.Empty;

                              public string? MiddleName { get; set; }
                          }
                          """);
            var provider = new CSharpProjectMetadataProvider();

            var metadata = await provider.GetMetadataAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory),
                cancellationToken: CancellationToken.None);

            Assert.Empty(collection: metadata.Diagnostics);
            var type = Assert.Single(collection: metadata.Types.Where(predicate: type => type.Name == "CombinedQueryModel"));
            Assert.False(condition: Assert.Single(collection: type.Properties.Where(predicate: property => property.Name == "FirstName")).Type.IsNullable);
            Assert.True(condition: Assert.Single(collection: type.Properties.Where(predicate: property => property.Name == "MiddleName")).Type.IsNullable);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetMetadataExtractsCodeModelParityMembers()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "Sample.csproj");
            await File.WriteAllTextAsync(
                path: projectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                              <Nullable>enable</Nullable>
                              <ImplicitUsings>enable</ImplicitUsings>
                            </PropertyGroup>
                          </Project>
                          """);
            var sourcePath = Path.Combine(path1: directory, path2: "Parity.cs");
            await File.WriteAllTextAsync(
                path: sourcePath,
                contents: """
                          namespace Sample;

                          using System;

                          public delegate string TopLevelDelegate(int value);

                          public abstract class BaseModel
                          {
                              public abstract string AbstractName { get; }
                          }

                          public interface IModel
                          {
                          }

                          /// <summary>
                          /// Outer model summary.
                          /// </summary>
                          public class Outer<T> : BaseModel, IModel
                          {
                              /// <summary>Instance field summary.</summary>
                              public string InstanceField = "";

                              /// <summary>Static label summary.</summary>
                              public static readonly string StaticLabel = "ready";

                              /// <summary>Changed event summary.</summary>
                              public event EventHandler? Changed;

                              public override string AbstractName { get; } = "";

                              public virtual (string Name, int Count) Pair { get; init; }

                              public delegate TResult Mapper<TArg, TResult>(TArg value);

                              public class NestedClass
                              {
                              }

                              public record NestedRecord;

                              public enum NestedEnum
                              {
                                  One = 1
                              }

                              public interface INested
                              {
                              }

                              /// <summary>
                              /// Echoes a value.
                              /// </summary>
                              /// <param name="value">Value description.</param>
                              /// <returns>Return description.</returns>
                              public string Echo<TMethod>(string value) => value;
                          }
                          """);
            var provider = new CSharpProjectMetadataProvider();

            var metadata = await provider.GetMetadataAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory),
                cancellationToken: CancellationToken.None);

            Assert.Empty(collection: metadata.Diagnostics);
            var topLevelDelegate = Assert.Single(collection: metadata.Delegates.Where(predicate: item => item.Name == "TopLevelDelegate"));
            Assert.Equal(expected: "String", actual: topLevelDelegate.ReturnType.Name);
            Assert.Equal(expected: "value", actual: Assert.Single(collection: topLevelDelegate.Parameters).Name);

            var outer = Assert.Single(collection: metadata.Types.Where(predicate: type => type.FullName == "Sample.Outer"));
            Assert.Equal(expected: "Outer model summary.", actual: outer.DocComment?.Summary);
            Assert.Equal(expected: Path.GetFullPath(path: sourcePath), actual: Assert.Single(collection: outer.FileLocations));
            Assert.Equal(expected: "T", actual: Assert.Single(collection: outer.TypeParameters).Name);
            Assert.Contains(collection: outer.BaseTypes, filter: type => type.Name == "BaseModel");
            Assert.Contains(collection: outer.BaseTypes, filter: type => type.Name == "IModel");

            var field = Assert.Single(collection: outer.Fields.Where(predicate: item => item.Name == "InstanceField"));
            Assert.Equal(expected: "Instance field summary.", actual: field.DocComment?.Summary);
            var staticField = Assert.Single(collection: outer.StaticReadOnlyFields.Where(predicate: item => item.Name == "StaticLabel"));
            Assert.Equal(expected: "ready", actual: staticField.Value);
            Assert.Equal(expected: "Static label summary.", actual: staticField.DocComment?.Summary);
            var changed = Assert.Single(collection: outer.Events.Where(predicate: item => item.Name == "Changed"));
            Assert.Equal(expected: "Changed event summary.", actual: changed.DocComment?.Summary);

            var mapper = Assert.Single(collection: outer.Delegates.Where(predicate: item => item.Name == "Mapper"));
            Assert.True(condition: mapper.IsGeneric);
            Assert.Equal(expected: ["TArg", "TResult"], actual: mapper.TypeParameters.Select(selector: item => item.Name));
            Assert.Equal(expected: "value", actual: Assert.Single(collection: mapper.Parameters).Name);

            Assert.Contains(collection: outer.NestedClasses, filter: type => type.Name == "NestedClass");
            Assert.Contains(collection: outer.NestedRecords, filter: type => type.Name == "NestedRecord");
            Assert.Contains(collection: outer.NestedEnums, filter: type => type.Name == "NestedEnum");
            Assert.Contains(collection: outer.NestedInterfaces, filter: type => type.Name == "INested");

            var pair = Assert.Single(collection: outer.Properties.Where(predicate: property => property.Name == "Pair"));
            Assert.True(condition: pair.IsVirtual);
            Assert.True(condition: pair.Type.IsValueTuple);
            Assert.Equal(expected: ["Name", "Count"], actual: pair.Type.TupleElements.Select(selector: element => element.Name));

            var echo = Assert.Single(collection: outer.Methods.Where(predicate: method => method.Name == "Echo"));
            Assert.True(condition: echo.IsGeneric);
            Assert.Equal(expected: "TMethod", actual: Assert.Single(collection: echo.TypeParameters).Name);
            Assert.Equal(expected: "Echoes a value.", actual: echo.DocComment?.Summary);
            Assert.Equal(expected: "Return description.", actual: echo.DocComment?.Returns);
            var valueComment = Assert.Single(collection: echo.DocComment?.Parameters ?? []);
            Assert.Equal(expected: "value", actual: valueComment.Name);
            Assert.Equal(expected: "Value description.", actual: valueComment.Description);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetMetadataCompilesProjectReferencesSeparatelyBeforeMergingMetadata()
    {
        var directory = CreateProjectDirectory();
        try
        {
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: directory, path2: "Directory.Build.props"),
                contents: """
                          <Project>
                            <PropertyGroup>
                              <NuGetAudit>false</NuGetAudit>
                            </PropertyGroup>
                          </Project>
                          """);
            var libADirectory = Path.Combine(path1: directory, path2: "LibA");
            var libBDirectory = Path.Combine(path1: directory, path2: "LibB");
            var appDirectory = Path.Combine(path1: directory, path2: "App");
            Directory.CreateDirectory(path: libADirectory);
            Directory.CreateDirectory(path: libBDirectory);
            Directory.CreateDirectory(path: appDirectory);

            await File.WriteAllTextAsync(
                path: Path.Combine(path1: libADirectory, path2: "LibA.csproj"),
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                              <ImplicitUsings>enable</ImplicitUsings>
                              <Nullable>enable</Nullable>
                            </PropertyGroup>
                            <ItemGroup>
                              <PackageReference Include="Newtonsoft.Json" Version="12.0.3" />
                            </ItemGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: libADirectory, path2: "HelperA.cs"),
                contents: """
                          using Newtonsoft.Json;

                          namespace LibA;

                          public static class HelperA
                          {
                              public static string Serialize(object value) => JsonConvert.SerializeObject(value);
                          }
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: libBDirectory, path2: "LibB.csproj"),
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                              <ImplicitUsings>enable</ImplicitUsings>
                              <Nullable>enable</Nullable>
                            </PropertyGroup>
                            <ItemGroup>
                              <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
                            </ItemGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: libBDirectory, path2: "HelperB.cs"),
                contents: """
                          using Newtonsoft.Json;

                          namespace LibB;

                          public static class HelperB
                          {
                              public static string Serialize(object value) => JsonConvert.SerializeObject(value);
                          }
                          """);

            var appProjectPath = Path.Combine(path1: appDirectory, path2: "App.csproj");
            await File.WriteAllTextAsync(
                path: appProjectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                              <ImplicitUsings>enable</ImplicitUsings>
                              <Nullable>enable</Nullable>
                            </PropertyGroup>
                            <ItemGroup>
                              <ProjectReference Include="..\LibA\LibA.csproj" />
                              <ProjectReference Include="..\LibB\LibB.csproj" />
                            </ItemGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: appDirectory, path2: "MyModel.cs"),
                contents: """
                          namespace App;

                          public sealed class MyModel
                          {
                              public int Id { get; set; }
                          }
                          """);
            var provider = new CSharpProjectMetadataProvider();

            var metadata = await provider.GetMetadataAsync(
                project: new ProjectContext(ProjectPath: appProjectPath, WorkspacePath: directory),
                cancellationToken: CancellationToken.None);

            Assert.Empty(collection: metadata.Diagnostics);
            Assert.Contains(collection: metadata.Types, filter: type => type.FullName == "App.MyModel");
            Assert.Contains(collection: metadata.Types, filter: type => type.FullName == "LibA.HelperA");
            Assert.Contains(collection: metadata.Types, filter: type => type.FullName == "LibB.HelperB");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    private static string CreateProjectDirectory()
    {
        var directory = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "Typewriter.Roslyn.Tests",
            path3: Guid.NewGuid().ToString(format: "N"));
        Directory.CreateDirectory(path: directory);
        return directory;
    }

    private static async Task DeleteDirectoryWithRetryAsync(string directory)
    {
        const int MaxAttempts = 10;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                if (Directory.Exists(path: directory))
                {
                    Directory.Delete(path: directory, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < MaxAttempts)
            {
                await Task.Delay(millisecondsDelay: 100).ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxAttempts)
            {
                await Task.Delay(millisecondsDelay: 100).ConfigureAwait(continueOnCapturedContext: false);
            }
        }
    }
}
