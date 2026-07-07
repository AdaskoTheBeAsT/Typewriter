The main remaining gap is full old Typewriter runtime compatibility, not the new engine/editor milestones.

## Status snapshot as of 2026-07-02

Recently completed:

- [x] Nullable dictionary mapping keeps outer nullability even when the dictionary value type is nullable, for example `Record<string, string | null> | null`.
- [x] Nullable list element mapping is parenthesized correctly, for example `(string | null)[]`.
- [x] Enum defaults use the first enum member when metadata is available, for example `FirstSet.ValA`, instead of falling back to `0`.
- [x] Enum value metadata is carried on `TypeMetadataReference`, so per-source rendering and legacy `$Type[$Default]` paths can resolve enum defaults without relying on a global lookup.
- [x] Attribute constructor arguments with `null` and constant values, including `[AllowedValues(null, Const.Value1, Const.Value2)]`, are extracted without the old Typewriter string-slicing failure from issue 55.
- [x] Compiled predicate helpers can receive both parent and current contexts, for example `bool Filter(Record r, Property p)` for `$Properties($Filter)`.

Still left:

- [ ] Full old Typewriter parser/runtime parity for edge cases not yet covered by real recipe snapshots.
- [ ] Additional `NetCoreTypewriterRecipes` service variants and request/response edge cases.
- [ ] More exact comparison against original Typewriter output where original output can be captured.
- [ ] Template reference hardening around restore diagnostics, incompatible assets, floating/range versions, and long-running cache/unload stress.
- [ ] Direct VS Code, Visual Studio, and JetBrains Marketplace publishing/signing.

Latest focused validation:

```text
dotnet test tests\unit\Typewriter.Engine.Tests\Typewriter.Engine.Tests.csproj -m:1
dotnet test tests\unit\Typewriter.Roslyn.Tests\Typewriter.Roslyn.Tests.csproj -m:1
```

## Compatibility target

The current practical target is old `.tst` templates used by real projects, especially the checked sibling `NetCoreTypewriterRecipes` repository when it is available at:

```text
D:\GitHub\NetCoreTypewriterRecipes
```

Compatibility should be judged in this order:

1. Existing checked-in sample snapshots.
2. External recipe snapshots from `NetCoreTypewriterRecipes`.
3. Real project templates that are not covered by those recipes.
4. Exact original Typewriter output, when original output can be captured.

## What is now covered

### Typewriter.CodeModel public surface parity

The original public `Typewriter.CodeModel` member surface is implemented for compiled template helpers and direct template rendering:

- classes, records, interfaces, enums, delegates
- properties, methods, parameters, constants
- fields, events, static readonly fields
- attributes and attribute arguments
- XML doc comments and parameter comments
- nested classes/interfaces/enums, type parameters, type arguments
- tuple/value tuple metadata, file locations, assembly/source metadata
- original implicit string conversions and class/record/interface/enum to `Type` conversions
- legacy direct collection rendering for string-convertible collections such as `$TypeParameters`

### Template language and helper execution

The renderer covers the common old `.tst` patterns currently exercised by samples and recipe snapshots:

- root collections such as `$Classes`, `$Records`, `$Interfaces`, `$Enums`, and `$Delegates`
- member-level collections such as `$Properties`, `$Parameters`, `$Values`/`$EnumValues`, `$Fields`, `$StaticReadOnlyFields`, `$Methods`, `$Constants`, and `$Events`
- scalar substitutions such as `$Name`, `$name`, `$FullName`, `$Namespace`, `$Type`, `$ReturnType`, and nullable/static flags
- collection separators and direct collection rendering for common legacy cases
- common filters, including `Class`, `Record`, `Interface`, `Enum`, `HasProperties`, `Public`, `Internal`, `Name=`, `Namespace=`, `[Attribute]`, base-type filters, and wildcard name/full-name filters
- inline lambda filters for common name/namespace/boolean/string predicates
- `$Type[...]` blocks, `$Parent[...]` traversal, and parent-aware CodeModel contexts
- compiled C# helper methods from `${ ... }` blocks, including simple bool/string helpers, helpers returning CodeModel collections, and two-parameter predicate helpers such as `bool Filter(Record r, Property p)`
- TypeScript type/default compatibility for nullable dictionaries, nullable list element types, and enum defaults in legacy `$Type[...]` rendering
- template `Settings.OutputFilenameFactory`, including source-file fan-out and no-match suppression
- legacy `SingleFileMode("...")` output filename detection
- TypeScript template literal preservation, such as `${environment.apiBaseUrl}`

