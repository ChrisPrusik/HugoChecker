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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HugoChecker;

public class YamlService : IYamlService
{
    public YamlMappingNode GetYamlFromText(string text)
    {
        var reader = new StringReader(text);
        var yaml = new YamlStream();
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }
    public string GetStringValue(YamlMappingNode? mapping, string key)
    {
        try
        {
            if (mapping == null)
                throw new Exception("Yaml is empty.");

            return mapping[new YamlScalarNode(key)].ToString();
        }
        catch (Exception ex)
        {
            throw new Exception($"Unable to get tag '{key}' from yaml. ", ex);
        }
    }

    public List<string> GetListValue(YamlMappingNode? mapping, string key)
    {
        try
        {
            if (mapping == null)
                throw new Exception("Yaml is empty.");

            var node = mapping[new YamlScalarNode(key)];
            return (from item in (YamlSequenceNode)node select item.ToString()).ToList();
        }
        catch (Exception ex)
        {
            throw new Exception($"Unable to get tag '{key}' from yaml. ", ex);
        }
    }
    
    public bool ContainsChild(YamlMappingNode? mapping, string key)
    {
        return mapping != null && mapping.Children.ContainsKey(key);
    }

    public async Task<HugoCheckerConfig> ReadFromFile(string filePath)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<HugoCheckerConfig>(await File.ReadAllTextAsync(filePath));
    }
}