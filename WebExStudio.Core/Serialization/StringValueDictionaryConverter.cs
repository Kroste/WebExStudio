using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebExStudio.Core.Serialization;

/// <summary>
/// Allows Dictionary&lt;string, string&gt; to deserialize JSON values that are numbers or booleans
/// by converting them to their string representation.
/// </summary>
public sealed class StringValueDictionaryConverter : JsonConverter<Dictionary<string, string>>
{
    public override Dictionary<string, string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject token.");

        var result = new Dictionary<string, string>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected PropertyName token.");

            string key = reader.GetString()!;
            reader.Read();

            string value = reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString()!,
                JsonTokenType.Number => reader.TryGetInt64(out long l) ? l.ToString() : reader.GetDouble().ToString(),
                JsonTokenType.True => "true",
                JsonTokenType.False => "false",
                JsonTokenType.Null => string.Empty,
                _ => throw new JsonException($"Unsupported token type '{reader.TokenType}' for string dictionary value.")
            };

            result[key] = value;
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, string> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (k, v) in value)
            writer.WriteString(k, v);
        writer.WriteEndObject();
    }
}
