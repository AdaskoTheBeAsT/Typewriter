namespace Typewriter.CodeModel;

public static class NameCaseExtensions
{
    public static string ToNameCase(
        this string? value,
        NameCase nameCase) =>
        NameCaseFormatter.Format(value: value, nameCase: nameCase);
}
