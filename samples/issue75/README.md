# Issue 75 Repro

This sample covers a project that has localized resources and therefore asks the .NET SDK
to produce a satellite assembly during build.

From the repository root:

```powershell
dotnet build samples/issue75/App/App.csproj
dotnet run --project src/Typewriter.Cli/Typewriter.Cli.csproj -- generate --workspace samples/issue75 --project samples/issue75/App/App.csproj --template samples/issue75/App/Models.tst
```

Expected behavior: `samples/issue75/App/MyModel.ts` is generated without `TW0003`.
