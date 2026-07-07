# Performance Improvement Plan: Incremental Generation

Target: GitHub issue [#99](https://github.com/AdaskoTheBeAsT/Typewriter/issues/99)
"Visual Studio Extension - Generate on save performance issues" and plan2026-07-02.md
Point 3 (metadata cache validation and syntax-tree reuse).

Date: 2026-07-06. All statements verified against code on branch
`bugfix/98-include-project` (master-based).

---

## 1. Problem Statement

With ~900 generated classes, saving a single `.cs` file in Visual Studio makes the
IDE unresponsive, because generate-on-save triggers a full generation of every
template against every source file instead of only the output affected by the
changed file.

Expected incremental behavior:

| Change | Expected regeneration scope |
| --- | --- |
| `.tst` template saved | All outputs of that template (template change invalidates every output it produces) |
| One `.cs` saved | Only the `.ts` output(s) produced from that source file, plus outputs of files that reference its types |
| `.csproj` / props / targets / lock / config saved | Full regeneration (project shape changed) |
| `.cs` deleted or renamed | Orphaned output removed; new output created for the renamed file |

---

## 2. Current Behavior (verified in code)

### 2.1 The `.tst` path is already scoped correctly

`src/Typewriter.VisualStudio/TypewriterSaveListener.cs` classifies a saved `.tst`
as `SaveGenerationScope.CurrentTemplate` and calls
`TypewriterCommandService.GenerateSavedTemplateAsync`, which generates only that
template. This matches the expected behavior and needs no change.

### 2.2 The `.cs` path regenerates everything

- `TypewriterSaveListener` classifies any non-template input extension as
  `SaveGenerationScope.AllTemplates` and calls
  `TypewriterCommandService.GenerateSavedInputAsync(inputPath)`.
- `GenerateSavedInputAsync` forwards to `GenerateAllTemplatesAsync(inputPathOverride)`
  where `inputPathOverride` is only used by `CreateGenerationContextAsync` to
  resolve the project context. The saved path is then discarded; the request that
  reaches the engine carries no information about what changed.
- In `src/Typewriter.Engine/TypewriterGenerator.cs`, `GenerateProjectAsync`
  builds `CreateSourceFileRenderContexts` for every source file with types and
  renders each context per template. With 900 classes and 3 templates that is
  2,700 renders per save.

### 2.3 No changed-path information exists anywhere in the pipeline

- `src/Typewriter.Abstractions/GenerationRequest.cs` has no dirty-paths field.
- `src/Typewriter.Cli/FileSystemGenerationWatcher.cs` only signals "something
  changed" through a bare `TaskCompletionSource`; the paths from
  `FileSystemEventArgs` are checked against watched extensions and then discarded.
- `src/Typewriter.LanguageServer/WorkspaceGenerationRequest.cs`
  (`typewriter/generate`) has `Command`, `WorkspacePath`, `ProjectPath`,
  `TemplatePath`, `Framework`, `AllProjects`, `TemplateSearchPath` and nothing else.

### 2.4 Metadata cache validation is a stat storm

`src/Typewriter.Roslyn/CSharpProjectMetadataProvider.cs`:

- `MetadataCache` (`ConcurrentDictionary<ProjectMetadataCacheKey, ProjectMetadataCacheEntry>`,
  line ~22) stores results, but every cache hit calls
  `ProjectMetadataFingerprint.IsCurrent()` (line ~1845), which recomputes a
  SHA256 hash over all tracked file paths plus a full recursive directory
  enumeration (`EnumerateDirectoriesForFingerprint`). Large repositories pay a
  large stat/hash cost just to prove nothing changed.
- On any miss, all sources are re-read and re-parsed serially via
  `CSharpSyntaxTree.ParseText` in a loop (line ~188-226). There is no
  `SourceText` or `SyntaxTree` reuse.
- Measured on `samples/SimpleApi` (see perf-opt.md): first generation 2,313 ms
  total, of which 2,293 ms is metadata load.

### 2.5 What already works and must be preserved

- `FileSystemGeneratedFileWriter` skips disk writes when content is unchanged
  (`WriteOnlyWhenChanged`), so disk churn is already fine; the remaining cost is
  CPU spent rendering.
- Bounded parsed-template cache in `TypewriterGenerator` keyed by
  `(path, length, lastWriteTicks)` with exact source verification.
- Persistent IDE generation via the language-server `typewriter/generate`
  request with CLI fallback (perf-opt.md Phase 5, implemented).
- `ProjectMetadataIndex` shared across templates and source render contexts.
- `TrimMetadataCache` bounds (32 entries) and `TrimTemplateDocumentCache`
  bounds (256 entries).

### 2.6 Gap between plan2026-07-02.md Point 3 and issue #99

Point 3 fixes the metadata cost (2.1 above becomes milliseconds) and re-parse
cost, but its acceptance criteria stop at "one-file change re-parses exactly one
file". Rendering is still full-project. Closing issue #99 requires a fourth
work stream: changed-path scoped rendering (section 3.4 below).

---

## 3. Design

Four work streams, in dependency order.

### 3.1 Changed-path plumbing (prerequisite for everything)

New contract:

```csharp
// Typewriter.Abstractions/GenerationRequest.cs
public sealed record GenerationRequest(
    string? WorkspacePath,
    string? ProjectPath,
    string? TemplatePath,
    GenerationMode Mode,
    TypewriterConfiguration Configuration,
    bool AllProjects = false,
    string? TemplateSearchPath = null)
{
    public bool IncludeDiff { get; init; }

    // null = unknown provenance, full generation (today's behavior).
    // non-null = only these inputs changed since the last successful run.
    public IReadOnlyCollection<ChangedInput>? ChangedInputs { get; init; }
}

public sealed record ChangedInput(string FullPath, ChangedInputKind Kind);

public enum ChangedInputKind
{
    Modified,
    Added,
    Deleted,
    Renamed,
}
```

Producers:

1. **CLI watch** (`src/Typewriter.Cli/FileSystemGenerationWatcher.cs`):
   replace the bare signal with an accumulating
   `ConcurrentDictionary<string, ChangedInputKind>` populated from
   `OnFileChanged` / `OnFileRenamed` (rename contributes a `Deleted` for
   `OldFullPath` and an `Added` for `FullPath`). `TypewriterCli.RunWatchedGenerationAsync`
   drains the set after the quiet period and passes it into the request.
   On `OnWatcherError` (buffer overflow), clear the set and pass `null`
   (full run) because events were lost.
2. **Visual Studio** (`TypewriterSaveListener` / `TypewriterCommandService`):
   the listener already knows the saved path. Extend `SaveGenerationRequest`
   to carry a path set instead of a single path; `Merge` unions paths instead
   of collapsing to a scope. `GenerateSavedInputAsync` passes the set through
   the persistent client and the CLI fallback (`--changed <path>` repeated
   argument on the `generate` command).
3. **Language server** (`WorkspaceGenerationRequest` in
   `src/Typewriter.LanguageServer`): add
   `IReadOnlyList<ChangedInputPayload>? ChangedInputs` (path + kind) to the
   `typewriter/generate` payload. Missing field = null = full generation, so
   older clients remain compatible.
4. **Rider plugin**: same payload addition; out of scope for the first PR but
   the protocol field is host-neutral from day one.

Safety valve: any consumer that cannot map a changed path to a known input
category treats the run as full generation. Incremental is an optimization
layered on a correct full path, never a replacement.

### 3.2 Metadata: precise stamps instead of a broad fingerprint (plan Point 3, step 1)

Replace `ProjectMetadataFingerprint` (paths + directories + single SHA256) with
a manifest:

```csharp
private sealed record FileStamp(long Length, long LastWriteTicks);

private sealed record ProjectInputManifest(
    IReadOnlyDictionary<string, FileStamp> Files,      // sources, csproj, props/targets, assets/lock, additional inputs
    IReadOnlyDictionary<string, DirectoryStamp> Dirs); // only where globs require existence/child-set checks
```

Validation rules:

- Compare stamps per file, short-circuit on the first difference.
- Record the invalidation reason (which file, which category) for the
  `TYPEWRITER_TRACE_PERF` counters requested by perf-opt.md Phase 0.
- Directory stamps only track the child-name set for directories where source
  globs can pick up added files; no recursive enumeration of the whole tree.

New counters behind `TYPEWRITER_TRACE_PERF`: cache hits, misses,
stat-validated hits, dirty-path-validated hits, invalidation reasons.

### 3.3 Metadata: watcher-driven dirty paths and syntax-tree reuse (plan Point 3, steps 2-3)

1. `IMetadataCacheInvalidation` in `Typewriter.Abstractions` (or
   `Typewriter.Roslyn`): `void MarkDirty(string fullPath)`. CLI watch and the
   language-server `WorkspaceGenerationService` feed changed paths in. A cache
   entry with zero dirty paths since its last validation is accepted with no
   filesystem access at all; entries with dirty paths validate only those paths
   against the manifest. One-shot CLI invocations fall back to the full
   manifest stamp check (still cheap: one stat per file, no hashing, no
   directory recursion).
2. Bounded `SyntaxTree` cache keyed by
   `(fullPath, parseOptionsChecksum, length, lastWriteTicks)`.
   On metadata rebuild: reuse trees for unchanged files, parse only changed
   files (in parallel via `Parallel.ForEachAsync`, preserving the current
   deterministic ordering of `syntaxTrees` by source path).
3. When a cached `CSharpCompilation` exists for the project, update it with
   `compilation.ReplaceSyntaxTree(oldTree, newTree)` per changed file (and
   `AddSyntaxTrees` / `RemoveSyntaxTrees` for added/deleted files) instead of
   `CSharpCompilation.Create` from scratch.
4. Re-extract `SourceFileMetadata` only for changed files; reuse the metadata
   objects of untouched files. Caveat: type metadata is symbol-derived, and a
   symbol graph can span files. Reused `SourceFileMetadata` is safe because it
   is a detached snapshot (records, no live Roslyn symbols); the cross-file
   correctness question is handled at render scoping time (3.4), not here.
5. Type-reference graph cache (perf-opt P1): per-compilation
   `Dictionary<(ITypeSymbol, NullableAnnotation), TypeMetadataReference>` with
   an in-progress sentinel for cycles; the pooled
   `TypeReferenceVisitedSymbolSets` guard stays as backstop.

### 3.4 Scoped rendering in `TypewriterGenerator` (new, closes issue #99)

#### 3.4.1 Scope decision

In `GenerateProjectAsync`, before the per-template loop, classify
`request.ChangedInputs`:

| Condition | Decision |
| --- | --- |
| `ChangedInputs == null` | Full generation (today's behavior) |
| Any changed path is `.tst`, `.csproj`, `.sln`/`.slnx`, props/targets, config json | Full generation |
| Any `Added`/`Deleted`/`Renamed` source file | Scoped generation + orphan handling (3.4.3) |
| Only `Modified` `.cs` files that belong to the project's source set | Scoped generation |
| Changed path not attributable to any known input | Full generation (safety valve) |

Per template, inside scoped generation:

| Template shape | Behavior |
| --- | --- |
| Per-source-file mode (`ShouldRenderPerSourceFile == true`) | Render only contexts in the affected-file closure (3.4.2) |
| Fixed `OutputPath` or `SingleFileMode` | Always render (single render over the project; writer skips unchanged output) |
| Uses `IncludeProject` | Always render fully; changed paths in included projects also invalidate via `MarkDirty` |

#### 3.4.2 Cross-file dependency closure (correctness)

"One `.cs` -> one `.ts`" alone is an approximation: the output for file A can
change when file B changes. Concrete cases: import lines computed from
referenced types, base classes, property/parameter/return types,
`ProducesResponseType` attribute types. Renaming a class in B must regenerate
A's imports.

Chosen approach: **reverse dependency index**, built during metadata
extraction where the full `TypeMetadataReference` graph is already walked, so
the marginal cost is near zero.

- Forward edges: for each source file F, the set of source files that define
  types referenced by F's types (base types, interfaces, property types,
  method parameter/return types, attribute argument types, type arguments,
  nested references transitively through the reference graph as already
  materialized).
- Store the **reverse** index (`file -> files that reference its types`) in
  `ProjectMetadataIndex`.
- Affected set for a change = changed files + reverse closure (transitive,
  because A -> B -> C renames propagate through re-exported types). In
  practice the closure is a handful of files, not 900.
- The index is rebuilt only for changed files during incremental metadata
  update; reverse edges from unchanged files into a changed file are looked up
  from the persisted index.

Fallback mode (config switch, also the interim behavior until the index PR
lands): render the changed files immediately, then schedule a debounced
full-project sweep in the background. The writer skips unchanged content so
the sweep is write-cheap, but it still costs full render CPU; this is a
stopgap, not the end state.

#### 3.4.3 Deletes and renames

- `Deleted` source file: locate its previously planned output via
  `GeneratedFilePlanner` conventions (same naming logic as
  `ApplyLegacySourceOutputPath` + `FileNameConventionFormatter`) and delete the
  output file if it exists and carries the Typewriter generated-file header
  (`GeneratedFileHeader`), never otherwise. Also render the reverse closure of
  the deleted file (imports pointing at it become compile errors in consuming
  outputs; surfacing them fast matters).
- `Renamed` = `Deleted` old + `Added` new.
- `Added`: render the new file's context; the reverse closure of an added file
  is empty by definition.

#### 3.4.4 Configuration

`TypewriterConfiguration`: `generation.incremental: "auto" | "off"`
(default `auto`). `off` forces full generation for troubleshooting and as an
escape hatch during rollout. Scope decisions are logged under
`TYPEWRITER_TRACE_PERF` ("scoped: 3 of 912 contexts, closure [+2]", "full:
changed path X not attributable").

### 3.5 Secondary win: compiled factory reuse (perf-opt P1, optional)

`CreateCompiledTemplateFactory` currently loads compiled bytes into a fresh
`AssemblyLoadContext` per template per run and disposes it at the end of
`GenerateProjectAsync`. For per-save generation through the persistent language
server this is a fixed per-save cost. Pool factories by compiled-cache key and
release on cache eviction. Independent of the streams above; schedule last.

---

## 4. PR Breakdown

| # | Content | Depends on | Size |
| --- | --- | --- | --- |
| PR1 | Stamp manifest replaces fingerprint hash; invalidation-reason counters behind `TYPEWRITER_TRACE_PERF`; perf fixtures (cold, no-change repeat, one-change) | - | M |
| PR2 | `ChangedInputs` on `GenerationRequest`; watcher path accumulation in CLI watch; `--changed` CLI argument; `typewriter/generate` payload field; VS save listener passes paths; `IMetadataCacheInvalidation` + `MarkDirty` feed | PR1 | M |
| PR3 | `SyntaxTree` cache, `ReplaceSyntaxTree` incremental compilation update, per-file metadata re-extraction, parallel parse of changed files; type-reference graph cache | PR1, PR2 | L |
| PR4 | Scoped rendering: scope decision table, per-source context filtering, delete/rename orphan handling, `generation.incremental` setting, debounced full-sweep fallback | PR2 | L |
| PR5 | Reverse dependency index in `ProjectMetadataIndex`; closure-based affected set replaces fallback sweep as default | PR3, PR4 | M |
| PR6 (optional) | Compiled template factory pooling across runs | - | S |

Each PR: full `dotnet build` / `dotnet test` (currently 265 tests), snapshot
byte-parity, `npm --prefix vscode run lint && bundle` where the extension is
touched, Rider `verifyPlugin` where touched (per implemented.md).

---

## 5. Acceptance Criteria

1. Repeated watch/LSP generation with no changes performs zero source
   re-parsing, zero renders, and no full-tree stat storm (PR1-3).
2. A one-file `.cs` change re-parses exactly one file (regression test with an
   injectable parse hook or counter, PR3).
3. A one-file `.cs` change renders only the affected closure: with 900 classes
   and no cross-file references, exactly 1 render context per per-source
   template (regression test on a two-file project asserting 1 of 2 contexts
   rendered, PR4).
4. Renaming a type referenced from another file regenerates both outputs
   (closure test, PR5).
5. Deleting a `.cs` removes its generated output only when the output carries
   the Typewriter header (PR4).
6. `.tst` save regenerates all outputs of that template (existing behavior,
   guarded by a test).
7. All existing tests and snapshots pass byte-identical before/after every PR.
8. Measured on `samples/SimpleApi` equivalent scaled fixture: save-to-generated
   latency drops from seconds (full render) to tens of milliseconds
   (stamp check + 1 parse + `ReplaceSyntaxTree` + closure renders).

---

## 6. Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| Missed cross-file dependency produces a stale output | Reverse index derived from the same reference graph used for rendering; closure tests; `generation.incremental: off` escape hatch; fallback sweep mode |
| `FileSystemWatcher` event loss (buffer overflow) | `OnWatcherError` clears accumulated paths and forces a full run |
| Stale metadata after incremental compilation update | Reused `SourceFileMetadata` is a detached snapshot; changed files always fully re-extracted; snapshot byte-parity gates every PR |
| Deleting a user file mistaken for generated output | Delete only files carrying the `GeneratedFileHeader` marker |
| Protocol drift between IDE clients and language server | `ChangedInputs` is optional in `typewriter/generate`; missing field means full generation, so old client + new server and new client + old server both degrade to today's behavior |
| Duplicate-output detection (`ReportDuplicateOutputPath`) weakened by partial runs | Persist the planned-output map per template across incremental runs in the session state |

---

## 7. Relationship to Existing Plans

- plan2026-07-02.md Point 3 maps to PR1-3 plus the type-reference cache; this
  document supersedes its "3 PRs" estimate with the extended scope.
- perf-opt.md "CLI watch session state" (Partial) and "Metadata cache
  validation" / "Source text and syntax-tree reuse" (Pending) are covered by
  PR2/PR3; "Feed watcher dirty paths into ... cache invalidation" (Phase 5,
  item 4) is PR2.
- The suggested overall sequence in plan2026-07-02.md (Point 5 -> Point 4 ->
  Point 3 -> Point 2) is unaffected; within Point 3, use the PR ordering above.
