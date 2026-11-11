using System.Text.Json;
using System.Text.Json.Serialization;

namespace TeamForge.Application;

internal static class AnalysisJsonOptions
{
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
