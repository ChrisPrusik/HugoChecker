using System.Collections.Generic;
using System.Text.Json;

namespace LanguageDetection;

public class JsonLanguageProfileNamingPolicy: JsonNamingPolicy
{
    private readonly Dictionary<string, string> NameMapping = new()
    {
        [nameof(Models.JsonLanguageProfile.Name)] = "name",
        [nameof(Models.JsonLanguageProfile.Freq)] = "freq",
        [nameof(Models.JsonLanguageProfile.NWords)] = "n_words",
    }; 

    public override string ConvertName(string name)
    {
        return NameMapping.GetValueOrDefault(name, name);
    }
}