# ADR 0003: C# lexical brace scanning for template code blocks

## Status

Accepted for Typewriter 4.12.0.

## Context

Typewriter templates define C# configuration and helper methods inside `${ ... }` compatibility code blocks, while using `$Identifier[...]` tokens and optional TypeScript template literals (`` `${expression}` ``) in the surrounding template body.

Prior to this decision, `TemplateDocument.FindBalancedEnd` (as well as the corresponding block scanners in `Typewriter.LanguageServer`, `Typewriter.VisualStudio`, the VS Code extension, and the Rider plugin) located the closing `}` of a `${ ... }` block or C# helper method body by counting raw `{` and `}` characters without lexical awareness of C# strings, character literals, interpolated string escape sequences, or comments.

This caused valid templates such as the one reported in [Issue #114](https://github.com/AdaskoTheBeAsT/Typewriter/issues/114) to fail with `TW0002: Template code block is not closed.` or truncate helper method bodies prematurely whenever a C# code block contained unbalanced braces inside lexical literals or comments:

```csharp
${
    string ModelName(Class model) => model.Name;

    string Foo(Class model)
    {
        var methodName = "foo";
        var sb = new StringBuilder();
        sb.AppendLine($"public {methodName}() {{");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
$Classes[export class $ModelName {
    $Foo
}]
```

In this template:

- `sb.AppendLine($"public {methodName}() {{");` contains `{methodName}` (a balanced interpolation hole) followed by `{{` (an escaped literal `{` in a C# interpolated string), which the naive scanner counted as two opening braces (`depth + 2`).
- `sb.AppendLine("}");` contains a literal `}` inside a regular string literal, which the naive scanner counted as one closing brace (`depth - 1`).
- Consequently, the scanner exited the `${ ... }` block with a net brace depth offset of `+1`, consumed the rest of the template file, and emitted `TW0002: Template code block is not closed.`.
- Even when outer brace counts happened to cancel out, `TemplateDocument.AddReturnStatementMethods` also used `FindBalancedEnd` to extract helper method bodies, so a literal `}` inside a string or comment before the end of a method body truncated the extracted method body prematurely.

## Decision

1. **C# lexical brace scanning in `TemplateCodeBlockScanner` (`Typewriter.Engine`)**:
   - Introduce `TemplateCodeBlockScanner.FindBalancedEnd` and `TemplateCodeBlockScanner.IsCompatibilityCodeBlock` in `Typewriter.Engine` (shared with `Typewriter.LanguageServer` via `InternalsVisibleTo`).
   - When scanning from an opening `{` for its matching `}`, ignore `{` and `}` characters that appear inside non-code lexical regions while recursively balancing C# expressions inside interpolated string holes:
     - Single-line comments (`// ...` to end of line).
     - Multi-line block comments (`/* ... */`).
     - Character literals (`'...'` with `\` escape handling, bounded to the current line).
     - Regular string literals (`"..."` with `\` escape handling).
     - Verbatim string literals (`@"..."` with `""` escape handling).
     - Interpolated regular string literals (`$"..."` with `\` escapes, `{{` / `}}` escaped braces, and nested C# brace balancing inside `{ ... }` interpolation holes).
     - Interpolated verbatim string literals (`$@"..."` and `@$"..."` with `""` escapes, `{{` / `}}` escaped braces, and nested C# brace balancing inside `{ ... }` interpolation holes).
     - Raw string literals (`"""..."""` with 3 or more quotes) and interpolated raw string literals (`$"""..."""`, `$$"""..."""`, etc., where $N$ leading `$` characters require $N$ consecutive `{` characters to open an interpolation hole and $N$ consecutive `}` characters to close it, treating fewer than $N$ braces as literal text).
   - Allow leading C# single-line and multi-line comments before the first declaration or directive when identifying a compatibility code block in `IsCompatibilityCodeBlock`, and align `#load ` directive recognition between `Typewriter.Engine` and `Typewriter.LanguageServer`.

2. **Consistent C# block boundary detection across editor integrations**:
   - `Typewriter.LanguageServer` (`TemplateEmbeddedLanguage`) delegates `FindBalancedEnd` and `IsCompatibilityCodeBlock` to `TemplateCodeBlockScanner`.
   - `Typewriter.VisualStudio` (`TypewriterCompletion` and `TypewriterEditorClassification`), the VS Code extension (`vscode/src/extension.js`), and the Rider syntax-highlighting lexer (`TypewriterHighlightingLexer.kt`) use matching C#-aware lexical scanning so embedded C# / TypeScript region detection, completion routing, and syntax highlighting remain accurate when C# helper blocks contain braces in strings, interpolated strings, character literals, or comments.

## Compatibility

Existing valid templates, TypeScript template literal interpolations (`` `${...}` ``), and unclosed block diagnostics (`TW0002`) continue to behave as before. Templates whose `${ ... }` C# helper blocks contain `{` or `}` inside strings, interpolated strings, character literals, or comments now parse, compile, and render without false `TW0002` errors.

## Consequences

- `TemplateDocument.Parse` accurately locates the closing `}` of `${ ... }` compatibility blocks and C# helper method bodies regardless of braces inside C# strings, interpolated strings (`{{` / `}}`), raw strings, character literals, or comments.
- Language server virtual documents (`csharp` and `typescript` projections) and IDE extensions stay synchronized with engine parsing for all C# string and comment constructs.
