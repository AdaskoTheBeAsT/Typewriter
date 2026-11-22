using System.Diagnostics;

namespace Typewriter.VisualStudio;

public static class TemplateDebugging
{
    public static void Break()
    {
        if (Debugger.IsAttached)
        {
            Debugger.Break();
        }
    }
}
