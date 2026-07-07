# Incremental Generation Implementation Progress

## Overview

Implementing performance optimization so that changing a `.cs` file only regenerates affected `.ts` outputs, while `.tst` changes trigger full regeneration. Based on the plan in `docs/perf_improvement.md`.

## Completed Work

### 1. Abstractions Layer (DONE)

Created new types in `src/Typewriter.Abstractions/`:

- **`ChangedInputKind.cs`** — enum (Modified, Added, Deleted, Renamed)
- **`ChangedInput.cs`** — record (FullPath, Kind)
- **`MetadataCacheInvalidation.cs`** — process-wide dirty-path tracking with version-based validation. Hosts with filesystem watchers call `EnableTracking()` + `MarkDirty(path)`. Cache entries validated at a version with no dirty paths since then can be accepted without filesystem access. Uses `System.Threading.Lock`.
- **`MetadataCacheMetrics.cs`** — process-wide counters (CacheHits, CacheMisses, StatValidatedHits, DirtyValidatedHits, SourceOnlyRebuilds, FullLoads, ParsedSourceFiles, ReusedSyntaxTrees, LastInvalidationReason). Lock-based for ParallelChecker compliance.
- **`GenerationConfiguration.cs`** — record with `Incremental` property ("auto"/"off"), `IsIncrementalEnabled` check.

Modified existing types:

- **`GenerationRequest.cs`** — added `IReadOnlyCollection<ChangedInput>? ChangedInputs` property
- **`TypewriterConfiguration.cs`** — added `GenerationConfiguration Generation { get; init; }` property (defaults to `GenerationConfiguration.Default`)

### 2. Roslyn Layer (DONE)

Major rewrite of `src/Typewriter.Roslyn/CSharpProjectMetadataProvider.cs`:

**New caching architecture** (replaced old `ProjectMetadataFingerprint` hash-based approach):

- **`ProjectInputManifest`** — per-file stamp manifest separating source files from shape files:
  - Source files: `.cs` files tracked by (Exists, Length, LastWriteTicks) via `FileStamp`
  - Shape files: `.csproj`, project references, assembly references — changes require full reload
  - Directories: tracked by `LastWriteTimeUtc.Ticks` — new/deleted files change directory stamp
  - `Validate()` — full stat-based validation (fallback when no watcher)
  - `ValidateDirtyPaths(dirtyPaths)` — only stats the changed paths (fast path when watcher active)

- **`LookupCache()`** — replaces `TryGetCachedResult()`. Returns `CacheLookupResult` with three outcomes:
  - `Hit` — cache valid, return cached result
  - `RebuildFromSources` — only source `.cs` files changed, reuse `ProjectLoadResult` (skip MSBuild), re-parse only changed files
  - `Miss` — shape changed or no cache, full MSBuild reload

- **`FileStamp`** — record (Exists, Length, LastWriteTicks) with `Create(path)` factory

- **`ManifestValidation`** — record distinguishing Valid / ShapeChanged / SourcesChanged

- **`ProjectMetadataCacheEntry`** — changed from record to class. Now holds `Result`, `Manifest`, `LoadedProject`, and `LastValidatedVersion` (atomic, for dirty-path tracking).

- **SyntaxTree cache** — `ConcurrentDictionary<string, CachedSyntaxTree>` keyed by normalized path. Each entry stores `ParseOptionsKey`, `FileStamp`, and `SyntaxTree`. On rebuild, only changed source files are re-parsed; unchanged ones reuse cached syntax trees. Bounded at 8192 entries with FIFO eviction.

- **TypeReference cache** — `ConditionalWeakTable<ITypeSymbol, TypeReferenceCacheNode>` caches `CreateTypeReference` results by symbol + `NullableAnnotation` index. Avoids re-traversing the type graph for repeated references.

- **Metrics integration** — `MetadataCacheMetrics.RecordCacheHit/RecordCacheMiss/RecordSourceOnlyRebuild/RecordFullLoad/RecordParsedSourceFile/RecordReusedSyntaxTree` called at each decision point.

- **Test hooks** — `internal static` methods: `GetSourceParseCountForTests(path)`, `GetLastCacheOutcomeForTests(projectPath)`, `ClearCachesForTests()`. Added `InternalsVisibleTo("Typewriter.Roslyn.Tests")` in `Properties/AssemblyInfo.cs`.

- **Removed unused usings** — `System.Security.Cryptography`, `System.Text` (were only used by old SHA256 fingerprint hashing).

- **Builds clean** with 0 warnings, 0 errors.

### 3. Engine Layer (DONE)

- **`ProjectMetadataIndex`** — reverse dependency index maps declared type names to source files and resolves transitive affected source files for incremental renders.
- **`TypewriterGenerator.GenerateProjectAsync`** — scopes per-source-file rendering when `ChangedInputs` contains only changed `.cs` files and incremental generation is enabled. Template, project, config, deleted, and renamed inputs still trigger full rendering.
- **`TypewriterConfigurationLoader`** — reads the `generation.incremental` configuration option and defaults to `"auto"`.
- **Performance trace** — emits stage timings and the `MetadataCacheMetrics.CreateSummary()` counter line behind `TYPEWRITER_TRACE_PERF`.

