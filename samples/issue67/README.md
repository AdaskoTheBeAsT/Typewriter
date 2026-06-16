# Issue 67 Repro

This sample reproduces generation failure when a project uses a source generator, for example `[GeneratedRegex]`.

From this directory:

```powershell
dotnet build App/App.csproj
dotnet run --project ..\..\src\Typewriter.Cli\Typewriter.Cli.csproj -- generate --workspace . --project App\App.csproj
```

Expected behavior after the issue is fixed: `App/generated/models.ts` is generated for `App.MyModel`.
