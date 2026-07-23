using Typewriter.Abstractions;

namespace Typewriter.Engine;

internal sealed record TypeMappingContext(
    TypeMetadataReference Reference,
    FrontendRuntimeTypeKind RuntimeType);