### 4. CLI Layer (DONE)
- `--changed` is parsed into `CliOptions.ChangedPaths`.
- Watch mode accumulates changed paths, enables `MetadataCacheInvalidation`, drains changed inputs per generation cycle, and passes them to `GenerationRequest`.

### 5. LSP Layer (DONE)
- `WorkspaceGenerationRequest` accepts `ChangedInputs`.
- `WorkspaceGenerationService` maps request changed inputs to engine changed inputs, enables metadata dirty-path tracking for the persistent service lifetime, marks explicit dirty paths, and falls back to full manifest validation when provenance is unknown.

### 6. VS Extension (DONE)
- Save handling collects changed paths.
- Persistent language-server requests include `changedInputs`.
- CLI fallback passes `--changed` arguments.

### 7. VS Code Extension (DONE)
- Save requests merge changed paths and send them through the language-server generation request.
- CLI fallback builds matching `--changed` arguments.

### 8. Schema (DONE)
- `typewriter.schema.json` includes the `generation.incremental` enum with `"auto"` and `"off"`.

### 9. Tests — DONE
- **Roslyn incremental metadata test** — `GetMetadataReusesCacheAndRebuildsFromSourcesOnDirtyPath` in `CSharpProjectMetadataProviderTests.cs`: creates temp project with 2 source files, verifies cache miss on first load, cache hit (dirty-validated) on second, source-only rebuild on third (only changed file re-parsed, unchanged file parse count stays same).
- **Engine scoped rendering tests** — 5 tests in `TypewriterGeneratorWorkspaceTests.cs`: (1) only affected source files rendered when ChangedInputs provided (UserDto changed → UserDto.ts + OrderDto.ts via transitive closure, ProductDto.ts excluded); (2) nothing rendered when changed file is not part of the project; (3) full render when ChangedInputs include non-.cs file (template); (4) full render when ChangedInput kind is Deleted; (5) full render when incremental is disabled via config.
- **CLI parsing tests** — 2 tests in `CliCommandLineTests.cs`: (1) `--changed` repeated option captures all paths in order; (2) ChangedPaths defaults to empty when option omitted.
- **Language-server dirty-path test** — `GenerateAsyncRefreshesOutputAfterSourceFileChanges` verifies the persistent workspace generation path marks the changed source path and refreshes output.

### 10. Verification
- Targeted suites on 2026-07-07: LanguageServer 37 pass, Roslyn 15 pass, Engine 174 pass.
- Serialized full solution suite on 2026-07-07: 292 pass, 0 failed.

### 11. Documentation
- Update `docs/perf_improvement.md` with implementation notes and deviations from original plan

## Key Design Decisions

1. **Stamp manifest vs SHA256 hash** — replaced the old fingerprint that hashed all file sizes+timestamps into a single hash with a per-file stamp manifest. This allows distinguishing "source file changed" (can skip MSBuild) from "shape file changed" (must reload project).

2. **Dirty-path tracking** — `MetadataCacheInvalidation` provides O(1) cache validation when a filesystem watcher is active. Without a watcher, falls back to stat-based manifest validation (still faster than old approach because it can identify source-only changes).

3. **Source-only rebuild** — when only `.cs` files change, the `ProjectLoadResult` (MSBuild evaluation) is reused. Only changed source files are re-parsed; unchanged ones use the SyntaxTree cache. The compilation is rebuilt from the new+cached syntax trees.

4. **TypeReference cache** — keyed by `ITypeSymbol` + `NullableAnnotation` via `ConditionalWeakTable`. Prevents re-traversing the Roslyn type graph for repeated type references within a single compilation.

5. **Lock-based metrics** — `MetadataCacheMetrics` uses a simple `Lock` instead of `Interlocked` to satisfy ParallelChecker analyzer. Performance impact is negligible since these are debug/trace counters.

## Files Changed Summary

### Created
- `docs/progress.md` (this file)
- `src/Typewriter.Abstractions/ChangedInput.cs`
- `src/Typewriter.Abstractions/ChangedInputKind.cs`
- `src/Typewriter.Abstractions/MetadataCacheInvalidation.cs`
- `src/Typewriter.Abstractions/MetadataCacheMetrics.cs`
- `src/Typewriter.Abstractions/GenerationConfiguration.cs`
- `src/Typewriter.Roslyn/Properties/AssemblyInfo.cs`

### Modified
- `src/Typewriter.Abstractions/GenerationRequest.cs` — added `ChangedInputs` property
- `src/Typewriter.Abstractions/TypewriterConfiguration.cs` — added `Generation` property
- `src/Typewriter.Roslyn/CSharpProjectMetadataProvider.cs` — stamp manifest, dirty-path validation, SyntaxTree cache, type-reference cache, metrics, test hooks (+450/-144 lines)
