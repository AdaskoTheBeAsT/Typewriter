# Issue 112 v2 Sample (Interface & Generic Reference Parity)

Extends [issue #112](https://github.com/AdaskoTheBeAsT/Typewriter/issues/112) and
[issue #113](https://github.com/AdaskoTheBeAsT/Typewriter/issues/113) to cover
two additional edge cases:

1. **Direct template-token and filter access to `$Interfaces` and `$Type[$Interfaces]`**:
   Templates can iterate `$Interfaces[...]` directly on classes/records/structs/interfaces
   and `$Type[$Interfaces[...]]` on property/method/parameter type references without
   requiring a C# helper method.
2. **Constructed generic arguments on in-project interfaces and base classes**:
   When a generic class or interface is referenced with closed type arguments
   (such as `Factory : Factory<string>` where `Factory<T> : Factory<T, int>, IFactoryContract<T>`,
   or property `IFactoryContract<string> Contract` where `IFactoryContract<T> : IBaseContract<T>`),
   nested `BaseClass` and `Interfaces` preserve the constructed type arguments (`string`, `number`)
   instead of falling back to open type parameter names (`T`, `TResult`).

From the repository root:

```powershell
dotnet build samples/issue112v2/Issue112V2.csproj
dotnet run --project src/Typewriter.Cli/Typewriter.Cli.csproj -- generate --workspace samples/issue112v2 --project samples/issue112v2/Issue112V2.csproj --framework net48 --template samples/issue112v2/Models.tst
```

To run without .NET Framework reference assemblies, build with `-f net10.0` and use
`--framework net10.0` in the generation command instead.
