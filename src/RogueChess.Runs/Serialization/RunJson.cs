using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RogueChess.Runs;

public static class RunJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new UlongStringConverter());
        return options;
    }
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new FormatException($"Empty {typeof(T).Name}.");
    public static T Copy<T>(T value) => Read<T>(Write(value));
    public static JsonElement Data<T>(T value) => JsonSerializer.SerializeToElement(value, Options);
    public static T Decode<T>(JsonElement data) => data.Deserialize<T>(Options)
        ?? throw new FormatException($"Empty {typeof(T).Name}.");
    public static string Hash<T>(T value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Canonical(writer, Data(value));
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private static void Canonical(Utf8JsonWriter writer, JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var item in data.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(item.Name); Canonical(writer, item.Value); }
            writer.WriteEndObject();
        }
        else if (data.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in data.EnumerateArray()) Canonical(writer, item);
            writer.WriteEndArray();
        }
        else data.WriteTo(writer);
    }
    private sealed class UlongStringConverter : JsonConverter<ulong>
    {
        public override ulong Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.String
                ? ulong.Parse(reader.GetString()!, System.Globalization.CultureInfo.InvariantCulture) : reader.GetUInt64();
        public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}

/// <summary>Extension-owned state. No CLR type names, node references, or inventory policy in the run core.</summary>
public sealed record StatePayload
{
    public string Kind { get; init; } = "inventory";
    public int Version { get; init; } = 1;
    public JsonElement Data { get; init; } = RunJson.Data(new { });
    public static StatePayload From<T>(string kind, T value, int version = 1)
        => new() { Kind = kind, Version = version, Data = RunJson.Data(value) };
    public T As<T>(string kind, int version = 1)
    {
        if (Kind != kind || Version != version) throw new FormatException($"Unsupported {Kind} state version {Version}.");
        return RunJson.Decode<T>(Data);
    }
}
