# Issue 114 Repro

Reproduces [issue #114](https://github.com/AdaskoTheBeAsT/Typewriter/issues/114):
a `${ ... }` template code block contains a helper method that builds TypeScript
code with `StringBuilder`, using an interpolated string with an escaped opening
brace (`$"public {methodName}() {{"`) and a regular string literal with a
closing brace (`"}"`).

Before the fix, `FindBalancedEnd` counted raw `{` and `}` characters without
skipping C# string literals or escaped interpolated braces (`{{` / `}}`), leaving
an unbalanced brace count and failing with
`TW0002: Template code block is not closed.`.

From the repository root:

```powershell
dotnet build samples/issue114/Issue114.csproj
dotnet run --project src/Typewriter.Cli/Typewriter.Cli.csproj -- generate --workspace samples/issue114 --project samples/issue114/Issue114.csproj --framework net48 --template samples/issue114/Models.tst
```

To run without .NET Framework reference assemblies, build with `-f net10.0` and
use `--framework net10.0` in the generation command instead.
