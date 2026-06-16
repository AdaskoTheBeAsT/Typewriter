using Newtonsoft.Json;

namespace LibB;

public static class HelperB
{
    public static string Serialize(object value) => JsonConvert.SerializeObject(value);
}
