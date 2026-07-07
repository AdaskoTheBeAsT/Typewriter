# Typewriter To Implement

Target architecture, roadmap, and remaining backlog. Completed and current-state details are tracked in [implemented.md](implemented.md).

## Goal

Reimplement Typewriter from scratch as a modern, editor-independent TypeScript generator for C# projects.

The new design should not be a Visual Studio-only extension. Instead, it should have a shared generator engine that can be used by:

- a CLI / `dotnet tool`
- a Visual Studio extension
- a VS Code extension
- an optional Language Server Protocol server
- CI pipelines and pre-commit hooks

The main principle:

```text
One generator engine.
Multiple thin adapters.
```

---

## Target Architecture

```text
                         ┌────────────────────────┐
                         │   Typewriter.Engine    │
                         │  Template + Generation │
                         └───────────┬────────────┘
                                     │
              ┌──────────────────────┼──────────────────────┐
              │                      │                      │
┌─────────────▼─────────────┐ ┌──────▼──────┐ ┌─────────────▼─────────────┐
│ Typewriter.VisualStudio   │ │ Typewriter  │ │ Typewriter.LanguageServer │
│ VSIX adapter              │ │ CLI         │ │ LSP for .tst files        │
└───────────────────────────┘ └──────┬──────┘ └─────────────┬─────────────┘
                                     │                      │
                               ┌─────▼──────────────────────▼────-─-─┐
                               │        VS Code extension            │
                               │ TypeScript/Node adapter             │
                               └─────────────────────────────────────┘
```

---

## Core Product Decisions

### 1. The engine must be editor-independent

The generator must not depend on:

- `DTE`
- `EnvDTE`
- `IVsHierarchy`
- `AsyncPackage`
- Visual Studio MEF
- VS Code APIs
- Node.js APIs
- editor save events
- editor output windows

The engine should only know about:

- solutions
- projects
- source files
- templates
- metadata
- generation output
- diagnostics
- configuration

### 2. CLI comes before extensions

The CLI should be the first real product surface.

If the CLI works well, Visual Studio and VS Code become wrappers around it.

The CLI should support:

```bash
typewriter generate --workspace ./MySolution.sln
typewriter generate --project ./src/MyApi/MyApi.csproj
typewriter generate --template ./src/MyApi/Models.tst
typewriter watch --workspace ./MySolution.sln
typewriter validate --workspace ./MySolution.sln
```

### 3. Visual Studio and VS Code are adapters

Visual Studio should provide:

- `.tst` file recognition
- commands
- generate on save
- diagnostics
- output window integration
- optional syntax highlighting / editor features

VS Code should provide:

- `.tst` file association
- syntax highlighting
- commands
- file watchers
- diagnostics in the Problems panel
- optional LSP-powered completion and hover

---

## Proposed Repository Structure

```text
repo/
  src/
    Typewriter.Abstractions/
    Typewriter.Engine/
    Typewriter.Roslyn/
    Typewriter.Buildalyzer/
    Typewriter.Cli/
    Typewriter.LanguageServer/
    Typewriter.VisualStudio/
    Typewriter.VisualStudio.Legacy/

  vscode/
    package.json
    tsconfig.json
    src/
      extension.ts
      typewriterClient.ts
      diagnostics.ts
      watcher.ts
      configuration.ts
    syntaxes/
      tst.tmLanguage.json
    language-configuration.json

  tests/unit/
    Typewriter.Engine.Tests/
    Typewriter.Roslyn.Tests/
    Typewriter.Cli.Tests/
    Typewriter.SnapshotTests/
    Typewriter.LanguageServer.Tests/

  samples/
    SimpleApi/
    NullableModels/
    RecordsAndEnums/
    MultiProjectSolution/
    NetCoreTypewriterRecipes/

  docs/
    architecture.md
    template-language.md
    cli.md
    vscode-extension.md
    visual-studio-extension.md
```

---

## Main Projects

## Typewriter.Abstractions

Contains contracts shared by all other projects.

Example interfaces:

```csharp
public interface ITypewriterGenerator
{
    Task<GenerationResult> GenerateAsync(
        GenerationRequest request,
        CancellationToken cancellationToken);
}

public interface ITemplateDiscovery
{
    Task<IReadOnlyList<TemplateFile>> FindTemplatesAsync(
        WorkspaceContext workspace,
        CancellationToken cancellationToken);
}

public interface IProjectMetadataProvider
{
    Task<ProjectMetadata> GetMetadataAsync(
        ProjectContext project,
        CancellationToken cancellationToken);
}

public interface IGeneratedFileWriter
{
    Task WriteAsync(
        GeneratedFile file,
        CancellationToken cancellationToken);
}

public interface IGenerationReporter
{
    void Info(string message);
    void Warning(GenerationDiagnostic diagnostic);
    void Error(GenerationDiagnostic diagnostic);
}
```

Suggested models:

```csharp
public sealed record GenerationRequest(
    string WorkspacePath,
    string? ProjectPath,
    string? TemplatePath,
    GenerationMode Mode,
    TypewriterConfiguration Configuration);

public sealed record GenerationResult(
    bool Success,
    IReadOnlyList<GeneratedFile> GeneratedFiles,
    IReadOnlyList<GenerationDiagnostic> Diagnostics,
    TimeSpan Duration);

public sealed record GeneratedFile(
    string Path,
    string Content,
    bool Changed);

public sealed record GenerationDiagnostic(
    string? File,
    int? Line,
    int? Column,
    DiagnosticSeverity Severity,
    string Message,
    string? Code = null);
```

---

## Typewriter.Engine

Responsible for:

- template parsing
- template compilation
- template execution
- generation orchestration
- diagnostics
- output file mapping
- incremental generation decisions

Should not contain Roslyn-specific or editor-specific code directly.

Internal flow:

```text
GenerationRequest
    ↓
ConfigurationLoader
    ↓
TemplateDiscovery
    ↓
ProjectMetadataProvider
    ↓
TemplateCompiler
    ↓
TemplateExecutor
    ↓
GeneratedFilePlanner
    ↓
GeneratedFileWriter
    ↓
GenerationResult
```

Important design goal:

```text
Generation should be deterministic:
Same input files + same configuration = same output files.
```

---

## Typewriter.Roslyn

Responsible for reading C# code using Roslyn.

Should provide metadata such as:

- classes
- records
- interfaces
- enums
- properties
- methods
- attributes
- generic type arguments
- nullable annotations
- XML documentation
- inheritance
- implemented interfaces
- namespaces
- accessibility
- constants
- static members

Avoid leaking Roslyn types into the public engine API.

Do not expose:

```csharp
INamedTypeSymbol
IPropertySymbol
ICompilationUnitSyntax
SemanticModel
Compilation
```

Instead expose Typewriter-owned metadata models:

```csharp
public sealed record TypeMetadata(
    string Name,
    string FullName,
    string Namespace,
    TypeKind Kind,
    IReadOnlyList<PropertyMetadata> Properties,
    IReadOnlyList<AttributeMetadata> Attributes,
    IReadOnlyList<TypeMetadataReference> BaseTypes,
    bool IsNullableAware);
```

---

## Typewriter.Buildalyzer

Responsible for loading projects and solutions outside Visual Studio.

Used by:

- CLI
- VS Code
- Language Server
- CI

Responsibilities:

- load `.sln`
- load `.csproj`
- resolve target frameworks
- resolve project references
- create Roslyn workspace/compilation inputs
- handle multi-targeted projects
- report project loading diagnostics

Important scenarios:

```text
- single SDK-style .csproj
- multi-project solution
- project references
- shared projects, if needed
- generated source files
- nullable-enabled projects
- multi-targeting: net8.0;net9.0;net10.0
```

---

## Typewriter.Cli

The CLI is the main automation surface.

Initial commands:

```bash
typewriter generate
typewriter watch
typewriter validate
typewriter list-templates
typewriter list-projects
```

Suggested options:

```bash
--workspace <path-to-sln-or-folder>
--project <path-to-csproj>
--template <path-to-tst>
--configuration <Debug|Release>
--framework <target-framework>
--output json|text
--verbosity quiet|minimal|normal|detailed|diagnostic
--dry-run
--fail-on-warning
--no-restore
```

Example JSON output:

