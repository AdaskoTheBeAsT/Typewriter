# Remaining Generation Performance Work

Implementation handoff for [issue #139](https://github.com/AdaskoTheBeAsT/Typewriter/issues/139).
Updated 2026-11-07 against the working tree on `perf/139-generation-scaling`.

## Scope and Starting State

Improve cold CLI generation and repeated watch/IDE generation without changing output, diagnostics, template behavior, or target-framework selection.

This is a remaining-work plan, not a history of completed optimizations. Completed items and the old overlapping phase lists have been removed. Use the current working tree as the starting point, not `origin/master` or the reporter's older performance branch.

The working tree contains extensive modified and untracked implementation/test files from the preceding scaling work. Preserve them. Completed work includes request-scoped performance diagnostics, project-owned syntax-tree reuse, bounded assembly-reference reuse, per-item CodeModel laziness, extraction/adapter lifetime fixes, and request-scoped solution setup reuse. Their completed steps have been removed from the backlog below.

### Important Corrections to the Previous Roadmap

- Configured directory excludes already prune template traversal. Remaining discovery work concerns narrower include roots, filename filtering, overlapping roots, and repeated discovery.
- Unchanged metadata requests and eligible source-only rebuilds already bypass MSBuild. Do not add another project-load cache to solve a cost those paths no longer incur.
- Source-only rebuilds now reuse project-owned tree snapshots even after global cache eviction. Separate configurations retain their own snapshots, and source stamps describe the content actually parsed.
- Engine and Roslyn reuse stamped assembly-reference images through bounded internal pools. The images do not retain open DLL handles.
- Request-local metadata stage timings and bounded per-template render summaries are available behind `TYPEWRITER_TRACE_PERF`. Performance observers and measurements are not stored in cached metadata.
- Helper constructors, methods, and parameter metadata are already indexed with weak keys. The remaining cost is invocation/dispatch and its temporary allocations.
- Source/declaration indexes, deferred built-in CodeModel adaptation, dependency closure indexing, template source slices, and completed-symbol caches are part of the starting implementation. Do not redo or replace them without evidence.
- Built-in CodeModel collections now adapt individual items on demand. `Count` does not adapt items, and short-circuit traversal does not adapt the remaining items. Empty collections bypass deferred machinery. Public enumerable constructors and custom `Settings` retain eager behavior.
- `Class`/`Type` default collections and attribute/argument types are lazy. Explicit initializers, including null, and captured mapping settings are preserved.
- Adapter-to-metadata lookup uses weak keys so filtering does not own every discarded adapter. The extractor's type-reference cache uses extractor-owned, reference-identity entries instead: lazy edges can capture the extractor, so weak dependent-handle ownership prolonged old extraction graphs. These caches have different ownership requirements.
- A metadata request shares one Buildalyzer manager across its project graph, with serialized access and at most one workspace retained by that request. A fresh request gets a fresh session. Restore, SDK discovery, framework selection, node reuse, and project evaluation remain unchanged.

## Evidence and Reproduction State

### Workload to Represent

The reporter uses 14 mostly SDK-style `net48` projects, including two `net48;net8.0` libraries, 9,275 source files, 13,563 types, and five templates. Two templates visit approximately 9,260 source files each while producing output for only a small subset.

The reporter measured about 70.8 seconds of MSBuild evaluation across the 14 projects after their rendering optimizations. This is evidence for investigating cold project loading, not a current measurement of this working tree.

Historical local evidence is retained in `artifacts/issue139-scaling/`:

- `cli-comparison.json`: saved original/current CLI comparison of approximately 112.00 / 55.26 seconds.
- `cli-fixture/`: 14 SDK `net10.0` projects, 9,275 source files, and five templates.
- `baseline-generated/` and `baseline-generated-hashes.json`: original output bytes and hashes for 931 outputs.
- Previous test/build logs and focused scaling harnesses.

Those timings are single samples from an earlier run. The fixture distributes files across a project chain, unlike the reporter's skewed project sizes, and does not exercise `net48` toolchain selection. Do not present it as a reproduction of the private solution or reuse historical test totals as fresh validation.

### New Follow-up Baseline

Files are local under `artifacts/issue139-followup/`:

- `baseline/`: current pre-follow-up Release CLI binaries and dependencies.
- `Probe.csproj`, `Probe.cs`, and `probe-baseline/`: focused measurement source and executable.
- `baseline-metadata.jsonl`: successful fresh-process metadata experiment.
- `baseline-templates.jsonl`: **not a valid result** from the original failed assertion. The probe is now repaired: it checks the expected generated-file header plus helper output. `baseline-templates-repaired.jsonl` is the first successful template baseline.

The metadata probe loads 9,275 existing fixture source files as **one constructed project**, using a supplied `IProjectWorkspaceLoader`. It exercises the actual Roslyn provider, parsing, extraction, and caching, but excludes MSBuild and rendering. It edits a temporary copy of one source file; the original fixture is not modified.

| Request in one fresh process | Time | Managed allocations | Parsed source files |
| --- | ---: | ---: | ---: |
| Initial load | 4,037 ms | 1,011.8 MB | 9,275 |
| Unchanged request | 1.25 ms | 1,184 bytes | 0 |
| First one-file edit | 3,593 ms | 978.3 MB | 9,275 |
| Second one-file edit | 3,960 ms | 984.6 MB | 9,275 |

These are historical single samples, not statistical estimates. The two edits demonstrate full reparsing at this working-set size, not merely a theoretical capacity concern. The final repeated comparisons below supersede them for before/after claims.

Commands, from PowerShell:

```powershell
dotnet build D:\GitHub\Typewriter\artifacts\issue139-followup\Probe.csproj -c Release --no-restore -o D:\GitHub\Typewriter\artifacts\issue139-followup\probe-baseline
dotnet D:\GitHub\Typewriter\artifacts\issue139-followup\probe-baseline\Probe.dll metadata D:\GitHub\Typewriter\artifacts\issue139-scaling\cli-fixture 9275
```

Do not overwrite the preserved baseline with changed production binaries. Build a separate after-change harness output, supplying `BaselineDir` with the directory containing the new dependency DLLs. The repaired template probe retains its output assertions.

### Validated Follow-up Results

Raw evidence is under `artifacts/issue139-followup/measurements/`; `Measure-Followup.ps1` records the procedure. Focused probes used three alternating baseline/candidate fresh processes per variant, with four requests in each process. Times and allocations below are medians, and MB means decimal megabytes.

| Scenario | Baseline | Candidate | Additional evidence |
| --- | ---: | ---: | --- |
| Metadata initial load | 4,020 ms | 3,917 ms | Ranges overlap; no established cold-load speedup |
| Metadata unchanged | 0.96 ms | 0.87 ms | Both allocate 1,184 bytes and parse zero sources |
| Metadata first one-file edit | 3,451 ms / 970.7 MB | 2,213 ms / 685.5 MB | 9,275 versus 1 parsed source |
| Metadata second one-file edit | 3,178 ms / 973.8 MB | 1,726 ms / 694.9 MB | 9,275 versus 1 parsed source |
| Template first request | 1,376 ms / 74.6 MB | 1,090 ms / 45.2 MB | Includes first helper compilation |
| Template repeated request 1 | 436 ms / 14.1 MB | 251 ms / 7.5 MB | Existing compiled-byte cache hit |
| Template repeated request 2 | 445 ms / 14.1 MB | 227 ms / 7.5 MB | Preparation work still remains |
| Template repeated request 3 | 456 ms / 13.8 MB | 217 ms / 7.5 MB | Preparation work still remains |

- Every metadata cycle's digest matches across all six processes (24 observations). Both successive edits parse exactly one source in each candidate process.
- Metadata edit GC-pause medians fell from 1,450/1,467 ms to 935/1,020 ms. However, final process peak working-set medians **rose** from 988 MB to 1,077 MB (worst samples: 1,007/1,078 MB). Lower allocation and fewer collections do not imply lower peak memory; do not claim an across-the-board memory improvement.
- Template-process peak working-set medians fell from 827 MB to 208 MB. These are process-wide high-water marks, not retained/live object sizes.
- Two alternating fresh CLI dry-run pairs took 34.21/33.65 seconds for baseline and 32.99/32.01 seconds for candidate. Worst samples were 34.21/32.99 seconds. This is a modest improvement on the synthetic `net10.0` fixture, not proof of a material cold-load improvement on the reporter's solution.
- All untraced CLI runs succeeded with 931 output records and zero diagnostics. A traced actual-write run emitted 147 `TWPERF` diagnostics, including bounded summaries for two templates with 9,275 render calls each. All 931 filenames and SHA-256 hashes match the saved original output snapshots.

### Build Environment Blocker

The normal full solution build failed because the running language server locked DLLs in `src/Typewriter.LanguageServer/bin/Release/net10.0/`. This was a copy/file-lock failure, not a demonstrated source compilation error. Do not stop the user's editor or its server without permission.

An isolated CLI baseline build succeeded:

```powershell
dotnet build D:\GitHub\Typewriter\src\Typewriter.Cli\Typewriter.Cli.csproj -c Release --no-restore -o D:\GitHub\Typewriter\artifacts\issue139-followup\baseline
```

Logs:

- `artifacts/issue139-followup-baseline-build.log`: failed normal solution build.
- `artifacts/issue139-followup-cli-build.log`: successful isolated CLI build.

Use a separate output location for subsequent CLI builds. For the full solution, use SDK artifacts output with per-project directories rather than sending every project into one shared output directory. Restore into that layout before using `--no-restore` or `--no-build`.

The current full solution builds with `--artifacts-path artifacts/valuetuple-fix/after -m:1 -nr:false`, with zero warnings and zero errors (`artifacts/valuetuple-solution-after.log`). Serial MSBuild avoids an intermediate-output lock/node failure seen during a parallel isolated build. The earlier `System.ValueTuple` `MSB3277` warning was fixed in the Buildalyzer submodule: restore the `net472` logger with `ExcludeRestorePackageImports=true`, then build it in a separate project evaluation. This loads the package's existing facade-unification targets on the first build from fresh outputs, without suppressing warnings or changing dependency versions.

All 682 tests passed after the logger build fix (`artifacts/valuetuple-tests.log`). The latest isolated solution build has zero warnings/errors, and all **693 tests pass with no skips** (`artifacts/issue139-next/ownership-solution-build-2.log`, `ownership-full-tests.log`). Added coverage includes lazy defaults, sparse collection access, captured/custom settings, adapter reclamation, old-compilation reclamation across two edits, request session boundaries, cancellation/retry, changed project shape, and workspace replacement. An earlier full run exposed concurrent test cache clearing/count assertions. Those test classes now use a nonparallel collection; the production cache's semantics were not relaxed to make the assertions pass.

### Direct Reporter-branch Comparison

Evidence and reproduction scripts are under `artifacts/issue139-next/`:

- `baseline/` preserves the current pre-laziness CLI, including the earlier scaling changes and logger build fix. It is not the historical original branch.
- `reporter/` is an isolated detached worktree at `614de06` from `klanchman-revecore/Typewriter:performance-changes`; `reporter-build/` contains its unchanged CLI build.
- `validation/` contains the candidate solution build. `NextProbe.cs` and `Measure-Next.ps1` run three rotating fresh-process samples per focused scenario. The initial CLI compatibility gate found a reporter-branch mismatch. `Complete-Next.ps1` separately compares a shared supported template and verifies candidate full-fixture output. `Summarize-Next.ps1` checks the observations against preserved digests and summarizes the results.
- The attribute workload has 13,563 classes, four attributes per class, six-level nested type references, five helper-filter templates, and 977 declarations sharing one source path. Independent assertions verify the selected names, rather than trusting agreement among implementations. This stresses deferred adaptation; it is not the reporter's private solution.
- The metadata workload loads the real 9,275 fixture files into one supplied project loader, excluding MSBuild. The separate `retention` scenario forces two full collections before live-memory observations, outside the timed interval. Do not treat its timings as ordinary generation timings.
- The solution-setup microprobe invokes the loader's actual manager setup for the same 14-project solution. It excludes SDK discovery, restore, evaluation, and rendering. A reduction here is not an end-to-end MSBuild speedup.

Focused results are under `artifacts/issue139-next/measurements/`. There are 135 observations from 36 fresh processes; all 72 metadata digests and 27 attribute digests match the preserved expectations. Values below are medians of three processes; MB means decimal megabytes.

| Scenario | Pre-laziness baseline | Reporter branch | Candidate |
| --- | ---: | ---: | ---: |
| Attribute-heavy first request | 6,947 ms / 1,743.5 MB allocated | 1,784 ms / 815.0 MB | 1,880 ms / 768.6 MB |
| Attribute-heavy repeat 1 | 7,265 ms / 1,683.1 MB allocated | 778 ms / 756.3 MB | 723 ms / 718.2 MB |
| Attribute-heavy repeat 2 | 6,890 ms / 1,682.8 MB allocated | 1,435 ms / 756.2 MB | 1,225 ms / 718.4 MB |
| Attribute-process final peak | 737.8 MB | 427.7 MB | 306.9 MB |
| Metadata cold, excluding MSBuild | 4,561 ms | 5,108 ms | 4,481 ms |
| Metadata edit 1 | 3,937 ms / 676.3 MB allocated | 5,925 ms / 1,110.2 MB | 3,993 ms / 677.9 MB |
| Metadata edit 2 | 3,006 ms / 683.8 MB allocated | 5,358 ms / 1,112.2 MB | 3,072 ms / 687.6 MB |
| Ordinary metadata-process final peak | 1,068.6 MB | 1,030.4 MB | 1,079.7 MB |
| Live managed memory after two edits, forced-GC probe | 818.0 MB | 435.7 MB | 393.4 MB |
| Cold setup for 14 project loads, excluding MSBuild | 49.3 ms / 2.73 MB allocated | 47.4 ms / 2.73 MB | 41.1 ms / 0.25 MB |

- The candidate reduces attribute allocations and peak memory beyond the reporter branch, but is **not uniformly faster**. Its first request is about 5% slower. Repeat medians improve, but worst repeat times are 1,035/2,149 ms versus the reporter's 802/1,470 ms. Worst attribute-process peaks are 747.0/427.8/307.7 MB for baseline/reporter/candidate.
- Unchanged metadata requests still parse zero sources and allocate 1,184 bytes for baseline/candidate, versus 616 bytes for the reporter. Baseline/candidate edits parse one source; reporter edits reparse all 9,275. This follow-up does not establish another metadata speedup over the already optimized baseline.
- Old-compilation reclamation tests and the forced-GC probe verify the retention fix. Candidate live memory remains about 391–395 MB after two edits rather than baseline 818 MB. After clearing caches, observations remain influenced by the probe's last metadata result; do not call these complete process-release measurements.
- Lower live memory does **not** imply a lower natural peak. Without forced collections, worst metadata peaks are 1,074.0/1,038.9/1,080.6 MB for baseline/reporter/candidate. The candidate has not solved the ordinary metadata peak-memory problem.
- Request-scoped solution setup saves about 8 ms in this cold microprobe, not seconds of MSBuild work. Subsequent in-process setup batches fall from about 13 ms to 1.5 ms. Restore and per-project process/evaluation costs remain; do not claim a material end-to-end cold-load win from this change.
- The reporter branch returns only **467 of the expected 931 full-fixture outputs**, with no diagnostics. All 464 missing outputs come from the inline-predicate-only `Others.tst` template (names `Type{20k+5}.ts`). Its full-fixture timing therefore cannot be compared as equivalent work. The branch remains unchanged, and the 931-output compatibility requirement for the candidate has not been relaxed.

The common CLI workload uses the unchanged `Models.tst` template on the same 14-project fixture. Every variant produces the independently expected 464 filenames, and each actual-write run matches their original SHA-256 hashes. Fresh-process wall times, including startup, are:

| Variant | Dry-run sample | Actual-write sample |
| --- | ---: | ---: |
| Pre-laziness baseline | 37.62 s | 29.03 s |
| Reporter branch | 39.92 s | 35.09 s |
| Candidate | 35.54 s | 35.06 s |

These are one sample per mode, not interchangeable repetitions. Their spread does **not** establish a reliable end-to-end cold speedup: the candidate beats the reporter in the dry-run sample, ties it in the write sample, and loses to the baseline in the write sample. Do not pool modes or omit the slower observation.

The candidate also passes the original, unchanged **five-template/931-output** actual-write check: every filename and SHA-256 matches the saved original snapshots, with zero diagnostics. The common-template comparison supplements that gate; it does not replace or weaken it.

`measurements/summary.json` contains the validated summary; `compatibility.json` records all three 464-output checks and the candidate's 931-output check. `full-fixture-comparison.json` records the reporter's 464 missing outputs. The initial runner stopped at its full-fixture compatibility gate; completed focused observations were preserved, and `Complete-Next.ps1` performed the separate supported-workload comparison. No reporter code or original template was changed to make the comparison pass.

Overall: the candidate substantially improves this working tree's attribute-heavy behavior and old-graph retention. It uses less attribute-process memory than the reporter branch, but does not win every timing or metadata peak measurement. These synthetic `net10.0` results do not establish performance on the reporter's private, mostly `net48` solution. File-buffer and cache-capacity limits remain unchanged.

## Implementation Order

All work below remains pending.

| Order | Work package | Main benefit | Risk |
| --- | --- | --- | --- |
| 4 | Cheaper compiled-template cache hits | Reduce repeated helper preparation | Medium |
| 5 | Reuse compilations and generator state | Reduce source-only rebuild work beyond parsing | High |
| 6 | Deeper graph-scoped MSBuild work | Reduce the dominant cold-load cost beyond solution setup | High |
| 7 | Cached helper dispatch | Reduce per-item invocation overhead | Medium |
| Later | Measured discovery, rendering, metadata, and formatting work | Workload-dependent | Varies |

Start with package 4 using the new timings and shared reference images. Package 6 is the strongest cold-run candidate, but needs its own design and compatibility validation rather than being mixed into the cache changes. Keep packages independently measurable and reviewable.

## 4. Cheaper Compiled-template Cache Hits

### Current Cost and Locations

`src/Typewriter.Engine/TemplateRuntimeCompiler.cs`, `GetOrCompileCacheEntry`, builds host source, resolves references, and calls `TemplateInlinePredicateCompiler.AppendToSource` before looking up compiled bytes.

`src/Typewriter.Engine/TemplateInlinePredicateCompiler.cs` may create a Roslyn declaration compilation to infer item types for helper-returned collections, even on a later compiled-byte hit.

### Implementation

1. Measure preparation, actual compilation, assembly loading, and helper construction as separate costs using the existing stamped reference pools.
2. Cache prepared template source/binding results using the parsed document and the identities of all relevant dependencies.
3. Include local `#load` contents, reference stamps/properties, template path/line mapping, and inline-predicate inputs in validity checks. Preserve remote `#load` refresh behavior and package-resolution behavior.
4. Reproduce diagnostics correctly on cache hits. Do not hide a changed dependency, resolution failure, or new compiler error behind an old success.
5. If concurrent misses duplicate compilation measurably, use per-key single-flight publication without caching failed/cancelled work permanently.
6. Keep the current fresh assembly-load-context boundary between generation factories. Do not cache a loaded host type across generation runs as a shortcut: that changes template static-state lifetime.

### Validation / Completion

- First compile and unchanged repeated requests produce identical output and diagnostics.
- Edit template code, loaded helper code, and referenced DLLs independently; each must invalidate the appropriate entry.
- Cover helper-returned collection predicates, template-local paths, CRLF line diagnostics, concurrent requests, and cancellation/retry.
- Preserve collectible-context tests and isolation of static state, instance state, settings, and callbacks.
- Use the repaired local `templates` probe and retain its output assertions for before/after comparisons.

## 5. Reuse Compilations and Source-generator State

### Current Cost and Locations

Source-only rebuilds still call `CSharpCompilation.Create`, recreate the generator loader and driver, and run metadata extraction for every source type in each affected project. `RunSourceGenerators` discards the driver returned by `RunGeneratorsAndUpdateCompilation`.

### Implementation

1. Build on the existing project-owned tree snapshots. Retain the **pre-generator** compilation separately from the final generated compilation.
2. Replace changed trees and update references/options on a derived immutable compilation. Do not feed previously generated trees back as ordinary source inputs.
3. Retain the returned generator driver so incremental generators can reuse their own state. Update additional texts, analyzer configuration, parse options, and generator identities as their inputs change.
4. Define ownership for generator assemblies/load contexts. The cache must not unload a context while a retained driver still uses it, or leak contexts through discarded historical results.
5. Keep per-request updates isolated when concurrent callers rebuild the same cache key. Do not mutate a driver or shared builder in place.
6. Preserve all generators and required diagnostics. Do not achieve speedups by disabling either.
7. Initially continue full metadata extraction against the new compilation. Symbol-derived metadata reuse is a separate, higher-risk optimization because a referenced type change can alter unchanged source symbols.

### Validation / Completion

- Compare cold, unchanged, one-source-edit, and dependency-edit runs.
- Test incremental and non-incremental generators, no generators, additional-file changes, analyzer-config changes, changed generator DLLs, and generated-type removal.
- Check duplicate generated trees, stale diagnostics, cancellation/retry, and concurrent rebuilds.
- Confirm load-context reclamation after eviction and that dependent compilations remain usable.
- Measure generator/extraction stages independently so retained state is justified by actual savings.

## 6. Graph-scoped MSBuild Work

### Current Cost and Locations

- `CSharpProjectMetadataProvider.GetMetadataCoreAsync` awaits referenced projects sequentially.
- `src/Typewriter.Buildalyzer/MsBuildProjectLoader.cs`, `LoadAsync`, calls synchronous `LoadCore` before returning `Task.FromResult`.
- Each metadata request now reuses an `AnalyzerManager` within its project graph. Direct standalone loader calls remain independent. This eliminates repeated solution/project-file setup, not the individual MSBuild evaluations.
- `CreateEnvironmentOptions` disables node reuse and shared compilation.
- Vendored `Buildalyzer/src/Buildalyzer/Environment/EnvironmentOptions.cs` defaults `Restore` to `true`; `ProjectAnalyzer.cs` adds `/restore`.
- Buildalyzer already uses `BuildProjectReferences=false` for design-time builds. Do not propose this as a missing optimization.

### Implementation

1. Use the existing request-scoped metadata timings and representative MSBuild logs to separate process startup, restore, evaluation, and design-time target execution.
2. Model project identity using path, target framework, workspace/global properties, and other relevant evaluation inputs. Deduplicate shared dependencies by that identity, not path alone.
3. Extend the existing request-scoped session only when graph discovery or restore coordination is supported by measurements. Do not add another long-lived manager or project-load cache.
4. Deduplicate restore only after establishing that the successful restore covers the same graph/configuration. Preserve failures and a conservative fallback for unsupported/custom project behavior.
5. Schedule independent evaluations with bounded concurrency. The synchronous loader needs real worker execution or an asynchronous process API; wrapping current calls in `Task.WhenAll` is insufficient.
6. The current session serializes manager access. Do not remove that protection or share mutable load state and ordinary dictionaries concurrently without a redesign and tests.
7. Avoid concurrent restore/build writes to the same project intermediates. Release concurrency slots before waiting for child tasks to avoid dependency deadlocks.
8. Preserve deterministic project/reference/diagnostic ordering and existing target-framework selection. Do not substitute compiled assemblies for source projects as a transparent optimization.
9. Evaluate node reuse separately with cleanup and host-lifetime tests. Do not flip it globally as an unmeasured shortcut.

### Validation / Completion

- Fixtures: a chain, independent branches, a diamond/shared dependency, mixed SDK `net48`/`net8.0`, imported target-framework properties, and an old-style project where supported.
- A chain with references discovered only after evaluation will not automatically benefit from sibling concurrency. Measure it honestly.
- Test missing imports, restore failures, cancellation, generated inputs, custom targets, and repeated requests.
- Measure the actual CLI in fresh processes, including child-process resource use. Report the slowest sample as well as the distribution.
- Require identical generated bytes, diagnostics, selected frameworks, and source-project coverage.

## 7. Cached Helper Dispatch

### Current Cost and Locations

`src/Typewriter.Engine/CompiledTemplateHelper.cs` still allocates context arrays and argument arrays per call, scans same-name overloads, adapts arguments, and uses `MethodInfo.Invoke`.

`CompiledTemplateBindings.cs` already caches reflection metadata using weak type keys.

### Implementation

1. Index candidates by method name and arity while retaining current overload order.
2. Cache a dispatch/invocation path for compatible one- and two-argument signatures. Keep reflection fallback for unusual signatures.
3. Use open delegates or wrappers that receive the current host rather than retaining a previous helper instance.
4. Avoid temporary context arrays on common paths. Do not share a mutable argument array across recursive or concurrent invocations.
5. Preserve adaptation behavior and custom-settings observations. Cache stable dispatch facts, not the result of arbitrary helper or predicate execution.
6. Preserve current error formatting, including template stack frames and underlying invocation exceptions.
7. Keep caches weakly associated with host types so collectible template assemblies can unload.

### Validation / Completion

- Cover overloads, one/two contexts, incompatible arguments, private/static/instance methods, null and boxed values, throwing helpers, and callbacks.
- Exercise parent-aware helpers, stateful helpers, reentrant invocation, parallel independent helpers, and collectible assemblies.
- Measure actual template rendering, not just an isolated delegate microbenchmark. Compare allocations per helper call and total generation.

## Later Work: Implement Only Where Remaining Profiles Justify It

### A. Reusable Render Plans and Recipe Predicates

Locations: `TemplateDocument.cs`, `TemplateSourceIndex.cs`, and `TemplateRenderer.cs`.

- Precompute compatibility recipe predicate descriptors instead of repeatedly scanning the same method body in `MatchesRecipeTypePredicate`.
- Compile stable token interpretation into a reusable render plan while retaining the existing source slices and shared indexes.
- Consolidate remaining parse passes only if they remain measurable; do not replace working delimiter/line indexing unnecessarily.
- Preserve nested collections, scalar parentheses, escaping, separators, inline-lambda locations, malformed-template diagnostics, and CRLF mappings.
- Do not skip a source-file render based on an assumed pure filter: constructors, helper calls, settings, output-filename factories, and completion callbacks may have side effects.

### B. Feature-driven Metadata and Lazy Documentation

Locations: `CSharpProjectMetadataProvider.MetadataExtractor.cs`, template parsing, and metadata-provider request/cache contracts.

- Compute conservative required metadata features for templates without arbitrary compiled helper code.
- Union requirements for templates sharing a project load; include the feature set in cache identity or support safe upgrades to richer entries.
- Build a lightweight type identity/classification layer first, then only the requested categories.
- Treat arbitrary helpers, custom settings, reflection, and unknown feature use as requiring the full model.
- Begin with measured documentation extraction; avoid retaining an entire old compilation through lazy delegates after eviction.
- Test a simple template followed by a richer template, mixed template sets, dependent types, documentation tags, and source edits.
- `GetMembers()` is already obtained once per type, but category methods enumerate it repeatedly. A single classification pass is a small separate candidate, not a substitute for eliminating expensive extraction.

### C. Narrower Discovery and Remaining Watch-session Work

Locations: `FileSystemTemplateDiscovery.cs`, `WorkspaceProjectResolver.cs`, `TypewriterGenerator.cs`, and CLI/IDE watcher entry points.

- Derive traversal roots from the longest safe literal prefix of include patterns; apply filename filters for simple patterns.
- Deduplicate overlapping directory roots before traversal, preserving existing ordering and final matching semantics.
- Preserve solution items, external project directories, explicit template paths, default patterns, configured excludes, and user-enabled directories.
- Reuse discovery results within a request and across watched requests only with adequate invalidation. A top-level directory timestamp does not detect arbitrary nested changes.
- Scope configuration/template discovery invalidation using changed paths while retaining full fallback for unknown provenance, deletion, rename, and shape changes.
- Keep persistent IDE generation and existing incremental dependency filtering intact; those are not new tasks.
- Benchmark a large repository with few templates, nested/overlapping projects, and repeated no-change/source-only requests.

### D. Output Formatting with Fewer Allocations

Location: `OutputContentFormatter.cs`.

- Keep the default-settings fast path.
- Replace full-string replacement and split/join chains with span-based scanning and one output writer.
- Preserve global indentation-unit detection. It depends on all relevant lines, so use a cheap first scan followed by the writer rather than forcing a semantically different one-pass algorithm.
- Cover CRLF, LF, lone CR, mixed indentation, blank lines, trailing spaces/tabs, empty content, and final-newline settings.
- Compare exact bytes for every settings combination and measure large outputs, including peak memory.

### E. Long-pattern Glob Construction

Location: `PathGlobMatcher.cs`.

- Match-time nonbacktracking behavior is already present. Remaining concern is construction cost for very long patterns.
- The saved 256-star probe reports roughly 400 ms and 273 MB allocated; remeasure before changing the implementation.
- If relevant in real configurations, replace regex construction for the supported glob grammar with a bounded matcher while preserving existing semantics.
- Do not add arbitrary pattern-length rejection or silently change wildcard behavior to meet a performance target.

## Cross-cutting Compatibility Rules

- Preserve generated filenames and bytes, root-item behavior, output collision ordering, and meaningful diagnostic content/multiplicity.
- Preserve actual `File`/member parent navigation, partial declarations, generic substitution, nested types, and enum values.
- Preserve constructor timing, custom `Settings` behavior, and helper/static-state isolation boundaries.
- Do not disable restore, diagnostics, source generators, or project references to manufacture a speedup.
- Do not cache recursion-context-truncated type references as globally reusable completed references.
- Do not remove necessary transitive compilation references merely to make graph traversal appear linear.
- Keep cancellation, cache eviction, failure recovery, and concurrency in the tests for every new cache.
- Preserve the user's existing modified and untracked files. Keep new work distinguishable from the preceding changes.

## Validation and Handoff Checklist

For each work package:

1. Add focused regression tests that exercise the real public entry point where possible.
2. Reproduce the pre-change behavior with the preserved baseline; keep assertions and expected outputs independent of the optimized implementation.
3. Run targeted tests, then the full solution build/test suite from an isolated output layout.
4. Compare all 931 existing CLI output filenames and bytes against the preserved original snapshots. Dry-run counts alone are insufficient.
5. Measure cold and unchanged repeated requests plus at least two successive edits. Include the largest input and first helper compilation.
6. For cache work, test below, at, and above capacity; multiple configurations; failures; and eviction while consumers still use returned objects.
7. Use fresh processes for cold comparisons, run timing experiments sequentially, and avoid overlapping builds/tests. Alternate baseline and candidate runs where practical.
8. Record elapsed time, allocated bytes, GC behavior, peak memory, parse/load counts, and stage timings. Distinguish allocation totals from live or peak memory.
9. Repeat measurements after the final implementation change. Report regressions and noise rather than claiming every metric improved.
10. Update this document by removing a work package only when its implementation and validation are complete. Keep raw evidence under a clearly named artifact directory and summarize unresolved limitations.

Next implementer: continue with compiled-template preparation (package 4) using the new stage diagnostics, then assess compilation/generator-state reuse and graph-scoped MSBuild work separately. Preserve the baseline binaries, measured output assertions, compatibility snapshots, and bounded caches.
