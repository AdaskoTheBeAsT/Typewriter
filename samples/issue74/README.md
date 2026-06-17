# Issue 74 Repro

This sample reproduces generation failure when a project uses a type from a transitively referenced project.

`App` references `Mid`, `Mid` references `Leaf`, and `App.MyModel` uses `Leaf.Widget`. `dotnet build` succeeds, and Typewriter should resolve `Leaf.Widget` while generating from `App`.

From this directory:

```powershell
dotnet build App/App.csproj
dotnet run --project ..\..\src\Typewriter.Cli\Typewriter.Cli.csproj -- generate --workspace . --project App\App.csproj
```

Expected behavior after the issue is fixed: `App/MyModel.ts` is generated for `App.MyModel`.
