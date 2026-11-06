# 🧩 Typewriter CodeModel

CodeModel contracts, configuration types, and helper extension methods for Typewriter `.tst` templates, `#load` C# helper files, and external template helper libraries.

## ✨ What is included

- `Typewriter.CodeModel` (`File`, `Class`, `Record`, `Struct`, `Interface`, `Enum`, `EnumValue`, `Delegate`, `Method`, `Property`, `Parameter`, `Field`, `Constant`, `Event`, `Attribute`, `AttributeArgument`, `DocComment`, `Type`, `TypeParameter`, collections, and `NameCase` helpers)
- `Typewriter.Configuration` (`Settings`, `PartialRenderingMode`, `DateLibrary`)
- `Typewriter.Extensions.Documentation` (`DocCommentExtensions`)
- `Typewriter.Extensions.Types` (`TypeExtensions`)
- `Typewriter.Extensions.WebApi` (`HttpMethodExtensions`, `RequestDataExtensions`, `UrlExtensions`)
- `Typewriter.VisualStudio` (`ILog`, `NullLog`)

## 🚀 Referencing without conflicts

Typewriter's CLI and language server already load `Typewriter.CodeModel` in the template host. Follow the patterns below so IDE IntelliSense works in your helper `.cs` files without shipping unnecessary runtime DLLs or causing duplicate-assembly (`CS0433`) conflicts.

### 1. Reference as a compile-only dependency (`PrivateAssets="all"` + `ExcludeAssets="runtime"`)

Add the package with `PrivateAssets="all"` and `ExcludeAssets="runtime"` so it is available to the IDE and compiler for IntelliSense, does not flow transitively to downstream projects, and is not copied into your application's runtime `bin` output:

```xml
<ItemGroup>
  <PackageReference Include="AdaskoTheBeAsT.Typewriter.CodeModel" Version="4.15.0" PrivateAssets="all" ExcludeAssets="runtime" />
</ItemGroup>
```

### 2. Using `#load` C# helper files vs `#r` helper assemblies

When a `.tst` template uses `#load "TemplateHelpers.cs"`, Typewriter splices the body of `TemplateHelpers.cs` directly into the generated `TypewriterTemplateHost` class.

Because `#load` files often contain bare methods (such as `string ToCustomName(Class c) => ...;`) rather than a top-level `class` declaration, compiling bare methods directly inside a standard `.csproj` will fail with C# compiler errors (`CS0116` / `CS8803`).

Choose one of the following patterns to get full IDE IntelliSense on `Typewriter.CodeModel` types without breaking your build:

- **Option A — Static class inside `#load` with thin template wrappers**:
  Write a `static class` inside your `.cs` helper file so it compiles cleanly as valid C# in any project that references `AdaskoTheBeAsT.Typewriter.CodeModel`:

  ```csharp
  // templates/_helpers/ModelHelpers.cs
  using Typewriter.CodeModel;

  static class ModelHelpers
  {
      public static string GetCustomTypeName(Class c) => $"I{c.Name}Dto";
  }
  ```

  Because `#load` splices `static class ModelHelpers` as a nested class inside the template host, expose a one-line wrapper method in the template (or at the bottom of the `#load` file if excluded from `Compile`):

  ```csharp
  ${
      #load "_helpers/ModelHelpers.cs"
      string CustomTypeName(Class c) => ModelHelpers.GetCustomTypeName(c);
  }
  ```

- **Option B — Compiled helper class library with `#r` (recommended for large or unit-tested helpers)**:
  Put your helper methods in a `public static` class inside a dedicated helper project (`netstandard2.0` or `net10.0`) that references `AdaskoTheBeAsT.Typewriter.CodeModel` with `PrivateAssets="all" ExcludeAssets="runtime"`:

  ```csharp
  using Typewriter.CodeModel;

  namespace MyProject.TypewriterHelpers;

  public static class ModelHelpers
  {
      public static string GetCustomTypeName(Class c) => $"I{c.Name}Dto";
  }
  ```

  Then reference the compiled helper assembly from your `.tst` template:

  ```csharp
  ${
      #r "bin/Release/netstandard2.0/MyProject.TypewriterHelpers.dll"
      using MyProject.TypewriterHelpers;

      string CustomTypeName(Class c) => ModelHelpers.GetCustomTypeName(c);
  }
  ```

- **Option C — Bare `#load` methods excluded from the application build**:
  If `TemplateHelpers.cs` contains bare methods without a wrapping `class`, exclude the template helper folder from `Compile` in your application `.csproj` so `dotnet build` ignores it while the project still holds the `AdaskoTheBeAsT.Typewriter.CodeModel` package reference:

  ```xml
  <ItemGroup>
    <PackageReference Include="AdaskoTheBeAsT.Typewriter.CodeModel" Version="4.15.0" PrivateAssets="all" ExcludeAssets="runtime" />
    <Compile Remove="templates/**/*.cs" />
    <None Include="templates/**/*.cs" />
  </ItemGroup>
  ```

### 3. Do not `#r` this package inside `.tst` templates

Do **not** add `#r "nuget: AdaskoTheBeAsT.Typewriter.CodeModel"` or `#r "Typewriter.CodeModel.dll"` inside `.tst` files. The Typewriter engine and language server automatically reference the host's `Typewriter.CodeModel` assembly and import the default `Typewriter.*` namespaces for every template.
