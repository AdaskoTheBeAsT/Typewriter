using System.Text.RegularExpressions;

namespace App;

public partial class Validator
{
    [GeneratedRegex(@"\d+")]
    public static partial Regex Digits();
}
