namespace Typewriter.Abstractions;

/// <summary>A measurement from one metadata request, never part of cached metadata.</summary>
public readonly record struct MetadataStageTiming(string ProjectPath, string Stage, TimeSpan Elapsed);
