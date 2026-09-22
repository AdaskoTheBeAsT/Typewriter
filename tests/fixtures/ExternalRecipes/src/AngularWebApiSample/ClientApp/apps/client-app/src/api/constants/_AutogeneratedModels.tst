${
    using Typewriter.Extensions.Types;
    using System.Text.RegularExpressions;
    using Typewriter.VisualStudio;

    static char separator;
    static string templatePath;
    static ILog log;
    
    Template(Settings settings)
    {
        settings
            .IncludeCurrentProject()
            .IncludeReferencedProjects()
            .UseStringLiteralCharacter('\'')
            .DisableUtf8BomGeneration()
            ;
        separator = settings.StringLiteralCharacter;
        templatePath = settings.TemplatePath;
        log = settings.Log;
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

    bool IncludeClassStaticOnly(Class c){
      if(!c.IsStatic){
        return false;
      }

      return IncludeClass(c);
    }

    char Sep(Constant c){
        return separator;
    }

    string LoudName(Constant constant)
    {
        return constant.Name.ToUpperInvariant();
    }
}/* eslint-disable @typescript-eslint/no-explicit-any */
$Classes($IncludeClassStaticOnly)[$Constants[
export const $LoudName: string = $Sep$Value$Sep;]
]
