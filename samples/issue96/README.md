# Issue 96 Repro

This sample covers record inheritance when a derived record and its base record are declared in separate source files.

From the repository root:

```powershell
dotnet build samples/issue96/App/App.csproj
dotnet run --project src/Typewriter.Cli/Typewriter.Cli.csproj -- generate --workspace samples/issue96 --project samples/issue96/App/App.csproj --template samples/issue96/App/Models.tst
```

Expected behavior: `samples/issue96/App/DerivedRecordVm.ts` declares `DerivedRecordVm extends BaseRecordVm`.
