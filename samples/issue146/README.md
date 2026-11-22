# Issue 146 Repro

Reproduces [issue #146](https://github.com/AdaskoTheBeAsT/Typewriter/issues/146):
`CustomersController.Search(CustomerSearchQuery query, int retryCount)` accepts a
parameter whose type (`CustomerSearchQuery`) inherits from `BaseQuery<Guid>`.

`Services.tst` inspects `$Methods -> Parameters -> Type` both through the C#
helper pattern from issue #146 (`CollectTypeProperties(Type t, List<string> props)`
using `Break()`, `t.Fields`, `t.Properties`, and recursive `t.BaseClass`
traversal) and through direct template syntax (`$Parameters[$Type[$Properties[...]]]`).

From the repository root:

```powershell
dotnet build samples/issue146/Issue146.csproj
dotnet run --project src/Typewriter.Cli/Typewriter.Cli.csproj -f net11.0 -- generate --workspace samples/issue146 --project samples/issue146/Issue146.csproj --template samples/issue146/Services.tst
```
