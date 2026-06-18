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

            metadata.Diagnostics.Should().BeEmpty();
            var user = metadata.Types.Should().ContainSingle(type => type.Name == "User").Which;
            user.Kind.Should().Be(TypeMetadataKind.Class);
            user.Documentation.Should().Be("Frontend user.");
            user.Location.Should().NotBeNull();
            var email = user.Properties.Should().ContainSingle(property => property.Name == "Email").Which;
            email.Type.IsNullable.Should().BeTrue();
            var displayName = user.Properties.Should().ContainSingle(property => property.Name == "DisplayName").Which;
            displayName.Documentation.Should().Be("Name shown to users.");
            displayName.Location.Should().NotBeNull();
            var indexer = user.Properties.Should().ContainSingle(property => property.IsIndexer).Which;
            indexer.Parameters.Should().SatisfyRespectively(
                parameter =>
                {
                    parameter.Name.Should().Be("index");
                    parameter.Type.Name.Should().Be("Int32");
                    parameter.Attributes.Should().Contain(attribute => attribute.Name == "FromRoute");
                    parameter.ParentPropertyFullName.Should().Be(indexer.FullName);
                },
                parameter =>
                {
                    parameter.Name.Should().Be("key");
                    parameter.Type.IsNullable.Should().BeTrue();
                    parameter.ParentPropertyFullName.Should().Be(indexer.FullName);
                });
            var orders = user.Properties.Should().ContainSingle(property => property.Name == "Orders").Which;
            orders.Type.IsCollection.Should().BeTrue();
            orders.Type.ElementType.Should().NotBeNull();
            orders.Type.ElementType!.Name.Should().Be("Order");
            var aliases = user.Properties.Should().ContainSingle(property => property.Name == "Aliases").Which;
            aliases.Type.IsCollection.Should().BeTrue();
            aliases.Type.ElementType.Should().NotBeNull();
            aliases.Type.ElementType!.IsNullable.Should().BeTrue();
            var relatedUsers = user.Properties.Should().ContainSingle(property => property.Name == "RelatedUsers").Which;
            relatedUsers.Type.IsDictionary.Should().BeTrue();
            relatedUsers.Type.TypeArguments[index: 0].Name.Should().Be("String");
            relatedUsers.Type.TypeArguments[index: 1].IsNullable.Should().BeTrue();
            var defaultStatus = user.Properties.Should().ContainSingle(property => property.Name == "DefaultStatus").Which;
            defaultStatus.Type.IsEnum.Should().BeTrue();
            defaultStatus.Type.EnumValues.Select(selector: value => value.Name).Should().Equal("Draft", "Paid");
            var order = metadata.Types.Should().ContainSingle(type => type.Name == "Order").Which;
            order.Kind.Should().Be(TypeMetadataKind.Record);
            var money = metadata.Types.Should().ContainSingle(type => type.Name == "Money").Which;
            money.Kind.Should().Be(TypeMetadataKind.Struct);
            money.Properties.Should().Contain(property => property.Name == "Amount");
            money.NestedStructs.Should().Contain(nested => nested.Name == "Token");
            var controller = metadata.Types.Should().ContainSingle(type => type.Name == "UsersController").Which;
            controller.IsStatic.Should().BeFalse();
            var apiVersion = controller.Constants.Should().ContainSingle(constant => constant.Name == "ApiVersion").Which;
            apiVersion.Accessibility.Should().Be(MetadataAccessibility.Public);
            apiVersion.Value.Should().Be("v1");
            apiVersion.ParentTypeFullName.Should().Be(controller.FullName);
            var getAsync = controller.Methods.Should().ContainSingle(method => method.Name == "GetAsync").Which;
            getAsync.Accessibility.Should().Be(MetadataAccessibility.Public);
            getAsync.ReturnType.Name.Should().Be("Task");
            getAsync.ParentTypeFullName.Should().Be(controller.FullName);
            getAsync.Attributes.Should().Contain(attribute => attribute.Name == "HttpGet");
            getAsync.Attributes
                .Where(
                    predicate: attribute => attribute.Name == "ProducesResponseType"
                                            && attribute.Arguments.FirstOrDefault()?.Value == "typeof(Sample.User)")
                .Should()
                .ContainSingle();
            var id = getAsync.Parameters.Should().ContainSingle(parameter => parameter.Name == "id").Which;
            id.HasDefaultValue.Should().BeFalse();
            id.Attributes.Should().Contain(attribute => attribute.Name == "FromRoute");
            var filter = getAsync.Parameters.Should().ContainSingle(parameter => parameter.Name == "filter").Which;
            filter.Type.IsNullable.Should().BeTrue();
            filter.HasDefaultValue.Should().BeTrue();
            filter.DefaultValue.Should().Be("null");
            filter.ParentMethodFullName.Should().Be(getAsync.FullName);
            var constants = metadata.Types.Should().ContainSingle(type => type.Name == "AppConstants").Which;
            constants.IsStatic.Should().BeTrue();
            constants.Constants.Should().ContainSingle(constant => constant.Name == "Answer").Which.Value.Should().Be("42");
            var status = metadata.Types.Should().ContainSingle(type => type.Name == "OrderStatus").Which;
            status.EnumValues.Should().SatisfyRespectively(
                value =>
                {
                    value.Name.Should().Be("Draft");
                    value.Value.Should().Be(0);
                    value.ParentTypeFullName.Should().Be(status.FullName);
                    value.Attributes.Should().Contain(attribute => attribute.Name == "EnumLabel");
                },
                value =>
                {
                    value.Name.Should().Be("Paid");
                    value.Value.Should().Be(1);
                });
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetMetadataLoadsWebSdkProjectWithRazorSourceGenerators()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var projectPath = Path.Combine(path1: directory, path2: "WebSample.csproj");
            await File.WriteAllTextAsync(
                path: projectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk.Web">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                              <Nullable>enable</Nullable>
                            </PropertyGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: directory, path2: "Model.cs"),
                contents: """
                          namespace WebSample;

                          public sealed class WeatherForecast
                          {
                              public string? Summary { get; init; }
                          }
                          """);
            var provider = new CSharpProjectMetadataProvider();

            var metadata = await provider.GetMetadataAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory),
                cancellationToken: CancellationToken.None);

            metadata.Diagnostics.Should().BeEmpty();
            metadata.Types.Should().ContainSingle(type => type.Name == "WeatherForecast");
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

            metadata.Diagnostics.Should().BeEmpty();
            var enabled = metadata.Types.Should().ContainSingle(type => type.Name == "EnabledRegion").Which;
            enabled.IsNullableAware.Should().BeTrue();
            enabled.Properties.Should().ContainSingle(property => property.Name == "OptionalName").Which.Type.IsNullable.Should().BeTrue();
            var optionalAliases = enabled.Properties.Should().ContainSingle(property => property.Name == "OptionalAliases").Which;
            optionalAliases.Type.ElementType.Should().NotBeNull();
            optionalAliases.Type.ElementType!.IsNullable.Should().BeTrue();
            var relatedItems = enabled.Properties.Should().ContainSingle(property => property.Name == "RelatedItems").Which;
            relatedItems.Type.IsDictionary.Should().BeTrue();
            relatedItems.Type.TypeArguments[index: 1].IsNullable.Should().BeTrue();
            var getAsync = enabled.Methods.Should().ContainSingle(method => method.Name == "GetAsync").Which;
            getAsync.ReturnType.TypeArguments[index: 0].IsNullable.Should().BeTrue();

            var disabled = metadata.Types.Should().ContainSingle(type => type.Name == "DisabledRegion").Which;
            disabled.IsNullableAware.Should().BeFalse();

            var restored = metadata.Types.Should().ContainSingle(type => type.Name == "RestoredRegion").Which;
            restored.IsNullableAware.Should().BeTrue();
            restored.Properties.Should().ContainSingle(property => property.Name == "OptionalName").Which.Type.IsNullable.Should().BeTrue();
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

            metadata.Diagnostics.Should().BeEmpty();
            var test = metadata.Types.Should().ContainSingle(type => type.Name == "Test").Which;
            var pseudoEnum = test.Properties.Should().ContainSingle(property => property.Name == "PseudoEnum").Which;
            var allowedValues = pseudoEnum.Attributes.Should().ContainSingle(attribute => attribute.Name == "AllowedValues").Which;
            var argument = allowedValues.Arguments.Should().ContainSingle().Which;
            argument.Value.Should().Contain("value1");
            argument.Value.Should().Contain("value2");
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

            metadata.Diagnostics.Should().BeEmpty();
            var type = metadata.Types.Should().ContainSingle(type => type.Name == "CombinedQueryModel").Which;
            type.Properties.Should().ContainSingle(property => property.Name == "FirstName").Which.Type.IsNullable.Should().BeFalse();
            type.Properties.Should().ContainSingle(property => property.Name == "MiddleName").Which.Type.IsNullable.Should().BeTrue();
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

            metadata.Diagnostics.Should().BeEmpty();
            var topLevelDelegate = metadata.Delegates.Should().ContainSingle(item => item.Name == "TopLevelDelegate").Which;
            topLevelDelegate.ReturnType.Name.Should().Be("String");
            topLevelDelegate.Parameters.Should().ContainSingle().Which.Name.Should().Be("value");

            var outer = metadata.Types.Should().ContainSingle(type => type.FullName == "Sample.Outer").Which;
            outer.DocComment.Should().NotBeNull();
            outer.DocComment!.Summary.Should().Be("Outer model summary.");
            outer.FileLocations.Should().ContainSingle().Which.Should().Be(Path.GetFullPath(path: sourcePath));
            outer.TypeParameters.Should().ContainSingle().Which.Name.Should().Be("T");
            outer.BaseTypes.Should().Contain(type => type.Name == "BaseModel");
            outer.BaseTypes.Should().Contain(type => type.Name == "IModel");

            var field = outer.Fields.Should().ContainSingle(item => item.Name == "InstanceField").Which;
            field.DocComment.Should().NotBeNull();
            field.DocComment!.Summary.Should().Be("Instance field summary.");
            var staticField = outer.StaticReadOnlyFields.Should().ContainSingle(item => item.Name == "StaticLabel").Which;
            staticField.Value.Should().Be("ready");
            staticField.DocComment.Should().NotBeNull();
            staticField.DocComment!.Summary.Should().Be("Static label summary.");
            var changed = outer.Events.Should().ContainSingle(item => item.Name == "Changed").Which;
            changed.DocComment.Should().NotBeNull();
            changed.DocComment!.Summary.Should().Be("Changed event summary.");

            var mapper = outer.Delegates.Should().ContainSingle(item => item.Name == "Mapper").Which;
            mapper.IsGeneric.Should().BeTrue();
            mapper.TypeParameters.Select(selector: item => item.Name).Should().Equal("TArg", "TResult");
            mapper.Parameters.Should().ContainSingle().Which.Name.Should().Be("value");

            outer.NestedClasses.Should().Contain(type => type.Name == "NestedClass");
            outer.NestedRecords.Should().Contain(type => type.Name == "NestedRecord");
            outer.NestedEnums.Should().Contain(type => type.Name == "NestedEnum");
            outer.NestedInterfaces.Should().Contain(type => type.Name == "INested");

            var pair = outer.Properties.Should().ContainSingle(property => property.Name == "Pair").Which;
            pair.IsVirtual.Should().BeTrue();
            pair.Type.IsValueTuple.Should().BeTrue();
            pair.Type.TupleElements.Select(selector: element => element.Name).Should().Equal("Name", "Count");

            var echo = outer.Methods.Should().ContainSingle(method => method.Name == "Echo").Which;
            echo.IsGeneric.Should().BeTrue();
            echo.TypeParameters.Should().ContainSingle().Which.Name.Should().Be("TMethod");
            echo.DocComment.Should().NotBeNull();
            echo.DocComment!.Summary.Should().Be("Echoes a value.");
            echo.DocComment.Returns.Should().Be("Return description.");
            var valueComment = echo.DocComment.Parameters.Should().ContainSingle().Which;
            valueComment.Name.Should().Be("value");
            valueComment.Description.Should().Be("Value description.");
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

            metadata.Diagnostics.Should().BeEmpty();
            metadata.Types.Should().Contain(type => type.FullName == "App.MyModel");
            metadata.Types.Should().Contain(type => type.FullName == "LibA.HelperA");
            metadata.Types.Should().Contain(type => type.FullName == "LibB.HelperB");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetMetadataCompilesWithTransitivelyReferencedProject()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var leafDirectory = Path.Combine(path1: directory, path2: "Leaf");
            var midDirectory = Path.Combine(path1: directory, path2: "Mid");
            var appDirectory = Path.Combine(path1: directory, path2: "App");
            Directory.CreateDirectory(path: leafDirectory);
            Directory.CreateDirectory(path: midDirectory);
            Directory.CreateDirectory(path: appDirectory);

            await File.WriteAllTextAsync(
                path: Path.Combine(path1: leafDirectory, path2: "Leaf.csproj"),
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                              <ImplicitUsings>enable</ImplicitUsings>
                              <Nullable>enable</Nullable>
                            </PropertyGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: leafDirectory, path2: "Widget.cs"),
                contents: """
                          namespace Leaf;

                          public sealed class Widget
                          {
                              public int Id { get; set; }
                          }
                          """);

            await File.WriteAllTextAsync(
                path: Path.Combine(path1: midDirectory, path2: "Mid.csproj"),
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                              <ImplicitUsings>enable</ImplicitUsings>
                              <Nullable>enable</Nullable>
                            </PropertyGroup>
                            <ItemGroup>
                              <ProjectReference Include="..\Leaf\Leaf.csproj" />
                            </ItemGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: midDirectory, path2: "MidMarker.cs"),
                contents: """
                          namespace Mid;

                          public sealed class MidMarker
                          {
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
                              <ProjectReference Include="..\Mid\Mid.csproj" />
                            </ItemGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: appDirectory, path2: "MyModel.cs"),
                contents: """
                          using Leaf;

                          namespace App;

                          public sealed class MyModel
                          {
                              public Widget Thing { get; set; } = new();
                          }
                          """);
            var provider = new CSharpProjectMetadataProvider();

            var metadata = await provider.GetMetadataAsync(
                project: new ProjectContext(ProjectPath: appProjectPath, WorkspacePath: directory),
                cancellationToken: CancellationToken.None);

            metadata.Diagnostics.Should().BeEmpty();
            metadata.Types.Should().Contain(type => type.FullName == "Leaf.Widget");
            var model = metadata.Types.Should().ContainSingle(type => type.FullName == "App.MyModel").Which;
            var thing = model.Properties.Should().ContainSingle(property => property.Name == "Thing").Which;
            thing.Type.FullName.Should().Be("Leaf.Widget");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetMetadataDoesNotLeakReferencedProjectImplicitGlobalUsings()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var leafDirectory = Path.Combine(path1: directory, path2: "Leaf");
            var coreDirectory = Path.Combine(path1: directory, path2: "Core");
            Directory.CreateDirectory(path: leafDirectory);
            Directory.CreateDirectory(path: Path.Combine(path1: coreDirectory, path2: "Models"));

            await File.WriteAllTextAsync(
                path: Path.Combine(path1: leafDirectory, path2: "Leaf.csproj"),
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                              <ImplicitUsings>enable</ImplicitUsings>
                              <Nullable>enable</Nullable>
                            </PropertyGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: leafDirectory, path2: "LeafMarker.cs"),
                contents: """
                          namespace Leaf;

                          public sealed class LeafMarker
                          {
                          }
                          """);

            var coreProjectPath = Path.Combine(path1: coreDirectory, path2: "Core.csproj");
            await File.WriteAllTextAsync(
                path: coreProjectPath,
                contents: """
                          <Project Sdk="Microsoft.NET.Sdk">
                            <PropertyGroup>
                              <TargetFramework>net10.0</TargetFramework>
                              <ImplicitUsings>disable</ImplicitUsings>
                              <Nullable>enable</Nullable>
                            </PropertyGroup>
                            <ItemGroup>
                              <ProjectReference Include="..\Leaf\Leaf.csproj" />
                            </ItemGroup>
                          </Project>
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: coreDirectory, path2: "Models", path3: "File.cs"),
                contents: """
                          namespace Core.Models;

                          public sealed class File
                          {
                          }
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: coreDirectory, path2: "MyModel.cs"),
                contents: """
                          using Core.Models;

                          namespace Core;

                          public sealed class MyModel
                          {
                              public File Attachment { get; set; } = new();
                          }
                          """);
            var provider = new CSharpProjectMetadataProvider();

            var metadata = await provider.GetMetadataAsync(
                project: new ProjectContext(ProjectPath: coreProjectPath, WorkspacePath: directory),
                cancellationToken: CancellationToken.None);

            metadata.Diagnostics.Should().BeEmpty();
            var model = metadata.Types.Should().ContainSingle(type => type.FullName == "Core.MyModel").Which;
            var attachment = model.Properties.Should().ContainSingle(property => property.Name == "Attachment").Which;
            attachment.Type.FullName.Should().Be("Core.Models.File");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetMetadataRunsSourceGeneratorsBeforeCollectingDiagnostics()
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
                path: Path.Combine(path1: directory, path2: "Validator.cs"),
                contents: """
                          using System.Text.RegularExpressions;

                          namespace Sample;

                          public partial class Validator
                          {
                              [GeneratedRegex(@"\d+")]
                              public static partial Regex Digits();
                          }
                          """);
            await File.WriteAllTextAsync(
                path: Path.Combine(path1: directory, path2: "MyModel.cs"),
                contents: """
                          namespace Sample;

                          public sealed class MyModel
                          {
                              public string Name { get; set; } = string.Empty;
                          }
                          """);
            var provider = new CSharpProjectMetadataProvider();

            var metadata = await provider.GetMetadataAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory),
                cancellationToken: CancellationToken.None);

            metadata.Diagnostics.Should().BeEmpty();
            metadata.Types.Should().Contain(type => type.FullName == "Sample.MyModel");
            metadata.Types.Should().Contain(type => type.FullName == "Sample.Validator");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }
    }

    [Fact]
    public async Task GetMetadataTruncatesCircularEnumerableTypeReferences()
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
                          using System.Collections;
                          using System.Collections.Generic;

                          namespace Sample;

                          public sealed class SelfEnumerable : IEnumerable<SelfEnumerable>
                          {
                              public IEnumerator<SelfEnumerable> GetEnumerator() => throw new System.NotImplementedException();

                              IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                          }

                          public sealed class Holder
                          {
                              public SelfEnumerable Property { get; init; } = new();

                              public SelfEnumerable Field = new();

                              public SelfEnumerable Create() => new();

                              public void Handle(SelfEnumerable parameter)
                              {
                              }
                          }
                          """);
            var provider = new CSharpProjectMetadataProvider();

            var metadata = await provider.GetMetadataAsync(
                project: new ProjectContext(ProjectPath: projectPath, WorkspacePath: directory),
                cancellationToken: CancellationToken.None);

            metadata.Diagnostics.Should().BeEmpty();
            var holder = metadata.Types.Should().ContainSingle(type => type.Name == "Holder").Which;
            var property = holder.Properties.Should().ContainSingle(item => item.Name == "Property").Which;
            VerifyTruncatedSelfEnumerable(type: property.Type);
            var field = holder.Fields.Should().ContainSingle(item => item.Name == "Field").Which;
            VerifyTruncatedSelfEnumerable(type: field.Type);
            var create = holder.Methods.Should().ContainSingle(item => item.Name == "Create").Which;
            VerifyTruncatedSelfEnumerable(type: create.ReturnType);
            var handle = holder.Methods.Should().ContainSingle(item => item.Name == "Handle").Which;
            var parameter = handle.Parameters.Should().ContainSingle(item => item.Name == "parameter").Which;
            VerifyTruncatedSelfEnumerable(type: parameter.Type);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory: directory);
        }

        static void VerifyTruncatedSelfEnumerable(TypeMetadataReference type)
        {
            type.Name.Should().Be("SelfEnumerable");
            type.IsCollection.Should().BeTrue();
            type.ElementType.Should().NotBeNull();
            type.ElementType!.Name.Should().Be("SelfEnumerable");
            type.ElementType.IsCollection.Should().BeFalse();
            type.ElementType.ElementType.Should().BeNull();
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
