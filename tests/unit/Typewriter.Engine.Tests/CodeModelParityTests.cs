using System.Reflection;
using Typewriter.CodeModel;
using Xunit;
using Attribute = Typewriter.CodeModel.Attribute;
using Delegate = Typewriter.CodeModel.Delegate;
using Enum = Typewriter.CodeModel.Enum;
using File = Typewriter.CodeModel.File;
using Record = Typewriter.CodeModel.Record;
using Type = Typewriter.CodeModel.Type;

namespace Typewriter.Engine.Tests;

public sealed class CodeModelParityTests
{
#pragma warning disable CC0021 // Use nameof
    public static TheoryData<System.Type, string[]> OriginalPropertySurface =>
        new()
        {
            { typeof(Attribute), ["Arguments", "AssemblyName", "FullName", "Name", "Parent", "Type", "Value", "name"] },
            { typeof(AttributeArgument), ["Type", "TypeValue", "Value"] },
            { typeof(Class), ["AssemblyName", "Attributes", "BaseClass", "Constants", "ContainingClass", "Delegates", "DocComment", "Events", "Fields", "FullName", "Interfaces", "IsAbstract", "IsGeneric", "IsStatic", "Methods", "Name", "Namespace", "NestedClasses", "NestedEnums", "NestedInterfaces", "Parent", "Properties", "StaticReadOnlyFields", "TypeArguments", "TypeParameters", "name"] },
            { typeof(Constant), ["AssemblyName", "Attributes", "DocComment", "FullName", "Name", "Parent", "Type", "Value", "name"] },
            { typeof(Delegate), ["AssemblyName", "Attributes", "DocComment", "FullName", "IsGeneric", "Name", "Parameters", "Parent", "Type", "TypeParameters", "name"] },
            { typeof(DocComment), ["Parameters", "Parent", "Returns", "Summary"] },
            { typeof(Enum), ["AssemblyName", "Attributes", "ContainingClass", "DocComment", "FullName", "IsFlags", "Name", "Namespace", "Parent", "Values", "name"] },
            { typeof(EnumValue), ["AssemblyName", "Attributes", "DocComment", "FullName", "Name", "Parent", "Value", "name"] },
            { typeof(Event), ["AssemblyName", "Attributes", "DocComment", "FullName", "Name", "Parent", "Type", "name"] },
            { typeof(Field), ["AssemblyName", "Attributes", "DocComment", "FullName", "Name", "Parent", "Type", "name"] },
            { typeof(File), ["Classes", "Delegates", "Enums", "FullName", "Interfaces", "Name", "Records"] },
            { typeof(Interface), ["AssemblyName", "Attributes", "ContainingClass", "DocComment", "Events", "FullName", "Interfaces", "IsGeneric", "Methods", "Name", "Namespace", "Parent", "Properties", "Type", "TypeArguments", "TypeParameters", "name"] },
            { typeof(Method), ["AssemblyName", "Attributes", "DocComment", "FullName", "IsAbstract", "IsGeneric", "Name", "Parameters", "Parent", "Type", "TypeParameters", "name"] },
            { typeof(Parameter), ["AssemblyName", "Attributes", "DefaultValue", "FullName", "HasDefaultValue", "Name", "Parent", "Type", "name"] },
            { typeof(ParameterComment), ["Description", "Name", "Parent"] },
            { typeof(Property), ["AssemblyName", "Attributes", "DocComment", "FullName", "HasGetter", "HasSetter", "IsAbstract", "IsVirtual", "Name", "Parent", "Type", "name"] },
            { typeof(Record), ["AssemblyName", "Attributes", "BaseRecord", "Constants", "ContainingRecord", "Delegates", "DocComment", "Events", "Fields", "FullName", "Interfaces", "IsAbstract", "IsGeneric", "Methods", "Name", "Namespace", "Parent", "Properties", "StaticReadOnlyFields", "TypeArguments", "TypeParameters", "name"] },
            { typeof(StaticReadOnlyField), ["AssemblyName", "Attributes", "DocComment", "FullName", "Name", "Parent", "Type", "Value", "name"] },
            { typeof(Type), ["AssemblyName", "Attributes", "BaseClass", "Constants", "ContainingClass", "DefaultValue", "Delegates", "DocComment", "ElementType", "Fields", "FileLocations", "FullName", "Interfaces", "IsDate", "IsDefined", "IsDictionary", "IsDynamic", "IsEnum", "IsEnumerable", "IsGeneric", "IsGuid", "IsNullable", "IsPrimitive", "IsTask", "IsTimeSpan", "IsValueTuple", "Methods", "Name", "Namespace", "NestedClasses", "NestedEnums", "NestedInterfaces", "OriginalName", "Parent", "Properties", "Settings", "StaticReadOnlyFields", "TupleElements", "TypeArguments", "TypeParameters", "name"] },
            { typeof(TypeParameter), ["Name", "Parent", "name"] },
        };
#pragma warning restore CC0021 // Use nameof

