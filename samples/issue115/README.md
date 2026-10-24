# Issue 115 Repro

Reproduces [issue #115](https://github.com/AdaskoTheBeAsT/Typewriter/issues/115):
`TestModel` declares properties with leading multi-letter acronyms (`UPN`,
`ADExtendedAttribute1`) alongside a property with an interior acronym
(`LastADLogin`), rendered through `$Properties[public $name: $Type;]`.

Before the fix, `NameCase.LegacyCamelCase` and `TemplateRenderer.ToCamelCase`
only lowercased the first character (`uPN`, `aDExtendedAttribute1`) instead of
applying legacy Typewriter's acronym-aware camelCase algorithm (`upn`,
`adExtendedAttribute1`, `lastADLogin`).

From the repository root:

```powershell
dotnet build samples/issue115/Issue115.csproj
dotnet run --project src/Typewriter.Cli/Typewriter.Cli.csproj -- generate --workspace samples/issue115 --project samples/issue115/Issue115.csproj --framework net48 --template samples/issue115/Models.tst
```

To run without .NET Framework reference assemblies, build with `-f net10.0` and
use `--framework net10.0` in the generation command instead.
