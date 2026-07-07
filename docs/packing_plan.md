# Typewriter Packaging Plan

Implemented and verified locally against the repository on July 2, 2026.

Local commands and the CI workflow only create artifacts. The tag-triggered release workflow publishes the CLI and language-server packages to NuGet and attaches editor packages to a GitHub release. Marketplace publishing remains separate.

## Package Matrix

| Product | Installable artifact | Marketplace | Current state |
|---|---|---|---|
| Typewriter CLI | `.nupkg` | NuGet | Published by the tag release workflow |
| Typewriter language server | `.nupkg` | NuGet | Published by the tag release workflow |
| Visual Studio | `.vsix` | GitHub release; Visual Studio Marketplace planned | Visual Studio 2026 package with architecture-neutral CLI/LSP payload |
| Visual Studio Code | `.vsix` | GitHub release; VS Code Marketplace planned | Bundled package configured |
| JetBrains Rider | `.zip` | GitHub release; JetBrains Marketplace planned | Gradle wrapper and verification configured |

All locally built artifacts are copied to `artifacts/packages/`.

## Versioning

Use one release version for every package. `setver.ps1` synchronizes it in:

| File | Version source |
|---|---|
| `Directory.Build.props` | `Version`, `AssemblyVersion`, and `FileVersion` |
| `src/Typewriter.VisualStudio/source.extension.vsixmanifest` | VSIX identity |
| `vscode/package.json` | Extension package |
| `rider/gradle.properties` | Plugin package |

Before a future release, build all artifacts from the same commit and tag. Never
publish different content under an already published version.

`setver.ps1` receives the version once and updates all package version sources.

## Visual Studio

### Current package

Build:

```powershell
dotnet build src/Typewriter.VisualStudio/Typewriter.VisualStudio.csproj `
  --configuration Release `
  -m:1
```

Output:

```text
src/Typewriter.VisualStudio/bin/Release/net472/Typewriter.VisualStudio.vsix
```

The existing CI already builds and uploads this artifact.

### Supported Visual Studio version

The current manifest uses version range `[18.0,19.0)`. This means the VSIX targets
Visual Studio 2026/Dev18, not Visual Studio 2022/Dev17.

Visual Studio 2026 is the intended and documented target.

### ARM64 assessment

The current manifest correctly declares both `amd64` and `arm64` installation
targets. The built extension assembly was inspected and is IL-only AnyCPU:

```text
Typewriter.VisualStudio.dll: I386 PE, ILOnly
```

The CLI and language-server payloads are now published with `UseAppHost=false`.
The VSIX contains their managed `.dll`, `.deps.json`, `.runtimeconfig.json`, and
dependencies without x64 apphosts. The adapter launches them through the matching
system `dotnet` host.

### ARM64 implementation

The implemented architecture-neutral VSIX:

1. Publishes the CLI and language server with `UseAppHost=false`.
2. Packages their managed payloads and runtime configuration.
3. Does not include architecture-specific apphost executables.
4. Launches them as:

```text
dotnet Typewriter.Cli.dll
dotnet Typewriter.LanguageServer.dll
```

The package requires a matching .NET 10 runtime on AMD64 or ARM64. Final ARM64
release qualification still requires an install-and-run smoke test on ARM64
hardware or an ARM64 CI runner.

### Visual Studio release checks

Before release:

```powershell
dotnet build src/Typewriter.VisualStudio/Typewriter.VisualStudio.csproj `
  --configuration Release `
  -m:1
```

Then test installation and these workflows on every declared architecture and
Visual Studio major version:

- Open a `.tst` file.
- Confirm highlighting and completion.
- Run Generate Current Template.
- Run Generate All Templates.
- Confirm language-server diagnostics.
- Confirm generate-on-save.
- Inspect the Typewriter Output pane and Error List.

Do not advertise ARM64 support based only on the manifest. Execute this smoke test
on an ARM64 machine or ARM64 CI runner.

## Visual Studio Code

### Package format

VS Code extensions are packaged as VSIX files with the official `@vscode/vsce`
tool:

```powershell
cd vscode
npm ci
npx @vscode/vsce package
```

Expected output:

```text
vscode/typewriter-vscode-<version>.vsix
```

Install locally:

```powershell
code --install-extension vscode/typewriter-vscode-<version>.vsix --force
```

### Bundling

The extension is bundled before running `vsce`:

1. `@vscode/vsce` and `esbuild` are development dependencies.
2. `src/extension.js` is bundled to `dist/extension.js`.
3. The `vscode` module remains external.
4. `package.json` points `main` to `./dist/extension.js`.
5. `node_modules/` and source files are excluded from the VSIX.

Suggested scripts:

```json
{
  "scripts": {
    "check": "node --check src/extension.js",
    "bundle": "esbuild src/extension.js --bundle --platform=node --external:vscode --outfile=dist/extension.js",
    "vscode:prepublish": "npm run bundle",
    "package": "vsce package"
  }
}
```

The package also contains repository metadata, a license, a 256px PNG icon, and a
changelog.

### Platform handling

The current extension JavaScript has no native Node dependency and does not bundle
the Typewriter CLI. Produce one universal VSIX; do not pass `--target`.

If native binaries or native Node modules are bundled later, produce separate
packages with targets such as:

```powershell
vsce package --target win32-x64
vsce package --target win32-arm64
vsce package --target linux-x64
vsce package --target linux-arm64
vsce package --target darwin-x64
vsce package --target darwin-arm64
```

### VS Code release checks

