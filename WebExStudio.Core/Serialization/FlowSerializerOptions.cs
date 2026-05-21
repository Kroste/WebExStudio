using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebExStudio.Core.Serialization;

public static class FlowSerializerOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Converters = { new StringValueDictionaryConverter() },
    };
}
