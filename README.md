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
| Packaging         | VSIX releases                                                                          | NuGet tool packages, language-server package, VSIX, VS Code VSIX, and Rider plugin artifacts from CI                                                                            |

### New goodies at a glance

- **Run it anywhere**: generate from a terminal, build server, VS Code, Visual Studio, Rider, or another LSP editor.
- **Watch mode without IDE magic**: `typewriter watch` regenerates when C# or `.tst` files change.
- **Safer generated output**: `TW0005` blocks escaping the workspace, `TW0006` protects hand-written files, and `TW0008` exposes duplicate output paths.
- **Better large-template ergonomics**: `Template(Settings settings, File file)` lets templates count and pre-filter types before rendering, while `OnRenderComplete(File file)` supports final summary logging.
- **Shared helper files**: `#load "shared-helpers.cs"` or `#load "https://raw.githubusercontent.com/..."` keeps large templates maintainable and reusable.
- **Configurable output style**: line endings, UTF-8 BOM, indentation, trailing whitespace, final newline, quote style, strict null generation, and file naming are all configuration-driven.
- **Better diagnostics**: template logs surface as `TW0007`, helper failures include template method/line details, and editors show diagnostics live.
- **Modern C# metadata**: Roslyn metadata covers records, nullable reference types, generics, tuples, doc comments, attributes, static constants, static readonly fields, events, delegates, nested types, and Web API helpers.
- **Web API hardening**: route helpers ignore named-only HTTP attribute arguments, honor `Template = "..."`, and safely encode nullable string/date query values.
- **Recipe-backed compatibility**: real Angular/React recipes from `NetCoreTypewriterRecipes` are snapshot-tested so old-template support improves against production templates, not toy examples.

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
        public createdAt: string
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
| `watch`          | 👀 Watch C# projects and templates, regenerate on change (debounced)       |
| `list-templates` | 📋 List the `.tst` templates discovered for the workspace                  |

### Options

| Option                | Description                                             |
| --------------------- | ------------------------------------------------------- |
| `--workspace <path>`  | Solution, project, or folder to operate on              |
| `--project <path>`    | Specific C# project to read                             |
| `--template <path>`   | A single template file or template directory            |
| `--framework <tfm>`   | Target framework for metadata loading (e.g. `net9.0`)   |
| `--all-projects`      | Generate for every project in a multi-project workspace |
| `--output text\|json` | Output format (use `json` in CI and tooling)            |
| `--dry-run`           | Render everything, write nothing                        |
| `--fail-on-warning`   | Non-zero exit code when warnings are emitted            |

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

---

## ⚙️ Configuration: `typewriter.json`

Generated by `typewriter init`. All values below are the defaults:

```json
{
  "templates": ["**/*.tst"],
  "exclude": ["**/bin/**", "**/obj/**", "**/node_modules/**"],
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
    "quoteStyle": "double"
  },
  "diagnostics": {
    "failOnWarning": false
  }
}
```

### Top-level settings

| Key                      | Default                            | Description                                           |
| ------------------------ | ---------------------------------- | ----------------------------------------------------- |
| `templates`              | `["**/*.tst"]`                     | Glob patterns used to discover templates              |
| `exclude`                | `bin`, `obj`, `node_modules` globs | Glob patterns excluded from discovery                 |
| `defaultTargetFramework` | `null`                             | TFM used when a project multi-targets (e.g. `net9.0`) |

### `output` settings

| Key                      | Default      | Description                                                                                                                                              |
| ------------------------ | ------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `newline`                | `"lf"`       | `lf` or `crlf` line endings in generated files                                                                                                           |
| `encoding`               | `"utf-8"`    | `utf-8` (no BOM) or `utf-8-bom` ⚠️ the original Typewriter emitted a BOM by default — set `utf-8-bom` for byte-identical output                           |
| `writeOnlyWhenChanged`   | `true`       | Skip disk writes when content is unchanged (keeps file watchers and HMR calm)                                                                            |
| `dryRun`                 | `false`      | Render without writing files                                                                                                                             |
| `fileNameConvention`     | `"preserve"` | Output file naming: `preserve`, `kebab`, `pascal`, `camel`, `snake`                                                                                      |
| `strictNull`             | `true`       | Render nullable types as `T \| null`; templates can override via `settings.DisableStrictNullGeneration()`                                                |
| `indentStyle`            | `"preserve"` | Re-indent generated output: `preserve`, `space`, or `tab` (the rendered indent unit is detected automatically)                                           |
| `indentSize`             | `4`          | Target indent width used when `indentStyle` is `space`                                                                                                   |
| `insertFinalNewline`     | `false`      | Ensure generated files end with a single trailing newline                                                                                                |
| `trimTrailingWhitespace` | `false`      | Strip trailing spaces and tabs from every generated line                                                                                                 |
| `quoteStyle`             | `"double"`   | Default string literal character for generated defaults: `double`, `single`, or `backtick`; `settings.UseStringLiteralCharacter(...)` in a template wins |

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

