# ⌨️ Typewriter CLI

Generate TypeScript from C# projects with Typewriter `.tst` templates, from your terminal, editor task, or CI pipeline.

## ✨ Why developers use it

- 🔄 **Keep contracts in sync**: regenerate TypeScript DTOs, API clients, SignalR contracts, and other template-driven output from real Roslyn metadata.
- 🚦 **CI-friendly by design**: deterministic exit codes, JSON output, dry runs, and warning-as-failure support.
- 👀 **Fast local feedback**: `watch` mode tracks C#, project, solution, configuration, and `.tst` changes.
- 🧩 **Flexible workspaces**: point it at a folder, project, or solution, then generate one project or every project.

## 🚀 Install

```bash
dotnet tool install --global AdaskoTheBeAsT.Typewriter.Cli
```

Then run:

```bash
typewriter --help
```

The official Typewriter plugins for Visual Studio, VS Code, and JetBrains Rider include this CLI automatically. Install it globally when you want direct terminal, CI, or custom tooling access.

## ⚡ Quick start

```bash
typewriter init --workspace .
typewriter generate --workspace .
typewriter watch --workspace .
```

## 🛠️ Commands

| Command | What it does |
| --- | --- |
| `typewriter` | Runs `generate` with the provided options. |
| `typewriter init` | Creates a default `typewriter.json`. |
| `typewriter generate` | Generates TypeScript files from discovered or selected `.tst` templates. |
| `typewriter validate` | Loads project metadata and validates templates without writing files. |
| `typewriter watch` | Watches inputs and regenerates after changes. |
| `typewriter list-templates` | Lists discovered templates for the workspace. |

## 🎛️ Switches

| Switch | Commands | Description |
| --- | --- | --- |
| `--workspace <path>` | all | Solution, project, or folder to operate on. |
| `--project <path>` | generate, validate, watch, list-templates | C# project to read metadata from. |
| `--template <path>` | generate, validate, watch, list-templates | Single `.tst` template file or template directory to use. |
| `--template-search-path <path>` | generate, validate, watch, list-templates | Restrict template discovery without changing workspace or project context. |
| `--framework <tfm>` | generate, validate, watch, list-templates | Target framework to use when loading project metadata, for example `net10.0`. |
| `--all-projects` | generate, validate, watch, list-templates | Process every project in a multi-project workspace. |
| `--output text\|json` | generate, validate, watch, list-templates | Choose human-readable text or machine-readable JSON output. |
| `--dry-run` | generate, validate, watch, list-templates | Render and validate without writing generated files. |
| `--diff` | generate, validate, watch | Include unified diffs for changed files, including line-ending changes. |
| `--fail-on-warning` | generate, validate, watch, list-templates | Return a non-zero exit code when warnings are emitted. |
| `--force` | init | Overwrite an existing `typewriter.json`. |

## 💡 Examples

```bash
# Generate for a solution
typewriter generate --workspace ./MyApp.sln

# Validate in CI and fail on warnings
typewriter validate --workspace . --output json --fail-on-warning

# Generate from one project and one template
typewriter generate --project ./src/MyApi/MyApi.csproj --template ./templates/contracts.tst

# Watch every project in a workspace
typewriter watch --workspace . --all-projects
```

## ✅ Best fit

Use the CLI when you want repeatable TypeScript generation in local scripts, build steps, pull request checks, or any environment where an IDE should not be required.
