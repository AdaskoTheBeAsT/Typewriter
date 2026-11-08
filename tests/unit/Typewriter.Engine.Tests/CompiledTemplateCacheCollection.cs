using Xunit;

namespace Typewriter.Engine.Tests;

// Cache-count assertions and cache clearing cannot overlap other template compilations.
[CollectionDefinition("Compiled template cache", DisableParallelization = true)]
public sealed class CompiledTemplateCacheCollection;