| Variable                              | Overrides                                      |
| ------------------------------------- | ---------------------------------------------- |
| `TYPEWRITER_DEFAULT_TARGET_FRAMEWORK` | `defaultTargetFramework`                       |
| `TYPEWRITER_OUTPUT_NEWLINE`           | `output.newline`                               |
| `TYPEWRITER_FAIL_ON_WARNING`          | `diagnostics.failOnWarning` (`true`/`1`/`yes`) |

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

| Setting                                                                                                     | Status | Notes                                                                                                           |
| ----------------------------------------------------------------------------------------------------------- | :----: | --------------------------------------------------------------------------------------------------------------- |
| `OutputExtension`                                                                                           |   ✅    | Default `.ts`                                                                                                   |
| `OutputFilenameFactory`                                                                                     |   ✅    | Per-source fan-out and no-match suppression supported                                                           |
| `OutputDirectory`                                                                                           |   ✅    | Relative paths resolve against the template location                                                            |
| `SingleFileMode("name.ts")`                                                                                 |   ✅    | Renders the whole template into one file                                                                        |
| `UseStringLiteralCharacter('\'')`                                                                           |   ✅    | Affects generated string literals and defaults; defaults from `output.quoteStyle` in `typewriter.json`          |
| `TemplatePath`                                                                                              |   ✅    | Full path of the executing template                                                                             |
| `Log` (`ILog`)                                                                                              |   ✅    | Messages surface as `TW0007` diagnostics in CLI output and editors                                              |
| `IncludeCurrentProject()` / `IncludeReferencedProjects()` / `IncludeAllProjects()` / `IncludeProject(name)` |   ♻️    | Accepted for compatibility; project scope is now controlled by `--project`, `--all-projects`, and the workspace |
| `DisableStrictNullGeneration()`                                                                             |   ✅    | Defaults from `output.strictNull` in `typewriter.json`; calling this in the template overrides it               |
| `DisableUtf8BomGeneration()` / `Utf8BomGeneration`                                                          |   ✅    | Defaults from `output.encoding` in `typewriter.json`; the template override wins per file                       |
| `PartialRenderingMode`                                                                                      |   🚧    | Accepted but not wired yet                                                                                      |
| `SolutionFullName`                                                                                          |   ✅    | Populated from the resolved workspace (solution or folder path)                                                 |
| `SkipAddingGeneratedFilesToProject`                                                                         |   ➖    | Obsolete — SDK-style projects include files automatically                                                       |

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

This is not a ReSharper backend plugin. Deep C# semantic loading remains in the shared Typewriter CLI/Roslyn engine, so the Rider plugin can stay thin unless future Rider-native inspections or refactorings require a C# backend.

### 🌐 Language Server (any LSP client)

`Typewriter.LanguageServer` speaks stdio JSON-RPC and provides live template diagnostics, completion, hover, go-to-definition, and semantic tokens. Initialization options: `workspacePath`, `projectPath`, `framework`, `allProjects`.

---

## 🧪 Samples

Each sample in [`samples/`](samples) ships with checked-in TypeScript snapshots used by the test suite:

| Sample                                                 | Demonstrates                                            |
| ------------------------------------------------------ | ------------------------------------------------------- |
| [`SimpleApi`](samples/SimpleApi)                       | 🍃 Basic model generation                                |
| [`NullableModels`](samples/NullableModels)             | 🎯 Nullable reference types → `T \| null`                |
| [`RecordsAndEnums`](samples/RecordsAndEnums)           | 📊 C# records and enum generation                        |
| [`MultiProjectSolution`](samples/MultiProjectSolution) | 🏗️ Multi-project workspaces and project references       |
| [`WebApiServices`](samples/WebApiServices)             | 🌐 Controllers → typed API clients, constants generation |
| [`SignalRHubs`](samples/SignalRHubs)                   | 📡 SignalR hub → typed client interfaces                 |

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
git clone --recurse-submodules https://github.com/AdaskoTheBeAsT/AdaskoTheBeAsT.Typewriter.git
cd AdaskoTheBeAsT.Typewriter

# Restore, build, test
dotnet restore AdaskoTheBeAsT.Typewriter.slnx
dotnet build AdaskoTheBeAsT.Typewriter.slnx --configuration Release --no-restore -m:1
dotnet test AdaskoTheBeAsT.Typewriter.slnx --configuration Release --no-build -m:1

# VS Code extension
npm ci --prefix vscode
npm --prefix vscode run lint
npm --prefix vscode run bundle
npm --prefix vscode run package -- --out ../artifacts/packages/typewriter-vscode-3.0.0.vsix

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
| NuGet / Marketplace publishing                                       | 📋 planned |

- 📗 [`implemented.md`](implemented.md) — everything that is done, in detail
- 📙 [`to_implement.md`](to_implement.md) — roadmap and backlog
- 📕 [`compatibility.md`](compatibility.md) — original-Typewriter parity status

---

## Changelog

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

- 🐞 **Bugs and feature requests:** [GitHub Issues](https://github.com/AdaskoTheBeAsT/AdaskoTheBeAsT.Typewriter/issues)
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
