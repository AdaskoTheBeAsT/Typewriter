# Generation Performance Optimization Plan

## Goal

Improve Typewriter generation throughput, especially repeated watch and IDE generation runs, without changing generated output or public behavior.

## Measurement First

Before implementation, add lightweight timing around these pipeline stages:

1. Workspace and project resolution
2. Template discovery and parsing
3. MSBuild project loading
4. Roslyn parse, compile, generator execution, and metadata extraction
5. Template runtime compilation and helper invocation
6. Rendering
7. Planning, formatting, and writing outputs

Use the timings to validate each optimization independently and avoid optimizing unmeasured cold paths.

## Current Baseline

The following optimizations are already present:

- Performance stage diagnostics behind `TYPEWRITER_TRACE_PERF`.
- Project metadata and compilation caching with bounded entries.
- Linear rendered-whitespace normalization.
- Per-source mode inspection without a discarded full-project render.
- Existing generated-file content reuse between planning and writing.
- Precompiled template glob matchers and pruning of common ignored directories.
- Parse/declaration diagnostics during normal generation and full diagnostics during validation.
- A combined type/delegate syntax-tree walk that avoids method and accessor bodies.
- Pooled type-reference recursion guards.
- A shared `ProjectMetadataIndex` reused across templates and source-file render contexts.
- Compiled-helper method and parameter metadata indexing.
- Parsed documentation comment caching.
- TypeScript type mapping memoization.
- Default-setting fast paths in output formatting.
- Local `#load` caching and a shared remote-load `HttpClient`.
- Watcher extension checks before path-segment inspection.
- Bounded parsed-template caching keyed by template file stamp with exact source verification.
- CLI watch reuse of generator, metadata, renderer, discovery, and writer services across cycles.
- Persistent IDE generation through the language-server `typewriter/generate` request, with CLI fallback for unavailable or older servers.

## Remaining Original Opportunities

| Priority | Area | Current behavior | Recommended change | Impact | Risk |
| --- | --- | --- | --- | --- | --- |
| P0 | `CSharpProjectMetadataProvider.GetMetadataAsync` | A metadata cache exists, but validating a cache hit recomputes a broad file and directory fingerprint. Analyzer, generator, imported build, and additional-file inputs are not represented as a precise manifest. | Replace broad directory validation with an explicit project-input manifest and watcher-driven dirty-path invalidation. | Very high | High |
| P1 | `TemplateDocument.Parse` | Scans template content several times for code blocks, output directives, directive stripping, and compatibility block stripping. | Parse in a single pass that gathers directives, code blocks, cleaned content, and line mapping. | Medium | Medium |
| P1 | `TemplateRuntimeCompiler.CompileFactory` | Reuses compiled bytes but loads them into a fresh `AssemblyLoadContext` per template/project. | Cache loaded host type plus load context while safe, or pool factories by compiled cache key and release when cache entry is evicted. | Medium | Medium |
| P2 | `TemplateRenderer.MatchesRecipeTypePredicate` | Repeatedly scans the same helper body with `Contains` and literal extraction for every type. | Precompute a recipe predicate descriptor once per compatibility method. | Low-medium | Low |

## Additional Opportunities Found

| Priority | Area | Current behavior | Recommended change | Impact | Risk |
| --- | --- | --- | --- | --- | --- |
| P0 | `FileSystemTemplateDiscovery.FindTemplatesAsync` | Common ignored directories are pruned and glob matchers are compiled, but every remaining file is still enumerated and configured excludes are applied after traversal. | Derive traversal roots and filename filters from simple include patterns. Prune configured directory excludes before descending. | High in large repos | Low-medium |
| P1 | `CSharpProjectMetadataProvider.TryCreateType` | Repeatedly enumerates `symbol.GetMembers()` for properties, methods, constants, fields, static readonly fields, events, and delegates. Recreates nested metadata once per nested kind. | Materialize members and nested type members once per symbol, partition by kind, then build nested metadata once and group by `TypeMetadataKind`. | Medium-high | Medium |
| P1 | `TemplateRenderer.RenderCore`, `AppendCollection`, and block readers | Reparses template text and extracts block substrings while rendering every collection item. Nested collection rendering repeats the same parsing work many times. | Parse `TemplateDocument` into a reusable render plan or AST containing text, identifier, block, filter, separator, and conditional nodes. Render nodes directly. | High for large templates | Medium-high |
| P1 | `CompiledTemplateHelper` | Method names and parameters are indexed, but every invocation still allocates context/argument arrays and uses `MethodInfo.Invoke`. | Index by name, arity, and compatible parameter shape. Cache delegates for common one- and two-argument helper signatures. | Medium | Medium |
| P1 | `TemplateCodeModelAdapterFactory` | `CreateFile` is cached, but repeated type/member adaptation and parent lookup still recreate legacy `CodeModel` graphs. | Cache adapted objects per metadata node and settings, and lazily create child collections. | High for helper-heavy templates | Medium |
| P1 | `CSharpProjectMetadataProvider.GetDocComment` | Parsed comments are cached, but documentation is still requested eagerly for every supported symbol. | Make documentation extraction feature-driven or lazy when templates do not reference documentation. | Medium-high | Medium |
| P2 | `OutputContentFormatter.Format` | Default settings have a fast path, but non-default formatting still uses several full-string passes and split/join allocations. | Combine newline normalization, indentation, trailing-whitespace trimming, and final-newline handling in one writer pass. | Medium for large outputs | Low-medium |
| P2 | `WorkspaceProjectResolver.ResolveProjectPaths` | Common directories are pruned and explicit solution paths are parsed, but directory workspaces still recursively scan projects and results are not cached. | Prefer a single top-level solution when available and cache project discovery by workspace directory stamp or watcher invalidation. | Medium | Low |
| P2 | `MsBuildProjectLoader.LoadCore` | Creates a new Buildalyzer `AnalyzerManager` and evaluates MSBuild metadata per load. This cost repeats across watch runs. | Reuse manager and project load results through the same metadata cache. Invalidate on project, props, targets, lock file, or environment changes. | High in watch mode | Medium-high |

