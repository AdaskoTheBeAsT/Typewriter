using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Typewriter.Engine;

internal static class TemplateInlinePredicateCompiler
{
    public static string AppendToSource(
        TemplateDocument template,
        string source,
        IReadOnlyList<MetadataReference> references)
    {
        if (template.InlinePredicates.Count == 0)
        {
            return source;
        }

        CSharpCompilation? declarations = null;
        var methods = new StringBuilder();
        var emitted = new HashSet<(string MethodName, string ItemTypeName)>();
        foreach (var predicate in template.InlinePredicates)
        {
            IEnumerable<string> itemTypeNames;
            if (predicate.ItemTypeName is not null)
            {
                itemTypeNames = [predicate.ItemTypeName];
            }
            else
            {
                declarations ??= CSharpCompilation.Create(
                    assemblyName: "TypewriterInlinePredicateBinding",
                    syntaxTrees: [CSharpSyntaxTree.ParseText(text: source)],
                    references: references,
                    options: new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary));
                itemTypeNames = ResolveHelperItemTypes(declarations: declarations, collectionName: predicate.CollectionName);
            }

            foreach (var itemTypeName in itemTypeNames.Where(predicate: itemTypeName => emitted.Add(item: (predicate.MethodName, itemTypeName))))
            {
                AppendMethod(methods: methods, predicate: predicate, itemTypeName: itemTypeName, templatePath: template.Path);
            }
        }

        var namespaceEnd = source.LastIndexOf(value: '}');
        var hostEnd = source.LastIndexOf(value: '}', startIndex: namespaceEnd - 1);
        return source.Insert(startIndex: hostEnd, value: methods.ToString());
    }

    private static IEnumerable<string> ResolveHelperItemTypes(
        CSharpCompilation declarations,
        string collectionName)
    {
        var host = declarations.GetTypeByMetadataName(fullyQualifiedMetadataName: TemplateRuntimeCompiler.HostTypeFullName);
        var returnTypes = host?.GetMembers(name: collectionName).OfType<IMethodSymbol>().Select(selector: method => method.ReturnType) ?? [];
        foreach (var returnType in returnTypes)
        {
            if (returnType is IArrayTypeSymbol array)
            {
                yield return array.ElementType.ToDisplayString(format: SymbolDisplayFormat.FullyQualifiedFormat);
            }
            else if (returnType is INamedTypeSymbol type)
            {
                var enumerable = type.AllInterfaces.Prepend(element: type)
                    .FirstOrDefault(predicate: candidate => candidate.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);
                if (enumerable is not null)
                {
                    yield return enumerable.TypeArguments[0].ToDisplayString(format: SymbolDisplayFormat.FullyQualifiedFormat);
                }
            }
        }
    }

    private static void AppendMethod(
        StringBuilder methods,
        TemplateInlinePredicate predicate,
        string itemTypeName,
        string templatePath)
    {
        methods.Append(value: "private bool ").Append(value: predicate.MethodName)
            .Append(value: '(').Append(value: itemTypeName).AppendLine(value: " __item) =>");
        methods.Append(value: "((global::System.Func<").Append(value: itemTypeName).AppendLine(value: ", bool>)(");
        methods.Append(value: "#line ").Append(value: predicate.Line.ToString(provider: CultureInfo.InvariantCulture))
            .Append(value: ' ').AppendLine(value: SyntaxFactory.Literal(value: templatePath).ToString());
        methods.Append(value: ' ', repeatCount: predicate.Column - 1).AppendLine(value: predicate.Filter);
        methods.AppendLine(value: "#line default");
        methods.AppendLine(value: "))(__item);");
    }
}
