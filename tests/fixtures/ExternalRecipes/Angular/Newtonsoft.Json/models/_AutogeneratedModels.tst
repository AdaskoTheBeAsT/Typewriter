${
    using Typewriter.Extensions.Types;
    using System.Text.RegularExpressions;
    using Typewriter.VisualStudio;

    static char separator;
    static string templatePath;
    static ILog log;
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
        separator = settings.StringLiteralCharacter;
        templatePath = settings.TemplatePath;
        log = settings.Log;
        dateLibraryImports = settings.DateLibraryImportsGeneration;
    }

    bool IncludeClass(Class c){
        if(!c.Namespace.StartsWith("AngularWebApiSample"))
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

        log.LogInfo($"Processing class {c.Name}");

        return true;
    }

    bool IncludeClassWithoutStatic(Class c){
      if(c.IsStatic){
        return false;
      }

      return IncludeClass(c);
    }

    bool IncludeClassStaticOnly(Class c){
      if(!c.IsStatic){
        return false;
      }

      return IncludeClass(c);
    }

    bool IncludeInterface(Interface i){
        if(!i.Namespace.StartsWith("AngularWebApiSample"))
        {
            return false;
        }

        var attr = i.Attributes.FirstOrDefault(p => p.Name == "GenerateFrontendType");
        if(attr == null){
            return false;
        }

        return true;
    }

    bool IncludeRecord(Record r){
        if(!r.Namespace.StartsWith("AngularWebApiSample"))
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
        if(!e.Namespace.StartsWith("AngularWebApiSample"))
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
          .Where(p => (!p.Type.IsPrimitive || p.Type.IsEnum)
                      && !p.Type.IsDictionary
                      && !p.Type.IsDynamic
                      && (p.Type.ClassName() != "string"
                      && p.Type.ClassName() != "any"
                      && p.Type.ClassName() != "Record<string, string>"
                      && p.Type.ClassName() != "T")
                      && IncludeProperty(p)
                      && p.Type.ClassName() != c.Name)
          .Select(p => $"import {{ {p.Type.ClassName()} }} from {separator}./{p.Type.ClassName()}{separator};").ToList();

        AddRuntimeValueImports(neededImports, c.Properties.Where(IncludeProperty).Select(p => p.Type));
    
        if(c.BaseClass != null && c.BaseClass.TypeArguments != null)
        {
            foreach(var typeArgument in c.BaseClass.TypeArguments)
            {
                neededImports.Add($"import {{ I{typeArgument.Name}, {typeArgument.Name} }} from {separator}./{typeArgument.Name}{separator};");
            }
        }

        if(c.BaseClass != null)
        {
            neededImports.Add($"import {{ I{c.BaseClass.ToString()}, {c.BaseClass.ToString()} }} from {separator}./{c.BaseClass.ToString()}{separator};");
        }

        return String.Join(Environment.NewLine, neededImports.Distinct());
    }

    string ImportInterface(Interface i)
    {
        var neededImports = i.Properties
          .Where(p => (!p.Type.IsPrimitive || p.Type.IsEnum)
                      && !p.Type.IsDictionary && !p.Type.IsDynamic
                      && (p.Type.ClassName() != "string"
                      && p.Type.ClassName() != "any"
                      && p.Type.ClassName() != "Record<string, string>"
                      && p.Type.ClassName() != "T")
                      && IncludeProperty(p)
                      && p.Type.ClassName() != i.Name)
          .Select(p => $"import {{ {p.Type.ClassName()} }} from {separator}./{p.Type.ClassName()}{separator};").ToList();

        AddRuntimeValueImports(neededImports, i.Properties.Where(IncludeProperty).Select(p => p.Type));
    
        if(i.IsGeneric && i.TypeArguments != null)
        {
            foreach(var typeArgument in i.TypeArguments)
            {
                if(typeArgument.IsDynamic || typeArgument.IsDictionary)
                {
                    continue;
                }

                neededImports.Add($"import {{ I{typeArgument.Name}, {typeArgument.Name} }} from {separator}./{typeArgument.Name}{separator};");
            }
        }

        foreach(var baseInterface in i.Interfaces)
        {
            neededImports.Add($"import {{ {baseInterface.ToString()} }} from {separator}./{baseInterface.ToString()}{separator};");
        }

        return String.Join(Environment.NewLine, neededImports.Distinct());
    }

    string ImportRecord(Record r)
    {
        var neededImports = r.Properties
          .Where(p => (!p.Type.IsPrimitive || p.Type.IsEnum)
                      && !p.Type.IsDictionary
                      && !p.Type.IsDynamic
                      && (p.Type.ClassName() != "string"
                      && p.Type.ClassName() != "any"
                      && p.Type.ClassName() != "Record<string, string>"
                      && p.Type.ClassName() != "T")
                      && IncludeProperty(p)
                      && p.Type.ClassName() != r.Name)
          .Select(p => $"import {{ {p.Type.ClassName()} }} from {separator}./{p.Type.ClassName()}{separator};").ToList();

        AddRuntimeValueImports(neededImports, r.Properties.Where(IncludeProperty).Select(p => p.Type));
    
        if(r.BaseRecord != null && r.BaseRecord.TypeArguments != null)
        {
            foreach(var typeArgument in r.BaseRecord.TypeArguments)
            {
                neededImports.Add($"import {{ I{typeArgument.Name}, {typeArgument.Name} }} from {separator}./{typeArgument.Name}{separator};");
            }
        }

        if(r.BaseRecord != null)
        {
            neededImports.Add($"import {{ I{r.BaseRecord.ToString()}, {r.BaseRecord.ToString()} }} from {separator}./{r.BaseRecord.ToString()}{separator};");
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

    string InheritInterfaceForInterfaces(Interface i)
    {
        var list = new List<string>();
        foreach(var si in i.Interfaces) {
            if(si.IsGeneric)
            {
                list.Add($"{si.Name}<{si.TypeArguments.First()}>");
            }
            else
            {
                list.Add($"{si.Name}");
            }
        }
        if(list.Count == 0){
            return string.Empty;
        }
        
        return $" extends {string.Join(" ,", list)}";
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

    string GetDiscriminator(IAttributeCollection attributes) {
        var discriminator = "$type";
        var attr = attributes.FirstOrDefault(p => p.Name == "JsonPolymorphic");
        if(attr != null && !string.IsNullOrEmpty(attr.Value)) {
            var regex = new Regex(@"TypeDiscriminatorPropertyName\s*=\s*[""]([^""]*)[""]");
            discriminator = regex.Replace(attr.Value, "$1");
        }
        return discriminator;
    }

   string GenerateTypeForInterfaceByClass(Class c){
        var returnValue = string.Empty;
        if(c.BaseClass == null)
        {
            var discriminator = GetDiscriminator(c.Attributes);
            if(c.Attributes.Any(a => a.Name == "JsonDerivedType"))
            {
                returnValue = $"{Environment.NewLine}  {discriminator}?: string | number;";
            }            
        }
        return returnValue;
    }

    string GenerateTypeForInterfaceByRecord(Record r){
        var returnValue = string.Empty;
        if(r.BaseRecord == null)
        {
            var discriminator = GetDiscriminator(r.Attributes);
            if(r.Attributes.Any(a => a.Name == "JsonDerivedType"))
            {
                returnValue = $"{Environment.NewLine}  {discriminator}?: string | number;";
            }
        }
        return returnValue;
    }

    string GenerateTypeForClass(Class c){
        var discriminator = GetDiscriminator(c.Attributes);
        return c.BaseClass == null ? $"{Environment.NewLine}  public {discriminator}: string;" : string.Empty;
    }

    string GenerateTypeForRecord(Record r){
        var discriminator = GetDiscriminator(r.Attributes);
        return r.BaseRecord == null ? $"{Environment.NewLine}  public {discriminator}: string;" : string.Empty;
    }

    string GenerateTypeForClass2(Class c){
        var returnValue = string.Empty;
        if(c.BaseClass == null)
        {
            var discriminator = GetDiscriminator(c.Attributes);
            if(c.Attributes.Any(a => a.Name == "JsonDerivedType"))
            {
                returnValue = $"{Environment.NewLine}  public {discriminator}?: string | number;";
            }
        }
        return returnValue;
    }

    string GenerateTypeForRecord2(Record r){
        var returnValue = string.Empty;
        if(r.BaseRecord == null)
        {
            var discriminator = GetDiscriminator(r.Attributes);
            if(r.Attributes.Any(a => a.Name == "JsonDerivedType"))
            {
                returnValue = $"{Environment.NewLine}  public {discriminator}?: string | number;";
            }
        }
        return returnValue;
    }

    string GenerateTypeInitForClass(Class c){
        var dllName = c.AssemblyName;
        var attr = c.Attributes.FirstOrDefault(p => p.Name == "GenerateFrontendType");
        if(attr != null && !string.IsNullOrEmpty(attr.Value)) {
            dllName = attr.Value;
        }
        var discriminator = GetDiscriminator(c.Attributes);
        return $"{Environment.NewLine}    this.{discriminator} = {separator}{c.FullName},{separator}\r\n            + {separator}{dllName}{separator};";
    }

    string GenerateTypeInitForRecord(Record r){
        var dllName = r.AssemblyName;
        var attr = r.Attributes.FirstOrDefault(p => p.Name == "GenerateFrontendType");
        if(attr != null && !string.IsNullOrEmpty(attr.Value)) {
            dllName = attr.Value;
        }
        var discriminator = GetDiscriminator(r.Attributes);
        return $"{Environment.NewLine}    this.{discriminator} = {separator}{r.FullName},{separator}\r\n            + {separator}{dllName}{separator};";
    }

    string GenerateTypeInitForClass2(Class c){
        var baseClass = c;
        while (baseClass?.BaseClass != null) {
            baseClass = baseClass.BaseClass;
        }

        if(!baseClass.Attributes.Any(a => a.Name == "JsonDerivedType"))
        {
            return string.Empty;
        }

        var discriminator = GetDiscriminator(baseClass.Attributes);
        
        var attrs = baseClass.Attributes.Where(p => p.Name == "JsonDerivedType");
        var regex1 = new Regex(@"\s*typeof\s*[(]\s*([^)\s]+)[)].*");
        var regexString = new Regex(@"[^,]*[,]\s*[""]([^""]*)[""].*");
        var regexNumber = new Regex(@"[^,]*[,]\s*(\d).*");
        foreach (var attr in attrs) {
            var typeName = regex1.Replace(attr.Value, "$1");
            if(typeName == c.FullName) {
                if (regexString.IsMatch(attr.Value)) {
                  var value = regexString.Replace(attr.Value, "$1");
                  return $"{Environment.NewLine}    this.{discriminator} = {separator}{value}{separator};";
                }
                else if (regexNumber.IsMatch(attr.Value)) {
                  var value = regexNumber.Replace(attr.Value, "$1");
                  return $"{Environment.NewLine}    this.{discriminator} = {value};";
                }
            }
        }

        return $"{Environment.NewLine}    this.{discriminator} = {separator}{separator};";
    }

    string GenerateTypeInitForRecord2(Record r){
        var baseRecord = r;
        while (baseRecord?.BaseRecord != null) {
            baseRecord = baseRecord.BaseRecord;
        }

        if(!baseRecord.Attributes.Any(a => a.Name == "JsonDerivedType"))
        {
            return string.Empty;
        }

        var discriminator = GetDiscriminator(baseRecord.Attributes);
        
        var attrs = baseRecord.Attributes.Where(p => p.Name == "JsonDerivedType");
        var regex1 = new Regex(@"\s*typeof\s*[(]\s*([^)\s]+)[)].*");
        var regexString = new Regex(@"[^,]*[,]\s*[""]([^""]*)[""].*");
        var regexNumber = new Regex(@"[^,]*[,]\s*(\d).*");
        foreach (var attr in attrs) {
            var typeName = regex1.Replace(attr.Value, "$1");
            if(typeName == r.FullName) {
                if (regexString.IsMatch(attr.Value)) {
                  var value = regexString.Replace(attr.Value, "$1");
                  return $"{Environment.NewLine}    this.{discriminator} = {separator}{value}{separator};";
                }
                else if (regexNumber.IsMatch(attr.Value)) {
                  var value = regexNumber.Replace(attr.Value, "$1");
                  return $"{Environment.NewLine}    this.{discriminator} = {value};";
                }
            }
        }

        return $"{Environment.NewLine}    this.{discriminator} = {separator}{separator};";
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
            return $"{separator}"+enumObj.Name+$"{separator}";
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

    string GetPropertyName(Property property){
        var attr1 = property.Attributes.FirstOrDefault(p => p.Name == "JsonPropertyName");
        if(attr1 != null && !string.IsNullOrEmpty(attr1.Value)) {
            var regex1 = new Regex(@".*nameof[(]([^)]*)[)].*");
            var match1 = regex1.Match(attr1.Value);
            if(match1.Success){
                return match1.Groups[1].Value;
            } else {
                return attr1.Value;
            }
        }

        var attr2 = property.Attributes.FirstOrDefault(p => p.Name == "JsonProperty");
        if(attr2 != null &&
            !string.IsNullOrEmpty(attr2.Value)) {
            var regex2 = new Regex(@"(.*PropertyName\s*=\s*)?[""]([^""]*)[""].*");
            var match2 = regex2.Match(attr2.Value);
            if(match2.Success){
                return match2.Groups[2].Value;
            }

            var regex3 = new Regex(@"(.*PropertyName\s*=\s*)?nameof[(]([^)]*)[)].*");
            var match3 = regex3.Match(attr2.Value);
            if(match3.Success){
                return match3.Groups[2].Value;
            }

            var regex4 = new Regex(@".*nameof[(]([^)]*)[)].*");
            var match4 = regex4.Match(attr2.Value);
            if(match4.Success){
                return match4.Groups[1].Value;
            }

            if(attr2.Value.IndexOf("=")<0){
                return attr2.Value;
            }
        }

        return property.name;
    }

    char Sep(Enum e){
        return separator;
    }

    char Sep(EnumValue e){
        return separator;
    }

    char Sep(Constant c){
        return separator;
    }

    string SanitizeDefault(Type t){
        var d = t.Default();
        if(d.StartsWith("new ") && !t.IsDate){
            return "null";
        }

        return d;
    }

    string LoudName(Constant constant)
    {
        return constant.Name.ToUpperInvariant();
    }
}/* eslint-disable @typescript-eslint/no-explicit-any */
$Enums($IncludeEnums)[
export enum $Name {$Values[
  $Name = $GetEnumAsStringIfItsStringable][,
]
}

// eslint-disable-next-line @typescript-eslint/no-namespace
export namespace $Name {
  export function getLabel(value: $Name): string {
    let toReturn = $Sep$Sep;
    switch(value) {$Values[
      case $Parent[$Name].$Name:
        toReturn = $Sep$GetAttributeValueOrReturnEnumNameIfNoAttribute$Sep;
        break;][]
    }
    return toReturn;
  }$IsEnumAsNumber[

  export function getKeys(): Array<number> {
    const list = new Array<number>();
    for (const enumMember in $Name) {
      const parsed = Number.parseInt(enumMember, 10);
      if (parsed < 0) {
        continue;
      }

      list.push(parsed);
    }

    return list;
  }]
}
]
$Interfaces($IncludeInterface)[
$ImportInterface

export interface $Name$TypeParameters$InheritInterfaceForInterfaces {$Properties($IncludeProperty)[
  $GetPropertyName?: $Type[$Name];]
}
]
$Classes($IncludeClassWithoutStatic)[
$ImportClass

export interface I$Name$TypeParameters$InheritInterfaceForClass {$GenerateTypeForInterfaceByClass$Properties($IncludeProperty)[
  $GetPropertyName?: $Type[$Name];]
}

export class $Name$TypeParameters$InheritClass$ImplementsInterfaceForClass {$GenerateTypeForClass$Properties($IncludeProperty)[
  public $GetPropertyName$NullableMark: $Type[$Name];]

  constructor(initObj?: I$Name$TypeParameters) {$SuperClass$GenerateTypeInitForClass
    if (initObj) {$Properties($IncludeProperty)[
      this.$GetPropertyName = initObj.$GetPropertyName ?? $Type[$SanitizeDefault];]
    } else {$Properties($IncludeProperty)[
      this.$GetPropertyName = $Type[$SanitizeDefault];]
    }
  }
}]
$Classes($IncludeClassStaticOnly)[$Constants[
export const $LoudName: string = $Sep$Value$Sep;]
]
$Records($IncludeRecord)[
$ImportRecord

export interface I$Name$TypeParameters$InheritInterfaceForRecord {$GenerateTypeForInterfaceByRecord$Properties($IncludeProperty)[
  $GetPropertyName?: $Type[$Name];]
}

export class $Name$TypeParameters$InheritRecord$ImplementsInterfaceForRecord {$GenerateTypeForRecord$Properties($IncludeProperty)[
  public $GetPropertyName$NullableMark: $Type[$Name];]

  constructor(initObj?: I$Name$TypeParameters) {$SuperRecord$GenerateTypeInitForRecord
    if (initObj) {$Properties($IncludeProperty)[
      this.$GetPropertyName = initObj.$GetPropertyName ?? $Type[$SanitizeDefault];]
    } else {$Properties($IncludeProperty)[
      this.$GetPropertyName = $Type[$SanitizeDefault];]
    }
  }
}]

