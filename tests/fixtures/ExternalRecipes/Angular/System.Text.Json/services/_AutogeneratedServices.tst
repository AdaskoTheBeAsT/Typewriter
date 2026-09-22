${
    using Typewriter.Extensions.Types;
    using Typewriter.Extensions.WebApi;
    using System.Text;
    using System.Text.RegularExpressions;

    // setting template
    Template(Settings settings)
    {
        settings.IncludeCurrentProject();

        // file should be named same as controller name with 'Service' suffix
        settings.OutputFilenameFactory = file => $"api-{Hyphenated(file.Classes.First().Name.Replace("Controller", string.Empty))}.service.ts";
    }

    public class NullParam
        : Parameter
    {
        private string _name;
        public NullParam() {
            _name = "null";
        }
        public override string name => _name;
        public override string FullName => string.Empty;
        public override bool HasDefaultValue => false;
        public override string DefaultValue => string.Empty;
        public override Type Type => null;
        public override string Name => string.Empty;
        public override IAttributeCollection Attributes => null;
        public override Item Parent => null;
        public override string AssemblyName => string.Empty;
    }

    public class AdvancedTypeInfo
    {
        public AdvancedTypeInfo(string typeName, string fileName, bool isEnum)
        {
            TypeName = typeName;
            FileName = fileName;
            IsEnum = isEnum;
         }

         public string TypeName { get; }

         public string FileName { get; }

         public bool IsEnum { get; }
    }

    string Hyphenated(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder();
        foreach (var ch in s.ToCharArray())
            if (char.IsUpper(ch))
            {
                if (sb.Length > 0)
                {
                    sb.Append("-");
                }
                sb.Append(char.ToLower(ch));
            }
            else
            {
                sb.Append(ch);
            }

        return sb.ToString();
    }

    // returns type of the method to typescript
    string ReturnType(Method m)
    {
        // check if there is special attribute for response type and take type from there
        var attr = m.Attributes.FirstOrDefault(a => a.Name == "ProducesResponseType");
        if(attr != null){
            // due to limited functionality of attribute value process value by regexp
            var r = new Regex(".*typeof[(]([^.<]*[.])*([^)<]*)(([<])([^.<]*[.])*([^)>]*)([>]))?[)].*");
            var value = r.Replace(attr.Value, "$2$4$6$7");
            switch(value)
            {
                case "bool":
                    value = "boolean";
                    break;
                case "int":
                    value = "number";
                    break;
                case "decimal":
                    value = "number";
                    break;
            }

            return value;
        }

        // if there is only IActionResult return void as type
        return m.Type.Name == "IActionResult" ? "void" : m.Type.Name;
    }

    Type ResponseSchemaType(Method method)
    {
        var responseAttribute = method.Attributes
            .FirstOrDefault(attribute => attribute.Name == "ProducesResponseType");
        var responseType = responseAttribute?.Arguments
            .FirstOrDefault(argument => argument.TypeValue != null)
            ?.TypeValue;
        if (responseType != null)
        {
            return responseType;
        }

        return method.Type == null
            || method.Type.Name == "IActionResult"
            || method.Type.Name == "ActionResult"
            ? null
            : method.Type.Unwrap();
    }

    Parameter BodyParameter(Method method)
    {
        var parameters = SkipParameters(method);
        var explicitBody = parameters.FirstOrDefault(
            parameter => parameter.Attributes.Any(attribute => attribute.Name == "FromBody"));
        if (explicitBody != null)
        {
            return explicitBody;
        }

        var httpMethod = method.HttpMethod();
        return httpMethod == "get" || httpMethod == "delete"
            ? null
            : parameters.FirstOrDefault();
    }

    string SchemaExpression(Type type)
    {
        var core = SchemaExpressionCore(type);
        return type != null && type.IsNullable
            ? $"schema.nullable({core})"
            : core;
    }

    string SchemaExpressionCore(Type type)
    {
        if (type == null)
        {
            return "schema.unknown()";
        }

        if (type.IsDictionary)
        {
            var valueType = type.TypeArguments.Count > 1 ? type.TypeArguments[1] : null;
            return valueType == null
                ? "schema.record(schema.unknown())"
                : $"schema.record({SchemaExpression(valueType)})";
        }

        if (type.IsEnumerable)
        {
            return type.ElementType == null
                ? "schema.array(schema.unknown())"
                : $"schema.array({SchemaExpression(type.ElementType)})";
        }

        if (type.IsGuid)
        {
            return "schema.uuid()";
        }

        if (IsDecimalType(type))
        {
            return "schema.decimal('string')";
        }

        if (type.IsTimeSpan)
        {
            return "schema.duration()";
        }

        if (type.IsDate)
        {
            return DateSchema(type);
        }

        if (type.IsEnum)
        {
            return "schema.number()";
        }

        var original = type.OriginalName ?? type.Name ?? string.Empty;
        if (original.Equals("String", StringComparison.OrdinalIgnoreCase)
            || original.Equals("string", StringComparison.OrdinalIgnoreCase))
        {
            return "schema.string()";
        }

        if (original.Equals("Boolean", StringComparison.OrdinalIgnoreCase)
            || original.Equals("bool", StringComparison.OrdinalIgnoreCase))
        {
            return "schema.boolean()";
        }

        return type.IsPrimitive
            ? "schema.number()"
            : type.Name + "Schema";
    }

    bool IsDecimalType(Type type)
    {
        var original = type?.OriginalName ?? string.Empty;
        return original.Equals("decimal", StringComparison.OrdinalIgnoreCase)
            || original.Equals("System.Decimal", StringComparison.Ordinal);
    }

    string DateSchema(Type type)
    {
        return (type.OriginalName ?? string.Empty) switch
        {
            "DateTimeOffset" => "schema.instant()",
            "DateOnly" => "schema.plainDate()",
            "TimeOnly" => "schema.plainTime()",
            _ => "schema.plainDateTime()",
        };
    }

    string SchemaContext(Method method)
    {
        var responseType = ResponseSchemaType(method);
        var body = BodyParameter(method);
        if (responseType != null && body != null)
        {
            return "context: withTypewriterResponseSchema("
                + SchemaExpression(responseType)
                + ", { context: withTypewriterRequestSchema("
                + SchemaExpression(body.Type)
                + ", { registry: apiTypeRegistry }), registry: apiTypeRegistry }),";
        }

        if (responseType != null)
        {
            return "context: withTypewriterResponseSchema("
                + SchemaExpression(responseType)
                + ", { registry: apiTypeRegistry }),";
        }

        return body == null
            ? string.Empty
            : "context: withTypewriterRequestSchema("
                + SchemaExpression(body.Type)
                + ", { registry: apiTypeRegistry }),";
    }

    string SchemaImports(Class c)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var method in c.Methods)
        {
            AddSchemaModel(ResponseSchemaType(method), names);
            AddSchemaModel(BodyParameter(method)?.Type, names);
        }

        return string.Join(
            Environment.NewLine,
            names.Select(name => $"import {{ {name}Schema }} from '../models/{name}.schema';"));
    }

    void AddSchemaModel(Type type, ISet<string> names)
    {
        if (type == null)
        {
            return;
        }

        if (type.IsDictionary)
        {
            if (type.TypeArguments.Count > 1)
            {
                AddSchemaModel(type.TypeArguments[1], names);
            }
            return;
        }

        if (type.IsEnumerable)
        {
            AddSchemaModel(type.ElementType, names);
            return;
        }

        if (!type.IsPrimitive
            && !type.IsEnum
            && !type.IsDate
            && !type.IsGuid
            && !type.IsTimeSpan
            && !IsDecimalType(type)
            && !string.IsNullOrWhiteSpace(type.Name))
        {
            names.Add(type.Name);
        }
    }

    bool UsesInlineSchema(Class c)
    {
        return c.Methods.Any(method =>
            UsesInlineSchema(ResponseSchemaType(method))
            || UsesInlineSchema(BodyParameter(method)?.Type));
    }

    bool UsesInlineSchema(File file)
    {
        return file.Classes.Any(UsesInlineSchema);
    }

    bool UsesInlineSchema(Type type)
    {
        if (type == null)
        {
            return false;
        }

        return type.IsDictionary
            || type.IsEnumerable
            || type.IsPrimitive
            || type.IsEnum
            || type.IsDate
            || type.IsGuid
            || type.IsTimeSpan
            || IsDecimalType(type)
            || string.IsNullOrWhiteSpace(type.Name);
    }

    // get angular service name based on controller class name
    string ServiceName(Class c) => $"Api{c.Name.Replace("Controller", "Service")}";

    // get angular service name based on controller in which given method is defined
    string ParentServiceName(Method m) => ServiceName((Class)m.Parent);

    // get method name
    string MethodName(Method m)
    {
        var methodName = m.Attributes.FirstOrDefault(a => a.Name.StartsWith("CustomName"))?.Value ?? string.Empty;
        if(!string.IsNullOrEmpty(methodName)) {
            return methodName;
        }
        var sb = new StringBuilder(m.name);
        foreach(var par in m.Parameters) {
            var nameOfType = NameOfType(par.Type);
            if(nameOfType == "CancellationToken"){
                continue;
            }

            sb.Append(nameOfType);
        }
        return sb.ToString();
    }

    // returns true if class should be treated as candidate for angular service
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
        if(parent == null){
            return false;
        }

        // all controllers are subclasses of Controller or ControllerBase
        if((parent.Name.EndsWith("Controller")
          || parent.Name.EndsWith("ControllerBase"))
          && !c.FullName.Contains("LogFileController"))
        {
            return true;
        }

        return false;
    }

    // generates imports in typescript
    string Imports(Class c)
    {
        var typeNameList = new List<AdvancedTypeInfo>();

        //walk through all the method of controller
        foreach(var method in c.Methods) {
            // generate list of candidates for imports only from non-primitive types
            if(!method.Type.IsPrimitive) {
                // check if method has special ProducesResponseType attribute and get type from there
                var attr = method.Attributes.FirstOrDefault(a => a.Name == "ProducesResponseType");
                if(attr != null){
                    var regexGenericExists = new Regex(".*typeof[(][^<]*[<][^>]*[>][)].*");
                    var match = regexGenericExists.Match(attr.Value);
                    if(match.Success)
                    {
                        var takeTwoTypesRegex = new Regex(@".*typeof[(]([^.<]*[.])*(?<firstType>[^)<]*)(([<])([^.<]*[.])*(?<secondType>[^)>[\]]*)([[][\]])?([>]))?[)].*");
                        var matchTwoTypes = takeTwoTypesRegex.Match(attr.Value);
                        var firstType = matchTwoTypes.Groups["firstType"].Value;
                        var secondType = matchTwoTypes.Groups["secondType"].Value;
                        typeNameList.Add(new AdvancedTypeInfo( firstType, $"{firstType}{{T}}", false));
                        typeNameList.Add(new AdvancedTypeInfo(secondType, secondType, false));
                    }
                    else
                    {
                        // due to limited functionality of attribute value process value by regexp
                        // additionally removing [] from the end of the type as imports can be done only on normal class name
                        var r = new Regex(@".*typeof[(]([^.]*[.])*([^)[\]]*)([[][\]])?[)].*");
                        var name = r.Replace(attr.Value, "$2");
                        if(!SkipReturnType(name) && !IsDictionary(name)){
                            typeNameList.Add(new AdvancedTypeInfo(name, name, false));
                        }
                    }
                }
                else { // if there is no attribute just get type name
                    var name = method.Type.Unwrap().Name;

                    // IActionResult should be skipped
                    if(name != "IActionResult") {
                        if(!IsDictionary(name)){
                            typeNameList.Add(new AdvancedTypeInfo(name, name, false));
                        }
                    }
                }
            }

            // walk through each parameter in method
            foreach(var parameter in method.Parameters)
            {
                // skip if it is primitive
                if(parameter.Type.IsPrimitive){
                    continue;
                }

                var name = parameter.Type.Unwrap().Name;
                var nameOfType = NameOfType(parameter.Type);
                if(parameter.Type.IsEnum){
                    typeNameList.Add(new AdvancedTypeInfo(name, name, true));
                }
                else if(!IsDictionary(name) && nameOfType != "CancellationToken"){

                    // add type to list
                    typeNameList.Add(new AdvancedTypeInfo(name, name, false));
                }
            }
        }

        var sb = new StringBuilder();
        foreach(var type in typeNameList.GroupBy(p => p.TypeName).Select(grp => grp.FirstOrDefault())){
            sb.AppendLine($"import {{ {type.TypeName} }} from '../models/{type.FileName}';");
        }

        return sb.ToString();
    }

    bool SkipReturnType(string name) {
        switch(name){
            case "string":
            case "int":
            case "bool":
            case "decimal":
            case "double":
                return true;
            default:
                return false;
        }
    }

    bool IsPrimitive(Parameter parameter){
        if(parameter.Type == "string")
        {
            return true;
        }

        if(parameter.Type == "number")
        {
            return true;
        }

        if(parameter.Type == "boolean")
        {
            return true;
        }

        return false;
    }


    // gets name of the url field in service
    string UrlFieldName(Class c) => $"{c.name.Replace("Controller", "Service")}Url";

    string HttpGetActionNameByAttribute(Method m){
        return GetActionNameByAttribute(m, "HttpGet");
    }

    string HttpPostActionNameByAttribute(Method m){
        return GetActionNameByAttribute(m, "HttpPost");
    }

    string HttpPutActionNameByAttribute(Method m){
        return GetActionNameByAttribute(m, "HttpPut");
    }

    string HttpDeleteActionNameByAttribute(Method m){
        return GetActionNameByAttribute(m, "HttpDelete");
    }

    string GetActionNameByAttribute(Method m, string name) {
        var value = m.Attributes.FirstOrDefault(a => a.Name == name)?.Value ?? string.Empty;
        if(!string.IsNullOrEmpty(value)){
             return "/"+value;
        }else{
            return string.Empty;
        }
    }

    // generates getter implementation for url - only controllers with Attribute Routing are processed
    string GetRouteValue(Class c)
    {
        var route = c.Attributes.Where(a => a.Name == "Route").FirstOrDefault();
        if(route == null)
        {
            return string.Empty;
        }

        const string controllerPlaceholder = "[controller]";
        var routeValue = route.Value;
        if(routeValue.Contains(controllerPlaceholder))
        {
            routeValue = routeValue.Replace(controllerPlaceholder, c.Name.Replace("Controller", string.Empty));
        }

        return routeValue;
    }

    bool IsGetMethod(Method method){
        return method.HttpMethod() == "get";
    }

    bool IsPostMethod(Method method){
        return method.HttpMethod() == "post" && !method.Attributes.Any(a => a.Name == "ProducesResponseType");;
    }

    bool IsPostMethodWithResult(Method method){
        return method.HttpMethod() == "post" && method.Attributes.Any(a => a.Name == "ProducesResponseType");
    }

    bool IsPutMethodWithResult(Method method){
        return method.HttpMethod() == "put" && method.Attributes.Any(a => a.Name == "ProducesResponseType");
    }

    bool IsPutMethod(Method method){
        return method.HttpMethod() == "put" &&  !method.Attributes.Any(a => a.Name == "ProducesResponseType");
    }

    bool IsDeleteMethod(Method method){
        return method.HttpMethod() == "delete";
    }

    bool IsGetOrDeleteMethod(Class c){
        foreach(var method in c.Methods) {
            if (method.HttpMethod() == "get") {
                return true;
            }

            if(method.HttpMethod() == "delete") {
                return true;
            }
        }

        return false;
    }

    string GetParameterValue(Parameter parameter){
        if(parameter.Type == "string"){
            return parameter.name;
        }

        return $"{parameter.name}.toString()";
    }

    bool IsDictionary(Parameter parameter){
        return IsDictionary(parameter.Type);
    }

    bool IsDictionary(string name){
        var r=new Regex(@"[{]\s[[]key:\s[^\]]+[\]]:\s[^;]+;\s[}]");
        return r.Match(name).Success;
    }

    string NameOfType(Type t){
        if(IsDictionary(t.Name)){
            return "Dictionary";
        }
        return t.Name;
    }

    List<Parameter> SkipParameters(Method m) {
        var result = new List<Parameter>();
        foreach(var parameter in m.Parameters) {
            if(parameter.Type.Name == "CancellationToken") {
                continue;
            }

            result.Add(parameter);
        }
        return result;
    }

    bool ContainsSkipParameters(Method m) {
      return SkipParameters(m).Count > 0;
    }

    List<Parameter> SkipParametersForBody(Method m) {
        var result = SkipParameters(m);
        if(result.Count == 0){
            result.Add(new NullParam());
        }
        return result;
    }
}// This file has been AUTOGENERATED by TypeWriter (https://github.com/adaskothebeast/Typewriter).
// Do not modify it.
import { Inject, Injectable, Optional } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import {
  withTypewriterRequestSchema,
  withTypewriterResponseSchema
} from '@adaskothebeast/typewriter-http-angular';
$UsesInlineSchema[import { schema } from '@adaskothebeast/typewriter-schema';
]