    [Theory]
    [MemberData(memberName: nameof(OriginalPropertySurface))]
    public void CodeModelTypesExposeOriginalPublicProperties(
        System.Type type,
        string[] expectedProperties)
    {
        var actualProperties = type.GetProperties(bindingAttr: BindingFlags.Instance | BindingFlags.Public)
            .Select(selector: property => property.Name)
            .ToHashSet(comparer: StringComparer.Ordinal);

        foreach (var expectedProperty in expectedProperties)
        {
            Assert.Contains(expected: expectedProperty, set: actualProperties);
        }
    }

    [Fact]
    public void CodeModelTypesExposeOriginalImplicitConversions()
    {
        var type = new Type { Name = "Widget" };
        var @class = new Class { Name = "Widget", Type = type };
        var record = new Record { Name = "WidgetRecord", Type = type };
        var @interface = new Interface { Name = "IWidget", Type = type };
        var @enum = new Enum { Name = "WidgetKind", Type = type };

        string attributeName = new Attribute { Name = "Generate" };
        string className = @class;
        Type? classType = @class;
        string constantName = new Constant { Name = "ApiVersion" };
        string delegateName = new Delegate { Name = "Factory" };
        string docComment = new DocComment { Summary = "Widget summary." };
        string enumName = @enum;
        Type? enumType = @enum;
        string enumValueName = new EnumValue { Name = "Draft" };
        string eventName = new Event { Name = "Changed" };
        string fieldName = new Field { Name = "InstanceField" };
        string interfaceName = @interface;
        Type? interfaceType = @interface;
        string methodName = new Method { Name = "GetAsync" };
        string parameterName = new Parameter { Name = "id" };
        string parameterCommentName = new ParameterComment { Name = "id" };
        string propertyName = new Property { Name = "DisplayName" };
        string recordName = record;
        Type? recordType = record;
        string staticReadOnlyFieldName = new StaticReadOnlyField { Name = "StaticLabel" };
        string typeName = type;

        Assert.Equal(expected: "Generate", actual: attributeName);
        Assert.Equal(expected: "Widget", actual: className);
        Assert.Same(expected: type, actual: classType);
        Assert.Equal(expected: "ApiVersion", actual: constantName);
        Assert.Equal(expected: "Factory", actual: delegateName);
        Assert.Equal(expected: "Widget summary.", actual: docComment);
        Assert.Equal(expected: "WidgetKind", actual: enumName);
        Assert.Same(expected: type, actual: enumType);
        Assert.Equal(expected: "Draft", actual: enumValueName);
        Assert.Equal(expected: "Changed", actual: eventName);
        Assert.Equal(expected: "InstanceField", actual: fieldName);
        Assert.Equal(expected: "IWidget", actual: interfaceName);
        Assert.Same(expected: type, actual: interfaceType);
        Assert.Equal(expected: "GetAsync", actual: methodName);
        Assert.Equal(expected: "id", actual: parameterName);
        Assert.Equal(expected: "id", actual: parameterCommentName);
        Assert.Equal(expected: "DisplayName", actual: propertyName);
        Assert.Equal(expected: "WidgetRecord", actual: recordName);
        Assert.Same(expected: type, actual: recordType);
        Assert.Equal(expected: "StaticLabel", actual: staticReadOnlyFieldName);
        Assert.Equal(expected: "Widget", actual: typeName);
    }

    [Fact]
    public void CodeModelItemsExposeNameCaseFormatting()
    {
        var @class = new Class { Name = "URLValue2Model" };

        Assert.Equal(expected: "uRLValue2Model", actual: @class.name);
        Assert.Equal(expected: "URL_VALUE_2_MODEL", actual: @class.GetName(nameCase: NameCase.UpperSnakeCase));
        Assert.Equal(expected: "url-value-2-model", actual: @class.GetName(nameCase: NameCase.LowerKebabCase));
        Assert.Equal(expected: "UrlValue2Model", actual: @class.GetName(nameCase: NameCase.PascalCase));
        Assert.Equal(expected: "urlValue2Model", actual: @class.GetName(nameCase: NameCase.CamelCase));
        Assert.Equal(expected: "url.value.2.model", actual: "URLValue2Model".ToNameCase(nameCase: NameCase.LowerDotCase));
    }
}