```json
{
  "success": false,
  "durationMs": 1240,
  "generatedFiles": [
    {
      "path": "src/api/generated/user.model.ts",
      "changed": true
    }
  ],
  "diagnostics": [
    {
      "file": "src/api/models.tst",
      "line": 12,
      "column": 5,
      "severity": "error",
      "code": "TW0001",
      "message": "Unknown extension method: ToTypeScriptType"
    }
  ]
}
```

The VS Code extension should use JSON output.

---

## Typewriter.VisualStudio

Visual Studio support should be a thin wrapper around the engine or CLI.

Use the modern SDK-style VSIX project format.

Recommended staged approach:

```text
Stage 1:
- SDK-style VSIX
- classic VSSDK integration
- use Typewriter.Engine internally

Stage 2:
- add VisualStudio.Extensibility hybrid shell
- migrate simple commands first

Stage 3:
- optionally move orchestration out-of-process
- keep VS-only APIs behind a small in-process broker
```

Visual Studio adapter responsibilities:

- discover active solution/project
- expose commands
- react to document save events
- call generator
- update generated files
- report diagnostics
- show output logs
- manage VS-specific file nesting/project inclusion

Visual Studio should not own the generator logic.

---

## VS Code Extension

Location:

```text
vscode/
```

Responsibilities:

- register `.tst` files
- provide syntax highlighting
- expose commands
- watch files
- run CLI or language server
- parse JSON diagnostics
- publish diagnostics to Problems panel
- show output in a Typewriter output channel

Example commands:

```json
{
  "contributes": {
    "commands": [
      {
        "command": "typewriter.generate",
        "title": "Typewriter: Generate Current Template"
      },
      {
        "command": "typewriter.generateAll",
        "title": "Typewriter: Generate All Templates"
      },
      {
        "command": "typewriter.watch",
        "title": "Typewriter: Start Watch Mode"
      },
      {
        "command": "typewriter.restartServer",
        "title": "Typewriter: Restart Language Server"
      }
    ]
  }
}
```

Suggested VS Code settings:

```json
{
  "typewriter.cliPath": "typewriter",
  "typewriter.generateOnSave": true,
  "typewriter.debounceMs": 750,
  "typewriter.workspacePath": null,
  "typewriter.outputFormat": "json",
  "typewriter.trace.server": "off"
}
```

File watchers:

```ts
const csWatcher = vscode.workspace.createFileSystemWatcher("**/*.cs");
const templateWatcher = vscode.workspace.createFileSystemWatcher("**/*.tst");
const projectWatcher = vscode.workspace.createFileSystemWatcher("**/*.{csproj,sln,props,targets}");
```

Debounce all watcher-triggered generation.

---

## Typewriter.LanguageServer

The language server is optional but recommended for rich `.tst` editor support.

Use it for:

- template diagnostics
- autocomplete
- hover
- go to definition
- find references
- code actions
- document symbols
- workspace symbols
- embedded C# helper-block highlighting and IntelliSense
- embedded TypeScript output-region highlighting and IntelliSense

Do not put full generation only in the language server. Generation should remain in the engine and CLI.

Suggested LSP features by milestone:

```text
Milestone 1:
- diagnostics
- document sync

Milestone 2:
- completion for known template members
- completion for known C# metadata

Milestone 3:
- hover documentation
- go to generated output
- go to C# source symbol

Milestone 4:
- code actions
- template refactoring helpers

Post-MVP:
- embedded-language region mapping for `.tst` files
- semantic highlighting for C# helper blocks and TypeScript output regions
- completion, hover, signature help, diagnostics, and go-to-definition inside C# helper blocks
- completion, hover, diagnostics, and symbol navigation inside TypeScript output regions
- virtual-document forwarding to Roslyn and the TypeScript language service instead of reimplementing either language server
```

---

## Template Language Strategy

You have two possible approaches.

## Option A: Preserve Typewriter `.tst` compatibility

Pros:

- easier migration for existing users
- compatible with old recipes
- less disruption

Cons:

- may preserve old design limitations
- harder to clean up syntax
- compatibility layer may be expensive

## Option B: New template language inspired by old Typewriter

Pros:

- clean implementation
- better diagnostics
- easier LSP support
- easier long-term maintenance

