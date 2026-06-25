# Typewriter VS Code Extension

VS Code adapter for Typewriter `.tst` templates, backed by the Typewriter CLI and language server.

## Features

- Associates `.tst` files with the Typewriter language.
- Adds basic `.tst` syntax highlighting.
- Provides `Typewriter: Generate Current Template`.
- Provides `Typewriter: Generate All Templates`.
- Provides `Typewriter: Validate Current Template`.
- Reuses the active language-server process for generation and validation, with CLI fallback, and publishes diagnostics to the Problems panel.
- Restricts source-file save generation to templates under the owning C# project.
- Starts the Typewriter language server for live `.tst` diagnostics, completion, hover, definition, and semantic highlighting support.
- Provides semantic highlighting and basic context-aware completions for Typewriter template expressions, C# helper blocks, and TypeScript output regions.
- Provides fallback Ctrl+Space completions when the language server is unavailable or still starting.
- Provides `Typewriter: Restart Language Server`.

## Package

- Version: `3.0.0`.
- Packaged entrypoint: `dist/extension.js`, created by `npm --prefix vscode run bundle`.
- `src/` is excluded from the VSIX.
- Icon, license, changelog, repository, homepage, and issue metadata are included.

## CLI Resolution

By default the extension:

1. Uses the local repo CLI project when `src/Typewriter.Cli/Typewriter.Cli.csproj` is available.
2. Falls back to `typewriter` on `PATH`.

Override this with:

```json
{
  "typewriter.cliPath": "typewriter"
}
```

For a custom `dotnet` invocation:

```json
{
  "typewriter.cliPath": "dotnet",
  "typewriter.cliArguments": [
    "run",
    "--project",
    "D:/GitHub/AdaskoTheBeAsT.Typewriter/src/Typewriter.Cli/Typewriter.Cli.csproj",
    "--"
  ]
}
```

## Workspace Settings

```json
{
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

## Language Server

By default the extension starts the local repo language server project when
`src/Typewriter.LanguageServer/Typewriter.LanguageServer.csproj` is available,
then falls back to `typewriter-lsp` on `PATH`.

Override this with:

```json
{
  "typewriter.languageServer.enabled": true,
  "typewriter.languageServer.path": "typewriter-lsp",
  "typewriter.languageServer.arguments": []
}
```

## Build and package

```powershell
npm ci --prefix vscode
npm --prefix vscode run check
npm --prefix vscode run bundle
npm --prefix vscode run package -- --out ../artifacts/packages/typewriter-vscode-3.0.0.vsix
```
