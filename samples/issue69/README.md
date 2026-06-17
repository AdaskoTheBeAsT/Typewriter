# Issue 69 Repro

This sample uses an old-style non-SDK .NET Framework project.

From this directory:

```powershell
dotnet build LegacyApp/LegacyApp.csproj
dotnet run --project ..\..\src\Typewriter.Cli\Typewriter.Cli.csproj -- generate --workspace . --project LegacyApp\LegacyApp.csproj
```

Expected behavior: Buildalyzer returns project metadata and `LegacyApp/generated/models.ts` is generated for `LegacyApp.MyModel`.