Cons:

- breaking change
- users must migrate templates

Recommended approach:

```text
Start with old Typewriter compatibility where practical,
but version the template language explicitly.
```

Example:

```text
// typewriter-template: v1
```

or:

```json
{
  "templateVersion": 1
}
```

Later you can introduce:

```text
// typewriter-template: v2
```

---

## Configuration

Support a configuration file at repo or project level.

Suggested names:

```text
typewriter.json
typewriter.config.json
.typewriterrc.json
```

Example:

```json
{
  "templates": [
    "**/*.tst"
  ],
  "exclude": [
    "**/bin/**",
    "**/obj/**",
    "**/node_modules/**"
  ],
  "generateOnSave": true,
  "defaultTargetFramework": null,
  "output": {
    "newline": "lf",
    "encoding": "utf-8",
    "writeOnlyWhenChanged": true
  },
  "diagnostics": {
    "failOnWarning": false
  }
}
```

Configuration precedence:

```text
CLI arguments
    ↓
environment variables
    ↓
nearest project config
    ↓
repo config
    ↓
default settings
```

---

## Generation Pipeline

```text
1. Resolve workspace
2. Load configuration
3. Discover templates
4. Resolve project context
5. Load C# metadata
6. Compile/evaluate templates
7. Plan generated files
8. Compare with existing generated files
9. Write changed files only
10. Return diagnostics and result summary
```

Generation should support cancellation at each stage.

---

## Watch Mode

Watch mode should be implemented in the CLI, not only in the editors.

```bash
typewriter watch --workspace ./MySolution.sln
```

Watch these files:

```text
*.cs
*.tst
*.csproj
*.sln
Directory.Build.props
Directory.Build.targets
typewriter.json
```

Rules:

```text
- debounce changes
- collapse duplicate requests
- cancel previous generation if a new change arrives
- do not write files if content did not change
- log generated files
- report diagnostics continuously
```

---

## Diagnostics

Diagnostics should be first-class.

Required fields:

```text
- severity
- code
- message
- file
- line
- column
- optional help link
```

Example codes:

```text
TW0001 Unknown template member
TW0002 Template parse error
TW0003 Project load failed
TW0004 C# compilation failed
TW0005 Output path outside workspace
TW0006 Generated file conflict
TW0007 Unsupported target framework
TW0008 Extension method failed
```

All adapters should consume the same diagnostic model.

---

## Safety Rules for Generated Files

The generator should avoid destructive behavior.

Rules:

```text
- never delete files unless explicitly requested
- write only inside workspace by default
- block output paths outside workspace unless configured
- avoid overwriting non-generated files
- add a generated-file header by default
- write only if content changed
- preserve line endings if configured
```

Suggested generated header:

```ts
// <auto-generated />
// Generated by Typewriter. Do not edit manually.
```

---

## Testing Strategy

## Unit Tests

Cover:

```text
- template parsing
- template execution
- type mapping
- enum mapping
- nullable reference types
- generic types
- attributes
- records
- inheritance
- extension methods
- diagnostics
- output path calculation
```

## Snapshot Tests

Use sample C# projects and templates.

Input:

```text
samples/SimpleApi/**/*.cs
samples/SimpleApi/**/*.tst
```

Expected output:

```text
samples/SimpleApi/expected/**/*.ts
```

## CLI Tests

Cover:

```text
- generate solution
- generate project
- generate single template
- dry run
- JSON output
- error exit code
- warning handling
- invalid config
```

## VS Code Tests

Cover:

```text
- extension activates on .tst
- command calls CLI
- diagnostics are published
- generate on save works
- file watcher debounce works
```

## Visual Studio Tests

Cover manually at first:

```text
- install VSIX
- open solution
- save .cs file
- save .tst file
- generate current template
- generate all templates
- diagnostics shown
- output shown
```

---

## Milestone Plan

## Milestone 1: Clean Core Foundation

Deliverables:

```text
- new solution structure
- Typewriter.Abstractions
- Typewriter.Engine skeleton
- Typewriter.Roslyn skeleton
- simple metadata model
- basic template discovery
- first unit tests
```

Acceptance criteria:

