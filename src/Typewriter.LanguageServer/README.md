# 🌐 Typewriter Language Server

Bring first-class editor intelligence to Typewriter `.tst` templates with a stdio JSON-RPC Language Server Protocol tool.

## ✨ Why developers use it

- ⚡ **Catch template issues while typing** with live diagnostics.
- 🧠 **Understand the template model** with completion, hover, go-to-definition, and semantic highlighting.
- 🔬 **Use the same brain as generation** because analysis runs through the shared Typewriter engine and Roslyn metadata services.
- 🛡️ **Safe editor feedback** because the language server validates templates without writing generated files.

## 🚀 Install

```bash
dotnet tool install --global AdaskoTheBeAsT.Typewriter.LanguageServer
```

Editor integrations and custom LSP clients should launch:

```bash
typewriter-lsp
```

The official Typewriter plugins for Visual Studio, VS Code, and JetBrains Rider include this language server automatically. Install it globally only when you are wiring up a custom LSP client or integration.

## 🧰 Capabilities

| Capability | What developers get |
| --- | --- |
| Diagnostics | Template parse errors, metadata load issues, and Typewriter diagnostics in the editor Problems panel. |
| Completion | Context-aware suggestions triggered by `$`, `(`, `[`, and `.`. |
| Hover | Quick information for template members and embedded C# regions. |
| Go to definition | Navigation from template references to the matching source location when available. |
| Semantic tokens | Rich `.tst` highlighting for template syntax and embedded language regions. |
| Document sync | Open, change, save, and close notifications for responsive validation. |
| Persistent generation | `typewriter/generate` reuses project metadata and template caches across editor commands and save events. |

## 🎛️ Configuration

`typewriter-lsp` is configured by the LSP client through `initialize` parameters. It does not expose user-facing CLI switches.

### Initialization options

```json
{
  "workspacePath": ".",
  "projectPath": "./src/MyApi/MyApi.csproj",
  "framework": "net10.0",
  "allProjects": false
}
```

| Option | Description |
| --- | --- |
| `workspacePath` | Solution, project, or folder used as the Typewriter workspace. Defaults to the LSP root or the current document folder. |
| `projectPath` | Optional C# project used for metadata loading. Relative paths resolve from the workspace. |
| `framework` | Optional target framework used when loading metadata, for example `net10.0`. |
| `allProjects` | Analyze every project in a multi-project workspace. Defaults to `false`. |

## Persistent generation

Official IDE adapters send `typewriter/generate` with `command`, `workspacePath`, `projectPath`, `templatePath`, `templateSearchPath`, `framework`, and `allProjects`. Save-triggered source generation uses `templateSearchPath` to restrict discovery to the owning project without changing solution-level configuration or metadata context. The response uses the CLI JSON result shape without embedding generated contents. Visual Studio can lazily start a package-scoped server when its editor LSP is inactive. Adapters fall back to the CLI when the request is unavailable.

## ✅ Best fit

Use the language server when building an editor extension, wiring Typewriter into an LSP-capable editor, or giving template authors fast feedback without running generation manually.
