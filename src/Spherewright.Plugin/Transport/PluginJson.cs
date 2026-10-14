using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Spherewright.Plugin.Transport;

internal static class PluginJson
{
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        DateParseHandling = DateParseHandling.DateTimeOffset,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Include,
        TypeNameHandling = TypeNameHandling.None,
        MaxDepth = 64,
    };

    public static string Serialize<T>(T value)
    {
        return JsonConvert.SerializeObject(value, Formatting.None, Settings);
    }

    public static T? Deserialize<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json, Settings);
    }

    // Durable progress copies must replace initialized collections, not append defaults
    // to already recorded lists. Keep shared protocol settings and their defaults unchanged.
    public static T? DeserializeReplacingCollections<T>(string json)
    {
        var serializer = JsonSerializer.Create(Settings);
        serializer.ObjectCreationHandling = ObjectCreationHandling.Replace;
        using (var input = new StringReader(json))
        using (var reader = new JsonTextReader(input))
            return serializer.Deserialize<T>(reader);
    }
}