```text
- engine can receive a GenerationRequest
- engine returns a GenerationResult
- no editor-specific dependency exists in the engine
```

---

## Milestone 2: Roslyn Metadata

Deliverables:

```text
- load C# files
- extract classes, interfaces, records, enums
- extract properties and types
- support nullable annotations
- support attributes
- tests for metadata extraction
```

Acceptance criteria:

```text
- metadata model is independent from Roslyn types
- sample project metadata can be serialized to JSON
```

---

## Milestone 3: First Template Execution

Deliverables:

```text
- parse minimal .tst template
- execute template against metadata
- generate .ts file
- write changed files only
- produce diagnostics
```

Acceptance criteria:

```text
- sample C# class generates expected TypeScript interface
- snapshot test passes
```

---

## Milestone 4: CLI MVP

Deliverables:

```text
- typewriter generate
- --workspace
- --project
- --template
- --output json
- --dry-run
- proper exit codes
```

Exit codes:

```text
0 success
1 generation failed
2 invalid arguments
3 project loading failed
4 template parsing failed
```

Acceptance criteria:

```text
- CLI can generate from a sample project
- CLI JSON output can be consumed by another process
```

---

## Milestone 5: Watch Mode

Deliverables:

```text
- typewriter watch
- file watchers
- debounce
- cancellation
- continuous diagnostics
```

Acceptance criteria:

```text
- changing a .cs file regenerates affected output
- changing a .tst file regenerates affected output
- repeated quick saves cause one generation run
```

---

## Milestone 6: VS Code MVP

Deliverables:

```text
- VS Code extension project
- .tst file association
- basic syntax highlighting
- Typewriter: Generate command
- Typewriter: Generate All command
- run CLI
- parse JSON diagnostics
- show diagnostics in Problems panel
```

Acceptance criteria:

```text
- opening a workspace with .tst files activates extension
- generate command writes output
- template errors appear in Problems panel
```

---

## Milestone 7: Visual Studio MVP

Deliverables:

```text
- SDK-style VSIX project
- command: Generate Current Template
- command: Generate All Templates
- generate on save
- output window logging
- diagnostics integration
```

Acceptance criteria:

```text
- extension installs in Visual Studio
- generation uses shared engine
- no duplicated generator logic exists in VS project
```

---

## Milestone 8: Language Server MVP

Deliverables:

```text
- LSP server process
- document open/change events
- template diagnostics
- VS Code LSP client integration
```

Acceptance criteria:

```text
- .tst errors appear without running full generation manually
- server can be restarted from VS Code command palette
```

---

## Milestone 9: Rich Template Experience

Deliverables:

```text
- completion for metadata members
- completion for template functions
- hover documentation
- go to C# symbol
- go to generated file
```

Acceptance criteria:

```text
- editing .tst feels close to a real language experience
```

---

## Post-MVP Embedded Language Intelligence

Current status:

```text
- LSP semanticTokens support now provides Typewriter, C# helper-block, and TypeScript output-region semantic highlighting.
- Completion is context-aware: `$...` uses Typewriter template completions, C# helper blocks get basic C#/CodeModel completions, and TypeScript output regions get basic TypeScript/type completions.
- VS Code and Visual Studio both have native fallback Ctrl+Space completions for `.tst` files when the language server is unavailable or still starting.
- Remaining work is full embedded-language service forwarding for deeper C# and TypeScript IntelliSense.
```

Goal:

```text
Make `.tst` files behave like mixed Typewriter/C#/TypeScript documents without moving generation logic into the editor layer.
```

Deliverables:

```text
- Parse `.tst` files into template, C# helper, and TypeScript output regions.
- Expose virtual C# documents for `${ ... }` compatibility/helper blocks.
- Expose virtual TypeScript documents for generated output text regions.
- Forward embedded C# completion, hover, signature help, semantic tokens, diagnostics, and definitions to Roslyn language services.
- Forward embedded TypeScript completion, hover, semantic tokens, diagnostics, and definitions to the TypeScript language service.
- Translate positions and ranges between the original `.tst` document and each virtual embedded document.
- Keep Typewriter template completions active for `$...` expressions while embedded language services handle ordinary C# and TypeScript syntax.
```

