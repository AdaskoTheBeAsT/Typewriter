using Typewriter.Abstractions;
using Xunit;

namespace Typewriter.Engine.Tests;

public sealed class PublicApiCompatibilityTests
{
    [Fact]
    public void GeneratedFileRetainsPreviousConstructorSignature()
    {
        var constructor = typeof(GeneratedFile).GetConstructor(
            types: [typeof(string), typeof(string), typeof(bool), typeof(bool?)]);

        constructor.Should().NotBeNull();
    }

    [Fact]
    public void GenerationRequestRetainsPreviousConstructorSignature()
    {
        var constructor = typeof(GenerationRequest).GetConstructor(
            types:
            [
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(GenerationMode),
                typeof(TypewriterConfiguration),
                typeof(bool),
                typeof(string),
            ]);

        constructor.Should().NotBeNull();
    }
}
