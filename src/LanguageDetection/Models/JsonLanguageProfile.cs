using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LanguageDetection.Models;

public class JsonLanguageProfile
{
    public string Name { get; set; }

    public Dictionary<string, int> Freq { get; set; }

    public int[] NWords { get; set; }
}