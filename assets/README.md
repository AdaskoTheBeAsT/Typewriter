# Typewriter logo asset pack

Generated vector-style asset set for the new Typewriter project.

## Files

| Target | File |
|---|---|
| VS Code extension | `vscode/images/icon.png` — 128x128 PNG |
| Visual Studio VSIX | `visualstudio/vsix-icon-90.png` — 90x90 PNG |
| Visual Studio preview | `visualstudio/vsix-preview-200.png` — 200x200 PNG |
| JetBrains Rider plugin | `jetbrains/pluginIcon.svg` — 40x40 viewBox |
| JetBrains Rider dark icon | `jetbrains/pluginIcon_dark.svg` — 40x40 viewBox |
| NuGet package icon | `nuget/icon.png` — 128x128 PNG |
| Source/master SVG icon | `source/typewriter-icon.master.svg` |
| Source/master SVG logo | `source/typewriter-logo.master.svg` |
| Future resizing PNG | `source/typewriter-icon-1024.png` — 1024x1024 PNG |
| README/banner PNG | `source/typewriter-logo-1600x500.png` |

## VS Code package.json

```json
{
  "icon": "images/icon.png"
}
```

## Rider plugin

Place these files under the plugin resources directory expected by the Gradle IntelliJ plugin:

```text
src/main/resources/META-INF/pluginIcon.svg
src/main/resources/META-INF/pluginIcon_dark.svg
```

## NuGet package

```xml
<PropertyGroup>
  <PackageIcon>icon.png</PackageIcon>
</PropertyGroup>
<ItemGroup>
  <None Include="nuget/icon.png" Pack="true" PackagePath="" />
</ItemGroup>
```