Acceptance criteria:

```text
- C# code inside helper blocks has real C# highlighting, completion, hover, signature help, diagnostics, and go-to-definition.
- TypeScript output regions have TypeScript highlighting, completion, hover, diagnostics, and symbol navigation where source mapping is possible.
- Template member IntelliSense still works for `$Classes`, `$Properties`, `$Type`, helper calls, and metadata symbols.
- Embedded diagnostics point back to the correct `.tst` line and column.
- The implementation uses virtual documents and request forwarding, not a custom C# or TypeScript parser.
```

Implementation notes:

```text
- Reuse and harden the current embedded-region parser as virtual-document forwarding is added.
- Extend the current LSP semanticTokens support as the embedded region model grows.
- Add virtual-document providers in the VS Code extension for C# and TypeScript embedded regions.
- Prefer Roslyn LSP or C# Dev Kit/OmniSharp interop for C# requests when available.
- Prefer VS Code's built-in TypeScript language features for TypeScript virtual documents.
- If a host cannot provide embedded-language forwarding, keep graceful fallback to current template-only IntelliSense.
```

---

## Milestone 10: Packaging and Release

Deliverables:

```text
- NuGet package for engine abstractions if useful
- dotnet tool package for CLI
- VSIX package
- VS Code extension package
- GitHub Actions pipeline
- release notes
- docs site or README docs
```

Acceptance criteria:

```text
- CLI can be installed as dotnet tool
- VSIX can be installed manually
- VS Code extension can be packaged as .vsix
- samples are documented
```

---

## Suggested Development Order

```text
1. Create new repository/solution
2. Define abstractions and result/diagnostic models
3. Build metadata extraction with Roslyn
4. Build minimal template execution
5. Add snapshot tests
6. Add CLI generate command
7. Add JSON output
8. Add watch mode
9. Add VS Code extension MVP
10. Add Visual Studio extension MVP
11. Add LSP server
12. Add advanced template support
13. Add packaging and CI
```

---

## Recommended PR Sequence

```text
PR 01: Initial solution structure
PR 02: Abstractions and diagnostics model
PR 03: Roslyn metadata extraction
PR 04: Template discovery and parsing skeleton
PR 05: First generation pipeline
PR 06: Snapshot test infrastructure
PR 07: CLI generate command
PR 08: CLI JSON output and exit codes
PR 09: Project/solution loading through Buildalyzer
PR 10: Watch mode
PR 11: VS Code extension skeleton
PR 12: VS Code commands and CLI bridge
PR 13: VS Code diagnostics
PR 14: Visual Studio SDK-style VSIX skeleton
PR 15: Visual Studio commands and engine bridge
PR 16: Visual Studio generate-on-save
PR 17: Language server skeleton
PR 18: Language server diagnostics
PR 19: Completion and hover
PR 20: Packaging and release workflow
```

---

## CI Pipeline

Suggested GitHub Actions jobs:

```text
build-dotnet
- restore
- build
- test
- pack CLI

build-vscode
- npm ci
- npm run lint
- npm run test
- npm run package

build-visualstudio
- restore
- build VSIX
- collect artifact

snapshot-tests
- run generation against samples
- compare expected output

release
- publish NuGet dotnet tool
- publish VSIX artifact
- publish VS Code VSIX artifact
```

---

## Important Risks

| Risk                                                | Mitigation                                               |
| --------------------------------------------------- | -------------------------------------------------------- |
| Old `.tst` compatibility becomes too expensive      | Version the template language and provide migration docs |
| Project loading outside Visual Studio is unreliable | Use Buildalyzer and test many sample project shapes      |
| VS Code extension becomes slow                      | Keep heavy work in CLI/server process                    |
| Generated files overwrite user files                | Require generated-file header and safe write rules       |
| Visual Studio APIs force in-process code            | Keep VS-specific logic in thin adapter only              |
| LSP becomes too big too early                       | Ship CLI and basic VS Code integration first             |

---

## First Minimal Vertical Slice

Build this before anything fancy:

```text
Input:
- one .csproj
- one C# class
- one .tst template

Process:
- CLI loads project
- Roslyn extracts class metadata
- engine executes template
- CLI writes .ts file

Output:
- deterministic generated TypeScript
- JSON result
- snapshot test
```

