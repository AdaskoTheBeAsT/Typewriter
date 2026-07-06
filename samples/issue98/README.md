# Issue 98 Repro

This sample covers `settings.IncludeProject(...)` pulling classes from a workspace project that is not referenced by the template's project. `App` has no project reference to `Contracts`, yet the template asks for `Contracts` types via `IncludeProject("Contracts")`.

From the repository root:

```powershell
dotnet build samples/issue98/Issue98.slnx
dotnet run --project src/Typewriter.Cli/Typewriter.Cli.csproj -- generate --workspace samples/issue98 --project samples/issue98/App/App.csproj --template samples/issue98/App/Models.tst
```

Expected behavior: both `samples/issue98/App/LocalDto.ts` (from `App`) and `samples/issue98/App/SharedDto.ts` (from the unreferenced `Contracts` project) are generated. Removing the `IncludeProject("Contracts")` call generates only `LocalDto.ts`.
