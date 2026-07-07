# Typewriter Implemented

Completed and current-state implementation record. Remaining roadmap and backlog items are tracked in [to_implement.md](to_implement.md).

## Implementation Status as of 2026-07-02

This section records what has already been implemented in this repository after the initial reimplementation work. It is intentionally separate from the roadmap above so the plan remains useful as a target architecture document.

## Milestone Implementation Status

| Milestone | Status | Notes |
|---|---:|---|
| Milestone 1: Clean Core Foundation | Done | Solution structure, abstractions, engine/Roslyn/Buildalyzer/CLI projects, unit test projects, diagnostics/result models, and editor-independent core are in place. |
| Milestone 2: Roslyn Metadata | Done | Classes, records, interfaces, enums, delegates, properties, methods, parameters, constants, fields, static readonly fields, events, attributes, doc comments, enum values, base types, accessibility, collections, dictionaries, tuple/value tuple shapes, nullable metadata, generic type argument nullability, and per-declaration nullable context are implemented. Remaining old-template language compatibility work is tracked under Milestone 9. |
| Milestone 3: First Template Execution | Done | Minimal old-style `.tst` execution works, generated files are planned safely, changed-file writes are supported, diagnostics are returned, and SimpleApi generation is snapshot-tested. |
| Milestone 4: CLI MVP | Done | `generate`, `validate`, `list-templates`, JSON/text output, dry-run, framework, fail-on-warning, config loading, exit code mapping, and CLI integration tests are implemented. |
| Milestone 5: Watch Mode | Done | `typewriter watch` is implemented with file watching, debounce, cancellation, continuous result output, and CLI integration coverage for initial generation and source-change regeneration. |
| Milestone 6: VS Code MVP | Done | VS Code extension project, `.tst` language association, syntax highlighting, generate/validate commands, CLI bridge, JSON diagnostic parsing, Problems diagnostics, and save-time validation/generation settings are implemented. |
| Milestone 7: Visual Studio MVP | Done | SDK-style VSIX adapter, VSIX/pkgdef packaging, generate/validate commands, CLI bridge, generate-on-save, Output window logging, Error List diagnostics, `.tst` classification, and native Ctrl+Space fallback completions are implemented. Focused VSIX build and full solution build/test pass; manual VS installation smoke testing is still recommended before release. |
| Milestone 8: Language Server MVP | Done | LSP server process, JSON-RPC protocol loop, document open/change/save/close sync, live template diagnostics, completion, hover, go-to-definition, semantic tokens, VS Code language client startup, and restart command are implemented. |
| Milestone 9: Rich Template Experience | Done; compatibility hardening ongoing | LSP completions, hover, and definition support are implemented. The renderer now also has compatibility filters, metadata substitutions, inline lambda filters, compiled C# template helpers, two-parameter predicate helper invocation, local `#r`/NuGet reference resolution, TypeScript template-literal interpolation preservation, original public Typewriter.CodeModel surface parity, richer CodeModel type/attribute/default handling, and per-source `OutputFilenameFactory` fan-out with no-match suppression. Checked snapshots cover external Angular/React System.Text.Json service recipes, Angular/React System.Text.Json model recipes, Angular/React Newtonsoft.Json model recipes, and an external Angular constants recipe when the sibling `NetCoreTypewriterRecipes` checkout is available. Recent hardening covers nullable dictionary/list mapping, enum defaults, and the `AllowedValuesAttribute` scenario from issue 55. Remaining work is old parser/runtime edge-case hardening as real recipes expose gaps. |
| Milestone 10: Packaging and Release | Done for NuGet and GitHub release assets; marketplaces ongoing | Tag releases publish the CLI and language-server dotnet tools to NuGet and attach VS Code, Visual Studio, and Rider packages to GitHub releases. Direct marketplace publishing remains backlog work. |

## PR Sequence Status

```text
PR 01: Initial solution structure                 Done
PR 02: Abstractions and diagnostics model         Done
PR 03: Roslyn metadata extraction                 Done
PR 04: Template discovery and parsing skeleton    Done
PR 05: First generation pipeline                  Done
PR 06: Snapshot test infrastructure               Done
PR 07: CLI generate command                       Done
PR 08: CLI JSON output and exit codes             Done
PR 09: Project/solution loading through Buildalyzer Done
PR 10: Watch mode                                 Done
PR 11: VS Code extension skeleton                 Done
PR 12: VS Code commands and CLI bridge            Done
PR 13: VS Code diagnostics                        Done
PR 14: Visual Studio SDK-style VSIX skeleton      Done
PR 15: Visual Studio commands and engine bridge   Done
PR 16: Visual Studio generate-on-save             Done
PR 17: Language server skeleton                   Done
PR 18: Language server diagnostics                Done
PR 19: Completion and hover                       Done
```

