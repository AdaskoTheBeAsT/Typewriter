# Issue 90 Repro

This sample covers TypeScript mapping for closed generic types nested in properties and collections.

From the repository root:

```powershell
dotnet build samples/issue90/App/App.csproj
dotnet run --project src/Typewriter.Cli/Typewriter.Cli.csproj -- generate --workspace samples/issue90 --project samples/issue90/App/App.csproj --template samples/issue90/App/Models.tst
```

Expected behavior: `samples/issue90/App/Models.ts` preserves the closed `Box<number>` type for the direct property, list element, and dictionary value.
