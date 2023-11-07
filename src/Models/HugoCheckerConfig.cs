// This file is part of HugoChecker - A GitHub Action to check Hugo markdown files.
// Copyright (c) Krzysztof Prusik and contributors
// https://github.com/marketplace/actions/hugochecker
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;

namespace HugoChecker.Models;

public class HugoCheckerConfig
{
    public string? DefaultLanguage { get; set; }

    public List<string>? Languages { get; set; }

    public List<string>? RequiredHeaders { get; set; }

    public Dictionary<string, Dictionary<string, List<string>>>? RequiredLists { get; set; } 

    public bool? CheckLanguageStructure { get; set; }
    
    public bool? CheckMarkDown { get; set; }
    
    public bool? CheckFileLanguage { get; set; } 

    public List<string>? IgnoreFiles { get; set; } 

    public List<string>? CheckHeaderDuplicates { get; set; }
    
    public bool? CheckSlugRegex { get; set; }
    
    public string? PatternSlugRegex { get; set; }
    
    public bool? CheckSpelling { get; set; }

    public bool? ChatGptSpellCheck { get; set; }

    public string? ChatGptPrompt { get; set; }
    
    public double? ChatGptTemperature { get; set; }
    
    public int? ChatGptMaxTokens { get; set; }
    
    public string? ChatGptModel { get; set; }
}