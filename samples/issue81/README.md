# Issue 81 Repro

This sample covers metadata extraction for a project that references a type with cyclic object relationships, such as `Newtonsoft.Json.Linq.JObject`.

From the repository root:

```powershell
dotnet build samples/issue81/App/App.csproj
dotnet run --project src/Typewriter.Cli/Typewriter.Cli.csproj -- generate --workspace samples/issue81 --project samples/issue81/App/App.csproj --template samples/issue81/App/Models.tst
```

Expected behavior: generation completes without a stack overflow and `samples/issue81/App/MyModel.ts` is generated for `App.MyModel`.