Example target command:

```bash
typewriter generate \
  --project ./samples/SimpleApi/SimpleApi.csproj \
  --template ./samples/SimpleApi/Models.tst \
  --output json
```

Only after this works should you build editor integration.

---

## Definition of Done for the Rewrite

The rewrite is successful when:

```text
- Typewriter can run without Visual Studio
- Typewriter can generate from CLI
- Typewriter can run in CI
- VS Code can generate using the same engine
- Visual Studio can generate using the same engine
- diagnostics are shared across all adapters
- generated output is deterministic
- snapshot tests protect compatibility
- no generator logic is duplicated between editors
```

---

## Long-Term Opportunities

Once the core is editor-independent, future support becomes much easier:

```text
- Rider plugin
- GitHub Action
- pre-commit hook
- MSBuild integration
- NuGet package for generation during build
- watch daemon
- web-based template playground
- template formatter
- template analyzer
- migration tool from old Typewriter templates
```

---

## Final Recommendation

Reimplement Typewriter as:

```text
Typewriter.Engine       = core product
Typewriter.Cli          = first-class automation interface
Typewriter.VisualStudio = Visual Studio adapter
VS Code extension       = VS Code adapter
Language Server         = optional rich editor experience
```

Do not start with the Visual Studio extension.
Do not start with the VS Code extension.
Do not duplicate the generator in TypeScript.

Start with the engine and CLI.
Everything else should wrap them.

---

## NetCoreTypewriterRecipes Open Audit Items

Observed service/constants-template requirements still needing recipe-backed implementation:

```text
- Additional service template variants from `D:\GitHub\NetCoreTypewriterRecipes`, especially recipes that depend on request/response helper edge cases not represented by the current WebApi sample.
- More complete legacy parser/runtime collection behavior where real recipes depend on old Typewriter edge cases.
- Request body/response helper coverage beyond the representative WebApi route/verb/response tests.
```

## Additional Helper and Public Surface Backlog

This section comes from a direct audit of `.tst` helper methods under `D:\GitHub\NetCoreTypewriterRecipes` and the original public CodeModel surface under `D:\GitHub\Typewriter`.

Recipe helper candidates to move into shared extensions:

```text
- Naming helpers:
  - ServiceName(Class), ConstName(Class), ParentServiceName(Method), ParentConstName(Method)
  - UrlFieldName(Class), UrlFieldName2(Method)
  - MethodName(Method), including CustomName* attributes and non-CancellationToken parameter type suffixes
  - HookNameQuery(Method) and HookNameMutation(Method) for React query/mutation naming

- Web API action helpers:
  - GetActionNameByAttribute(Method, string)
  - HttpGetActionNameByAttribute(Method), HttpPostActionNameByAttribute(Method), HttpPutActionNameByAttribute(Method), HttpDeleteActionNameByAttribute(Method)
  - IsGetMethod(Method), IsPostMethod(Method), IsPostMethodWithResult(Method), IsPutMethod(Method), IsPutMethodWithResult(Method), IsDeleteMethod(Method), IsGetOrDeleteMethod(Class)
  - GetRouteValue(Class), especially controller placeholder replacement and class-level Route fallback behavior

- Request/parameter helpers:
  - GetParameterValue(Parameter)
  - SkipParameters(Method), SkipParametersForBody(Method)
  - ContainsSkipParameters(Method), NotContainsSkipParameters(Method), ContainsOneSkipParameters(Method), ContainsMoreSkipParameters(Method)
  - IsPrimitive(Parameter), IsDictionary(Parameter), IsDictionary(string), NameOfType(Type)

- Return type and import helpers:
  - ReturnType(Method), especially ProducesResponseType parsing and Task/ActionResult/IActionResult handling
  - SkipReturnType(string)
  - Imports(Class), including model import discovery from method return types, ProducesResponseType attributes, parameters, dictionaries, and enums
  - A small shared shape equivalent to AdvancedTypeInfo / NullParam if service import and parameter helpers need structured return data

- Serializer/model helpers:
  - GetPropertyName(Property), including JsonPropertyName, JsonProperty(PropertyName = "..."), and nameof(...) parsing
  - GetDiscriminator(IAttributeCollection)
  - GenerateTypeForInterfaceByClass(Class), GenerateTypeForInterfaceByRecord(Record), GenerateTypeForClass(Class), GenerateTypeForRecord(Record)
  - GenerateTypeForClass2(Class), GenerateTypeForRecord2(Record), GenerateTypeInitForClass2(Class), GenerateTypeInitForRecord2(Record) for JsonDerivedType / JsonPolymorphic inheritance chains
  - SanitizeDefault(Type), Sep(Enum), Sep(EnumValue), Sep(Constant), LoudName(Constant)

- Enum helpers:
  - IsEnumAsNumber(Enum)
  - GetAttributeValueOrReturnEnumNameIfNoAttribute(EnumValue)
  - GetEnumAsStringIfItsStringable(EnumValue)
```

