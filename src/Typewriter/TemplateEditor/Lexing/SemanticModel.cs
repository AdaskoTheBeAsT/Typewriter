using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Typewriter.TemplateEditor.Lexing.Roslyn;

namespace Typewriter.TemplateEditor.Lexing
{
    public class SemanticModel
    {
        private static readonly Identifier[] _keywords = new[]
        {
            new Identifier { Name = "bool", QuickInfo = "bool Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "byte", QuickInfo = "byte Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "char", QuickInfo = "char Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "decimal", QuickInfo = "decimal Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "double", QuickInfo = "double Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "float", QuickInfo = "float Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "int", QuickInfo = "int Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "long", QuickInfo = "long Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "object", QuickInfo = "object Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "sbyte", QuickInfo = "sbyte Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "short", QuickInfo = "short Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "string", QuickInfo = "string Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "uint", QuickInfo = "uint Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "ulong", QuickInfo = "ulong Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "ushort", QuickInfo = "ushort Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "void", QuickInfo = "void Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },

            new Identifier { Name = "as", QuickInfo = "as Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "break", QuickInfo = "break Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "case", QuickInfo = "case Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },

            // new Identifier { Name = "class", QuickInfo = "class Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "const", QuickInfo = "const Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "continue", QuickInfo = "continue Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "do", QuickInfo = "do Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "else", QuickInfo = "else Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },

            // new Identifier { Name = "enum", QuickInfo = "enum Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "false", QuickInfo = "false Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "finally", QuickInfo = "finally Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "for", QuickInfo = "for Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "foreach", QuickInfo = "foreach Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "if", QuickInfo = "if Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "is", QuickInfo = "is Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "new", QuickInfo = "new Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "null", QuickInfo = "null Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "out", QuickInfo = "out Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "ref", QuickInfo = "ref Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "return", QuickInfo = "return Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "static", QuickInfo = "static Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "struct", QuickInfo = "struct Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "switch", QuickInfo = "switch Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "throw", QuickInfo = "throw Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "true", QuickInfo = "true Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "try", QuickInfo = "try Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "typeof", QuickInfo = "typeof Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "using", QuickInfo = "using Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "var", QuickInfo = "var Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
            new Identifier { Name = "while", QuickInfo = "while Keyword", Glyph = StandardGlyphGroup.GlyphKeyword },
        };

        private readonly ShadowClass _shadowClass;
        private readonly Tokens _tokens = new Tokens();
        private readonly Tokens _errorTokens = new Tokens();
        private readonly ContextSpans _contextSpans = new ContextSpans();
        private readonly Identifiers _tempIdentifiers = new Identifiers();

        public SemanticModel(ShadowClass shadowClass)
        {
            _shadowClass = shadowClass;
        }

        public Tokens Tokens => _tokens;

        public Tokens ErrorTokens => _errorTokens;

        public ContextSpans ContextSpans => _contextSpans;

        public Identifiers TempIdentifiers => _tempIdentifiers;

        public ShadowClass ShadowClass => _shadowClass;

        // Completion
        public IEnumerable<Identifier> GetIdentifiers(int position)
        {
            var contextSpan = _contextSpans.GetContextSpan(position);
            if (contextSpan != null)
            {
                if (contextSpan.Type == ContextType.Template)
                {
                    var contextIdentifiers = contextSpan.Context.Identifiers;
                    var customIdentifiers = _tempIdentifiers.GetTempIdentifiers(contextSpan.Context);

#pragma warning disable MA0026

                    // TODO: Optimize performance
#pragma warning restore MA0026
                    var extensionIdentifiers = _shadowClass.Snippets.Where(s => s.Type == SnippetType.Using && s.Code.StartsWith("using", StringComparison.OrdinalIgnoreCase))
                        .SelectMany(s => contextSpan.Context.GetExtensionIdentifiers(s.Code.Remove(0, 5).Trim().TrimEnd(';')));

                    return contextIdentifiers.Concat(customIdentifiers).Concat(extensionIdentifiers).OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase);
                }

                var identifiers = _shadowClass.GetRecommendedSymbols(position)
                    .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => Identifier.FromSymbol(g.First())).ToList();

                // Add common keywords to the statement completion list. (Roslyn 1.1 might provide this functionality)
                if (identifiers.Any(i => i.Name.Equals("Boolean", StringComparison.OrdinalIgnoreCase)) &&
                    identifiers.Any(i => i.Name.Equals("Class", StringComparison.OrdinalIgnoreCase)))
                {
                    identifiers.AddRange(_keywords);
                }

                return identifiers.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase);
            }

            return Array.Empty<Identifier>();
        }

        // BraceMatching
        public Token GetToken(int position)
        {
            return _tokens.GetToken(position);
        }

        // QuickInfo
        public string GetQuickInfo(int position)
        {
            var contextSpan = _contextSpans.GetContextSpan(position);
            if (contextSpan?.Type == ContextType.Template)
            {
                var quickInfo = _tokens.GetToken(position)?.QuickInfo;
                if (quickInfo != null && quickInfo.StartsWith("Item Parent", StringComparison.OrdinalIgnoreCase))
                {
                    var parent = contextSpan.ParentContext?.Name;
                    if (parent != null)
                    {
                        quickInfo = parent + quickInfo.Remove(0, 4);
                    }
                }

                return quickInfo;
            }

            var error = _errorTokens.FindTokens(position).FirstOrDefault();
            if (error != null)
            {
                return error.QuickInfo;
            }

            var symbol = _shadowClass.GetSymbol(position);
            if (symbol != null)
            {
                return Identifier.FromSymbol(symbol).QuickInfo;
            }

            return null;
        }

        // Classification
        public IEnumerable<Token> GetTokens(Span span)
        {
            return _tokens.GetTokens(span);
        }

        // Snytax errors
        public IEnumerable<Token> GetErrorTokens(Span span)
        {
            return _errorTokens.GetTokens(span);
        }

        // Statement completion
        public ContextSpan GetContextSpan(int position)
        {
            return _contextSpans.GetContextSpan(position);
        }

        // Outlining
        public IEnumerable<ContextSpan> GetContextSpans(ContextType type)
        {
            return _contextSpans.GetContextSpans(type);
        }

        // Lexers
        internal Identifier GetIdentifier(Context context, string name)
        {
            var identifier = context.GetIdentifier(name);
            if (identifier != null)
            {
                return identifier;
            }

            identifier = _tempIdentifiers.GetTempIdentifiers(context).FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
            if (identifier != null)
            {
                return identifier;
            }

#pragma warning disable MA0026

            // TODO: Optimize performance
#pragma warning restore MA0026
            foreach (var snippet in _shadowClass.Snippets.Where(s => s.Type == SnippetType.Using && s.Code.StartsWith("using", StringComparison.OrdinalIgnoreCase)))
            {
                identifier = context.GetExtensionIdentifier(snippet.Code.Remove(0, 5).Trim().TrimEnd(';'), name);
                if (identifier != null)
                {
                    return identifier;
                }
            }

            return null;
        }
    }
}