## Further Opportunities

| Priority | Area | Current behavior | Recommended change | Impact | Risk | Status |
| --- | --- | --- | --- | --- | --- | --- |
| P0 | IDE generation process lifetime | VS Code reuses its active language-server connection. Visual Studio prefers its editor LSP connection and lazily starts a package-scoped server when no `.tst` editor has activated LSP. Rider owns a project-scoped language-server process. All adapters fall back to the CLI when persistent generation is unavailable. | Keep the `typewriter/generate` request compatible and route future IDE generation entry points through it. | Very high for IDE saves | High | Implemented |
| P0 | CLI watch session state | Watch mode now reuses generator services, but each event still performs full configuration, project discovery, metadata validation, template discovery, and generation work. | Introduce a generation session that receives changed paths and invalidates only affected configuration, projects, templates, and outputs. | High | Medium | Partial |
| P0 | Metadata cache validation | Cache hits synchronously stat many files and directories and hash the result. Large repositories can spend substantial time proving that nothing changed. | Feed changed paths from watchers into the cache and validate only affected projects. Use precise fallback stamps for non-watch invocations. | High in repeated runs | High | Pending |
| P1 | Source text and syntax-tree reuse | A metadata cache miss reads and parses every source file serially, even when only one file changed. | Cache `SourceText` and `SyntaxTree` by path, parse options, length, and timestamp. Rebuild the compilation from reused trees and changed trees. | High | High | Pending |
| P1 | Declaration discovery | Symbol discovery previously traversed all syntax descendants, including method and accessor bodies. | Restrict traversal to namespace and type declaration containers. | Medium | Low | Implemented |
| P1 | Type-reference graph creation | The same Roslyn symbols are converted into equivalent `TypeMetadataReference` graphs from properties, parameters, methods, attributes, and base types. | Add a per-compilation cache keyed by symbol identity and nullable annotation. Ensure recursive/in-progress entries cannot create cycles. | Medium-high | Medium | Pending |
| P1 | Feature-driven metadata extraction | Metadata construction eagerly builds methods, fields, events, attributes, documentation, initializers, nested types, and legacy compatibility data regardless of template usage. | Analyze parsed templates for required metadata features and build only those categories. Treat arbitrary compiled helper code as requiring the full model. | High for simple templates | High | Pending |
| P1 | Project metadata index reuse | Full-project indexes were rebuilt for template inspection and each template render; source-scoped indexes were rebuilt across templates. | Build one project index and reusable source render contexts per generation project. | Medium | Low | Implemented |
| P1 | Parsed template reuse | Template parsing repeated on every generation even when the template was unchanged. | Cache parsed documents and parse diagnostics by full path, length, and modification timestamp, verify exact source content on hits, and use bounded eviction. | Medium | Low | Implemented |

## Recommended Implementation Phases

### Phase 0: Baseline and Guardrails

1. Extend timing diagnostics with cache hit/miss and invalidation-reason counters.
2. Add representative performance fixtures:
   - cold generation
   - repeated generation with no changes
   - watch-trigger generation after one source change
   - large output rendering
3. Capture generated-output snapshots before optimizations.

### Phase 1: Quick, Low-risk Wins

1. Precompute recipe predicate metadata for compatibility methods.
2. Finish the single-pass `OutputContentFormatter`.
3. Derive direct traversal roots and filename filters from simple template patterns.
4. Partition Roslyn members once per type.

### Phase 2: Discovery and Rendering Throughput

1. Derive direct traversal roots and filename filters from simple include patterns.
2. Prune configured directory excludes before traversal.
3. Cache project discovery results and avoid broad recursive scans when a solution is available.
4. Replace repeated template text parsing during rendering with a parsed render plan.

### Phase 3: Roslyn Metadata Pipeline

1. Replace broad metadata fingerprinting with precise invalidation.
2. Cache source text and syntax trees by file stamp and parse options.
3. Partition `GetMembers()` and nested type members once per symbol.
4. Cache type-reference graphs per compilation.
5. Make XML documentation extraction feature-driven.
6. Add feature-driven metadata construction for non-helper templates.

### Phase 4: Compiled Template and Legacy CodeModel Runtime

1. Cache `CompiledTemplateHelper` delegates by name, arity, and parameter shape.
2. Cache adapted legacy `CodeModel` objects per render state.
3. Reuse loaded compiled template assemblies where safe.

### Phase 5: Persistent IDE and Watch Sessions

1. Introduce a formal generation session with changed-path invalidation for CLI watch.
2. Define a generation request endpoint in the language server or a dedicated daemon. Implemented as `typewriter/generate`.
3. Route IDE save generation through the persistent process. Implemented for Visual Studio, VS Code, and Rider with CLI fallback.
4. Feed watcher dirty paths into project, template, and discovery cache invalidation.

Measured on `samples/SimpleApi` with the Release pipeline:

- First language-server generation: 2,313 ms total, including 2,293 ms metadata load.
- Second generation in the same server process: 7 ms total, including 6.7 ms metadata validation.

## Validation Strategy

For every phase:

1. Run unit tests and snapshot tests.
2. Compare generated outputs before and after.
3. Record timing before and after on the same fixtures.
4. Keep changes behind internal abstractions where behavior risk is medium or high.
5. Prefer one optimization per pull request unless changes are tightly coupled.
