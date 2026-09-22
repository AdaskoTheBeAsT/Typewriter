${
    using Typewriter.Extensions.Types;
    using Typewriter.Extensions.WebApi;
    using System.Text;
    using System.Text.RegularExpressions;

    // setting template
    Template(Settings settings)
    {
        settings
            .IncludeCurrentProject()
            .IncludeReferencedProjects()
            .UseStringLiteralCharacter('\'')
            .DisableUtf8BomGeneration();

        // file should be named same as controller name with 'Service' suffix
        settings.OutputFilenameFactory = file => $"{Hyphenated(file.Classes.First().Name.Replace("Controller", string.Empty))}.api.ts";
    }

    public class NullParam
        : Parameter
    {
        private string _name;
        public NullParam() {
            _name = "null";
        }
        public override string name => _name;
        public override string AssemblyName => string.Empty;
        public override string FullName => string.Empty;
        public override bool HasDefaultValue => false;
        public override string DefaultValue => string.Empty;
        public override Type Type => null;
        public override string Name => string.Empty;
        public override IAttributeCollection Attributes => null;
        public override Item Parent => null;

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

    // get react service name based on controller class name
    string ConstName(Class c) => $"{c.name.Replace("Controller", "Api")}";

    string ServiceName(Class c) => $"{c.Name.Replace("Controller", "Api")}";

    // get react service name based on controller in which given method is defined
    string ParentConstName(Method m) => ConstName((Class)m.Parent);

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

    // get react hook name for query
    string HookNameQuery(Method m)
    {
      var methodName = MethodName(m);
      methodName = methodName.Substring(0,1).ToUpper() + methodName.Substring(1, methodName.Length - 1);
      return $"use{methodName}Query";
    }
    // get react hook name for mutation
    string HookNameMutation(Method m)
    {
      var methodName = MethodName(m);
      methodName = methodName.Substring(0,1).ToUpper() + methodName.Substring(1, methodName.Length - 1);
      return $"use{methodName}Mutation";
    }

    // returns true if class should be treated as candidate for react service
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

    string UrlFieldName2(Method m) => $"{(m.Parent as Class).name.Replace("Controller", "Service")}Url";

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

    bool NotContainsSkipParameters(Method m) {
        return SkipParameters(m).Count == 0;
    }

    bool ContainsOneSkipParameters(Method m) {
        return SkipParameters(m).Count == 1;
    }

     bool ContainsMoreSkipParameters(Method m) {
        return SkipParameters(m).Count > 1;
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
import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react'

import { environment } from '../../environments/environment';
$Classes($IncludeClass)[
$IsGetOrDeleteMethod[import * as qs from 'qs';]
$Imports

const getUrl = () => {
  if(environment.apiBaseUrl) {
    return environment.apiBaseUrl.endsWith('/') ?
      `${environment.apiBaseUrl}$GetRouteValue` :
      `${environment.apiBaseUrl}/$GetRouteValue`;
  }

  return '$GetRouteValue';
}; 

const $UrlFieldName = getUrl();

export const $ConstName = createApi({
  reducerPath: '$ConstName',
  baseQuery: fetchBaseQuery({ baseUrl: environment.apiBaseUrl }),
  endpoints: (builder) => ({$Methods[$IsGetMethod[
    $MethodName: builder.query<$ReturnType, $ContainsSkipParameters[$SkipParameters[$Type][, ]]$NotContainsSkipParameters[void]>({
      query: ($SkipParameters[$name][, ]) => {
        $ContainsSkipParameters[
        const paramObj = {
          $SkipParameters[$name: $name][, ]
        };
        const queryPath = qs.stringify(paramObj);
        const url = $UrlFieldName2 + '?' + queryPath;
        ]
        $NotContainsSkipParameters[
        const url = $UrlFieldName2;
        ]

        return {
          url: url,
          method: 'GET',
          headers: {
            'Accept': 'application/json',
            'If-Modified-Since': '0',
          }
        };
      },
    }),
  ]
  $IsPutMethod[
    $MethodName: builder.mutation<$ReturnType, $ContainsSkipParameters[$SkipParameters[$Type][, ]]$NotContainsSkipParameters[void]>({
      query: ($SkipParameters[$name][, ]) => {
        $ContainsMoreSkipParameters[
        const body = {
          $SkipParameters[$name: $name][, ]
        };
        ]
        $ContainsOneSkipParameters[
        $SkipParameters[const body=$name;]
        ]
        $NotContainsSkipParameters[
        const body = {};
        ]
        return {
          url: $UrlFieldName2,
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
            'If-Modified-Since': '0',
          },
          body: body
        };
      },
    }),
  ]
  $IsPutMethodWithResult[
    $MethodName: builder.mutation<$ReturnType, $ContainsSkipParameters[$SkipParameters[$Type][, ]]$NotContainsSkipParameters[void]>({
      query: ($SkipParameters[$name][, ]) => {
        $ContainsMoreSkipParameters[
        const body = {
          $SkipParameters[$name: $name][, ]
        };
        ]
        $ContainsOneSkipParameters[
        $SkipParameters[const body=$name;]
        ]
        $NotContainsSkipParameters[
        const body = {};
        ]
        return {
          url: $UrlFieldName2,
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
            'If-Modified-Since': '0',
          },
          body: body
        };
      },
    }),
  ]
  $IsPostMethod[
    $MethodName: builder.mutation<$ReturnType, $ContainsSkipParameters[$SkipParameters[$Type][, ]]$NotContainsSkipParameters[void]>({
      query: ($SkipParameters[$name][, ]) => {
        $ContainsMoreSkipParameters[
        const body = {
          $SkipParameters[$name: $name][, ]
        };
        ]
        $ContainsOneSkipParameters[
        $SkipParameters[const body=$name;]
        ]
        $NotContainsSkipParameters[
        const body = {};
        ]
        return {
          url: $UrlFieldName2,
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
            'If-Modified-Since': '0',
          },
          body: body
        };
      },
    }),
  ]
  $IsPostMethodWithResult[
    $MethodName: builder.mutation<$ReturnType, $ContainsSkipParameters[$SkipParameters[$Type][, ]]$NotContainsSkipParameters[void]>({
      query: ($SkipParameters[$name][, ]) => {
        $ContainsMoreSkipParameters[
        const body = {
          $SkipParameters[$name: $name][, ]
        };
        ]
        $ContainsOneSkipParameters[
        $SkipParameters[const body=$name;]
        ]
        $NotContainsSkipParameters[
        const body = {};
        ]
        return {
          url: $UrlFieldName2,
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
            'If-Modified-Since': '0',
          },
          body: body
        };
      },
    }),
  ]
  $IsDeleteMethod[
    $MethodName: builder.mutation<$ReturnType, $ContainsSkipParameters[$SkipParameters[$Type][, ]]$NotContainsSkipParameters[void]>({
      query: ($SkipParameters[$name][, ]) => {
        $ContainsMoreSkipParameters[
        const body = {
          $SkipParameters[$name: $name][, ]
        };
        ]
        $ContainsOneSkipParameters[
        $SkipParameters[const body=$name;]
        ]
        $NotContainsSkipParameters[
        const body = {};
        ]
        return {
          url: $UrlFieldName2,
          method: 'DELETE',
          headers: {
            'Accept': 'application/json',
            'If-Modified-Since': '0',
          },
          body: body
        };
      },
    }),
  ]
  ]
  })
});    

export const {
  $Methods[$IsGetMethod[$HookNameQuery]$IsPutMethod[$HookNameMutation]$IsPutMethodWithResult[$HookNameMutation]$IsPostMethod[$HookNameMutation]$IsPostMethodWithResult[$HookNameMutation]$IsDeleteMethod[$HookNameMutation]][,
  ]
} = $ConstName;
]