## Implemented

```text
- Initial solution structure with source and unit test projects
- Typewriter.Abstractions project
- Typewriter.Engine project
- Typewriter.Roslyn project
- Typewriter.Buildalyzer project
- Typewriter.Cli project
- Typewriter.LanguageServer project
- Typewriter.VisualStudio project
- Typewriter.Engine.Tests project
- Typewriter.Roslyn.Tests project
- Typewriter.Buildalyzer.Tests project
- Typewriter.Cli.Tests project
- Typewriter.LanguageServer.Tests project
- Typewriter.SnapshotTests project
- SimpleApi sample project and .tst template
- SimpleApi checked-in generated TypeScript snapshot
- NullableModels sample project and generated TypeScript snapshot
- RecordsAndEnums sample project and generated TypeScript snapshot
- MultiProjectSolution sample project and generated TypeScript snapshot
- WebApiServices sample project with service and constants templates plus checked-in TypeScript snapshots
- SignalRHubs sample project with SignalR hub template plus checked-in TypeScript snapshot
```

Implemented core models and contracts:

```text
- GenerationRequest
- GenerationResult
- GeneratedFile
- GenerationDiagnostic
- TypewriterConfiguration
- ProjectMetadata
- SourceFileMetadata
- TypeMetadata
- PropertyMetadata
- AttributeMetadata
- TypeMetadataReference
- EnumValueMetadata
- ITypewriterGenerator
- ITemplateDiscovery
- IProjectMetadataProvider
- IGeneratedFileWriter
```

Implemented generation pipeline:

```text
- Resolve workspace and project path
- Discover templates from explicit --template or configured template globs
- Load project metadata
- Parse template directives
- Render a minimal old-style .tst template dialect
- Plan generated output paths
- Block output paths outside workspace
- Refuse to overwrite existing non-generated files
- Add generated-file header
- Write only changed files
- Return shared diagnostics and generated-file results
```

Implemented CLI features:

```text
- System.CommandLine-backed command and option parsing
- typewriter generate
- typewriter validate
- typewriter watch
- typewriter list-templates
- --workspace
- --project
- --template (single file or directory)
- --template-search-path
- --framework
- --output text|json
- --dry-run
- --fail-on-warning
- --all-projects
- --diff (unified diff output for content, line-ending, and final-newline changes in text and JSON modes)
- Exit codes for success, generation failure, invalid arguments, project-load failure, and template parse failure
```

Implemented watch mode:

```text
- FileSystemWatcher-based CLI watcher
- Initial generation when watch starts
- Watches .cs, .csproj, .json, .props, .targets, .sln, .slnx, and .tst files
- Ignores bin, obj, node_modules, and generated output directories
- Debounces watched changes before regeneration
- Cancels an in-flight generation when a new watched change arrives
- Supports Ctrl+C / cancellation-token shutdown
- Prints generation results on each run using the selected text or JSON output mode
```

Implemented VS Code MVP:

```text
- vscode/package.json extension manifest
- .tst language association
- basic .tst TextMate syntax highlighting
- language configuration for comments, brackets, and auto-closing pairs
- Typewriter: Generate Current Template command
- Typewriter: Generate All Templates command
- Typewriter: Validate Current Template command
- CLI bridge that runs the local repo CLI through dotnet run when available, otherwise falls back to typewriter on PATH
- Settings for cliPath, cliArguments, workspacePath, projectPath, templatePath, framework, allProjects, generateOnSave, and validateOnSave
- JSON CLI result parsing
- Problems panel diagnostics mapped from shared Typewriter diagnostics
- Typewriter output channel with command, CLI output, generated files, and diagnostic summary
- Save-time validation by default, with optional generate-on-save
```

Implemented Visual Studio MVP:

