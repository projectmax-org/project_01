using System.Text.Json;
using System.Text.Json.Serialization;

namespace Boatdrill.Core;

/// <summary>One set of JSON conventions for history and settings: camelCase, enums by name, nulls left out.</summary>
public static class BoatdrillJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static readonly JsonSerializerOptions Indented = new(Options) { WriteIndented = true };
}
