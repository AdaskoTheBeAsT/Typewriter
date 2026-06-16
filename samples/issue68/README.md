# Issue 68 Repro

This sample reproduces generation failure when a referenced project has implicit usings enabled and the consuming project has implicit usings disabled.

`Leaf` keeps implicit usings enabled, which creates generated global usings such as `System.IO`. `Core` disables implicit usings and defines a `Core.Models.File` type. Typewriter should not let `Leaf` global usings leak into `Core`, because that makes `File` ambiguous with `System.IO.File`.

From this directory:

```powershell
dotnet build Core/Core.csproj
dotnet run --project ..\..\src\Typewriter.Cli\Typewriter.Cli.csproj -- generate --workspace . --project Core\Core.csproj
```

Expected behavior after the issue is fixed: `Core/generated/models.ts` is generated for `Core.MyModel`.
