# ADR 0002: Interface resolution and generic reference parity across C# helpers and template tokens

## Status

Accepted for Typewriter 4.12.0.

## Context

In Typewriter, templates can inspect implemented interfaces and generic type references through two complementary execution paths:

1. Compiled C# helper methods and lambdas (`Typewriter.CodeModel.Type`, `Class`, `Record`, `Struct`, `Interface`), adapted by `TemplateCodeModelAdapterFactory`.
2. Direct template tokens and inline compatibility predicates (`$Interfaces`, `$Type[$Interfaces]`, `$Properties(p => p.Type.Interfaces.Any(...))`), evaluated by `TemplateRenderer`.

Prior to this decision, two gaps existed between these paths and in constructed generic hierarchy resolution:

- **Direct template-token and compatibility-filter parity**: While `CodeModel.Type.Interfaces` exposed implemented interfaces to compiled C# helper methods (handling both in-project and external/BCL types via `TypeMetadataReference.Interfaces` and `(FullName, Arity)` lookup), `TemplateRenderer.ResolveType` and `TemplateRenderer.ResolveTypeReference` did not expose `"Interfaces"`, and `RenderState` only indexed project types by `FullName` rather than `(FullName, Arity)`. Consequently, direct template expressions such as `$Classes[$Interfaces[$Name]]`, `$Properties[$Type[$Interfaces[$Name]]]`, and non-compiled predicate filters such as `$Properties(p => p.Type.Interfaces.Any(i => i.Name == "..."))` could not resolve `Interfaces` or disambiguate overloaded generic types with the same `FullName` and different arities.
- **Constructed generic arguments on in-project interfaces and base types**: When an in-project generic interface, class, record, or struct was referenced with concrete type arguments (for example, `IDerivedContract<string> : IBaseContract<T>` or `DerivedClass : GenericBase<string>`), `TemplateCodeModelAdapterFactory` populated the top-level `TypeArguments` from the `TypeMetadataReference`, but reconstructed nested `Interfaces`, `BaseClass`, and `BaseRecord` from the unconstructed `TypeMetadata` definition (`BaseTypes`). As a result, nested inherited interfaces and base classes exposed open type parameter names (`T`) instead of the constructed type arguments (`string`).

## Decision

1. **Shared `TemplateCodeModelAdapterFactory` in `TemplateRenderer` and arity-aware lookup in `RenderState`**:
   - `RenderState` retains the `ProjectMetadataIndex` (`TypesByFullNameAndArity` and `TypesByFullName`) and reuses the `CompiledTemplateHelper.AdapterFactory` when a compiled C# block is present, or lazily creates a configured `TemplateCodeModelAdapterFactory` when rendering pure token/compatibility templates.
   - `RenderState.TryResolveType(TypeMetadataReference, out TypeMetadata)` resolves by `(FullName, Arity)` first and falls back to `FullName` for non-generic references (`Arity == 0`).
   - `TemplateRenderer.ResolveType` and `TemplateRenderer.ResolveTypeReference` both resolve `"Interfaces"` via `AdapterFactory.CreateInterfaces(...)`, returning `Typewriter.CodeModel.InterfaceCollection` so nested interface tokens (`$Name`, `$FullName`, `$TypeArguments`, `$Properties`, `$Methods`, `$Interfaces`, etc.), filters (`Name=`, `Namespace=`, `[Attribute]`, `:BaseInterface`), and C# helpers work identically on declared types and referenced property/method/parameter types.
   - Non-compiled compatibility predicate and string-transform evaluators thread `RenderState` through member path resolution so expressions such as `p => p.Type.Interfaces.Any(...)` resolve against project metadata.

2. **Constructed generic type-argument preservation across `Interfaces`, `BaseClass`, and `BaseRecord`**:
   - `CreateClass`, `CreateRecord`, `CreateInterface`, and `CreateStruct` pass the constructing `TypeMetadataReference` into `CreateInterfaces(type, reference)`, `CreateBaseClass(type, reference)`, and `CreateBaseRecord(type, reference)`.
   - On `CodeModel.Type.Interfaces`, when `reference.Interfaces` is populated by Roslyn symbol extraction, `CreateInterfaces(reference, metadata)` adapts those constructed interface references directly; otherwise it falls back to `CreateInterfaces(type, reference)`.
   - When resolving `Interfaces` from `type.BaseTypes` or when resolving `BaseClass` / `BaseRecord` from `type.BaseTypes`, `SubstituteTypeArguments` substitutes `type.TypeParameters` with `reference.TypeArguments` recursively across `TypeArguments` and `ElementType`, and wraps `Interfaces` in a `LazySubstitutedTypeReferenceList` so cyclic CLR interface graphs (such as `int : IComparable<int>`) are only substituted on demand.

3. **Adapting `CodeModel` wrapper items to `CodeModel.Type` in compiled helpers**:
   - `TemplateCodeModelAdapterFactory.TryAdapt` adapts `CodeClass`, `CodeRecord`, `CodeStruct`, `CodeInterface`, and `CodeEnum` to their underlying `CodeType` when a compiled C# helper method expects `Typewriter.CodeModel.Type` inside a `$Classes`, `$Records`, `$Structs`, `$Interfaces`, or `$Enums` block.

## Compatibility

Existing templates, C# helpers, and configuration options continue to work without modification. Templates that previously relied on C# helper workarounds to inspect `Type.Interfaces` can now also use direct `$Interfaces` tokens and inline filters, and constructed generic interfaces/base types now report their closed type arguments consistently.

## Consequences

- Direct template tokens (`$Interfaces`, `$Type[$Interfaces]`) and compiled C# helpers (`type.Interfaces`, `cls.Interfaces`) share the same resolution logic and arity-aware metadata index.
- Same-name generic types with different arities (such as `Factory`, `Factory<T>`, and `Factory<T, TOrder>`) resolve deterministically in both C# helpers and template token/filter evaluation.
- Constructed generic references preserve closed type arguments through inherited interfaces, base classes, and base records.
