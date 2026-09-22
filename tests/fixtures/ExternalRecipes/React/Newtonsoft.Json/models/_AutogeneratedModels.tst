${
    using Typewriter.Extensions.Types;

    static string dateLibraryImports = string.Empty;
    
    Template(Settings settings)
    {
        settings
            .IncludeCurrentProject()
            .IncludeReferencedProjects()
            .UseStringLiteralCharacter('\'')
            .DisableUtf8BomGeneration()
            .UseDateLibrary(DateLibrary.Legacy)
            .UseGuidType("string")
            .UseGuidInitializer("auto")
            .UseDecimalType("Decimal")
            .UseDecimalInitializer("auto")
            ;
        dateLibraryImports = settings.DateLibraryImportsGeneration;
    }

    bool IncludeClass(Class c){
        if(!c.Namespace.StartsWith("ReactWebApiSample"))
        {
            return false;
        }

        var attr = c.Attributes.FirstOrDefault(p => p.Name == "GenerateFrontendType");
        if(attr == null){
            return false;
        }

        var parent = c.BaseClass;
        if(parent != null){
            if(parent.Name.EndsWith("Controller")
          || parent.Name.EndsWith("ControllerBase"))
          {
            return false;
          }
        }        

        return true;
    }

    bool IncludeRecord(Record r){
        if(!r.Namespace.StartsWith("ReactWebApiSample"))
        {
            return false;
        }

        var attr = r.Attributes.FirstOrDefault(p => p.Name == "GenerateFrontendType");
        if(attr == null){
            return false;
        }

        var parent = r.BaseRecord;
        if(parent != null){
            if(parent.Name.EndsWith("Controller")
          || parent.Name.EndsWith("ControllerBase"))
          {
            return false;
          }
        }        

        return true;
    }

    bool IncludeEnums(Enum e){
        if(!e.Namespace.StartsWith("ReactWebApiSample"))
        {
            return false;
        }

        var attr = e.Attributes.FirstOrDefault(p => p.Name == "GenerateFrontendType");
        if(attr == null){
            return false;
        }

        return true;
    }

    string ImportClass(Class c)
    {
        var neededImports = c.Properties
          .Where(p => (!p.Type.IsPrimitive || p.Type.IsEnum) && (p.Type.ClassName() != "string" && p.Type.ClassName() != "any" && p.Type.ClassName() != "T") && IncludeProperty(p))
          .Select(p => $"import {{ {p.Type.ClassName()} }} from './{p.Type.ClassName()}';").ToList();

        AddRuntimeValueImports(neededImports, c.Properties.Where(IncludeProperty).Select(p => p.Type));
    
        if(c.BaseClass != null && c.BaseClass.TypeArguments != null)
        {
            foreach(var typeArgument in c.BaseClass.TypeArguments)
            {
                neededImports.Add($"import {{ I{typeArgument.Name}, {typeArgument.Name} }} from './{typeArgument.Name}';");
            }
        }

        if(c.BaseClass != null)
        {
            neededImports.Add($"import {{ I{c.BaseClass.ToString()}, {c.BaseClass.ToString()} }} from './{c.BaseClass.ToString()}';");
        }

        return String.Join(Environment.NewLine, neededImports.Distinct());
    }

    string ImportRecord(Record r)
    {
        var neededImports = r.Properties
          .Where(p => (!p.Type.IsPrimitive || p.Type.IsEnum) && (p.Type.ClassName() != "string" && p.Type.ClassName() != "any" && p.Type.ClassName() != "T") && IncludeProperty(p))
          .Select(p => $"import {{ {p.Type.ClassName()} }} from './{p.Type.ClassName()}';").ToList();

        AddRuntimeValueImports(neededImports, r.Properties.Where(IncludeProperty).Select(p => p.Type));
    
        if(r.BaseRecord != null && r.BaseRecord.TypeArguments != null)
        {
            foreach(var typeArgument in r.BaseRecord.TypeArguments)
            {
                neededImports.Add($"import {{ I{typeArgument.Name}, {typeArgument.Name} }} from './{typeArgument.Name}';");
            }
        }

        if(r.BaseRecord != null)
        {
            neededImports.Add($"import {{ I{r.BaseRecord.ToString()}, {r.BaseRecord.ToString()} }} from './{r.BaseRecord.ToString()}';");
        }

        return String.Join(Environment.NewLine, neededImports.Distinct());
    }

    void AddRuntimeValueImports(List<string> imports, IEnumerable<Type> types)
    {
        if(types.Any(UsesDate) && !string.IsNullOrEmpty(dateLibraryImports))
        {
            imports.Add(dateLibraryImports);
        }

        if(types.Any(UsesDecimal))
        {
            imports.Add("import Decimal from 'decimal.js';");
        }
    }

    bool UsesDate(Type type)
    {
        return type.IsDate
            || type.FullName == "System.TimeSpan"
            || (type.ElementType != null && UsesDate(type.ElementType))
            || type.TypeArguments.Any(UsesDate);
    }

    bool UsesDecimal(Type type)
    {
        return type.FullName == "System.Decimal"
            || (type.ElementType != null && UsesDecimal(type.ElementType))
            || type.TypeArguments.Any(UsesDecimal);
    }

    string InheritClass(Class c)
    {
        if(c.BaseClass != null)
        {
            if(c.BaseClass.IsGeneric)
            {
                return $" extends {c.BaseClass.ToString()}<{c.BaseClass.TypeArguments.First()}>";
            }
            else
            {
                return $" extends {c.BaseClass.ToString()}";
            }
        }
        else
        {
            return string.Empty;
        }
    }

    string InheritRecord(Record r)
    {
        if(r.BaseRecord != null)
        {
            if(r.BaseRecord.IsGeneric)
            {
                return $" extends {r.BaseRecord.ToString()}<{r.BaseRecord.TypeArguments.First()}>";
            }
            else
            {
                return $" extends {r.BaseRecord.ToString()}";
            }
        }
        else
        {
            return string.Empty;
        }
    }

    string InheritInterfaceForClass(Class c)
    {
        if(c.BaseClass != null)
        {
            if(c.BaseClass.IsGeneric)
            {
                return $" extends I{c.BaseClass.ToString()}<{c.BaseClass.TypeArguments.First()}>";
            }
            else
            {
                return $" extends I{c.BaseClass.ToString()}";
            }
        }
        else
        {
            return string.Empty;
        }
    }

    string InheritInterfaceForRecord(Record r)
    {
        if(r.BaseRecord != null)
        {
            if(r.BaseRecord.IsGeneric)
            {
                return $" extends I{r.BaseRecord.ToString()}<{r.BaseRecord.TypeArguments.First()}>";
            }
            else
            {
                return $" extends I{r.BaseRecord.ToString()}";
            }
        }
        else
        {
            return string.Empty;
        }
    }

    string ImplementsInterfaceForClass(Class c)
    {
        if(c.IsGeneric)
        {
            return $" implements I{c.ToString()}<{c.TypeArguments.First()}>";
        }
        else
        {
            return $" implements I{c.ToString()}";
        }
    }

    string ImplementsInterfaceForRecord(Record r)
    {
        if(r.IsGeneric)
        {
            return $" implements I{r.ToString()}<{r.TypeArguments.First()}>";
        }
        else
        {
            return $" implements I{r.ToString()}";
        }
    }

    string SuperClass(Class c){
        if(c.BaseClass == null)
        {
            return string.Empty;
        }
        return $"{Environment.NewLine}    super(initObj);";
    }

    string SuperRecord(Record r){
        if(r.BaseRecord == null)
        {
            return string.Empty;
        }
        return $"{Environment.NewLine}    super(initObj);";
    }

    string GenerateTypeForInterfaceByClass(Class c){
        return c.BaseClass == null ? $"{Environment.NewLine}  $type?: string;" : string.Empty;
    }

    string GenerateTypeForInterfaceByRecord(Record r){
        return r.BaseRecord == null ? $"{Environment.NewLine}  $type?: string;" : string.Empty;
    }

    string GenerateTypeForClass(Class c){
        return c.BaseClass == null ? $"{Environment.NewLine}  public $type: string;" : string.Empty;
    }

    string GenerateTypeForRecord(Record r){
        return r.BaseRecord == null ? $"{Environment.NewLine}  public $type: string;" : string.Empty;
    }

    string GenerateTypeInitForClass(Class c){
        var dllName = c.Namespace;
        var attr = c.Attributes.FirstOrDefault(p => p.Name == "GenerateFrontendType");
        if(attr != null && !string.IsNullOrEmpty(attr.Value)) {
            dllName = attr.Value;
        }
        return $"this.$type = '{c.FullName},'\r\n            + '{dllName}';";
    }

    string GenerateTypeInitForRecord(Record r){
        var dllName = r.Namespace;
        var attr = r.Attributes.FirstOrDefault(p => p.Name == "GenerateFrontendType");
        if(attr != null && !string.IsNullOrEmpty(attr.Value)) {
            dllName = attr.Value;
        }
        return $"this.$type = '{r.FullName},'\r\n            + '{dllName}';";
    }

    string NullableMark(Property property) {
      return property.Type.IsNullable ? "?" : string.Empty;
    }

    string ReturnTypeDefault(Type type) {
        return type.Default().Replace("\"", $"{(char)39}");
    }

    string GetAttributeValueOrReturnEnumNameIfNoAttribute(EnumValue enumObj) {
        if (enumObj.Attributes.Any(a=>a.Name=="LabelForEnum")) {
            return enumObj.Attributes.First(a=>a.Name=="LabelForEnum").Value;
        } else {
            return enumObj.Name;
        }
    }

    bool IsEnumAsNumber(Enum e) {
      if(e.Attributes.Any(a=>a.Name=="AsString")){
        return false;
      }
      return true;
    }

    string GetEnumAsStringIfItsStringable(EnumValue enumObj) {
        var parent = (enumObj.Parent as Enum);
        if(parent.Attributes.Any(a=>a.Name=="AsString")){
            return "'"+enumObj.Name+"'";
        } else {
            return enumObj.Value.ToString();
        }
    }

    bool IncludeProperty(Property property) {
        var attr = property.Attributes.FirstOrDefault(p => p.Name == "JsonIgnore");
        if(attr != null){
            return false;
        }
        return true;
    }
}// This file has been AUTOGENERATED by TypeWriter (https://github.com/adaskothebeast/Typewriter).
// Do not modify it.
$Enums($IncludeEnums)[
export enum $Name {$Values[
  $Name = $GetEnumAsStringIfItsStringable][,
]
}

// eslint-disable-next-line @typescript-eslint/no-namespace
export namespace $Name {
  export function getLabel(value: $Name): string {
    let toReturn = '';
    switch(value) {$Values[
      case $Parent[$Name].$Name:
        toReturn = '$GetAttributeValueOrReturnEnumNameIfNoAttribute';
        break;][]
    }
    return toReturn;
  }$IsEnumAsNumber[

  export function getKeys(): Array<number> {
    const list = new Array<number>();
    for (const enumMember in $Name) {
      const parsed = parseInt(enumMember, 10);
      if (parsed < 0) {
        continue;
      }

      list.push(parsed);
    }

    return list;
  }]
}
]
$Classes($IncludeClass)[
$ImportClass

export interface I$Name$TypeParameters$InheritInterfaceForClass {$GenerateTypeForInterfaceByClass$Properties($IncludeProperty)[
  $name?: $Type[$Name];]
}

export class $Name$TypeParameters$InheritClass$ImplementsInterfaceForClass {$GenerateTypeForClass$Properties($IncludeProperty)[
  public $name$NullableMark: $Type[$Name];]

  constructor(initObj?: I$Name$TypeParameters) {$SuperClass
    $GenerateTypeInitForClass
    if (initObj) {$Properties($IncludeProperty)[
      this.$name = initObj.$name ?? $Type[$Default];]
    } else {$Properties($IncludeProperty)[
      this.$name = $Type[$Default];]
    }
  }
}]
$Records($IncludeRecord)[
$ImportRecord

export interface I$Name$TypeParameters$InheritInterfaceForRecord {$GenerateTypeForInterfaceByRecord$Properties($IncludeProperty)[
  $name?: $Type[$Name];]
}

export class $Name$TypeParameters$InheritRecord$ImplementsInterfaceForRecord {$GenerateTypeForRecord$Properties($IncludeProperty)[
  public $name$NullableMark: $Type[$Name];]

  constructor(initObj?: I$Name$TypeParameters) {$SuperRecord
    $GenerateTypeInitForRecord
    if (initObj) {$Properties($IncludeProperty)[
      this.$name = initObj.$name ?? $Type[$Default];]
    } else {$Properties($IncludeProperty)[
      this.$name = $Type[$Default];]
    }
  }
}]

