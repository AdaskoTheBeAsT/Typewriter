# Issue 112 Repro

Reproduces [issue #112](https://github.com/AdaskoTheBeAsT/Typewriter/issues/112):
`Factory<T>` inherits from `Factory<T, TResult>`, and `Factory` inherits from
`Factory<string>`. These are distinct C# types with the same name but different
generic arities. The derived generic class is deliberately declared first.

The template uses the issue's minimal `ModelName(Class model) => model.Name`
helper. Before the fix, adapting a class for this helper resolves the base to the
derived class again and crashes the CLI with a stack overflow.

From the repository root:

```powershell
dotnet build samples/issue112/Issue112.csproj
dotnet run --project src/Typewriter.Cli/Typewriter.Cli.csproj -- generate --workspace samples/issue112 --project samples/issue112/Issue112.csproj --framework net48 --template samples/issue112/Models.tst
```

The .NET Framework 4.8 target matches the reported environment. To run without
.NET Framework reference assemblies, build with `-f net10.0` and use
`--framework net10.0` in the generation command instead.

Expected: generation succeeds without a stack overflow and writes `Factory.ts`
containing three `export interface Factory {}` declarations, one per C# class.
The repeated TypeScript interface name is intentional: the original helper
returns only `model.Name`, not its generic parameters.