Original Typewriter public surface candidates to copy or verify:

```text
- Typewriter.CodeModel.Helpers public static class:
  - CamelCase(string)
  - GetTypeScriptName(ITypeMetadata, Settings)
  - GetOriginalName(ITypeMetadata)
  - IsPrimitive(ITypeMetadata)
- Typewriter.CodeModel.Attributes.ContextAttribute as a compatibility stub for external code that reflects CodeModel context metadata.
- Original acronym-aware CamelCase behavior. Current `$name` compatibility is lower-first; before changing it, add acronym snapshot coverage for names like URLValue, IPAddress, and XMLDocument.
- Keep verifying original CodeModel interfaces whenever new public members are copied. The current collection interfaces are already close to original IReadOnlyList<T> + IFilterable behavior.
```

Recommended implementation order:

```text
1. Add public Typewriter.CodeModel.Helpers facade backed by current NameCaseFormatter and TypeScriptTypeMapper-compatible logic.
2. Add a WebApi/service helper extension set for action names, method names, parameter skipping/body selection, and return-type extraction.
3. Add serializer/model helper extensions for JSON property names, discriminator parsing, and JsonDerivedType/JsonPolymorphic generation.
4. Add recipe-backed snapshots for each helper family before replacing repeated helper code in external recipe templates.
5. Add ContextAttribute only as a low-risk compile/reflection compatibility shim unless a real template or package needs more metadata.
```

## Not Implemented Yet

```text
- old Typewriter parser/runtime edge cases not yet covered by real recipe snapshots
- additional external service recipe variants and request/response helper edge cases not yet represented by current samples
- exact original Typewriter output comparison where original output can be captured
- shared WebApi/service helper extensions for repeated NetCoreTypewriterRecipes helpers such as MethodName, ReturnType, Imports, SkipParameters, and GetPropertyName
- original Typewriter.CodeModel.Helpers public facade and optional ContextAttribute compatibility shim
- template NuGet restore hardening for private-source authentication failures, floating/range version diagnostics, and incompatible asset edge cases
- full embedded C# and TypeScript IntelliSense inside `.tst` files through virtual documents and language-service forwarding
- VS Code package publishing
- Visual Studio VSIX signing/publishing
- JetBrains Marketplace publishing
- automated synchronization of repository and package changelogs
- migration tooling for old templates
```

## Next Best Development Steps

```text
1. Finish Milestone 10 publishing work.
   - add VS Code marketplace packaging/publishing.
   - add Visual Studio VSIX signing/publishing.
   - add JetBrains Marketplace publishing.
   - automate synchronization of repository and package changelogs.

2. Continue compatibility hardening in parallel as new real recipes expose gaps.
   - Add direct snapshots for additional service template variants under `D:\GitHub\NetCoreTypewriterRecipes`.
   - Tighten remaining old parser/runtime behaviors where direct recipe snapshots expose gaps.
   - Keep diagnostics/completions/hover aligned with the renderer's actual supported members.

3. Extend embedded-language IntelliSense beyond the current basic semantic-token and keyword/type completion support.
   - Map `.tst` regions to virtual C# and TypeScript documents.
   - Forward embedded-language requests to Roslyn and TypeScript services.
   - Translate diagnostics, semantic tokens, hovers, completions, and definitions back to `.tst` positions.
```
