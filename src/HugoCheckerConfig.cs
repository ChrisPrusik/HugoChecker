using System.Collections.Generic;

namespace HugoChecker;

public class HugoCheckerConfig
{
    public List<string>? FoldersToCheck { get; set; }
    
    public Dictionary<string, Dictionary<string, List<string>>>? Required { get; set; }
    
    public bool? CheckFileNames { get; set; }
    
    public bool? CheckFileLanguage { get; set; }
    
    public bool? TranslatedNewerThanOriginal { get; set; }
    
    public bool? SpellCheck { get; set; }
    
    public bool? OriginalLanguage { get; set; }
    
    public bool? TranslatedLanguage { get; set; }
}