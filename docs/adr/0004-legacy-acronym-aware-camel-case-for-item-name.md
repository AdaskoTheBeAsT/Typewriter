# ADR 0004: Legacy acronym-aware camelCase for `$name` and `Item.name`

## Status

Accepted for Typewriter 4.12.0.

## Context

In legacy Typewriter (the Visual Studio extension), every `Item` in the code model exposed `name` (template shorthand `$name`) via `Helpers.CamelCase(string s)`, which followed Json.NET's `StringUtils.ToCamelCase` leading-acronym algorithm:

- If a name does not start with an uppercase character, return it unchanged.
- Otherwise, lowercase consecutive leading uppercase characters:
  - Lowercase the first character (`index == 0`).
  - Stop at `index == 1` when `chars[1]` is not uppercase (for example, `LastADLogin` -> `lastADLogin`, preserving interior acronyms untouched).
  - For `index > 0`, when a next character exists and is not uppercase, stop before lowercasing `chars[index]` so the final uppercase letter of a leading acronym remains uppercase as the start of the next word (for example, `ADExtendedAttribute1` -> `adExtendedAttribute1`, `URLValue` -> `urlValue`).
  - When all characters in a leading acronym are followed only by the end of the identifier, lowercase all of them (for example, `UPN` -> `upn`, `URL` -> `url`, `ID` -> `id`).

During the v4 rewrite, two divergent implementations were introduced:

1. `NameCaseFormatter.ToLegacyCamelCase(string value)` (used by `NameCase.LegacyCamelCase` and `Item.name`) only lowercased `value[0]`, leaving `value[1..]` unchanged.
2. `TemplateRenderer.ToCamelCase(string value)` (used when resolving `$name` directly on metadata items in `TemplateRenderer`) duplicated the same first-character-only logic.

As reported in [Issue #115](https://github.com/AdaskoTheBeAsT/Typewriter/issues/115), migrating templates from the legacy Visual Studio extension to the v4 CLI produced inconsistent property names whenever a C# identifier started with a multi-letter uppercase acronym:

```csharp
public class TestModel
{
    public string UPN { get; set; }
    public string ADExtendedAttribute1 { get; set; }
    public DateTime LastADLogin { get; set; }
}
```

With `$Properties[public $name: $Type;]`:

- Legacy Typewriter rendered `upn`, `adExtendedAttribute1`, and `lastADLogin`.
- Typewriter 4.11.0 rendered `uPN`, `aDExtendedAttribute1`, and `lastADLogin`.

Note that `NameCase.CamelCase` (`$CamelCase` / `$camelName`) cannot be substituted for `NameCase.LegacyCamelCase` because `NameCase.CamelCase` splits identifiers into words and re-cases every subsequent word to PascalCase (`LastADLogin` -> `lastAdLogin`), which would break legacy parity for interior acronyms.

## Decision

1. **Restore the legacy acronym-aware algorithm in `NameCaseFormatter.ToLegacyCamelCase`**:
   - Implement the exact legacy `Helpers.CamelCase` loop in `NameCaseFormatter.ToLegacyCamelCase(string value)` so `NameCase.LegacyCamelCase`, `Item.name`, and `$LegacyCamelCase` lowercase leading acronyms (`UPN` -> `upn`, `ADExtendedAttribute1` -> `adExtendedAttribute1`, `URLValue2Model` -> `urlValue2Model`) while preserving the remainder of the identifier after the leading uppercase run (`LastADLogin` -> `lastADLogin`, `IsJSONProperty` -> `isJSONProperty`).
   - Keep `NameCase.CamelCase` (`SplitWords` + PascalCase word joining) unchanged for explicit word-splitting casing tokens (`$CamelCase`, `$camelName`, etc.).

2. **Unify `TemplateRenderer.ToCamelCase` with `NameCaseFormatter`**:
   - Delegate `TemplateRenderer.ToCamelCase(string value)` directly to `NameCaseFormatter.Format(value: value, nameCase: NameCase.LegacyCamelCase)` so direct metadata `$name` rendering and `CodeModel` `Item.name` rendering share a single implementation.
   - Ensure `$name` resolution on `TypeMetadataReference` (`$Type[$name]`), `AttributeArgumentMetadata`, `ParameterCommentMetadata`, and `CodeModel.ParameterComment` also applies `ToCamelCase` consistently with `CodeModel` items.

## Compatibility

Identifiers beginning with a single uppercase letter followed by lowercase characters (including those containing interior acronyms such as `LastADLogin`) continue to render identically. Identifiers beginning with multi-letter uppercase acronyms (`UPN`, `ADExtendedAttribute1`, `URLValue`, `ID`) now match legacy Typewriter output (`upn`, `adExtendedAttribute1`, `urlValue`, `id`).

## Consequences

- Templates using `$name` or `item.name` in C# helpers produce identical output to legacy Typewriter for leading acronyms and interior acronyms alike.
- `NameCaseFormatter` is the single source of truth for both `NameCase.LegacyCamelCase` and `TemplateRenderer` `$name` formatting.