import { Observable } from 'rxjs';

import { API_BASE_URL } from '../../app/app-config.module';
import { apiTypeRegistry } from '../models/typewriter-registry';

$Classes($IncludeClass)[
$IsGetOrDeleteMethod[import { HttpParamsProcessorService } from '@adaskothebeast/http-params-processor';]
$Imports
$SchemaImports

$IsGetOrDeleteMethod[
interface Object {
  hasOwnProperty<TK extends string>(v: TK): this is Record<TK, object>;
}]

export interface I$ServiceName {$Methods[
    $MethodName($SkipParameters[$name: $Type][, ]): Observable<$ReturnType>;]
}

@Injectable(
    { providedIn: 'root' }
)
export class $ServiceName implements I$ServiceName {
    constructor (
      @Inject(HttpClient) protected http: HttpClient,
      $IsGetOrDeleteMethod[@Inject(HttpParamsProcessorService) protected processor: HttpParamsProcessorService,]
      @Optional() @Inject(API_BASE_URL) protected baseUrl?: string) {
    }

    public get $UrlFieldName(): string {
        if (this.baseUrl) {
            return this.baseUrl.endsWith('/') ? this.baseUrl + '$GetRouteValue' : this.baseUrl + '/' + '$GetRouteValue';
        } else {
            return '$GetRouteValue';
        }
    }
$Methods[$IsGetMethod[
    public $MethodName($SkipParameters[$name: $Type][, ]): Observable<$ReturnType> {
        const headers = new HttpHeaders()
            .set('Accept', 'application/json')
            .set('If-Modified-Since', '0');

        let params = new HttpParams();
$ContainsSkipParameters[
        const parr = [];]
$SkipParameters[
        parr.push($name);
        params = this.processor.processWithParams(params, '$name', parr.pop());]

        return this.http.get<$ReturnType>(
            this.$Parent[$UrlFieldName] + '$HttpGetActionNameByAttribute',
            {
                $SchemaContext
                headers,
                params
            });
    }]
$IsPutMethod[
    public $MethodName($SkipParameters[$name: $Type][, ]): Observable<any> {
        const headers = new HttpHeaders()
            .set('Content-Type', 'application/json')
            .set('Accept', 'application/json')
            .set('If-Modified-Since', '0');

        return this.http.put(
            this.$Parent[$UrlFieldName] + '$HttpPutActionNameByAttribute',
            $SkipParametersForBody[$name],
            {
                $SchemaContext
                headers,
                responseType: 'text'
            });
    }]
$IsPutMethodWithResult[
    public $MethodName($SkipParameters[$name: $Type][, ]): Observable<$ReturnType> {
        const headers = new HttpHeaders()
            .set('Content-Type', 'application/json')
            .set('Accept', 'application/json')
            .set('If-Modified-Since', '0');

        return this.http.put<$ReturnType>(
            this.$Parent[$UrlFieldName] + '$HttpPutActionNameByAttribute',
            $SkipParametersForBody[$name],
            {
                $SchemaContext
                headers
            });
    }]
$IsPostMethod[
    public $MethodName($SkipParameters[$name: $Type][, ]): Observable<any> {
        const headers = new HttpHeaders()
            .set('Content-Type', 'application/json')
            .set('Accept', 'application/json')
            .set('If-Modified-Since', '0');

        return this.http.post(
            this.$Parent[$UrlFieldName] + '$HttpPostActionNameByAttribute',
            $SkipParametersForBody[$name],
            {
                $SchemaContext
                headers,
                responseType: 'text'
            });
    }]
$IsPostMethodWithResult[
    public $MethodName($SkipParameters[$name: $Type][, ]): Observable<$ReturnType> {
        const headers = new HttpHeaders()
            .set('Content-Type', 'application/json')
            .set('Accept', 'application/json')
            .set('If-Modified-Since', '0');

        return this.http.post<$ReturnType>(
            this.$Parent[$UrlFieldName] + '$HttpPostActionNameByAttribute',
            $SkipParametersForBody[$name],
            {
                $SchemaContext
                headers
            });
    }]
$IsDeleteMethod[
    public $MethodName($SkipParameters[$name: $Type][, ]): Observable<$ReturnType> {
        const headers = new HttpHeaders()
            .set('Accept', 'application/json')
            .set('If-Modified-Since', '0');

        let params = new HttpParams();
$ContainsSkipParameters[
        const parr = [];]
$SkipParameters[
        parr.push($name);
        params = this.processor.processWithParams(params, '$name', parr.pop());]

        return this.http.delete<$ReturnType>(
            this.$Parent[$UrlFieldName] + '$HttpDeleteActionNameByAttribute',
            {
                $SchemaContext
                headers,
                params
            });
    }]]
}]