```text
- SDK-style Typewriter.VisualStudio VSIX project targeting net472
- Microsoft.VSSDK.BuildTools-backed VSIX packaging with generated pkgdef metadata
- VSIX manifest targeting Visual Studio 2022 Community, Professional, and Enterprise on amd64 and arm64
- Tools menu commands for Typewriter: Generate Current Template, Generate All Templates, and Validate Current Template
- CLI bridge that runs the local repo CLI through dotnet run when available, otherwise falls back to typewriter on PATH
- Visual Studio options page for CLI path, CLI arguments, workspace/project/framework overrides, all-projects, and generate-on-save
- Active solution/project/template discovery for command execution
- Output window pane with command line, CLI output, generated file summary, and diagnostics summary
- Error List diagnostics mapped from shared CLI JSON diagnostics with file/line/column navigation
- Running Document Table save listener for .tst generate-on-save
- .tst editor classification for directives, template members, collections, comments, strings, and C# helper blocks
- Native Visual Studio Ctrl+Space fallback completions for Typewriter template expressions, C# helper blocks, and TypeScript output regions
- Visual Studio language-server startup prefers an already built local Typewriter.LanguageServer.dll before falling back to dotnet run or packaged typewriter-lsp
- VS project is included in AdaskoTheBeAsT.Typewriter.slnx
```

Implemented Language Server MVP:

```text
- Typewriter.LanguageServer executable project targeting net10.0
- Content-Length framed JSON-RPC transport over standard input/output
- initialize/shutdown/exit request lifecycle
- textDocument/didOpen, didChange, didSave, and didClose document sync
- Debounced live validation with cancellation of superseded validation runs
- textDocument/publishDiagnostics notifications mapped from shared Typewriter diagnostics
- In-memory template discovery so diagnostics use the current open document text, not only saved file content
- textDocument/completion for template collections, scalar members, filters, helper methods, and loaded C# metadata
- context-aware completion for C# helper blocks and TypeScript output regions
- textDocument/hover for template members, helper methods, and loaded C# metadata
- textDocument/definition for C# source symbols and generated output from output directives
- textDocument/semanticTokens/full for Typewriter tokens, C# helper blocks, and TypeScript output regions
- VS Code language client startup when typewriter.languageServer.enabled is true
- VS Code Typewriter: Restart Language Server command
- VS Code settings for languageServer.path and languageServer.arguments
```

Implemented project loading in Typewriter.Buildalyzer:

```text
- Buildalyzer submodule integrated from https://github.com/AdaskoTheBeAsT/Buildalyzer.git
- Submodule branch configured as feature/typewriter-v2
- Typewriter.Buildalyzer now uses Buildalyzer AnalyzerManager instead of the earlier lightweight parser
- TargetFramework / TargetFrameworks evaluation through Buildalyzer
- MSBuild-evaluated source files
- MSBuild-evaluated metadata references, including NuGet/package references
- MSBuild-evaluated preprocessor symbols
- Nullable and ImplicitUsings property capture
- Recursive ProjectReference traversal
- Generated obj assembly metadata files are filtered out before Roslyn compilation
- .sln workspace support through Buildalyzer AnalyzerManager
- .slnx workspace support through Typewriter project discovery plus explicit MSBuild solution properties
- Single-project .sln/.slnx workspaces can resolve the project automatically
- Multi-project .sln/.slnx workspaces report a diagnostic and require --project
- Explicit --all-projects fan-out for multi-project workspaces
- Project-local template discovery when fan-out is enabled
- Buildalyzer.Logger kept on netstandard2.0 for MSBuild logger loading
- Runtime dependencies for Buildalyzer.Logger, MsBuildPipeLogger, and Roslyn are copied for CLI/test execution
- Buildalyzer evaluation disables MSBuild node reuse and shared compilation for more predictable CLI/test execution
- Legacy non-SDK .csproj loading with ToolsVersion and TargetFrameworkVersion
- TW0003 diagnostic surfacing for unresolved unconditional <Import> elements even when Buildalyzer reports success with zero source files
```

Implemented Roslyn metadata extraction:

```text
- Classes
- Records
- Interfaces
- Enums
- Structs
- Delegates
- Properties
- Indexer properties with parameters
- Methods and parameters
- Constants, fields, static readonly fields, and events
- Property types
- Nullable reference annotations
- Nullable<T>
- Nullable generic type arguments
- Nullable collection element types
- Per-declaration nullable context from `#nullable enable`, `#nullable disable`, and `#nullable restore`
- Collections and arrays
- Dictionary detection
- Attributes and attribute arguments
- Attribute arguments containing nulls, constants, typeof values, source-like multi-argument values, and named arguments
- XML doc comments and parameter comments
- Base types and implemented interfaces
- Nested classes, records, interfaces, and enums
- Type parameters and type arguments
- Tuple/value tuple metadata
- Assembly names and source file locations
- Enum values
- Enum values on type references, so enum defaults can be resolved during per-source rendering
- Accessibility
```

Implemented template support:

```text
- $Classes[...] 
- $Records[...] 
- $Interfaces[...] 
- $Enums[...] 
- $Structs[...]
- $Properties[...] 
- $Properties($IsIndexer)[...] with $Parameters for indexer parameter rendering
- $Values[...] / $EnumValues[...]
- $Methods[...]
- $Parameters[...]
- $Constants[...]
- $Fields[...]
- $StaticReadOnlyFields[...]
- $Events[...]
- $Delegates[...]
- $DocComment[...] and $DocComment[$Parameters[...]]
- $TypeParameters and $TypeArguments as direct legacy string-convertible collections
- Scalar substitutions such as $Name, $name, $FullName, $Namespace, $Type
- Name case formatting through `GetName(NameCase)`, `ToNameCase(NameCase)`, and direct template aliases such as `$LowerKebabName`, `$UpperSnakeName`, and `$Name[$CamelCase]`
- Boolean blocks such as $IsNullable[...][...]
- Collection separators
- Basic filters: Class, Record, Interface, Enum, Struct, HasProperties, Public, Internal
- Name= and Namespace= filters
- Old-style [Attribute] filters
- Old-style :BaseType inheritance filters
- Inline lambda filters for common predicates such as Name/Namespace checks, boolean flags, .Any(...), and string StartsWith/EndsWith/Contains/Equals calls
- Compatibility code-block capture for simple bool/string helper methods
- Compiled helper methods returning CodeModel collections can feed template collection blocks
- Compiled predicate helpers can receive both parent and current CodeModel contexts, for example `bool Filter(Record r, Property p)` used by `$Properties($Filter)`
- Recipe-style predicate helpers used by NetCoreTypewriterRecipes model templates, including IncludeClass, IncludeRecord, IncludeEnums, IncludeInterface, and IncludeProperty
- Type helper blocks such as $Type[$Default], $Type[$Name], ClassName(), Default(), BaseClass/BaseRecord, nullable marks, and optional $Name$ closing delimiters
- `$Type[$Default]` resolves enum defaults to the first enum member when available, for example `FirstSet.ValA`, instead of falling back to `0`
- Nullable dictionaries preserve outer nullability even when the value type is nullable, for example `Record<string, string | null> | null`
- Nullable collection element types are parenthesized before array suffixes, for example `(string | null)[]`
- Parent-aware CodeModel contexts for class, method, parameter, property, constant, enum value, attribute, attribute argument, and type-reference helper results
- Original public Typewriter.CodeModel member surface, collection interfaces, and implicit conversions are exposed for compiled C# helpers
- Metadata-side $Parent[...] traversal for methods, parameters, properties, constants, and enum values
- Object-valued scalar substitutions keep their object context for nested blocks such as $Parent[$UrlFieldName]
- CodeModel Type.Name uses a legacy stable type name without nullable TypeScript union syntax; direct $Type rendering still keeps nullable TypeScript output.
- Wildcard name/full-name filters
- Template directives:
  - // typewriter-template: v1
  - // output: relative/path.ts
  - // typewriter-output: relative/path.ts
- Legacy SingleFileMode("...") compatibility block detection for output filename
- Template Settings.OutputFilenameFactory execution for the current rendered CodeModel file
- Per-source-file rendering when Settings.OutputFilenameFactory is configured and no explicit output path is present
- No-match suppression for per-source-file OutputFilenameFactory renders where root template filters match no types
```

Implemented configuration loading:

```text
- typewriter.json
- typewriter.config.json
- .typewriterrc.json
- Workspace-level config
- Project-level config
- Environment overrides
- CLI overrides for framework, dry-run, and fail-on-warning
- JSON Schema (typewriter.schema.json) for typewriter.json validation and editor autocomplete
- typewriter init emits $schema reference as the first property
```

Configuration precedence currently implemented:

```text
CLI arguments
    ↓