### Metadata extraction

Roslyn metadata currently covers the compatibility surface needed by the samples and recipe snapshots:

- classes, records, interfaces, enums, delegates
- properties, methods, parameters, return types, constants, fields, static readonly fields, and events
- attributes on types, members, parameters, enum values, and attribute arguments
- attribute argument values containing `null`, constants, `typeof(...)`, and named arguments
- const values, parameter defaults, enum values, and static type flags
- enum values on type references for default-value rendering during source-file fan-out
- nullable reference/value types, nullable generic arguments, arrays, collections, dictionaries, and nullable directive regions
- XML doc comments and parameter comments
- base types, implemented interfaces, nested types, type parameters, type arguments, tuple/value tuple metadata
- accessibility, assembly/source metadata, source locations, and source-file grouping

### Recipe-backed coverage

Checked-in snapshots and conditional external recipe snapshots cover representative compatibility paths:

- simple model generation
- nullable model generation
- records and enums
- multi-project/project-reference generation
- Web API service generation
- constants generation
- SignalR hub generation
- Angular and React model recipes for `System.Text.Json`
- Angular and React model recipes for `Newtonsoft.Json`
- Angular and React service recipes for `System.Text.Json`
- Angular sample constants recipe
- Angular model edge cases for nullable dictionaries, nullable lists, and enum defaults from the `AngularWebApiSample` shape
- the `AllowedValuesAttribute` crash scenario from Typewriter issue 55

### Template references and helper isolation

Compiled C# template helpers now support the compatibility pieces needed for long-running CLI/editor use:

- local `#r "path/to/assembly.dll"` references
- `#r "nuget: PackageId, Version"` references from the local NuGet cache
- restore/download for missing NuGet packages through `dotnet restore`
- nearest `NuGet.config` discovery from the template directory upward, so private/local package sources can be used
- compile-asset resolution from restored `project.assets.json`, including package dependencies selected by NuGet
- generated helper assemblies loaded into collectible `AssemblyLoadContext` instances
- helper assembly unload after render/output filename evaluation

## What is still left

### More exact `.tst` parser/runtime behavior

We support common filters, lambdas, compiled helpers, `$Type[...]`, `$Parent[...]`, direct CodeModel blocks, and the currently snapshot-tested recipe patterns, but not every old parser/rendering edge case:

- exact whitespace trimming
- all legacy collection separator quirks
- all filter combinations
- more complex helper/lambda expressions
- old quirks that real recipes may depend on

Next useful work:

- add focused renderer tests for each old parser/runtime quirk as it is discovered
- prefer real recipe fixtures over synthetic behavior guesses
- compare generated output against original Typewriter output where possible

### Template reference hardening

NuGet restore and isolated helper loading are implemented, but this area can still use migration hardening:

- richer diagnostics for restore source/authentication failures
- deeper tests for missing package, missing version, incompatible assets, floating versions, and version ranges
- target framework selection beyond the current engine-oriented restore target
- safe compiled-template caching that still preserves unloadability
- longer stress tests that repeated watch/editor renders do not grow memory over time

### More real recipe coverage

Current snapshots cover representative `NetCoreTypewriterRecipes` model/service/constants paths, but still need:

- additional service variants
- deeper request body/response helper cases
- more route, verb, query, body, cancellation-token, and response-type combinations
- recipes that use custom helper return types not represented by current samples
- more old recipes from real projects
- comparison against output from original Typewriter where possible

### Original Visual Studio behavior

New VS/VS Code/LSP support exists, but original Visual Studio extension behavior may still differ:

- project item add/update behavior
- generated file nesting behavior
- encoding/BOM behavior
- settings/options parity
- generated file tracking behavior
- save-trigger ordering and debounce behavior

## Other compatibility work worth tracking

These are not pure template-runtime gaps, but they affect migration confidence:

- VS Code package publishing
- Visual Studio VSIX signing/publishing
- JetBrains Marketplace publishing
- automated synchronization of repository and package changelogs
- migration tooling or diagnostics for old templates
- a documented compatibility matrix for old Typewriter features, recipe coverage, and known deviations
