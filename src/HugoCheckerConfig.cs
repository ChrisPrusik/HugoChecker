// This file is part of HugoChecker - A GitHub Action to check Hugo markdown files.
// Copyright (c) Krzysztof Prusik and contributors
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

namespace HugoChecker;

public class HugoCheckerConfig
{
    public string DefaultLanguage { get; set; } = "en";

    public List<string> Languages { get; set; } = new() { "en" };

    public List<string> RequiredHeaders { get; set; } = new() { "title", "date" };

    public Dictionary<string, Dictionary<string, List<string>>> RequiredLists { get; set; } = new()
    {
        {
            "sections", new Dictionary<string, List<string>>
            {
                {"en", new List<string> {"Information", "Other"}},
            }
        }
    };

    public bool CheckLanguageStructure { get; set; } = true;
    
    public bool CheckFileLanguage { get; set; } = true;

    public List<string> IgnoreFiles { get; set; } = new() { "Unused.md" };

    public List<string> CheckHeaderDuplicates { get; set; } = new() {"slug", "title"};
    
    public bool CheckSlugRegex { get; set; } = true;
    
    public string PatternSlugRegex { get; set; } = @"^[a-z0-9]+(?:-[a-z0-9]+)*$";

    public bool ChatGptSpellCheck { get; set; } = false;

    public string ChatGptPrompt { get; set; } = """
        Your role is to check the text message provided by the user in the next messages.
        You will have to tasks to done. And result of the task put in an answer as json,
        see example below:
        {
          "Language": "en",
          "SpellCheck": true
          "Comment": "Everything is ok"
        }
      
        Task 1: Language detection.
      
        Your role is to identify language of the text message provided by the user in the next messages.
        Do not ask any questions - just make a judgement.
        If you are not sure about the language, choose the most probable one.
        Your answer should be only two letter code of the language (ISO 639-1 code),
        as the first json value (in our example it is "en").
    
        Task 2: Spellcheck.

        Your role is to check the correctness of the text in terms of style and grammar.
        Do not ask any questions - just make a judgement.
        As an answer in one word "true" - if everything is correct, as second json value, and "" as third json value.
        Otherwise, as an answer, wrote "false" as second json value and
        write a comment with an explanation and necessarily
        indicate the exact incorrect fragment as a quote, enclosed in quotation marks "" as third json value.
    """;
    
    public Double ChatGptTemperature { get; set; } = 0.9;
    
    public int ChatGptMaxTokens { get; set; } = 1000;
    
    public string ChatGptModel { get; set; } = "gpt-4";
}