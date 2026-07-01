# Typewriter for JetBrains Rider

JetBrains Rider adapter for Typewriter `.tst` templates.

This is the Rider frontend layer: a Kotlin/IntelliJ Platform plugin for UI, actions, settings, editor integration, and file watching. It does not add a ReSharper backend plugin. C# semantic loading still happens in the existing Typewriter CLI/Roslyn engine.

## Compatibility

- Plugin version: synchronized from `rider/gradle.properties` by the root `setver.ps1` release script.
- Rider/IntelliJ Platform: `2026.1+` (`sinceBuild` `261`).
- Java: `21+`.

## Features

- Registers `.tst` files as Typewriter templates.
- Adds syntax highlighting and color scheme entries for Typewriter directives, template expressions, helper blocks, strings, numbers, comments, and delimiters.
- Adds Tools | Typewriter actions:
  - Generate Current Template
  - Generate All Templates
  - Validate Current Template
- Adds Settings | Typewriter project settings for CLI path, CLI arguments, workspace/project/template paths, framework, all-project generation, and save-time generation/validation for files matched by `typewriter.json` `inputExtensions`.
- Restricts source-file save generation to templates under the owning C# project.
- Reuses a project-scoped Typewriter language-server process for generation and validation, with CLI fallback.

## Build

Prerequisites:

- Eclipse Temurin JDK 21 (`EclipseAdoptium.Temurin.21.JDK` on Windows)

```powershell
winget install EclipseAdoptium.Temurin.21.JDK
```

```powershell
.\rider\gradlew.bat -p rider verifyPluginProjectConfiguration
.\rider\gradlew.bat -p rider verifyPlugin
.\rider\gradlew.bat -p rider buildPlugin
```

The plugin ZIP is written under `rider/build/distributions/typewriter-rider-<version>.zip`. CI collects it as `Typewriter-Rider-<version>.zip`.
