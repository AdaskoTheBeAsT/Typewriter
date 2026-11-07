using AwesomeAssertions;
using Typewriter.Abstractions;
using Typewriter.Configuration;
using Xunit;
using CodeFile = Typewriter.CodeModel.File;

namespace Typewriter.Engine.Tests;

public sealed class CompiledTemplateBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HelpersWithoutFileConstructorsDoNotCreateFileModels(bool settingsConstructor)
    {
        var declaration = AdapterTestData.CreateType();
        var types = new AdapterCountingList<TypeMetadata>(items: [declaration]);
        var source = new SourceFileMetadata(Path: "Models.cs", Types: [declaration]);
        var metadata = AdapterTestData.CreateProject(types: types, sources: [source]);
        var index = ProjectMetadataIndex.Create(metadata: metadata);
        var context = new TemplateAssemblyLoadContext(name: "ConstructorTests", referencePaths: []);
        types.Enumerations = 0;

        using var helper = TemplateRuntimeCompiler.CreateHelper(
            templatePath: "models.tst",
            metadata: metadata,
            diagnostics: [],
            defaults: TemplateRenderDefaults.Default,
            hostType: settingsConstructor ? typeof(SettingsHost) : typeof(ParameterlessHost),
            loadContext: context,
            unloadLoadContextOnDispose: true,
            metadataIndex: index);

        types.Enumerations.Should().Be(0);
        helper.Settings.TemplatePath.Should().Be("models.tst");
    }

    [Fact]
    public void FileConstructorHasPriorityAndEvaluatesItsFactoryOnce()
    {
        var calls = 0;
        var file = new CodeFile { Name = "Models" };
        var settings = new Settings();
        var bindings = CompiledTemplateBindings.For(hostType: typeof(FileHost));

        var host = bindings.CreateHost(
            settings: settings,
            fileFactory: () =>
            {
                calls++;
                return file;
            }).Should().BeOfType<FileHost>().Which;

        host.File.Should().BeSameAs(file);
        host.Settings.Should().BeSameAs(settings);
        calls.Should().Be(1);
        CompiledTemplateBindings.For(hostType: typeof(FileHost)).Should().BeSameAs(bindings);
    }

    [Fact]
    public void SharedBindingsDoNotShareHostState()
    {
        var bindings = CompiledTemplateBindings.For(hostType: typeof(StatefulHost));
        var first = bindings.CreateHost(settings: new Settings(), fileFactory: ThrowIfFileRequested)
            .Should().BeOfType<StatefulHost>().Which;
        var second = bindings.CreateHost(settings: new Settings(), fileFactory: ThrowIfFileRequested)
            .Should().BeOfType<StatefulHost>().Which;

        first.Next().Should().Be(1);
        first.Next().Should().Be(2);
        second.Next().Should().Be(1);
    }

    private static CodeFile ThrowIfFileRequested() => throw new InvalidOperationException(message: "A file should not be requested.");

#pragma warning disable S2094 // An empty host exercises activation without a user-defined template constructor.
    private sealed class ParameterlessHost
    {
    }
#pragma warning restore S2094

#pragma warning disable S1144 // Template host constructors are selected and invoked through reflection.
    private sealed class SettingsHost
    {
        public SettingsHost(Settings settings)
        {
            settings.UseDateType(dateType: "ConfiguredDate");
        }
    }

    private sealed class FileHost
    {
        public FileHost(Settings settings, CodeFile file)
        {
            Settings = settings;
            File = file;
        }

        public FileHost(Settings settings) => throw new InvalidOperationException(message: settings.TemplatePath);

        public Settings Settings { get; }

        public CodeFile File { get; }
    }
#pragma warning restore S1144

    private sealed class StatefulHost
    {
        private int _count;

        public int Next() => ++_count;
    }
}
