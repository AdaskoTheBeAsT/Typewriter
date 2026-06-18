using System;

namespace Typewriter.VisualStudio;

internal static class TypewriterVisualStudioConstants
{
    public const string PackageGuidString = "0f42254e-a1f3-45bb-9209-b7a4d5c30791";
    public const string CommandSetGuidString = "70f36224-b7ab-491c-9f9c-b241ebafde9a";
    public static readonly Guid CommandSetGuid = new(g: CommandSetGuidString);

    public const int GenerateCurrentTemplateCommandId = 0x0100;
    public const int GenerateAllTemplatesCommandId = 0x0101;
    public const int ValidateCurrentTemplateCommandId = 0x0102;
    public const int RenderTemplateCommandId = 0x0200;
    public const int RenderAllTemplatesCommandId = 0x0201;
    public const int ValidateTemplateCommandId = 0x0202;
}