environment variables
    ↓
nearest project config
    ↓
workspace config
    ↓
default settings
```

Implemented tests:

```text
- Template directive parsing
- Template rendering
- Attribute and inheritance filters
- Inline lambda filters
- Recipe-style compatibility predicates from NetCoreTypewriterRecipes model templates
- Configuration loading
- Project loading
- Solution workspace project resolution
- Buildalyzer solution property handling for .slnx workspaces
- SimpleApi generation snapshot
- NullableModels generation snapshot
- RecordsAndEnums generation snapshot
- MultiProjectSolution ProjectReference generation snapshot
- WebApiServices service generation snapshot
- WebApiServices constants generation snapshot
- SignalRHubs generation snapshot
- System.CommandLine CLI command parsing
- Engine all-projects fan-out
- CLI integration tests for generate JSON output, all-projects fan-out, project-load failure, and template parse failure
- CLI integration tests for watch startup, cancellation, and source-change regeneration
- Engine test for Settings.OutputFilenameFactory output path planning
- Engine test for Settings.OutputFilenameFactory source-file fan-out
- Engine regression coverage for no-match suppression during source-file fan-out
- Engine parser regression coverage for preserving TypeScript template literal interpolation such as `${environment.apiBaseUrl}`
- Engine regression coverage for preferring dictionary TypeScript mapping over enumerable mapping
- Engine regression coverage for nullable dictionary outer nullability
- Engine regression coverage for nullable collection element parenthesizing
- Engine regression coverage for enum defaults in `$Type[$Default]`
- Engine regression coverage for two-parameter compiled predicate helpers
- Roslyn regression coverage for `[AllowedValues(null, Const.Value1, Const.Value2)]`
- Engine reflection and compile-time coverage for original public Typewriter.CodeModel properties and implicit conversions
- Engine renderer coverage for CodeModel parity members such as doc comments, fields, static readonly fields, events, delegates, type parameters, and nested types
- Engine compatibility probe for the external Angular/System.Text.Json service recipe when `D:\GitHub\NetCoreTypewriterRecipes` is available
- Direct external Angular/System.Text.Json service recipe snapshot when `D:\GitHub\NetCoreTypewriterRecipes` is available
- Direct external React/System.Text.Json service recipe snapshot when `D:\GitHub\NetCoreTypewriterRecipes` is available
- Direct external Angular/System.Text.Json and React/System.Text.Json model recipe snapshots when `D:\GitHub\NetCoreTypewriterRecipes` is available
- Direct external Angular/Newtonsoft.Json and React/Newtonsoft.Json model recipe snapshots when `D:\GitHub\NetCoreTypewriterRecipes` is available
- Direct external Angular sample constants recipe snapshot when `D:\GitHub\NetCoreTypewriterRecipes` is available
- Roslyn metadata extraction, including nullable generic arguments and nullable directive regions
- Language server template diagnostics over open document text
- Language server completion, hover, C# source definition, generated-output definition, semantic-token, and embedded-language context services
- Language server JSON-RPC protocol loop for initialize, document sync, completion, semantic tokens, shutdown, and exit
- UnifiedDiffBuilder unit tests for identical content, new files, deleted files, single-line changes, insertions, deletions, multi-line replacements, CRLF handling, line-ending-only changes, final-newline-only changes, hunk merging, and distant hunk separation
- Generated-file writer regression coverage verifies that `--diff` skips the LCS calculation for unchanged outputs
- Public API compatibility tests preserve the pre-4.6.1 `GeneratedFile` and `GenerationRequest` constructor signatures
- CLI integration tests for --diff output in JSON and text modes, including line-ending-only changes
- CLI integration test for $schema reference as first property in typewriter init output
- JSON Schema drift-detection test cross-checking typewriter.schema.json against serialized TypewriterConfiguration.Default
- Buildalyzer regression tests for old-style non-SDK csproj loading and TW0003 diagnostic surfacing for unresolved unconditional imports
- Exact snapshot coverage for every issue sample: issue66, issue67, issue68, issue69, issue69v2, issue74, issue75, issue81, issue90, and issue96
- Visual Studio generate-on-save scope tests for project-local, workspace-level, and missing templates
- Template discovery test for directory-valued --template
- Roslyn metadata tests for indexer properties and struct types
- Engine render tests for $Structs, $Types(Struct), struct properties, and nested structs
- CodeModel parity tests for Property.IsIndexer, Property.Parameters, and Struct public surface
```

Verified commands:

```bash
dotnet build AdaskoTheBeAsT.Typewriter.slnx --configuration Release --no-restore -m:1
dotnet test AdaskoTheBeAsT.Typewriter.slnx --configuration Release --no-build -m:1
dotnet run --project src/Typewriter.Cli/Typewriter.Cli.csproj --no-build -- generate --workspace AdaskoTheBeAsT.Typewriter.slnx --project samples/SimpleApi/SimpleApi.csproj --template samples/SimpleApi/Models.tst --framework net10.0 --output json --dry-run
npm --prefix vscode run lint
npm --prefix vscode run bundle
.\rider\gradlew.bat -p rider verifyPlugin
```

The full verification on 2026-07-02 passes with 265 tests and a warning-free clean Release build. Nullable and obsolete-API warnings from the pinned Buildalyzer submodule are suppressed only for that vendored project at the repository integration boundary; Typewriter projects retain their normal warnings-as-errors policy.

The Visual Studio focused build produces:

```text
src/Typewriter.VisualStudio/bin/Release/net472/Typewriter.VisualStudio.vsix
```

The CLI sample currently generates:

```text
samples/SimpleApi/generated/models.ts
```

## NetCoreTypewriterRecipes Audit

The `.tst` files under `D:\GitHub\NetCoreTypewriterRecipes` were reviewed as the current compatibility target.

Observed model-template requirements:

```text
- $Classes($IncludeClass), $Records($IncludeRecord), $Enums($IncludeEnums), $Interfaces($IncludeInterface)
- $Properties($IncludeProperty)
- code-block bool/string helpers such as IncludeClass, IncludeRecord, IncludeEnums, IncludeProperty, NullableMark
- $Type[$Default], $Type[$Name], BaseClass/BaseRecord, TypeArguments, ClassName(), Default()
- enum helper methods such as IsEnumAsNumber and GetEnumAsStringIfItsStringable
```

Completed model-template edge cases:

```text
- nullable dictionary output keeps both value nullability and outer dictionary nullability
- nullable list output uses `(T | null)[]` precedence instead of `T | null[]`
- enum defaults render as the first enum member when enum metadata is available
- TypeMetadataReference carries enum values so source-file fan-out can resolve enum defaults
- two-parameter compiled predicate helpers receive parent and child CodeModel objects
- AllowedValuesAttribute with null and constant arguments no longer fails metadata extraction
- shared name-case formatting covers recipe helpers such as Hyphenated(...) and direct `$LowerKebabName` / `$UpperSnakeName` template aliases
```

Observed service/constants-template requirements now partially covered:

```text
- $Methods, $Parameters, and $Constants are available in the renderer.
- Method return types, parameter default values, method/parameter/constant attributes, and parent links are extracted and exposed.
- Static class detection, constant values, and enum value attributes are extracted and exposed.
- Compiled service helpers can override built-in member names such as $ReturnType.
- Compiled helpers can now return CodeModel object collections that are consumed by nested template blocks, for example `$SkipParameters[$name: $Type][, ]`.
- Template Settings.OutputFilenameFactory is executed and can provide the generated output filename for the current render.
- Settings.OutputFilenameFactory can now fan out one render per populated source file when no explicit output path is present.
- Per-source-file fan-out now skips files where filtered root collections such as `$Classes($IncludeClass)` match no items, even if the template contains static header text.
- CodeModel Type.Name no longer leaks nullable TypeScript union syntax into legacy helper logic such as service method name generation.
- `$Parent[...]` works for metadata-backed methods, parameters, properties, constants, and enum values, not only CodeModel-backed helper results.
- WebApi helper coverage now includes HttpMethod(), Url()/Route() route composition, Typewriter-style Type-to-string comparisons, Type.OriginalName, task unwrapping, and typeof(...) response-type attribute formatting.
- SignalR hub template coverage now includes hub class filtering by Hub base type, HubMethodName, infrastructure parameter skipping, stream-like return detection, hub routes, and Type.OriginalName/TypeArguments support.
- End-to-end local snapshots now cover representative WebApi service generation, constants generation, and SignalR hub generation.
- A direct external recipe probe now renders the Angular/System.Text.Json service recipe against AngularWebApiSample-shaped metadata when the sibling `NetCoreTypewriterRecipes` checkout is available.
- Direct external snapshots now render the Angular/System.Text.Json and React/System.Text.Json service recipes from the sibling `NetCoreTypewriterRecipes` checkout against representative WebApi-shaped metadata.
- Direct external snapshots now render Angular/React System.Text.Json model recipes, Angular/React Newtonsoft.Json model recipes, and the Angular sample constants recipe from the sibling `NetCoreTypewriterRecipes` checkout.
- Template runtime compatibility now covers source-like multi-argument attribute values for recipes such as `JsonDerivedType` and named-argument values such as `JsonPolymorphic(TypeDiscriminatorPropertyName = "...")`.
- Metadata-side `$Type[...]` blocks now use the legacy TypeScript-facing type view for `Name`, `ClassName`, `Default`, `OriginalName`, primitive/date/guid/time-span flags, and string literal settings.
- Per-source-file `OutputFilenameFactory` resolution is lazy, so files with records/enums/models but no root filter matches do not fail service recipe generation.
- Template parsing now distinguishes legacy `${ ... }` C# helper blocks from TypeScript template-literal interpolation, so recipe output such as `${environment.apiBaseUrl}` is preserved.
- TypeScript type mapping now preserves nullable dictionary and nullable list precedence for Angular/React model recipes.
- Enum default rendering now uses enum members for recipe constructor defaults when enum values are known.
- Attribute metadata extraction handles the Typewriter issue 55 `AllowedValuesAttribute` shape with `null` plus constant values.
```

## Shared Helper and Public Surface Coverage

This section comes from a direct audit of `.tst` helper methods under `D:\GitHub\NetCoreTypewriterRecipes` and the original public CodeModel surface under `D:\GitHub\Typewriter`.

Already covered or mostly covered:

```text
- Typewriter.Extensions.Types: ClassName(), Default(), and Unwrap() are present.
- Typewriter.Extensions.WebApi: HttpMethod(), Url(), Route(), and RequestData() are present.
- Settings surface used by the recipes is present: IncludeCurrentProject(), IncludeProject(), IncludeReferencedProjects(), IncludeAllProjects(), SingleFileMode(), OutputFilenameFactory, UseStringLiteralCharacter(), DisableStrictNullGeneration(), DisableUtf8BomGeneration().
- CodeModel collections and common item members are present for classes, records, interfaces, enums, delegates, members, attributes, doc comments, type references, and parent links.
- Name-case formatting now covers Hyphenated(string)-style helpers through NameCase.LowerKebabCase and direct template aliases.
```

## Partially Implemented

```text
- Solution workspace loading supports a single resolved project, explicit --project, and explicit --all-projects fan-out.
- Template compatibility covers common collection/scalar/filter patterns, inline lambda filters, model-template predicates from NetCoreTypewriterRecipes, and two-parameter parent/child predicate helpers, but not full old Typewriter compatibility.
- Template `${ ... }` C# code blocks are compiled with Roslyn. User `using` lines, `Template(Settings settings)` constructors, multi-statement helper methods, local `#r` references, local-cache `#r "nuget: PackageId, Version"` references, and missing NuGet package restore through `dotnet restore` are supported.
- The compiled helper runtime exposes the original public `Typewriter.CodeModel` member surface, collection interfaces, and implicit conversions for classes, records, interfaces, enums, delegates, members, attributes, doc comments, type references, settings, logging, and common type/WebApi extensions.
- Roslyn metadata now includes ordinary public/internal methods, parameters, return types, method/parameter attributes, const fields, fields, static readonly fields, events, delegates, doc comments, static type flags, enum member attributes, nested types, tuple elements, and parent identifiers for members.
- The renderer and compiled helper adapter now expose `$Methods`, `$Parameters`, `$Constants`, `$Fields`, `$StaticReadOnlyFields`, `$Events`, `$Delegates`, `$TypeParameters`, `$DocComment`, `$IsStatic`, method return types, parameter defaults, constant values, enum value attributes, and parent-aware CodeModel objects for helper parameters.
- Compiled helpers are resolved before built-in members, so recipe helpers such as `ReturnType(Method m)` can intentionally override template member names.
- CodeModel `Type` and metadata-side `$Type[...]` blocks now carry TypeScript-friendly `Name`, C# `OriginalName`, generic `TypeArguments`, task flags, primitive/date/guid/time-span flags, dynamic flags, Typewriter-style implicit string conversion for old helper comparisons, and template string-literal settings for defaults.
- CodeModel `Type` and metadata-side `$Type[...]` default rendering can resolve enum defaults from reference-local enum values or project metadata.
- Attribute values are formatted for common legacy recipe expectations, including raw single string values, source-like multi-argument values such as `typeof(Foo), "bar"`, and named arguments such as `PropertyName = "displayName"`.
- Record base-class selection ignores compiler-added interface references such as `IEquatable<T>` so records do not render false base-record inheritance.
- Settings.OutputFilenameFactory is compiled and evaluated for the current render, and generated output planning can use the runtime filename when no explicit `// output:` directive or SingleFileMode filename is present.
- Settings.OutputFilenameFactory can drive source-file fan-out, with each populated SourceFileMetadata rendered as the current CodeModel file.
- Source-file fan-out skips no-match renders for filtered root type collections and resolves OutputFilenameFactory lazily, avoiding generated header-only files and factory errors for non-matching source files.
- Object-valued scalar substitutions keep their object context for nested blocks; this supports real recipe patterns such as `$Parent[$UrlFieldName]`.
- CodeModel Type.Name is separated from nullable TypeScript rendering, so direct `$Type` can still render `string | null` while legacy helper code sees `Type.Name == "string"`.
- Representative WebApi and SignalR helper tests cover verb detection, route composition, response type attributes, SignalR hub routes, HubMethodName, stream-like methods, and skipped infrastructure parameters.
- Representative WebApi service, WebApi constants, and SignalR hub templates are covered by checked-in sample snapshots.
- The external Angular/System.Text.Json service recipe has a renderer compatibility probe that executes when the sibling recipe repository is present.
- External Angular/System.Text.Json and React/System.Text.Json service recipes have checked snapshots that execute when the sibling recipe repository is present.
- External Angular/React System.Text.Json model recipes, Angular/React Newtonsoft.Json model recipes, and the Angular sample constants recipe have checked snapshots that execute when the sibling recipe repository is present.
- Recent Angular model compatibility fixes cover nullable dictionaries, nullable lists, and enum default constructor values.
- The Typewriter issue 55 `AllowedValuesAttribute` scenario is covered by Roslyn metadata and renderer helper tests.
- Template parsing preserves TypeScript template literal interpolation while still compiling legacy C# helper blocks.
- Template diagnostics are returned through the shared diagnostic model. C# compile diagnostics are line-mapped back to the original `.tst` file; CLI text output writes compiler-style diagnostics to stderr, while JSON output remains structured.
- Nullable awareness is resolved from Roslyn nullable context at each declaration position, including project Nullable settings plus `#nullable enable`, `#nullable disable`, and `#nullable restore` regions.
- Watch mode is implemented in the CLI; VS Code save-time validate/generate settings and Visual Studio generate-on-save are implemented.
- Language server editor intelligence is implemented for common template members and loaded C# metadata, but the analysis catalog still depends on the currently implemented metadata and compatibility surface.
- Compiled template helpers are loaded into collectible assembly load contexts and unloaded after render/output filename evaluation.
```

## Current Practical Scope

The repository now has a working first vertical slice:

```text
Input:
- one SDK-style .csproj, or a .sln/.slnx workspace plus one resolvable project
- one or more C# source files
- one .tst template

Process:
- CLI loads configuration
- Engine resolves project inputs from --project, .csproj, .sln, or .slnx
- Buildalyzer project loader evaluates MSBuild inputs
- Roslyn extracts metadata
- Engine renders the template
- Engine compiles and invokes C# helpers from template code blocks when present
- CLI writes deterministic generated TypeScript

Output:
- generated .ts file
- shared diagnostics
- compiler-style template diagnostics in text mode and structured diagnostics in JSON mode
- JSON or text CLI result
- repeated generation in watch mode
- VS Code commands, syntax highlighting, and Problems diagnostics over the CLI JSON contract
- Visual Studio VSIX adapter with commands, generate-on-save, Output window logging, and Error List diagnostics over the CLI JSON contract
- Visual Studio native Ctrl+Space fallback completions for `.tst` files
- Language server live diagnostics, completion, hover, definition, semantic highlighting, and basic embedded C#/TypeScript completion support for `.tst` files
```