```powershell
npm ci --prefix vscode
npm --prefix vscode run check
npm --prefix vscode run bundle
npx --yes @vscode/vsce ls --tree
npx --yes @vscode/vsce package --allow-missing-repository
```

Inspect the `vsce ls --tree` output and confirm that `dist/extension.js` is present.
Install the resulting VSIX in a clean Extension Development Host or clean VS Code
profile and test:

- Extension activation after opening a `.tst` file.
- Generate Current Template.
- Generate All Templates.
- Validate Current Template.
- Restart Language Server.
- Diagnostics, completion, hover, definition, and semantic highlighting.
- Generate-on-save and validate-on-save.

No `vsce publish` command is part of this workflow.

## JetBrains Rider

### Package format

JetBrains plugins are distributed as ZIP archives. The IntelliJ Platform Gradle
Plugin `buildPlugin` task creates the archive under:

```text
rider/build/distributions/
```

Build:

```powershell
gradle -p rider buildPlugin
```

Expected output:

```text
rider/build/distributions/Typewriter-<version>.zip
```

Install locally in Rider:

1. Open Settings.
2. Select Plugins.
3. Open the gear menu.
4. Select Install Plugin from Disk.
5. Select the generated ZIP.
6. Restart Rider.

Do not extract or re-zip the generated distribution manually.

### Gradle wrapper

The repository contains:

```text
rider/gradlew
rider/gradlew.bat
rider/gradle/wrapper/gradle-wrapper.jar
rider/gradle/wrapper/gradle-wrapper.properties
```

Use:

```powershell
.\rider\gradlew.bat -p rider buildPlugin
```

The wrapper uses Gradle `9.0.0` with IntelliJ Platform Gradle Plugin `2.16.0`.

### Rider validation

Run the available Gradle verification tasks before packaging:

```powershell
.\rider\gradlew.bat -p rider verifyPluginProjectConfiguration
.\rider\gradlew.bat -p rider test
.\rider\gradlew.bat -p rider verifyPlugin
.\rider\gradlew.bat -p rider buildPlugin
```

Use `runIde` for an isolated local Rider instance:

```powershell
.\rider\gradlew.bat -p rider runIde
```

Test:

- `.tst` file association and highlighting.
- Tools | Typewriter actions.
- Settings persistence.
- Generate and validate commands.
- Generate-on-save and validate-on-save.
- CLI resolution from the source repository.
- Fallback to a globally installed `typewriter` tool.

The current plugin contains Kotlin/JVM code only and does not bundle native
binaries. One Rider ZIP is sufficient across operating systems and CPU
architectures supported by the targeted Rider build. The Typewriter CLI/.NET SDK
must be installed separately when the source repository is not available.

### Rider publishing

No `publishPlugin` task is configured or run by this workflow. `buildPlugin` only
creates the local ZIP distribution.

## CI Packaging Workflow

Extend `.github/workflows/ci.yml` with these stages.

### Prerequisites

- .NET SDK from `global.json`.
- Node.js 22.
- Java 21.
- Committed Gradle wrapper.
- An existing `artifacts/packages/` directory.

### Validation

```text
dotnet restore
dotnet build
dotnet test
npm ci --prefix vscode
npm --prefix vscode run check
npm --prefix vscode run bundle
vsce ls --tree
Gradle verifyPluginProjectConfiguration
Gradle test
Gradle verifyPlugin
```

### Packaging

```text
dotnet pack CLI
dotnet pack language server
dotnet build Visual Studio VSIX
vsce package VS Code extension
Gradle buildPlugin Rider extension
```

### Artifact names

Normalize release artifact names:

```text
AdaskoTheBeAsT.Typewriter.Cli.<version>.nupkg
AdaskoTheBeAsT.Typewriter.LanguageServer.<version>.nupkg
Typewriter.VisualStudio-<version>.vsix
typewriter-vscode-<version>.vsix
Typewriter-Rider-<version>.zip
```

If Visual Studio uses self-contained architecture-specific payloads:

```text
Typewriter.VisualStudio-<version>-win-x64.vsix
Typewriter.VisualStudio-<version>-win-arm64.vsix
```

## Local Packaging Gate

Current status:

- Package versions match the release tag.
- The Visual Studio manifest and README target Visual Studio 2026.
- The VSIX contains architecture-neutral managed CLI and language-server payloads.
- The VS Code VSIX contains bundled `vscode-languageclient` code.
- The VS Code package contains license, icon, changelog, and repository metadata.
- The Rider Gradle wrapper is committed.
- Rider verification passes against Rider 2026.1.2.
- CI builds and retains all package files from the same commit without publishing.
- The tag release workflow publishes NuGet packages and GitHub release assets.

Still required before claiming tested ARM64 support:

- Install and exercise the Visual Studio VSIX on an ARM64 Windows machine.
- Test each installable artifact from the packaged file in a clean IDE profile.

## References

- VS Code extension publishing and packaging:
  https://code.visualstudio.com/api/working-with-extensions/publishing-extension
- IntelliJ Platform Gradle Plugin tasks:
  https://plugins.jetbrains.com/docs/intellij/tools-intellij-platform-gradle-plugin-tasks.html
- JetBrains plugin signing:
  https://plugins.jetbrains.com/docs/intellij/plugin-signing.html
- JetBrains Marketplace upload API:
  https://plugins.jetbrains.com/docs/marketplace/plugin-upload.html
- Visual Studio ARM64 extension guidance:
  https://devblogs.microsoft.com/visualstudio/now-introducing-arm64-support-for-vs-extensions/
