using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LanguageDetection.Models;

public class JsonLanguageProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("freq")]
    public Dictionary<string, int> Freq { get; set; }

    [JsonPropertyName("n_words")]
    public int[] NWords { get; set; }
}