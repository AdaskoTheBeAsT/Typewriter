using Newtonsoft.Json;

namespace LibA;

public static class HelperA
{
    public static string Serialize(object value) => JsonConvert.SerializeObject(value);
}
