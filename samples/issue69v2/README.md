# Issue 69 v2 Repro

This sample uses the old-style project shape from the follow-up issue 69 comment:

- `MyProject/MyProject.csproj` imports TypeScript/CodeDom targets conditionally.
- `MyProject/MyProject.csproj` imports `Shared/Classic.Web.csproj` before the main property group.
- `Shared/Classic.Web.csproj` supplies the compile items, references, and CSharp targets for the project.
- `Shared/Classic.Web.csproj` includes a design-time-only failing target to simulate an unrelated legacy web/TypeScript target failing during Buildalyzer evaluation after C# compiler inputs are available.

From this directory:

```powershell
dotnet build MyProject/MyProject.csproj
dotnet run --project ..\..\src\Typewriter.Cli\Typewriter.Cli.csproj -- generate --workspace Issue69v2.sln --project MyProject\MyProject.csproj --template MyProject\_Resources\_Services.tst
```

Expected behavior: `MyProject/generated/services.ts` is generated for `MyProject.CustomerDto`.
