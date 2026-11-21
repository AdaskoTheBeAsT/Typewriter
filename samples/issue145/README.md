# Issue 145 Repro

Reproduces [issue #145](https://github.com/AdaskoTheBeAsT/Typewriter/issues/145):
`CustomerModel` declares `public` and `internal` members (including a property
with an `internal get` accessor), alongside an `internal` top-level class
`InternalSyncState`.

By default (`IncludeInternals = false`), Typewriter excludes `internal` C# types
and members from template collections such as `$Classes`, `$Properties`, and
`$Constants` (`Models.tst` -> `Models.ts`) without requiring `(Public)` filters.

Templates or workspaces that want `internal` C# declarations included can opt in
via `settings.IncludeInternals()` (`ModelsWithInternals.tst` ->
`ModelsWithInternals.ts`) or `"output": { "includeInternals": true }` in
`typewriter.json`.

From the repository root:

```powershell
dotnet build samples/issue145/Issue145.csproj
dotnet run --project src/Typewriter.Cli/Typewriter.Cli.csproj -f net11.0 -- generate --workspace samples/issue145 --project samples/issue145/Issue145.csproj --template samples/issue145/Models.tst
dotnet run --project src/Typewriter.Cli/Typewriter.Cli.csproj -f net11.0 -- generate --workspace samples/issue145 --project samples/issue145/Issue145.csproj --template samples/issue145/ModelsWithInternals.tst
```
