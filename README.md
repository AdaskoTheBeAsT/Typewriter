# Typewriter

![Typewriter](assets/source/typewriter-logo-1600x500.png)

## Generate TypeScript from C#, Everywhere


[![CI](https://img.shields.io/github/actions/workflow/status/AdaskoTheBeAsT/Typewriter/ci.yml?branch=master&label=CI&logo=github)](https://github.com/AdaskoTheBeAsT/Typewriter/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](global.json)
[![C#](https://img.shields.io/badge/C%23-14.0-239120?logo=csharp)](Directory.Build.props)
[![Platforms](https://img.shields.io/badge/platforms-CLI%20%C2%B7%20VS%20Code%20%C2%B7%20VS%202026%20%C2%B7%20Rider%20%C2%B7%20LSP-blue)](#-editor-integrations)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](#-contributing)

> **Keep your frontend and backend in perfect sync.** Typewriter turns your C# models, Web API controllers, and SignalR hubs into fully-typed TypeScript — automatically, on every change.
>
> This repository is a **ground-up reimplementation** of the classic [Typewriter](https://github.com/frhagn/Typewriter) Visual Studio extension as a **cross-platform toolchain**: a standalone CLI with watch mode, a Language Server, a modern Visual Studio 2026 extension, a VS Code extension, and a JetBrains Rider plugin — all sharing one Roslyn-powered engine.

---

## 📑 Table of Contents

- [Typewriter](#typewriter)
  - [Generate TypeScript from C#, Everywhere](#generate-typescript-from-c-everywhere)
  - [📑 Table of Contents](#-table-of-contents)
  - [✨ Why Typewriter?](#-why-typewriter)
  - [🚀 Why This Version Beats the Classic VS Extension](#-why-this-version-beats-the-classic-vs-extension)
    - [New goodies at a glance](#new-goodies-at-a-glance)
  - [🧠 Template IntelliSense: What Changed and Where It Works](#-template-intellisense-what-changed-and-where-it-works)
    - [IDE support matrix](#ide-support-matrix)
  - [📦 What Is in the Box](#-what-is-in-the-box)
  - [🚦 Quick Start (5 Minutes to Generated Code)](#-quick-start-5-minutes-to-generated-code)
    - [1️⃣ Choose your IDE plugin or dotnet tool](#1️⃣-choose-your-ide-plugin-or-dotnet-tool)
    - [2️⃣ Initialize your workspace](#2️⃣-initialize-your-workspace)
    - [3️⃣ Write your first template](#3️⃣-write-your-first-template)
    - [4️⃣ Add a C# model](#4️⃣-add-a-c-model)
    - [5️⃣ Generate ✨](#5️⃣-generate-)
  - [🛠 CLI Reference](#-cli-reference)
    - [Commands](#commands)
    - [Options](#options)
    - [Exit codes](#exit-codes)
    - [Diagnostic codes](#diagnostic-codes)
  - [⚙️ Configuration: `typewriter.json`](#️-configuration-typewriterjson)
    - [Top-level settings](#top-level-settings)
    - [`output` settings](#output-settings)
    - [Date and time mapping](#date-and-time-mapping)
    - [System and NodaTime profile matrix](#system-and-nodatime-profile-matrix)
    - [Guid mapping](#guid-mapping)
    - [Numeric and decimal mapping](#numeric-and-decimal-mapping)
    - [`diagnostics` settings](#diagnostics-settings)
    - [🔍 Discovery and precedence](#-discovery-and-precedence)
    - [🌱 Environment variables](#-environment-variables)
  - [📝 Template Authoring](#-template-authoring)
    - [Core syntax](#core-syntax)
    - [Struct and indexer templates](#struct-and-indexer-templates)
    - [Sharing logic between templates: `#load` vs `#r`](#sharing-logic-between-templates-load-vs-r)
    - [Compiled C# helpers](#compiled-c-helpers)
    - [Shared helpers and render completion hooks](#shared-helpers-and-render-completion-hooks)
    - [Custom output paths](#custom-output-paths)
    - [Web API URL helpers](#web-api-url-helpers)
  - [🎛 Template Settings API](#-template-settings-api)
  - [🧩 Editor Integrations](#-editor-integrations)
    - [Installing editor packages](#installing-editor-packages)
    - [💜 VS Code](#-vs-code)
    - [🟣 Visual Studio 2026](#-visual-studio-2026)
    - [🧠 JetBrains Rider](#-jetbrains-rider)
    - [🌐 Language Server (any LSP client)](#-language-server-any-lsp-client)
  - [🧪 Samples](#-samples)
  - [🔄 Migrating from the Original Typewriter](#-migrating-from-the-original-typewriter)
  - [🏗️ Architecture](#️-architecture)
  - [🔨 Building from Source](#-building-from-source)
  - [🗺 Project Status and Roadmap](#-project-status-and-roadmap)
  - [Changelog](#changelog)
    - [4.9.0](#490)
    - [4.8.0](#480)
    - [4.7.0](#470)
    - [4.6.1](#461)
    - [4.6.0](#460)
    - [4.5.4](#454)
    - [4.5.3](#453)
    - [4.5.2](#452)
    - [4.5.0](#450)
    - [4.4.0](#440)
    - [4.3.0](#430)
    - [4.2.0](#420)
    - [4.1.1](#411)
    - [4.1.0](#410)
    - [4.0.0](#400)
    - [4.0.0-beta.1](#400-beta1)
  - [🤝 Contributing](#-contributing)
  - [🐛 Issues and Support](#-issues-and-support)
  - [🙏 Acknowledgments](#-acknowledgments)

---

## ✨ Why Typewriter?

- 🔄 **Auto-sync** — regenerate TypeScript the moment C# files or templates change (`typewriter watch`)
- 🧠 **Roslyn-powered** — real compiler metadata: records, nullable reference types, generics, tuples, attributes, XML docs
- 🖥️ **Editor-independent** — works headless in CI, in Visual Studio 2026, in VS Code, in JetBrains Rider, or via any LSP client
- 🎯 **Type safety end to end** — eliminate hand-written DTO drift between backend and frontend
- 🔌 **Powerful `.tst` templates** — filters, lambdas, shared local/remote helpers, and compiled C# helper methods with `#r` NuGet references
- 🧩 **Legacy compatible** — runs the original Typewriter template dialect, validated against real-world recipes
- 📅 **Configurable temporal types** — map `DateTime`, `DateOnly`, `TimeOnly`, NodaTime types, `Guid`, and `decimal` to the TypeScript/runtime types your frontend actually uses
- 🚦 **CI-friendly** — JSON output, dry-run, deterministic exit codes, `--fail-on-warning`
- ⚡ **Fast feedback** — live diagnostics, completion, hover, and semantic highlighting for `.tst` files

---

## 🚀 Why This Version Beats the Classic VS Extension

The previous [`AdaskoTheBeAsT/Typewriter`](https://github.com/AdaskoTheBeAsT/Typewriter) fork is a mature Visual Studio extension. This repository keeps the same `.tst` template spirit, then turns Typewriter into a modern, scriptable, multi-editor toolchain.

| Area              | Classic Typewriter fork                                                                | This version                                                                                                                                                                    |
| ----------------- | -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Runtime model     | Visual Studio extension centered around VS events, DTE/COM, and VS project integration | Editor-independent engine plus thin adapters for CLI, LSP, VS Code, Visual Studio, and Rider                                                                                    |
| Automation        | Best inside Visual Studio, difficult to run consistently in headless builds            | `typewriter generate`, `validate`, `watch`, and `list-templates` work locally and in CI                                                                                         |
| Configuration     | Mostly per-template `Settings` and Visual Studio options                               | Repository-local `typewriter.json` with discovery, formatting, encoding, nullability, naming, dry-run, and warning policy                                                       |
| CI behavior       | No first-class JSON contract or deterministic command exit model                       | JSON/text output, dry-run, deterministic exit codes, and `--fail-on-warning`                                                                                                    |
| Editor experience | Visual Studio editor support                                                           | Language Server diagnostics, completion, hover, go-to-definition, and semantic tokens, reused by VS Code and any LSP client                                                     |
| IDE coverage      | Visual Studio 2022 focused                                                             | CLI, VS Code, Visual Studio 2026, JetBrains Rider, and any LSP-capable editor                                                                                                   |
| Project loading   | Visual Studio-oriented solution/project context                                        | Buildalyzer and Roslyn loading for solutions, projects, folders, target frameworks, references, and multi-project workspaces                                                    |
| File safety       | Generated file behavior depends on the VS extension workflow                           | Planned writes block paths outside the workspace, refuse non-Typewriter overwrites, skip unchanged files, and warn on duplicate outputs                                         |
| Template runtime  | Classic helpers and settings                                                           | Classic helpers plus `#load`, NuGet `#r`, `settings.Log` diagnostics, `Template(Settings, File)`, `OnRenderComplete`, parent/current helper parameters, and richer stack traces |
| Type mapping      | Mostly template-authored conventions                                                   | Configurable date-time, date-only, time-only, NodaTime, `Guid`, and `decimal` mappings with matching default initializers                                                       |
| Metadata depth    | Classic type/member surface                                                            | Structs, indexers, member initializer `$Value`, preserved XML doc inline elements, and JSDoc formatting helpers                                                                 |
| Performance       | Event-driven generation per IDE workflow                                               | Persistent generation, scoped discovery, metadata indexes, cached templates, glob matchers, type mappings, and loaded dependencies                                              |
| Packaging         | VSIX releases                                                                          | NuGet tool packages, language-server package, VSIX, VS Code VSIX, and Rider plugin artifacts from CI                                                                            |

### New goodies at a glance

- **Run it anywhere**: generate from a terminal, build server, VS Code, Visual Studio, Rider, or another LSP editor.
- **Watch mode without IDE magic**: `typewriter watch` regenerates when configured input file extensions change.
- **Safer generated output**: `TW0005` blocks escaping the workspace, `TW0006` protects hand-written files, and `TW0008` exposes duplicate output paths.
- **Better large-template ergonomics**: `Template(Settings settings, File file)` lets templates count and pre-filter types before rendering, while `OnRenderComplete(File file)` supports final summary logging.
- **Shared helper files**: `#load "shared-helpers.cs"` or `#load "https://raw.githubusercontent.com/..."` keeps large templates maintainable and reusable.
- **Configurable output style**: line endings, UTF-8 BOM, indentation, trailing whitespace, final newline, quote style, strict null generation, and file naming are all configuration-driven.
- **Better diagnostics**: template logs surface as `TW0007`, helper failures include template method/line details, and editors show diagnostics live.
- **Post-4.0 metadata upgrades**: structs, indexers, property/field initializer `$Value`, cyclic-reference handling, transitive project references, source generators, and full .NET Framework projects are covered.
- **Post-4.0 documentation upgrades**: XML doc-comment inline tags are preserved, and `Typewriter.Extensions.Documentation` can convert raw XML docs to TypeScript JSDoc.
- **Post-4.0 performance upgrades**: generation reuses persistent processes and caches parsed templates, metadata indexes, glob matchers, file contents, type mappings, and loaded dependencies.
- **Post-4.0 type mapping upgrades**: configurable `DateTime`/`DateOnly`/`TimeOnly`, NodaTime, `Guid`, and `decimal` mappings, with default initializers and date-interceptor guidance for runtime string conversion.
- **Post-4.0 reliability fixes**: primitive collection imports, closed generic arguments, dictionary key types, duplicate assembly identities, Buildalyzer diagnostics, and editor generate-on-save edge cases have regression coverage.
- **Modern C# metadata**: Roslyn metadata covers records, nullable reference types, generics, tuples, doc comments, attributes, static constants, static readonly fields, events, delegates, nested types, and Web API helpers.
- **Web API hardening**: route helpers ignore named-only HTTP attribute arguments, honor `Template = "..."`, and safely encode nullable string/date query values.
- **Recipe-backed compatibility**: real Angular/React recipes from `NetCoreTypewriterRecipes` are snapshot-tested so old-template support improves against production templates, not toy examples.

---

## 🧠 Template IntelliSense: What Changed and Where It Works

The classic VS extension offered basic completion inside `.tst` files. This version moves all template intelligence into the shared Language Server, then layers real compiler services on top. Concretely, editing a `.tst` file now gives you:

- **Documented template members.** Completion and hover for `$Classes`, `$Properties`, `$BaseClass`, `$Fields`, `$DocComment`, `$IsNullable`, filters, and the rest of the template dialect include a description of what each member returns, so you no longer need to guess what `$BaseClass` or `$Value` means for the current context.
- **Real Roslyn IntelliSense inside `${ ... }` C# helper blocks.** Helper code is projected into a virtual C# document analyzed by an in-process Roslyn workspace: member completion, hover, and go-to-definition work against the actual `Typewriter.CodeModel` API. The whole code model is XML-documented, so hovering `.BaseClass` on a `Class` explains "the direct base class this class inherits from, or null when the class inherits only from object" instead of showing a bare signature.
- **Workspace-aware completions.** Type names, properties, methods, constants, and enum values from *your* loaded C# projects are offered while writing template blocks and lambda filters, with details such as `Property UserModel.CreatedAt: DateTime`.
- **Forwarding to full language services (VS Code).** Through virtual documents (`typewriter/embeddedDocument`, `typewriter/embeddedPosition`, `typewriter/templateRange` LSP requests), C# helper blocks are forwarded to the installed C# extension and TypeScript output regions to the built-in TypeScript service, and results are mapped back to `.tst` positions. Controlled by `typewriter.embeddedLanguages.forwarding` (`"auto"` by default, `"off"` to keep only the built-in Typewriter IntelliSense).
- **Live everything.** Diagnostics, semantic highlighting for Typewriter/C#/TypeScript regions, go-to-definition into your C# sources, and go-to-generated-file all update as you type, backed by the same engine that generates the output.

### IDE support matrix

| Capability                                                      | 💜 VS Code | 🟣 Visual Studio 2026 |    🧠 Rider     |
| --------------------------------------------------------------- | :-------: | :------------------: | :------------: |
| Template member completion, hover, and docs (`$BaseClass`, ...) |     ✅     |    ✅ (LSP client)    | ✅ (LSP client) |
| Roslyn IntelliSense in `${ ... }` C# helper blocks (built-in)   |     ✅     |          ✅           | ✅ (LSP client) |
| Project-aware IntelliSense (your types in helper blocks)        |     ✅     |          ✅           | ✅ (LSP client) |
| Forwarding to installed C# / TypeScript language services       |     ✅     |          🚧           |       🚧        |
| Semantic highlighting of template / C# / TypeScript regions     |     ✅     |  ✅ (classification)  |    ✅ (LSP)     |
| Live diagnostics in the editor                                  |     ✅     |    ✅ (Error List)    |    ✅ (LSP)     |
| Generate / validate on save                                     |     ✅     |          ✅           |       ✅        |

**Will this work in VS, VS Code, and Rider?** Yes. All three IDEs now share the same language server, so template member IntelliSense (documented completions and hover for `$BaseClass`, `$Properties`, ...), real Roslyn IntelliSense inside `${ ... }` C# helper blocks (with XML doc tooltips and project-aware type completions), live diagnostics, and go-to-definition work identically in VS Code, Visual Studio 2026, and Rider. VS Code additionally forwards to the installed C# extension and built-in TypeScript service through virtual documents (`typewriter.embeddedLanguages.forwarding`), which is VS Code client-side code that could be replicated in VS and Rider later. Rider enables the LSP client via the built-in IntelliJ Platform LSP API; toggle it in Settings under Typewriter.

---

## 📦 What Is in the Box

| Component                          | Path                                                             | What it does                                                                                          |
| ---------------------------------- | ---------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| 🛠️ **Typewriter CLI**               | [`src/Typewriter.Cli`](src/Typewriter.Cli)                       | `init`, `generate`, `validate`, `watch`, `list-templates` — scriptable and CI-ready                   |
| ⚙️ **Engine**                       | [`src/Typewriter.Engine`](src/Typewriter.Engine)                 | Template parsing, rendering, compiled C# helpers, output planning and writing                         |
| 🔬 **Roslyn metadata**              | [`src/Typewriter.Roslyn`](src/Typewriter.Roslyn)                 | Extracts classes, records, interfaces, enums, delegates, members, attributes, doc comments            |
| 🏗️ **Buildalyzer bridge**           | [`src/Typewriter.Buildalyzer`](src/Typewriter.Buildalyzer)       | Loads solutions/projects and resolves references without Visual Studio                                |
| 🌐 **Language Server**              | [`src/Typewriter.LanguageServer`](src/Typewriter.LanguageServer) | LSP: live diagnostics, completion, hover, go-to-definition, semantic tokens                           |
| 💜 **VS Code extension**            | [`vscode/`](vscode)                                              | `.tst` language support, commands, Problems integration, LSP client                                   |
| 🟣 **Visual Studio 2026 extension** | [`src/Typewriter.VisualStudio`](src/Typewriter.VisualStudio)     | SDK-style VSIX for AMD64 and ARM64: generate on save, Error List diagnostics, Output window logging   |
| 🧠 **JetBrains Rider plugin**       | [`rider/`](rider)                                                | IntelliJ frontend plugin: `.tst` highlighting, Tools menu actions, settings, save-time CLI generation |
| 📐 **Abstractions**                 | [`src/Typewriter.Abstractions`](src/Typewriter.Abstractions)     | Shared contracts: configuration, diagnostics, metadata, generation results                            |

---

## 🚦 Quick Start (5 Minutes to Generated Code)

### 1️⃣ Choose your IDE plugin or dotnet tool

Start with the plugin for the IDE you use. Download editor packages from the
GitHub [Tags tab](https://github.com/AdaskoTheBeAsT/Typewriter/tags):
choose a tag, expand **Assets**, then install the matching package.

| IDE                | Download                                 | Install                                                                                                              |
| ------------------ | ---------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| VS Code            | `typewriter-vscode-<version>.vsix`       | Run `code --install-extension typewriter-vscode-<version>.vsix`, or use **Extensions → ... → Install from VSIX...**  |
| Visual Studio 2026 | `Typewriter.VisualStudio-<version>.vsix` | Double-click the VSIX, or open it with the Visual Studio VSIX installer, then restart Visual Studio                  |
| JetBrains Rider    | `Typewriter-Rider-<version>.zip`         | Use **Settings/Preferences → Plugins → gear menu → Install Plugin from Disk...**, select the ZIP, then restart Rider |

For Visual Studio, uninstall the old `Typewriter64` extension before installing this VSIX. Only one Typewriter add-in should be installed at a time.

The CLI and language server are also published to NuGet as dotnet tools:

```bash
dotnet tool install --global AdaskoTheBeAsT.Typewriter.Cli
dotnet tool install --global AdaskoTheBeAsT.Typewriter.LanguageServer
typewriter --help
```

### 2️⃣ Initialize your workspace

```bash
typewriter init --workspace path/to/your/solution
# created: path/to/your/solution/typewriter.json
```

### 3️⃣ Write your first template

Add `Models.tst` next to your C# project:

```typescript
$Classes(*Model)[
export class $Name {
    constructor($Properties[public $name: $Type][, ]) {
    }
}
]
```

### 4️⃣ Add a C# model

```csharp
namespace MyApp.Models;

public class UserModel
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### 5️⃣ Generate ✨

```bash
typewriter generate --workspace path/to/your/solution
```

Generated `UserModel.ts`:

```typescript
export class UserModel {
    constructor(
        public id: number,
        public name: string,
        public email: string | null,
        public createdAt: Date
    ) {
    }
}
```

> 💡 **Pro tip:** run `typewriter watch` once and forget about it — every C# or template save regenerates the affected TypeScript.

---

## 🛠 CLI Reference

```text
typewriter [command] [options]
```

### Commands

| Command          | Description                                                               |
| ---------------- | ------------------------------------------------------------------------- |
| `init`           | 🆕 Create a `typewriter.json` with default values (`--force` to overwrite) |
| `generate`       | 🏭 Generate TypeScript files from templates (default command)              |
| `validate`       | ✅ Parse templates and load metadata without writing files                 |
| `watch`          | 👀 Watch configured input extensions and regenerate on change (debounced)  |
| `list-templates` | 📋 List the `.tst` templates discovered for the workspace                  |

### Options

| Option                          | Description                                                               |
| ------------------------------- | ------------------------------------------------------------------------- |
| `--workspace <path>`            | Solution, project, or folder to operate on                                |
| `--project <path>`              | Specific C# project to read                                               |
| `--template <path>`             | A single template file or template directory                              |
| `--template-search-path <path>` | Restrict template discovery without changing workspace or project context |
| `--framework <tfm>`             | Target framework for metadata loading (e.g. `net10.0`)                    |
| `--all-projects`                | Generate for every project in a multi-project workspace                   |
| `--output text\|json`           | Output format (use `json` in CI and tooling)                              |
| `--dry-run`                     | Render everything, write nothing                                          |
| `--diff`                        | Include unified diffs, including line-ending and final-newline changes    |
| `--fail-on-warning`             | Non-zero exit code when warnings are emitted                              |

### Exit codes

| Code | Meaning                                                            |
| ---: | ------------------------------------------------------------------ |
|  `0` | 🟢 Success                                                          |
|  `1` | 🔴 Generation failed (errors, or warnings with `--fail-on-warning`) |
|  `2` | 🟠 Invalid command-line arguments                                   |
|  `3` | 🟠 Project load failed (`TW0003`)                                   |
|  `4` | 🟠 Template parse error (`TW0002`)                                  |

### Diagnostic codes

| Code     | Meaning                                                                                                                      |
| -------- | ---------------------------------------------------------------------------------------------------------------------------- |
| `TW0001` | Unknown template member                                                                                                      |
| `TW0002` | Template parse error                                                                                                         |
| `TW0003` | Project load failed                                                                                                          |
| `TW0004` | C# compilation diagnostic surfaced from Roslyn                                                                               |
| `TW0005` | Output path escapes the workspace (blocked for safety)                                                                       |
| `TW0006` | Output would overwrite a file not generated by Typewriter (blocked)                                                          |
| `TW0007` | Template log message (`settings.Log`) surfaced as a diagnostic                                                               |
| `TW0008` | Duplicate generated output path warning; generation continues and the last render wins unless `--fail-on-warning` is enabled |
| `TW0009` | Project passed to `Settings.IncludeProject` was not found in the workspace                                                   |

---

## ⚙️ Configuration: `typewriter.json`

Generated by `typewriter init`. The generated file includes a `$schema` reference for editor validation and autocomplete. All values below are the defaults:

```json
{
  "$schema": "https://raw.githubusercontent.com/AdaskoTheBeAsT/Typewriter/master/typewriter.schema.json",
  "templates": ["**/*.tst"],
  "exclude": ["**/bin/**", "**/obj/**", "**/node_modules/**"],
  "inputExtensions": [".cs", ".csproj", ".json", ".props", ".sln", ".slnx", ".targets", ".tst"],
  "defaultTargetFramework": null,
  "output": {
    "newline": "lf",
    "encoding": "utf-8",
    "writeOnlyWhenChanged": true,
    "dryRun": false,
    "fileNameConvention": "preserve",
    "strictNull": true,
    "indentStyle": "preserve",
    "indentSize": 4,
    "insertFinalNewline": false,
    "trimTrailingWhitespace": false,
    "quoteStyle": "double",
    "dateType": "Date",
    "dateInitializer": "new Date()",
    "dateOnlyType": "Date",
    "dateOnlyInitializer": "new Date()",
    "timeOnlyType": "string",
    "timeOnlyInitializer": "\"00:00:00\"",
    "guidType": "string",
    "guidInitializer": "auto",
    "decimalType": "number",
    "decimalInitializer": "auto"
  },
  "diagnostics": {
    "failOnWarning": false
  }
}
```

### Top-level settings

| Key                      | Default                                                                  | Description                                                                                  |
| ------------------------ | ------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------- |
| `templates`              | `["**/*.tst"]`                                                           | Glob patterns used to discover templates                                                     |
| `exclude`                | `bin`, `obj`, `node_modules` globs                                       | Glob patterns excluded from discovery                                                        |
| `inputExtensions`        | `.cs`, `.csproj`, `.json`, `.props`, `.sln`, `.slnx`, `.targets`, `.tst` | File extensions that trigger `typewriter watch` and editor generate-on-save. Dot is optional |
| `defaultTargetFramework` | `null`                                                                   | TFM used when a project multi-targets (e.g. `net10.0`)                                       |

### `output` settings

| Key                      | Default          | Description                                                                                                                                              |
| ------------------------ | ---------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `newline`                | `"lf"`           | `lf` or `crlf` line endings in generated files                                                                                                           |
| `encoding`               | `"utf-8"`        | `utf-8` (no BOM) or `utf-8-bom` ⚠️ the original Typewriter emitted a BOM by default — set `utf-8-bom` for byte-identical output                           |
| `writeOnlyWhenChanged`   | `true`           | Skip disk writes when content is unchanged (keeps file watchers and HMR calm)                                                                            |
| `dryRun`                 | `false`          | Render without writing files                                                                                                                             |
| `fileNameConvention`     | `"preserve"`     | Output file naming: `preserve`, `kebab`, `pascal`, `camel`, `snake`                                                                                      |
| `strictNull`             | `true`           | Render nullable types as `T \| null`; templates can override via `settings.DisableStrictNullGeneration()`                                                |
| `indentStyle`            | `"preserve"`     | Re-indent generated output: `preserve`, `space`, or `tab` (the rendered indent unit is detected automatically)                                           |
| `indentSize`             | `4`              | Target indent width used when `indentStyle` is `space`                                                                                                   |
| `insertFinalNewline`     | `false`          | Ensure generated files end with a single trailing newline                                                                                                |
| `trimTrailingWhitespace` | `false`          | Strip trailing spaces and tabs from every generated line                                                                                                 |
| `quoteStyle`             | `"double"`       | Default string literal character for generated defaults: `double`, `single`, or `backtick`; `settings.UseStringLiteralCharacter(...)` in a template wins |
| `dateLibrary`            | `"legacy"`       | Coherent semantic profile: `legacy`, `nativeDate`, `temporal`, `moment`, `luxon`, `dateFns`, `dayJs`, or `jsJoda`; `settings.UseDateLibrary(...)` wins   |
| `dateType`               | `"Date"`         | TypeScript type used for `DateTime`, `DateTimeOffset`, and date-time-like NodaTime values; `settings.UseDateType(...)` wins                              |
| `dateInitializer`        | `"new Date()"`   | TypeScript initializer used for non-null date-time defaults; `settings.UseDateInitializer(...)` wins                                                     |
| `dateOnlyType`           | `"Date"`         | TypeScript type used for `DateOnly`, `NodaTime.LocalDate`, and `NodaTime.OffsetDate`; `settings.UseDateOnlyType(...)` wins                               |
| `dateOnlyInitializer`    | `"new Date()"`   | TypeScript initializer used for non-null date-only defaults; `settings.UseDateOnlyInitializer(...)` wins                                                 |
| `timeOnlyType`           | `"string"`       | TypeScript type used for `TimeOnly`, `NodaTime.LocalTime`, and `NodaTime.OffsetTime`; `settings.UseTimeOnlyType(...)` wins                               |
| `timeOnlyInitializer`    | `"\"00:00:00\""` | TypeScript initializer used for non-null time-only defaults; default literal follows `quoteStyle`, and `settings.UseTimeOnlyInitializer(...)` wins       |
| `guidType`               | `"string"`       | TypeScript type used for C# `Guid`; set to `uuid`, `UUID`, or a branded alias with `settings.UseGuidType(...)` or config                                 |
| `guidInitializer`        | `"auto"`         | Non-null `Guid` initializer; `auto` emits the empty GUID string, or `new Uint8Array(16)` for `Uint8Array`; `settings.UseGuidInitializer(...)` wins       |
| `decimalType`            | `"number"`       | TypeScript type used for C# `decimal`; `settings.UseDecimalType(...)` in a template wins                                                                 |
| `decimalInitializer`     | `"auto"`         | Non-null `decimal` initializer; `auto` emits `0`, or `new <decimalType>(0)` for a custom type; `settings.UseDecimalInitializer(...)` wins                |

### Date and time mapping

`DateTime`, `DateTimeOffset`, and date-time-like NodaTime values generate `Date` by default, and non-null defaults generate `new Date()`. `DateOnly`, `NodaTime.LocalDate`, and `NodaTime.OffsetDate` also default to `Date`/`new Date()`. `TimeOnly`, `NodaTime.LocalTime`, and `NodaTime.OffsetTime` default to `string`/`"00:00:00"`. Nullable value types keep the normal strict-null behavior, so `DateTime?` and `Nullable<DateTime>` generate `Date | null` when `output.strictNull` is `true`.

```csharp
public sealed record AuditDto(
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    DateTimeOffset SubmittedAt);
```

```typescript
export interface AuditDto {
  createdAt: Date;
  approvedAt: Date | null;
  submittedAt: Date;
}
```

Typewriter 4.8.0 can select a coherent semantic profile instead of configuring each type and initializer separately:

```json
{
  "output": {
    "dateLibrary": "temporal"
  }
}
```

```csharp
${
    Template(Settings settings)
    {
        settings.UseDateLibrary(DateLibrary.JsJoda);
    }
}
```

All supported values are:

| `output.dateLibrary` value | Template API equivalent                           | Profile                                                                                   |
| -------------------------- | ------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| `"legacy"`                 | `settings.UseDateLibrary(DateLibrary.Legacy)`     | Preserves the legacy `dateType`, `dateOnlyType`, `timeOnlyType`, and initializer settings |
| `"nativeDate"`             | `settings.UseDateLibrary(DateLibrary.NativeDate)` | Native JavaScript `Date` where it can represent the semantic type; otherwise `string`     |
| `"temporal"`               | `settings.UseDateLibrary(DateLibrary.Temporal)`   | Temporal types from `@js-temporal/polyfill`                                               |
| `"moment"`                 | `settings.UseDateLibrary(DateLibrary.Moment)`     | Moment and Moment Duration types                                                          |
| `"luxon"`                  | `settings.UseDateLibrary(DateLibrary.Luxon)`      | Luxon `DateTime` and `Duration` types                                                     |
| `"dateFns"`                | `settings.UseDateLibrary(DateLibrary.DateFns)`    | Native `Date` plus the date-fns `Duration` type                                           |
| `"dayJs"`                  | `settings.UseDateLibrary(DateLibrary.DayJs)`      | Day.js types with its duration plugin                                                     |
| `"jsJoda"`                 | `settings.UseDateLibrary(DateLibrary.JsJoda)`     | js-joda semantic date/time types plus `@js-joda/timezone`                                 |

The JSON values above show the canonical camel-case spellings accepted by `output.dateLibrary`. `legacy` is the default.

The profile supplies semantic TypeScript types, non-null initializers, and `settings.DateLibraryImportsGeneration`. Templates remain responsible for emitting the import text at the appropriate location. Configuration-level `dateLibrary` takes precedence over the low-level date strings. Inside a template, call `UseDateLibrary(...)` first and then call a low-level `UseDateType`, `UseDateOnlyType`, or `UseTimeOnlyType` method when an intentional override is needed.

### System and NodaTime profile matrix

| C# type                   | Semantic kind    | Native `Date` | Temporal                  | Moment            | Luxon      | date-fns   | Day.js                              | js-joda         |
| ------------------------- | ---------------- | ------------- | ------------------------- | ----------------- | ---------- | ---------- | ----------------------------------- | --------------- |
| `DateTime`                | plain date-time  | `Date`        | `Temporal.PlainDateTime`  | `moment.Moment`   | `DateTime` | `Date`     | `Dayjs`                             | `LocalDateTime` |
| `DateTimeOffset`          | instant          | `Date`        | `Temporal.Instant`        | `moment.Moment`   | `DateTime` | `Date`     | `Dayjs`                             | `Instant`       |
| `DateOnly`                | plain date       | `Date`        | `Temporal.PlainDate`      | `moment.Moment`   | `DateTime` | `Date`     | `Dayjs`                             | `LocalDate`     |
| `TimeOnly`                | plain time       | `string`      | `Temporal.PlainTime`      | `string`          | `DateTime` | `string`   | `string`                            | `LocalTime`     |
| `TimeSpan`                | elapsed duration | `string`      | `Temporal.Duration`       | `moment.Duration` | `Duration` | `Duration` | `ReturnType<typeof dayjs.duration>` | `Duration`      |
| `NodaTime.Instant`        | instant          | `Date`        | `Temporal.Instant`        | `moment.Moment`   | `DateTime` | `Date`     | `Dayjs`                             | `Instant`       |
| `NodaTime.LocalDate`      | plain date       | `Date`        | `Temporal.PlainDate`      | `moment.Moment`   | `DateTime` | `Date`     | `Dayjs`                             | `LocalDate`     |
| `NodaTime.OffsetDate`     | plain date       | `Date`        | `Temporal.PlainDate`      | `moment.Moment`   | `DateTime` | `Date`     | `Dayjs`                             | `LocalDate`     |
| `NodaTime.LocalTime`      | plain time       | `string`      | `Temporal.PlainTime`      | `string`          | `DateTime` | `string`   | `string`                            | `LocalTime`     |
| `NodaTime.OffsetTime`     | plain time       | `string`      | `Temporal.PlainTime`      | `string`          | `DateTime` | `string`   | `string`                            | `LocalTime`     |
| `NodaTime.LocalDateTime`  | plain date-time  | `Date`        | `Temporal.PlainDateTime`  | `moment.Moment`   | `DateTime` | `Date`     | `Dayjs`                             | `LocalDateTime` |
| `NodaTime.OffsetDateTime` | instant          | `Date`        | `Temporal.Instant`        | `moment.Moment`   | `DateTime` | `Date`     | `Dayjs`                             | `Instant`       |
| `NodaTime.ZonedDateTime`  | zoned date-time  | `string`      | `Temporal.ZonedDateTime`  | `string`          | `DateTime` | `string`   | `string`                            | `ZonedDateTime` |
| `NodaTime.Duration`       | elapsed duration | `string`      | `Temporal.Duration`       | `moment.Duration` | `Duration` | `Duration` | `ReturnType<typeof dayjs.duration>` | `Duration`      |
| `NodaTime.Period`         | calendar period  | `string`      | `Temporal.Duration`       | `moment.Duration` | `Duration` | `Duration` | `ReturnType<typeof dayjs.duration>` | `Period`        |
| `NodaTime.YearMonth`      | plain year-month | `string`      | `Temporal.PlainYearMonth` | `string`          | `DateTime` | `string`   | `string`                            | `YearMonth`     |
| `NodaTime.AnnualDate`     | plain month-day  | `string`      | `Temporal.PlainMonthDay`  | `string`          | `DateTime` | `string`   | `string`                            | `MonthDay`      |

`Duration` means elapsed time, while `Period` means calendar-relative units such as years and months. They remain separate schema kinds even when a frontend library represents both with the same runtime class. A `string` entry means that the selected library backend cannot faithfully represent that semantic kind.

`DateTime` is inherently ambiguous, so its default is plain date-time. Override a member when the domain says otherwise:

```bash
dotnet add package AdaskoTheBeAsT.Typewriter.Annotations
```

```csharp
using AdaskoTheBeAsT.Typewriter.Annotations;

public sealed record AuditDto(
    [property: FrontendRuntimeType(FrontendRuntimeType.Instant)]
    DateTime CreatedAt);
```

The neutral annotation values include `Instant`, `PlainDate`, `PlainTime`, `PlainDateTime`, `ZonedDateTime`, `Duration`, `Period`, `PlainYearMonth`, and `PlainMonthDay`. Existing `Temporal*` names are aliases. The annotation selects semantics, not a JavaScript library; one `dateLibrary` and one matching runtime backend apply to the generated client.

The `AdaskoTheBeAsT.Typewriter.Annotations` package also includes source-level attributes that recipes and templates recognise by simple name:

| Attribute                       | Applies to                     | Purpose                                                                                                                       |
| ------------------------------- | ------------------------------ | ----------------------------------------------------------------------------------------------------------------------------- |
| `GenerateFrontendTypeAttribute` | Class, interface, enum, method | Marks a type or method for inclusion in frontend generation; optional `DllName` routes the type to a specific output assembly |
| `FrontendRuntimeTypeAttribute`  | Property, field, parameter     | Overrides the semantic runtime type for a single member                                                                       |
| `FrontendNoTransformAttribute`  | Property, field, parameter     | Emits the value as a plain passthrough, skipping runtime transformation                                                       |
| `AsStringAttribute`             | Enum                           | Emits enum values as strings instead of numbers                                                                               |
| `LabelForEnumAttribute`         | Field (enum member)            | Attaches a human-readable label to an enum value                                                                              |
| `CustomNameAttribute`           | Method                         | Overrides the generated frontend method name                                                                                  |

Typewriter matches these by simple name, so user-defined attributes with the same name in any namespace continue to work without the package.

To use another date library in generated types, configure the date-time, date-only, and time-only type/initializer pairs, or call the matching `Settings` methods from a template:

```json
{
  "output": {
    "dateType": "Dayjs",
    "dateInitializer": "dayjs()",
    "dateOnlyType": "Dayjs",
    "dateOnlyInitializer": "dayjs()",
    "timeOnlyType": "string",
    "timeOnlyInitializer": "\"00:00:00\""
  }
}
```

```csharp
${
    Template(Settings settings)
    {
        settings.UseDateType("DateTime"); // for Luxon
        settings.UseDateInitializer("DateTime.now()");
        settings.UseDateOnlyType("DateTime");
        settings.UseDateOnlyInitializer("DateTime.now().startOf('day')");
        settings.UseTimeOnlyType("string");
        settings.UseTimeOnlyInitializer("\"00:00:00\"");
    }
}
```

If the selected date type is not global, update your template to emit the required TypeScript import, for example `import type { Dayjs } from 'dayjs';`.

If your API returns ISO strings for `DateTime`, `DateTimeOffset`, `DateOnly`, or NodaTime values, pair generated models with [date-interceptors](https://github.com/adaskothebeast/date-interceptors) for runtime conversion. This is required when generated TypeScript uses `Date`, Day.js, Moment.js, Luxon, js-joda, Temporal, or another runtime date type, because JSON still arrives as strings and must be translated to the corresponding object type.

| Target runtime | Date-time config                                                                                            | Date-only config                                            | Time-only config                                                | Type imports                                                                | Converter                                          |
| -------------- | ----------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------- | --------------------------------------------------------------- | --------------------------------------------------------------------------- | -------------------------------------------------- |
| Native `Date`  | `dateType: "Date"`, `dateInitializer: "new Date()"`                                                         | `dateOnlyType: "Date"`, `dateOnlyInitializer: "new Date()"` | `timeOnlyType: "string"`, `timeOnlyInitializer: "\"00:00:00\""` | none                                                                        | `@adaskothebeast/hierarchical-convert-to-date`     |
| date-fns       | same as native `Date`                                                                                       | same as native `Date`                                       | ISO `string`                                                    | none                                                                        | `@adaskothebeast/hierarchical-convert-to-date-fns` |
| Day.js         | `Dayjs`, `dayjs()`                                                                                          | `Dayjs`, `dayjs()`                                          | ISO `string`                                                    | `import type { Dayjs } from 'dayjs';`                                       | `@adaskothebeast/hierarchical-convert-to-dayjs`    |
| Moment.js      | `Moment`, `moment()`                                                                                        | `Moment`, `moment()`                                        | ISO `string`                                                    | `import type { Moment } from 'moment';`                                     | `@adaskothebeast/hierarchical-convert-to-moment`   |
| Luxon          | `DateTime`, `DateTime.now()`                                                                                | `DateTime`, `DateTime.now().startOf('day')`                 | ISO `string`                                                    | `import type { DateTime } from 'luxon';`                                    | `@adaskothebeast/hierarchical-convert-to-luxon`    |
| js-joda        | `ZonedDateTime`, `ZonedDateTime.now()` or `LocalDateTime`, `LocalDateTime.now()`                            | `LocalDate`, `LocalDate.now()`                              | `LocalTime`, `LocalTime.now()`                                  | `import type { LocalDate, LocalTime, ZonedDateTime } from '@js-joda/core';` | `@adaskothebeast/hierarchical-convert-to-js-joda`  |
| Temporal       | `Temporal.Instant`, `Temporal.Now.instant()` or `Temporal.PlainDateTime`, `Temporal.Now.plainDateTimeISO()` | `Temporal.PlainDate`, `Temporal.Now.plainDateISO()`         | `Temporal.PlainTime`, `Temporal.Now.plainTimeISO()`             | `import { Temporal } from '@js-temporal/polyfill';`                         | `@adaskothebeast/hierarchical-convert-to-temporal` |

```bash
# Native Date
npm install @adaskothebeast/angular-date-http-interceptor @adaskothebeast/hierarchical-convert-to-date

# date-fns
npm install @adaskothebeast/angular-date-http-interceptor @adaskothebeast/hierarchical-convert-to-date-fns date-fns

# Day.js
npm install @adaskothebeast/angular-date-http-interceptor @adaskothebeast/hierarchical-convert-to-dayjs dayjs

# Moment.js
npm install @adaskothebeast/angular-date-http-interceptor @adaskothebeast/hierarchical-convert-to-moment moment

# Luxon
npm install @adaskothebeast/angular-date-http-interceptor @adaskothebeast/hierarchical-convert-to-luxon luxon

# js-joda, useful for NodaTime-style payloads
npm install @adaskothebeast/angular-date-http-interceptor @adaskothebeast/hierarchical-convert-to-js-joda @js-joda/core @js-joda/timezone

# Temporal
npm install @adaskothebeast/angular-date-http-interceptor @adaskothebeast/hierarchical-convert-to-temporal @js-temporal/polyfill
```

```typescript
import { NgModule } from '@angular/core';
import {
  AngularDateHttpInterceptorModule,
  HIERARCHICAL_DATE_ADJUST_FUNCTION,
} from '@adaskothebeast/angular-date-http-interceptor';
import { hierarchicalConvertToDate } from '@adaskothebeast/hierarchical-convert-to-date';

@NgModule({
  imports: [AngularDateHttpInterceptorModule],
  providers: [
    { provide: HIERARCHICAL_DATE_ADJUST_FUNCTION, useValue: hierarchicalConvertToDate },
  ],
})
export class AppModule {
}
```

Swap the converter import to match your target:

```typescript
import { hierarchicalConvertToDateFns } from '@adaskothebeast/hierarchical-convert-to-date-fns';
import { hierarchicalConvertToDayjs } from '@adaskothebeast/hierarchical-convert-to-dayjs';
import { hierarchicalConvertToMoment } from '@adaskothebeast/hierarchical-convert-to-moment';
import { hierarchicalConvertToLuxon } from '@adaskothebeast/hierarchical-convert-to-luxon';
import { hierarchicalConvertToJsJoda } from '@adaskothebeast/hierarchical-convert-to-js-joda';
```

Example generated model shapes:

```typescript
// Native Date or date-fns
createdAt: Date;
approvedAt: Date | null;

// Day.js
createdAt: Dayjs;
approvedAt: Dayjs | null;

// Moment.js
createdAt: Moment;
approvedAt: Moment | null;

// Luxon
createdAt: DateTime;
approvedAt: DateTime | null;

// js-joda
createdAt: ZonedDateTime;
approvedAt: ZonedDateTime | null;
```

Recommended semantic mappings:

| C# or NodaTime type                                                                                   | Recommended TypeScript shape                                                                                 | Notes                                                                                                                                                                                                   |
| ----------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `DateTime`, `DateTimeOffset`, `NodaTime.Instant`, `NodaTime.OffsetDateTime`, `NodaTime.ZonedDateTime` | `Date`, `Temporal.Instant`, Luxon `DateTime`, Day.js `Dayjs`, or js-joda `Instant`/`ZonedDateTime`           | Use a real date-time object when clients compare, format, or calculate with the value. Use [date-interceptors](https://github.com/adaskothebeast/date-interceptors) to convert JSON strings at runtime. |
| `DateOnly`, `NodaTime.LocalDate`, `NodaTime.OffsetDate`                                               | ISO `string`, `Temporal.PlainDate`, or js-joda `LocalDate`                                                   | Avoid native `Date` for pure dates if timezone shifts matter.                                                                                                                                           |
| `TimeOnly`, `NodaTime.LocalTime`, `NodaTime.OffsetTime`                                               | ISO `string`, `Temporal.PlainTime`, or js-joda `LocalTime`                                                   | JavaScript has no native time-only type.                                                                                                                                                                |
| `NodaTime.LocalDateTime`                                                                              | `Temporal.PlainDateTime`, Luxon `DateTime`, js-joda `LocalDateTime`, or ISO `string`                         | Use `output.dateType` for this local date-time type.                                                                                                                                                    |
| `TimeSpan`, `NodaTime.Duration`                                                                       | ISO duration `string`, `Temporal.Duration`, js-joda `Duration`, or a numeric convention such as milliseconds | Pick one wire format and document it. `Duration` means elapsed time.                                                                                                                                    |
| `NodaTime.Period`                                                                                     | ISO period `string`, `Temporal.Duration`, js-joda `Period`, or date-fns `Duration`                           | `Period` is calendar-based, such as 1 month and 3 days, so keep it distinct from elapsed `Duration`.                                                                                                    |

For date types that need an import, emit the import only when the current class actually has date fields or properties:

```csharp
${
    static string dateTypeImport = string.Empty;

    Template(Settings settings)
    {
        settings.UseDateType("Dayjs");
        settings.UseDateInitializer("dayjs()");
        dateTypeImport = settings.DateTypeGeneration switch
        {
            "Dayjs" => "import type { Dayjs } from 'dayjs';",
            "Moment" => "import type { Moment } from 'moment';",
            "DateTime" => "import type { DateTime } from 'luxon';",
            "ZonedDateTime" => "import type { ZonedDateTime } from '@js-joda/core';",
            _ => string.Empty, // Date and date-fns use native Date and need no import.
        };
    }

    string DateTypeImport(Class c)
    {
        return UsesDate(c) && !string.IsNullOrEmpty(dateTypeImport)
            ? dateTypeImport + Environment.NewLine
            : string.Empty;
    }

    bool UsesDate(Class c)
    {
        return c.Properties.Any(p => UsesDate(p.Type))
            || c.Fields.Any(f => UsesDate(f.Type))
            || c.StaticReadOnlyFields.Any(f => UsesDate(f.Type));
    }

    bool UsesDate(Type type)
    {
        return type.IsDate
            || (type.ElementType != null && UsesDate(type.ElementType))
            || type.TypeArguments.Any(UsesDate);
    }
}
$Classes[
$DateTypeImport
export interface I$Name {
$Properties[
  $name: $Type;]
}
]
```

With `settings.UseDateType("Dayjs")`, a class with `DateTime CreatedAt` generates:

```typescript
import type { Dayjs } from 'dayjs';

export interface IAuditDto {
  createdAt: Dayjs;
}
```

For NodaTime-specific DTOs, map those C# types in your template to the TypeScript date type you use, then let the interceptor convert response values:

```csharp
${
    string TypeName(Property property)
    {
        return property.Type.FullName switch
        {
            "NodaTime.Instant" => "Instant",
            "NodaTime.ZonedDateTime" => "ZonedDateTime",
            "NodaTime.OffsetDateTime" => "OffsetDateTime",
            "NodaTime.LocalDateTime" => "LocalDateTime",
            "NodaTime.LocalDate" => "LocalDate",
            "NodaTime.LocalTime" => "LocalTime",
            "NodaTime.Duration" => "Duration",
            "NodaTime.Period" => "Period",
            _ => property.Type.Name,
        };
    }
}

export interface $Name {
$Properties[
  $name: $TypeName;]
}
```

### Guid mapping

C# `Guid` maps to TypeScript `string` by default. If your frontend uses a stronger UUID alias, configure `output.guidType` or call `settings.UseGuidType(...)` from a template:

```json
{
  "output": {
    "guidType": "uuid"
  }
}
```

```csharp
${
    Template(Settings settings)
    {
        settings.UseGuidType("uuid");
    }
}
```

TypeScript itself does not have a built-in UUID type. The npm [`uuid`](https://github.com/uuidjs/uuid) package provides UUID generators, parsers, validators, and TypeScript declarations, but its standard generators return strings rather than introducing a lowercase `uuid` domain type. Use `string`, emit `type uuid = string;`, or import a project-specific branded type. With `guidInitializer: "auto"`, `$Type[$Default]` for `Guid` continues to generate the empty GUID string.

For schema-aware clients that hydrate UUID strings into 16-byte values, use `Uint8Array`:

```json
{
  "output": {
    "guidType": "Uint8Array",
    "guidInitializer": "auto"
  }
}
```

```csharp
${
    Template(Settings settings)
    {
        settings
            .UseGuidType("Uint8Array")
            .UseGuidInitializer("auto");
    }
}
```

With `Uint8Array`, the automatic non-null default is `new Uint8Array(16)`. For another UUID class or factory, set `guidInitializer` to any TypeScript expression. For example, when the template imports `{ NIL }` from the npm `uuid` package, use `"NIL"`.

### Numeric and decimal mapping

Numeric primitives map to TypeScript `number` by default, including `byte`, `short`, `int`, `long`, `float`, `double`, and `decimal`.

```csharp
public sealed record TotalsDto(
    int Count,
    float Ratio,
    double Average,
    decimal Amount);
```

```typescript
export interface TotalsDto {
  count: number;
  ratio: number;
  average: number;
  amount: number;
}
```

If you need exact decimal arithmetic, configure `decimal.js` by changing the generated decimal type:

```bash
npm install decimal.js
```

```json
{
  "output": {
    "decimalType": "Decimal",
    "decimalInitializer": "auto"
  }
}
```

```csharp
${
    Template(Settings settings)
    {
        settings
            .UseDecimalType("Decimal")
            .UseDecimalInitializer("auto");
    }
}
```

Then emit the `decimal.js` import only for classes that actually use C# `decimal` fields or properties:

```csharp
${
    string DecimalImport(Class c)
    {
        return UsesDecimal(c)
            ? "import Decimal from 'decimal.js';" + Environment.NewLine
            : string.Empty;
    }

    bool UsesDecimal(Class c)
    {
        return c.Properties.Any(p => UsesDecimal(p.Type))
            || c.Fields.Any(f => UsesDecimal(f.Type))
            || c.StaticReadOnlyFields.Any(f => UsesDecimal(f.Type));
    }

    bool UsesDecimal(Type type)
    {
        return type.FullName == "System.Decimal"
            || (type.ElementType != null && UsesDecimal(type.ElementType))
            || type.TypeArguments.Any(UsesDecimal);
    }
}
$Classes[
$DecimalImport
export interface I$Name {
$Properties[
  $name: $Type;]
}
]
```

With `settings.UseDecimalType("Decimal")`, `decimal Amount` generates `amount: Decimal;`. The automatic `$Type[$Default]` is `new Decimal(0)`; set `decimalInitializer` or call `UseDecimalInitializer(...)` when a custom decimal type uses another factory.

For lossless high-precision values, the API must serialize the decimal as a JSON string. A JSON number is parsed into a JavaScript `number` before Decimal.js sees it, so precision may already be lost. Runtime-schema templates should emit `schema.decimal('string')` for string-encoded decimals and `schema.decimal('number')` only when number-wire precision is acceptable. `[FrontendRuntimeType(FrontendRuntimeType.Decimal, WireFormat = "string")]` can communicate that choice to templates.

### `diagnostics` settings

| Key             | Default | Description                               |
| --------------- | ------- | ----------------------------------------- |
| `failOnWarning` | `false` | Treat warnings as failures (great for CI) |

### 🔍 Discovery and precedence

1. Configuration files are looked up by name, in order: `typewriter.json`, `typewriter.config.json`, `.typewriterrc.json`
2. Files in the **workspace folder** load first, then files in the **project folder** override them
3. **Environment variables** override file values
4. **CLI flags** (`--framework`, `--dry-run`, `--fail-on-warning`) win over everything

### 🌱 Environment variables

| Variable                                  | Overrides                                      |
| ----------------------------------------- | ---------------------------------------------- |
| `TYPEWRITER_INPUT_EXTENSIONS`             | `inputExtensions` (comma/semicolon/space list) |
| `TYPEWRITER_DEFAULT_TARGET_FRAMEWORK`     | `defaultTargetFramework`                       |
| `TYPEWRITER_OUTPUT_NEWLINE`               | `output.newline`                               |
| `TYPEWRITER_OUTPUT_DATE_LIBRARY`          | `output.dateLibrary`                           |
| `TYPEWRITER_OUTPUT_DATE_TYPE`             | `output.dateType`                              |
| `TYPEWRITER_OUTPUT_DATE_INITIALIZER`      | `output.dateInitializer`                       |
| `TYPEWRITER_OUTPUT_DATE_ONLY_TYPE`        | `output.dateOnlyType`                          |
| `TYPEWRITER_OUTPUT_DATE_ONLY_INITIALIZER` | `output.dateOnlyInitializer`                   |
| `TYPEWRITER_OUTPUT_TIME_ONLY_TYPE`        | `output.timeOnlyType`                          |
| `TYPEWRITER_OUTPUT_TIME_ONLY_INITIALIZER` | `output.timeOnlyInitializer`                   |
| `TYPEWRITER_OUTPUT_GUID_TYPE`             | `output.guidType`                              |
| `TYPEWRITER_OUTPUT_GUID_INITIALIZER`      | `output.guidInitializer`                       |
| `TYPEWRITER_OUTPUT_DECIMAL_TYPE`          | `output.decimalType`                           |
| `TYPEWRITER_OUTPUT_DECIMAL_INITIALIZER`   | `output.decimalInitializer`                    |
| `TYPEWRITER_FAIL_ON_WARNING`              | `diagnostics.failOnWarning` (`true`/`1`/`yes`) |

---

## 📝 Template Authoring

Templates are `.tst` files using the original Typewriter dialect — existing templates keep working.

### Core syntax

| Construct                                            | Meaning                                                                                                             |
| ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| `$Classes(filter)[...]`                              | Render block for every matching class (also `$Records`, `$Structs`, `$Interfaces`, `$Enums`, `$Delegates`)          |
| `$Properties[...][separator]`                        | Iterate members, including indexers (also `$Methods`, `$Parameters`, `$Fields`, `$Constants`, `$Events`, `$Values`) |
| `$Name`, `$name`, `$FullName`, `$Namespace`, `$Type` | Scalar substitutions (lowercase first letter via `$name`)                                                           |
| `$$`                                                 | Render one literal `$`, for example `$$type` outputs `$type` without treating it as a template member               |
| `(*Model)`, `([Attribute])`, `(c => c.IsPublic)`     | Wildcard, attribute, and lambda filters                                                                             |
| `$IsNullable[yes][no]`                               | Conditional true/false blocks                                                                                       |
| `${ ... }`                                           | Compiled C# helper block — write real C# methods used by the template                                               |
| `#r "nuget: PackageId, 1.2.3"`                       | Reference DLLs or NuGet packages (restored automatically) from helper code                                          |
| `#load "shared-helpers.cs"`                          | Include shared C# source helper members from a file relative to the current template/helper file                    |

### Struct and indexer templates

Version `4.1.0` adds first-class struct and indexer support. Structs are useful for DTO generation when your serializer can read and write them. Indexers are exposed for template completeness and lookup-style TypeScript APIs, but they are not JSON payload properties.

Use `$Structs[...]` when value types should be generated separately from classes and records:

```typescript
$Structs[
export interface $Name {
$Properties[
    $name: $Type;]
}
]
```

You can also filter all available types to structs:

```typescript
$Types(Struct)[
export type $NameValue = $Properties[$Type][ | ];
]
```

Indexer properties are included in `$Properties[...]`. Use `$IsIndexer` to distinguish them from normal properties, and render indexer parameters through `$Parameters[...]`:

```typescript
$Classes[
export interface $NameLookup {
$Properties[
    $IsIndexer[
    get($Parameters[$name: $Type][, ]): $Type;
    ][
    $name: $Type;
    ]]
}
]
```

For this C# type:

```csharp
public sealed class LocalizedText
{
    public string Default { get; init; } = string.Empty;

    public string this[string culture] => Default;
}

public readonly struct Money
{
    public decimal Amount { get; init; }
    public string Currency { get; init; }
}
```

Templates can now generate both the `Money` value shape and a typed lookup signature for `LocalizedText`.

> Indexer note: `System.Text.Json` and Newtonsoft.Json do not deserialize indexers as normal object properties. If the lookup values must round-trip through JSON, expose a real property and generate from that instead:

```csharp
public sealed class LocalizedTextDto
{
    public Dictionary<string, string> Values { get; init; } = [];
}
```

Then generate the TypeScript dictionary shape from `Values`, for example `Record<string, string>`.

### Sharing logic between templates: `#load` vs `#r`

Use `#load` when you want to share helper **source code** between templates. These files are usually `.cs` files, but they are helper snippets, not standalone C# files with their own namespace and class. Typewriter inserts their contents into each generated template helper class, so put helper members directly in the file:

```text
templates/
  _helpers/
    names.cs
  models.tst
  services.tst
```

```csharp
// templates/_helpers/names.cs
string ContractName(Class c) => c.Name.EndsWith("Dto") ? c.Name[..^3] : c.Name;
```

Then load the same helper file from any `.tst` template that needs it:

```csharp
${
    #load "_helpers/names.cs"
}
$Classes[
export interface $ContractName {
}
]
```

`#load` paths are relative to the current `.tst` file or the helper file that contains the nested `#load`. Loaded helper files may also contain `using`, `#r`, and more `#load` directives. Since each template is compiled separately, `#load` shares code, not runtime state.

`#load` can also download helper source from `http` or `https` URLs, which is handy for central template helper files hosted in GitHub:

```csharp
${
    #load "https://raw.githubusercontent.com/owner/repo/v1.2.3/templates/helpers/names.cs"
}
```

By default, remote helpers are downloaded on each template compilation. Add a positive `TimeSpan` after the URL to cache a remote helper in memory for the current process, which is useful for `watch` mode and language-server validation:

```csharp
${
    #load "https://raw.githubusercontent.com/owner/repo/v1.2.3/templates/helpers/names.cs", "00:10:00"
}
```

The cache is process-local, stores only successful downloads, and applies only to remote `http`/`https` loads. For GitHub, use raw file URLs (`raw.githubusercontent.com/...`), not normal `github.com/.../blob/...` pages. Prefer immutable commit SHAs or release tags instead of branch names for reproducible builds. Remote helper files are capped at 1 MB and compiled as C# template code, so only load URLs you trust.

Loaded files can contain helper methods directly, which makes them callable from template syntax such as `$ContractName`. They can also contain nested types such as `static class NameHelpers`, but those nested static class methods are not discovered as `$...` template helpers automatically. Use a thin helper method when you want to call a static class from a template:

```csharp
// templates/_helpers/names.cs
static class NameHelpers
{
    public static string ContractName(string name) => name.EndsWith("Dto") ? name[..^3] : name;
}

string ContractName(Class c) => NameHelpers.ContractName(c.Name);
```

If you want fully testable shared logic, the cleanest pattern is a normal C# class library with unit tests, referenced from templates with `#r`. Keep Typewriter-specific wrapper methods in `.tst` files or small `#load` snippets:

```csharp
${
    #r "../TemplateHelpers/bin/Release/net10.0/TemplateHelpers.dll"
    using MyCompany.TemplateHelpers;

    string ContractName(Class c) => NameHelpers.ContractName(c.Name);
}
```

Use `#r` when helper code needs a compiled **assembly reference**, not source. It can reference a local `.dll` or a NuGet package. If your shared logic grows into a normal C# class library with namespaces and classes, compile it and reference it with `#r`:

```csharp
${
    #r "nuget: Humanizer.Core, 2.14.1"
    using Humanizer;

    string DisplayName(Class c) => c.Name.Humanize();
}
```

NuGet references are restored automatically into the normal NuGet package cache. If you use private feeds, place a `NuGet.config` near the template or workspace; Typewriter searches upward from the template directory.

### Compiled C# helpers

```csharp
${
    using Typewriter.VisualStudio;

    static ILog log;

    Template(Settings settings)
    {
        settings.UseStringLiteralCharacter('\'');
        log = settings.Log;
    }

    bool IncludeClass(Class c)
    {
        log.LogInfo($"Processing {c.Name}");
        return c.Attributes.Any(a => a.Name == "GenerateFrontendType");
    }

    string LoudName(Constant constant) => constant.Name.ToUpperInvariant();
}
$Classes($IncludeClass)[$Constants[
export const $LoudName = '$Value';]
]
```

Helper methods can receive both the parent and current context when invoked from nested collections, for example `string Qualified(Class c, Property p)`. Runtime helper failures include the original template method and line when possible.

For large templates, the template constructor can also receive the current `File` before the template body renders. Use this for counts and in-memory pre-filtering:

```csharp
${
    private Class[] dtoClasses = Array.Empty<Class>();

    Template(Settings settings, File file)
    {
        settings.Log.LogInfo("Processing {0} classes", file.Classes.Count);
        dtoClasses = file.Classes.Where(c => c.Name.EndsWith("Dto")).ToArray();
    }

    IEnumerable<Class> DtoClasses(File file) => dtoClasses;
}
$DtoClasses[
export interface $Name {
}
]
```

### Shared helpers and render completion hooks

```csharp
${
    #load "shared-helpers.cs"

    static ILog log;

    Template(Settings settings)
    {
        log = settings.Log;
    }

    void OnRenderComplete(File file)
    {
        log.LogInfo("Rendered {0} classes", file.Classes.Count);
    }
}
```

`#load` supports nested helper files and reports missing files as `TW0002` diagnostics. `OnRenderComplete()` or `OnRenderComplete(File file)` runs after template rendering and surfaces hook failures as template diagnostics.

### Custom output paths

```csharp
${
    Template(Settings settings)
    {
        settings.OutputFilenameFactory = file => file.Classes.First().Name + ".generated.ts";
        // or render everything into one file:
        // settings.SingleFileMode("api-models.ts");
    }
}
```

### Web API URL helpers

The built-in Web API helpers parse `Route` and `RoutePrefix` attribute values, ignore named-only HTTP attribute arguments such as `Name = "ListUsers"` or `Order = 1`, honor `Template = "..."`, and encode nullable string/date query values as `encodeURIComponent(value ?? '')`.

> 📚 **Production-ready templates:** the [NetCoreTypewriterRecipes](https://github.com/AdaskoTheBeAsT/NetCoreTypewriterRecipes) repository contains battle-tested templates for 🅰️ Angular services, ⚛️ React models, 🔀 polymorphic JSON discriminators (System.Text.Json and Newtonsoft.Json), enums, records, and constants. This engine is snapshot-tested against those recipes.

---

## 🎛 Template Settings API

The `Settings` object passed to the template constructor keeps the original API so old templates compile unchanged. Current wiring status:

| Setting                                                                            | Status | Notes                                                                                                                                                    |
| ---------------------------------------------------------------------------------- | :----: | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `OutputExtension`                                                                  |   ✅    | Default `.ts`                                                                                                                                            |
| `OutputFilenameFactory`                                                            |   ✅    | Per-source fan-out and no-match suppression supported                                                                                                    |
| `OutputDirectory`                                                                  |   ✅    | Relative paths resolve against the template location                                                                                                     |
| `SingleFileMode("name.ts")`                                                        |   ✅    | Renders the whole template into one file                                                                                                                 |
| `UseStringLiteralCharacter('\'')`                                                  |   ✅    | Affects generated string literals and defaults; defaults from `output.quoteStyle` in `typewriter.json`                                                   |
| `UseDateLibrary(DateLibrary.Temporal)`                                             |   ✅    | Selects one coherent semantic type, initializer, and import profile; inspect `DateLibraryImportsGeneration` for imports                                  |
| `UseDateType("Date")`                                                              |   ✅    | Overrides date-time types such as `DateTime`, `DateTimeOffset`, and NodaTime `Instant`                                                                   |
| `UseDateInitializer("new Date()")`                                                 |   ✅    | Overrides the generated TypeScript initializer for non-null date-time defaults                                                                           |
| `UseDateOnlyType("Date")`                                                          |   ✅    | Overrides date-only types such as `DateOnly` and NodaTime `LocalDate`                                                                                    |
| `UseDateOnlyInitializer("new Date()")`                                             |   ✅    | Overrides the generated TypeScript initializer for non-null date-only defaults                                                                           |
| `UseTimeOnlyType("string")`                                                        |   ✅    | Overrides time-only types such as `TimeOnly` and NodaTime `LocalTime`                                                                                    |
| `UseTimeOnlyInitializer("\"00:00:00\"")`                                           |   ✅    | Overrides the generated TypeScript initializer for non-null time-only defaults                                                                           |
| `UseGuidType("string")`                                                            |   ✅    | Overrides the generated TypeScript type for C# `Guid`, for example `uuid` or `UUID`                                                                      |
| `UseGuidInitializer("auto")`                                                       |   ✅    | Overrides non-null `Guid` defaults; `auto` understands string and `Uint8Array` mappings                                                                  |
| `UseDecimalType("number")`                                                         |   ✅    | Overrides the generated TypeScript type for C# `decimal`, for example `Decimal` from `decimal.js`                                                        |
| `UseDecimalInitializer("auto")`                                                    |   ✅    | Overrides non-null decimal defaults; `auto` emits `0` or constructs the configured decimal type                                                          |
| `TemplatePath`                                                                     |   ✅    | Full path of the executing template                                                                                                                      |
| `Log` (`ILog`)                                                                     |   ✅    | Messages surface as `TW0007` diagnostics in CLI output and editors                                                                                       |
| `IncludeProject(name)`                                                             |   ✅    | Merges the named workspace project (and its references) into the template's code model, in addition to the current project; unknown names raise `TW0009` |
| `IncludeCurrentProject()` / `IncludeReferencedProjects()` / `IncludeAllProjects()` |   ♻️    | Accepted for compatibility; the current project and its references are always included, `--all-projects` controls full workspace scope                   |
| `DisableStrictNullGeneration()`                                                    |   ✅    | Defaults from `output.strictNull` in `typewriter.json`; calling this in the template overrides it                                                        |
| `DisableUtf8BomGeneration()` / `Utf8BomGeneration`                                 |   ✅    | Defaults from `output.encoding` in `typewriter.json`; the template override wins per file                                                                |
| `PartialRenderingMode`                                                             |   🚧    | Accepted but not wired yet                                                                                                                               |
| `SolutionFullName`                                                                 |   ✅    | Populated from the resolved workspace (solution or folder path)                                                                                          |
| `SkipAddingGeneratedFilesToProject`                                                |   ➖    | Obsolete — SDK-style projects include files automatically                                                                                                |

Legend: ✅ working · ♻️ superseded by CLI/config · 🚧 accepted no-op (tracked) · ➖ intentionally dropped

---

## 🧩 Editor Integrations

### Installing editor packages

Release builds produce installable editor packages as GitHub release assets:

| Editor             | Artifact                                 | Install                                                                                                              |
| ------------------ | ---------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| Visual Studio 2026 | `Typewriter.VisualStudio-<version>.vsix` | Double-click the VSIX, or run it with the Visual Studio VSIX installer, then restart Visual Studio                   |
| VS Code            | `typewriter-vscode-<version>.vsix`       | Run `code --install-extension typewriter-vscode-<version>.vsix`, or use **Extensions → ... → Install from VSIX...**  |
| JetBrains Rider    | `Typewriter-Rider-<version>.zip`         | Use **Settings/Preferences → Plugins → gear menu → Install Plugin from Disk...**, select the ZIP, then restart Rider |

The editor packages include the Typewriter CLI and language server tools used by the integrations. If you build locally, the same package names are written to `artifacts/packages`.

### 💜 VS Code

Located in [`vscode/`](vscode) ([extension README](vscode/README.md)).

**Commands:** `Typewriter: Generate Current Template`, `Generate All Templates`, `Validate Current Template`, `Restart Language Server`

**Settings:**

```json
{
  "typewriter.cliPath": "",
  "typewriter.cliArguments": [],
  "typewriter.workspacePath": null,
  "typewriter.projectPath": null,
  "typewriter.templatePath": null,
  "typewriter.framework": null,
  "typewriter.allProjects": false,
  "typewriter.generateOnSave": true,
  "typewriter.validateOnSave": true,
  "typewriter.languageServer.enabled": true,
  "typewriter.languageServer.path": "",
  "typewriter.languageServer.arguments": []
}
```

### 🟣 Visual Studio 2026

SDK-style VSIX in [`src/Typewriter.VisualStudio`](src/Typewriter.VisualStudio). **Tools → Options → Typewriter**:

| Category        | Options                                                                                                 |
| --------------- | ------------------------------------------------------------------------------------------------------- |
| CLI             | CLI path, CLI arguments                                                                                 |
| Language Server | Enabled (default ✔), path, arguments                                                                    |
| Generation      | Workspace path, project path, target framework, generate all projects, **generate on save** (default ✔) |

Diagnostics land in the **Error List**, logs in the **Output** window, and `.tst` files get classification plus Ctrl+Space completions.

### 🧠 JetBrains Rider

Frontend plugin in [`rider/`](rider) ([plugin README](rider/README.md)). It is an IntelliJ Platform plugin written in Kotlin for Rider UI concerns: `.tst` file recognition, syntax highlighting, **Tools → Typewriter** actions, project settings, and save-time generation/validation through the CLI.

The plugin also registers the Typewriter language server through the IntelliJ Platform LSP API, giving Rider the same IntelliSense as VS Code and Visual Studio: template member completion and hover with documentation, real Roslyn IntelliSense inside `${ ... }` C# helper blocks (including project-aware type completions), live diagnostics, and go-to-definition. Toggle it under **Settings → Typewriter → Enable language server IntelliSense** (enabled by default).

This is not a ReSharper backend plugin. Deep C# semantic loading remains in the shared Typewriter CLI/Roslyn engine, so the Rider plugin can stay thin unless future Rider-native inspections or refactorings require a C# backend.

### 🌐 Language Server (any LSP client)

`Typewriter.LanguageServer` speaks stdio JSON-RPC and provides live template diagnostics, completion, hover, go-to-definition, and semantic tokens. Initialization options: `workspacePath`, `projectPath`, `framework`, `allProjects`.

---

## 🧪 Samples

Feature samples and issue regressions in [`samples/`](samples) ship with checked-in TypeScript snapshots. The snapshot suite compares generated output exactly, including line endings.

| Sample                                                 | Demonstrates                                            |
| ------------------------------------------------------ | ------------------------------------------------------- |
| [`SimpleApi`](samples/SimpleApi)                       | 🍃 Basic model generation                                |
| [`NullableModels`](samples/NullableModels)             | 🎯 Nullable reference types → `T \| null`                |
| [`RecordsAndEnums`](samples/RecordsAndEnums)           | 📊 C# records and enum generation                        |
| [`MultiProjectSolution`](samples/MultiProjectSolution) | 🏗️ Multi-project workspaces and project references       |
| [`WebApiServices`](samples/WebApiServices)             | 🌐 Controllers → typed API clients, constants generation |
| [`SignalRHubs`](samples/SignalRHubs)                   | 📡 SignalR hub → typed client interfaces                 |

Issue regression samples:

| Sample                           | Regression covered                                                     |
| -------------------------------- | ---------------------------------------------------------------------- |
| [`issue66`](samples/issue66)     | Conflicting versions of the same referenced package                    |
| [`issue67`](samples/issue67)     | Projects that use source generators                                    |
| [`issue68`](samples/issue68)     | Implicit-using isolation across referenced projects                    |
| [`issue69`](samples/issue69)     | Old-style non-SDK .NET Framework project loading                       |
| [`issue69v2`](samples/issue69v2) | Legacy imported project shapes and captured design-time build failures |
| [`issue74`](samples/issue74)     | Types from transitively referenced projects                            |
| [`issue75`](samples/issue75)     | Localized resources and satellite assemblies                           |
| [`issue81`](samples/issue81)     | Cyclic metadata graphs without stack overflow                          |
| [`issue90`](samples/issue90)     | Closed generic arguments in properties and collections                 |
| [`issue96`](samples/issue96)     | Cross-file record inheritance                                          |

---

## 🔄 Migrating from the Original Typewriter

Coming from the [original VS extension](https://github.com/AdaskoTheBeAsT/Typewriter)? Your `.tst` templates should run unchanged — the differences are operational:

| Original behavior                                      | Replacement                                                                        |
| ------------------------------------------------------ | ---------------------------------------------------------------------------------- |
| 🖱 VS-only, DTE/COM based                               | Cross-platform CLI + LSP + editor adapters                                         |
| “Auto-render when C# files change” VS option           | `typewriter watch` (or editor generate-on-save)                                    |
| “Render template on save” VS option                    | VS: *Generate on save* · VS Code: `typewriter.generateOnSave`                      |
| “Add generated files to VS project”                    | Not needed — SDK-style projects glob files automatically                           |
| `settings.IncludeReferencedProjects()` etc.            | `--project`, `--all-projects`, workspace selection                                 |
| UTF-8 **with BOM** by default                          | UTF-8 **without BOM** — set `"encoding": "utf-8-bom"` to match old output          |
| Strict-null toggle via `DisableStrictNullGeneration()` | ✅ Works; set `"strictNull": false` in `typewriter.json` or call it in the template |

📋 Detailed parity tracking lives in [`compatibility.md`](compatibility.md) (template-runtime gaps) and [`implemented.md`](implemented.md) (full implementation record).

---

## 🏗️ Architecture

```text
src/
├── Typewriter.Abstractions/    # 📐 Contracts: configuration, diagnostics, metadata, results
├── Typewriter.Engine/          # ⚙️ Template parsing, rendering, compiled helpers, file writing
├── Typewriter.Roslyn/          # 🔬 C# metadata extraction (symbols, nullability, attributes, docs)
├── Typewriter.Buildalyzer/     # 🏗️ Solution/project loading without Visual Studio
├── Typewriter.Cli/             # 🛠️ System.CommandLine front end, watch mode, JSON output
├── Typewriter.LanguageServer/  # 🌐 LSP server (diagnostics, completion, hover, tokens)
└── Typewriter.VisualStudio/    # 🟣 VS 2026 VSIX adapter (bridges to the CLI)
vscode/                         # 💜 VS Code extension (bridges to CLI + LSP)
rider/                          # 🧠 JetBrains Rider frontend plugin (bridges to CLI)
samples/                        # 🧪 Sample projects with snapshot-tested output
tests/                          # ✅ Unit, CLI integration, and snapshot tests
```

**Design principles:**

- 🧱 **Editor-independent core** — the engine never references an IDE; editors shell out to the CLI or talk LSP
- 🛡️ **Safe writes** — output is planned first: paths outside the workspace and overwrites of non-generated files are refused
- 🧭 **Duplicate-output visibility** — duplicate planned output paths are reported as `TW0008` warnings before the last render wins
- 📸 **Snapshot-driven compatibility** — real recipes from `NetCoreTypewriterRecipes` are the source of truth
- ♻️ **Collectible helper assemblies** — compiled template helpers load into unloadable `AssemblyLoadContext`s so watch mode stays lean

---

## 🔨 Building from Source

**Prerequisites:**

- 🟪 .NET SDK **10.0.301** or a compatible latest .NET 10 SDK (pinned in [`global.json`](global.json))
- 🟩 Latest stable Node.js managed with Volta (VS Code extension)
- 🟦 Visual Studio 2026 (only for working on the VSIX)
- ☕ Eclipse Temurin JDK **21** (`EclipseAdoptium.Temurin.21.JDK`; the Rider plugin uses the checked-in Gradle wrapper)

Example Windows setup:

```powershell
winget install Microsoft.DotNet.SDK.10
winget install EclipseAdoptium.Temurin.21.JDK
winget install Volta.Volta
volta install node
```

```bash
# Clone with submodules (Buildalyzer fork)
git clone --recurse-submodules https://github.com/AdaskoTheBeAsT/Typewriter.git
cd Typewriter

# Restore, build, test
dotnet restore AdaskoTheBeAsT.Typewriter.slnx
dotnet build AdaskoTheBeAsT.Typewriter.slnx --configuration Release --no-restore -m:1
dotnet test AdaskoTheBeAsT.Typewriter.slnx --configuration Release --no-build -m:1

# VS Code extension
npm ci --prefix vscode
npm --prefix vscode run lint
npm --prefix vscode run bundle
pwsh ./Build-VSCodeExtension.ps1 -OutputDirectory artifacts/packages

# JetBrains Rider plugin
./rider/gradlew -p rider verifyPluginProjectConfiguration
./rider/gradlew -p rider verifyPlugin
./rider/gradlew -p rider buildPlugin

# Pack the dotnet tools
dotnet pack src/Typewriter.Cli/Typewriter.Cli.csproj --configuration Release --output artifacts/packages
dotnet pack src/Typewriter.LanguageServer/Typewriter.LanguageServer.csproj --configuration Release --output artifacts/packages

# Build the VSIX
dotnet build src/Typewriter.VisualStudio/Typewriter.VisualStudio.csproj --configuration Release -m:1
```

> ⚠️ Keep `-m:1` — parallel MSBuild is intentionally disabled for this solution.
>
> Packaging commands only create local artifacts. They do not publish to NuGet,
> Visual Studio Marketplace, or JetBrains Marketplace.

---

## 🗺 Project Status and Roadmap

| Milestone                                                            |  Status   |
| -------------------------------------------------------------------- | :-------: |
| Core engine, Roslyn metadata, first template execution               |     ✅     |
| CLI (`generate`, `validate`, `watch`, `list-templates`, JSON output) |     ✅     |
| VS Code extension + Language Server                                  |     ✅     |
| Visual Studio 2026 VSIX (AMD64 + ARM64)                              |     ✅     |
| JetBrains Rider frontend plugin                                      |     ✅     |
| Old-template compatibility hardening                                 | 🚧 ongoing |
| NuGet publishing for CLI and language-server tools                   |     ✅     |
| GitHub release assets for VS Code, Visual Studio, and Rider          |     ✅     |
| Visual Studio / VS Code / JetBrains Marketplace publishing           | 📋 planned |

- 📗 [`implemented.md`](implemented.md) — everything that is done, in detail
- 📙 [`to_implement.md`](to_implement.md) — roadmap and backlog
- 📕 [`compatibility.md`](compatibility.md) — original-Typewriter parity status

---

## Changelog

### 4.9.0

- additional release to fix publish error

### 4.8.0

- Added opt-in date-library profiles for native `Date`, Temporal, Moment, Luxon, date-fns, Day.js, and js-joda through `output.dateLibrary` and `Settings.UseDateLibrary(...)`.
- Added semantic date mapping for System and NodaTime values, including distinct elapsed `Duration` and calendar `Period` kinds plus year-month and month-day values.
- Added `AdaskoTheBeAsT.Typewriter.Annotations` with member-level semantic overrides for ambiguous values such as `DateTime`.
- Added `GenerateFrontendTypeAttribute`, `AsStringAttribute`, `LabelForEnumAttribute`, and `CustomNameAttribute` to the Annotations package so recipes can reference a canonical NuGet package instead of copy-pasting attribute definitions.
- Added `guidInitializer` / `UseGuidInitializer(...)` and `decimalInitializer` / `UseDecimalInitializer(...)`, with automatic defaults for string and `Uint8Array` UUIDs, numeric decimals, and Decimal.js.
- Preserved the 4.7.0 generated output and low-level date settings under the default `legacy` profile.
- Documented the decision in [`docs/adr/0001-semantic-date-library-profiles.md`](docs/adr/0001-semantic-date-library-profiles.md).

### 4.7.0

- Added a much richer shared Language Server experience for `.tst` templates: documented completions and hover, semantic highlighting, go-to-definition, project-aware template completions, and Roslyn-backed IntelliSense inside `${ ... }` C# helper blocks.
- Added VS Code embedded-language forwarding through virtual C# and TypeScript documents, so the installed C# extension and TypeScript service can provide completion, hover, and definitions inside template helper/output regions. This is controlled by `typewriter.embeddedLanguages.forwarding`.
- Added Rider LSP integration through the IntelliJ Platform LSP API, allowing Rider to use the shared Typewriter language server while retaining CLI-based generation and validation fallbacks.
- Fixed `Settings.IncludeProject(name)` so templates can explicitly merge a named workspace project, plus its referenced projects, into the current template code model. Missing projects now raise `TW0009`, and `samples/issue98` covers the unreferenced-project scenario.
- Improved watch mode and editor generate-on-save performance with changed-input tracking. `typewriter watch`, IDE integrations, and the language server can now pass changed paths through `--changed` / `changedInputs`, and the default `generation.incremental: "auto"` mode re-renders only affected outputs when only C# source files changed.
- Added safe full-generation fallbacks for incremental runs when change provenance is unknown, the changed input is a template/project/config file, a file was deleted or renamed, or incremental generation is disabled with `generation.incremental: "off"`.
- Improved Roslyn metadata caching for incremental generation with dirty-path invalidation, source-only rebuilds, syntax-tree and type-reference reuse, reverse dependency indexing, and optional metadata cache metrics in performance traces.
- Added configuration/schema support for the new `generation.incremental` setting and CLI parsing coverage for repeated `--changed` arguments.
- Added regression coverage for embedded-language services, LSP embedded document/range requests, `IncludeProject`, incremental rendering, dirty-path metadata refresh, and editor changed-path plumbing.
- Added release and maintenance documentation for compatibility status, implemented work, packaging, Rider installation, and generation performance planning/progress.

### 4.6.1

- Fixed cross-file record inheritance so `Record.BaseRecord` resolves when a derived record and its base record are declared in separate source files.
- Fixed Visual Studio generate-on-save to skip saved inputs only when neither the project nor workspace contains `.tst` templates, preserving templates stored outside the saved file's project, and to tolerate documents that do not expose project metadata through DTE.
- Fixed project-load diagnostics for legacy non-SDK `.csproj` files with unresolved `<Import>` elements: Buildalyzer may report success with zero source files while capturing an MSBuild import error, which is now surfaced as a `TW0003` diagnostic instead of being silently ignored.
- Added a JSON Schema (`typewriter.schema.json`) for `typewriter.json` configuration files; `typewriter init` now emits a `$schema` reference for editor validation and autocomplete.
- Added `--diff` CLI option to include unified diffs for changed files in both text and JSON output, including line-ending-only and final-newline-only changes.
- Added support for passing a template directory to `--template`.
- Added exact snapshot coverage and individual documentation for every issue regression under `samples/`.

### 4.6.0

- Added configurable date-time, date-only, and time-only TypeScript mappings and default initializers with `output.dateType`, `output.dateOnlyType`, `output.timeOnlyType`, their initializer settings, and matching template `Settings` methods.
- Added NodaTime date/time mapping support for `Instant`, `LocalDate`, `LocalTime`, `LocalDateTime`, `OffsetDate`, `OffsetTime`, `OffsetDateTime`, and `ZonedDateTime`.
- Added configurable C# `Guid` TypeScript mapping with `output.guidType` and `settings.UseGuidType(...)`, defaulting to `string` and allowing aliases such as `uuid` or `UUID`.
- Documented DateOnly, TimeOnly, TimeSpan, NodaTime, Duration, Period, and runtime conversion guidance, including the need for [date-interceptors](https://github.com/adaskothebeast/date-interceptors) when JSON strings should become date library objects.

### 4.5.4

- Restored detailed project-load diagnostics for Buildalyzer/MSBuild failures, including file, line, column, and underlying MSBuild error text in `TW0003` JSON diagnostics.
- Restored full JSON result output for the Visual Studio persistent language-server generation path, so VS Output includes detailed diagnostics instead of only a diagnostic count.
- Added regression coverage for Buildalyzer project-load diagnostics through the loader, CLI JSON output, and language-server generation service.

### 4.5.3

- Fixed primitive collection imports so `List<int>` and `IList<int>` no longer generate invalid imports such as `import { number } from "./number";`.
- Changed `DateTime`, `DateTimeOffset`, and `DateOnly` TypeScript mapping from `string` to `Date` by default, with `output.dateType` or `settings.UseDateType(...)` available for Moment, Luxon, js-joda, Day.js, date-fns, or other compatible date types.
- Added configurable C# `decimal` mapping, defaulting to `number` with `output.decimalType` or `settings.UseDecimalType(...)` available for `decimal.js`.
- Fixed generic `$type` initializers by exposing generic type parameters in generated full names, for example `Sample.Box<T>`.
- Fixed dictionary key mapping so `IDictionary<int, string>` emits `Record<number, string>` instead of `Record<string, string>`.

### 4.5.2

- Fixed closed generic type mapping so custom generic types keep their arguments, for example `Box<int>` now emits `Box<number>`, `List<Box<int>>` emits `Box<number>[]`, and `Dictionary<string, Box<int>>` emits `Record<string, Box<number>>`.
- Fixed IDE generate-on-save for C# source changes when templates are stored outside the saved file's project directory.
- Fixed duplicate assembly identity loading during analyzer/source-generator and template helper resolution, avoiding intermittent "assembly already loaded" failures.
- Updated the Rider plugin build to IntelliJ Platform Gradle Plugin `2.17.0`.

### 4.5.0

- **Dramatically improved generation performance**, especially for watch mode and IDE generate-on-save workflows. Typewriter now reuses persistent generation processes, scopes template discovery to the owning project, and caches parsed templates, project metadata indexes, glob matchers, generated-file contents, type mappings, and loaded dependencies.
- Reduced unnecessary rendering, formatting, diagnostics, project traversal, and disk I/O. Editor integrations use the faster persistent path when available and retain the CLI as a compatibility fallback.

### 4.4.0

- Fixed XML doc-comment extraction so inline elements (such as `<see cref="..."/>`) are preserved in `$DocComment[$Summary]` and `$DocComment[$Returns]` instead of being stripped to their text content, so referenced type names are no longer lost in generated comments.
- Added an opt-in `Typewriter.Extensions.Documentation` formatter with `DocComment.ToJsDoc()`, `ToJsDocSummary()`, and `ToJsDocReturns()` helpers that convert C# XML doc tags to TypeScript JSDoc (for example `<see cref="GeometryFieldType"/>` becomes `{@link GeometryFieldType}`, and `<c>`/`<see langword>`/`<paramref>` become backtick code). The metadata stays raw; conversion is applied only when a template calls these helpers.

### 4.3.0

- Added property and instance-field initializer values to the metadata model, exposed to templates as `$Value` (for example `$Properties[$Value]` and `$Fields[$Value]`).
- Initializer values come from the source declaration: literals render as their text value, non-literal initializers (such as `Guid.NewGuid()`) render as the expression text, and members without an initializer render empty.
- Added Roslyn extraction and renderer support plus language-server completion documentation for the new `$Value` member, covered by engine and Roslyn tests.

### 4.2.0

- Restored the editor right-click commands **Render Template** and **Generate Current Template** so a template can be regenerated on demand from the context menu.
- Fixed a stack overflow that occurred while extracting metadata for types with cyclic references.

### 4.1.1

- Fixed resolution of types that come from transitively referenced projects.
- Fixed remaining metadata-loading issues for older full .NET Framework projects.
- Fixed handling of satellite (localized) assemblies during project loading.

### 4.1.0

- Added struct metadata and template rendering through `$Structs[...]`, `$Types(Struct)[...]`, CodeModel `Struct`, and LSP/editor completions.
- Added indexer metadata through `Property.IsIndexer` and property-level `$Parameters[...]`, intended for lookup-style API generation rather than JSON DTO payloads.
- Added literal dollar escaping with `$$`, so templates can emit `$type`, `${...}`, and other TypeScript dollar syntax without triggering template member lookup.
- Fixed processing of full .NET Framework projects.
- Fixed implicit `using` resolution for referenced projects.
- Fixed loading of projects that use source generators.
- Fixed conflicts when two projects reference different versions of the same package.
- Added tests that cover Roslyn extraction, template rendering, CodeModel parity, and language-server completions for structs and indexers.

### 4.0.0

- First stable release of the ground-up, cross-platform reimplementation: a shared Roslyn-powered engine with a CLI, Language Server, VS Code extension, Visual Studio 2026 VSIX, and JetBrains Rider plugin.
- Documentation and packaging refinements over `4.0.0-beta.1`.

### 4.0.0-beta.1

- Initial public preview of the new toolchain: `typewriter init`, `generate`, `validate`, `watch`, and `list-templates`, with `typewriter.json` configuration and discovery.
- Editor-independent engine running the original `.tst` dialect, plus compiled C# helpers, shared `#load` source helpers, and NuGet `#r` references.
- Safe output planning (`TW0005`/`TW0006`/`TW0008`), template logging (`TW0007`), and deterministic CLI exit codes with text/JSON output.
- Language Server features: live diagnostics, completion, hover, go-to-definition, and semantic tokens.

---

## 🤝 Contributing

1. 🍴 Fork and create a feature branch: `git checkout -b feature/amazing-feature`
2. 🧹 Match the existing style — analyzers are wired through [`Directory.Build.props`](Directory.Build.props), `.editorconfig`, and StyleCop
3. ✅ Add tests (unit, CLI integration, or snapshot) and make `dotnet test ... -m:1` pass
4. 📸 For template-compatibility work, prefer **real recipe fixtures** over synthetic cases
5. 🚀 Open a pull request with a clear description

---

## 🐛 Issues and Support

- 🐞 **Bugs and feature requests:** [GitHub Issues](https://github.com/AdaskoTheBeAsT/Typewriter/issues)
- ❓ **Questions:** [Stack Overflow `typewriter` tag](https://stackoverflow.com/questions/tagged/typewriter)
- 🍳 **Template recipes:** [NetCoreTypewriterRecipes](https://github.com/AdaskoTheBeAsT/NetCoreTypewriterRecipes)

---

## 🙏 Acknowledgments

- **Fredrik Hagnelius** — creator of the original [Typewriter](https://github.com/frhagn/Typewriter)
- **[AdaskoTheBeAsT/Typewriter](https://github.com/AdaskoTheBeAsT/Typewriter)** — the maintained VS extension fork this project grew from
- **[Buildalyzer](https://github.com/phmonte/Buildalyzer)** — design-time project analysis (vendored fork in [`Buildalyzer/`](Buildalyzer))
- The **.NET and TypeScript communities** ❤️

---

<div align="center">

**Made with ❤️ by the community, for the community**

[⬆ Back to top](#%EF%B8%8F-typewriter--generate-typescript-from-c-everywhere)

</div>
