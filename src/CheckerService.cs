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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotnetActionsToolkit;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using LanguageDetection;

namespace HugoChecker;

public class CheckerService : ICheckerService
{
    private readonly Core core;
    private readonly LanguageDetector languageDetector;

    public CheckerService(Core core)
    {
        this.core = core;
        languageDetector = new LanguageDetector();
        languageDetector.AddAllLanguages();
    }

    public async Task Check(string? hugoFolder = null)
    {
        StartInformation();
        var model = new ProcessingModel
        {
            HugoFolder = GetHugoFolder(hugoFolder)
        };
        model.HugoConfig = await ReadHugoConfigAsync(model);
        model.CheckerConfig = await ReadCheckerConfigAsync(model);

        CheckLanguages(model);

        model.CheckedFolders = GetFileNames(model);
        CheckFileNames(model);
        await CheckAllFilesAsync(model);

        FinishInformation();
    }

    private async Task CheckAllFilesAsync(ProcessingModel model)
    {
        core.Info("Checking all files content...");
        core.Info($"Check required lists: {model.CheckerConfig.RequiredLists is { Count: > 0 }}");
        core.Info($"Check spellCheck: {model.CheckerConfig.SpellCheck}");
        core.Info($"Check required headers: {model.CheckerConfig.RequiredHeaders is { Count: > 0 }}");
        core.Info($"Check file language: {model.CheckerConfig.CheckFileLanguage}");
        core.Info($"Check slug regex: {model.CheckerConfig.CheckSlugRegex}");
        core.Info($"Slug regex pattern: {model.CheckerConfig.PatternSlugRegex}");
        foreach (var subfolder in model.CheckedFolders) 
            await CheckFilesInSubfolder(model, subfolder);
    }

    private async Task CheckFilesInSubfolder(ProcessingModel model, KeyValuePair<string, FolderModel> subfolder)
    {
        foreach (var file in subfolder.Value.Files) 
            await CheckLanguageFiles(model, file);
    }

    private async Task CheckLanguageFiles(ProcessingModel model, KeyValuePair<string, FileModel> file)
    {
        foreach (var language in file.Value.LanguageFiles)
            await CheckFileAsync(model, language.Value);
    }

    private async Task CheckFileAsync(ProcessingModel model, FileLanguageModel languageModel)
    {
        if (IsFileIgnored(model, languageModel))
        {
            core.Warning($"Ignore file '{languageModel.FilePath}'");
            return;
        }

        core.Info($"Checking file '{languageModel.FilePath}' language '{languageModel.Language}'");
        await ReadLanguageFileAsync(model, languageModel);
        
        CheckLanguageFile(model, languageModel);
    }

    private bool IsFileIgnored(ProcessingModel model, FileLanguageModel languageModel)
    {
        var relativePath = Path.GetRelativePath(model.HugoFolder, languageModel.FilePath);
        return model.CheckerConfig.IgnoreFiles.Any() && 
               model.CheckerConfig.IgnoreFiles.Contains(relativePath);
    }

    private async Task ReadLanguageFileAsync(ProcessingModel model, FileLanguageModel languageModel)
    {
        var text = await File.ReadAllTextAsync(languageModel.FilePath);
        languageModel.FileInfo = new FileInfo(languageModel.FilePath);
        languageModel.Header = GetFileHeaderAsText(text);
        languageModel.Yaml = GetYamlFromText(languageModel.Header);
        
        languageModel.Body = text.Substring(text.IndexOf(languageModel.Header) + languageModel.Header.Length).Trim();
    }

    private void CheckLanguageFile(ProcessingModel model, FileLanguageModel languageModel)
    {
        if (model.CheckerConfig.RequiredHeaders is { Count: > 0 })
            CheckRequiredHeaders(model, languageModel);
        
        if (model.CheckerConfig.RequiredLists is { Count: > 0 })
            CheckRequiredLists(model, languageModel);
        
        if (model.CheckerConfig.CheckFileLanguage)
            CheckFileLanguage(model, languageModel);
        
        if (model.CheckerConfig.SpellCheck)
            SpellCheckFileLanguage(model, languageModel);
        
        if (model.CheckerConfig.CheckSlugRegex)
            CheckSlugRegex(model, languageModel);
        
        if (model.CheckerConfig.CheckHeaderDuplicates != null &&
            model.CheckerConfig.CheckHeaderDuplicates.Any())
            CheckHeaderDuplicates(model, languageModel);
    }

    private void CheckHeaderDuplicates(ProcessingModel model, FileLanguageModel languageModel)
    {
        foreach(var header in model.CheckerConfig.CheckHeaderDuplicates)
            if (languageModel.Yaml.Children.ContainsKey(header))
            {
                var value = GetYamlValue(languageModel.Yaml, header);
                
                if (model.ProcessedDuplicates.ContainsKey(header) && 
                    model.ProcessedDuplicates[header].ContainsKey(value))
                    throw new Exception($"Detected duplicates {header}: '{value}' in two files '{model.ProcessedDuplicates[header][value]}' and '{languageModel.FilePath}'");

                if (!model.ProcessedDuplicates.ContainsKey(header))
                    model.ProcessedDuplicates.Add(header, new Dictionary<string, string>());
                
                model.ProcessedDuplicates[header][value] = languageModel.FilePath;
            }
    }

    private void CheckSlugRegex(ProcessingModel model, FileLanguageModel languageModel)
    {
        if (languageModel.Yaml.Children.ContainsKey("slug"))
        {
            var slug = GetYamlValue(languageModel.Yaml, "slug");
            if (!Regex.IsMatch(slug, model.CheckerConfig.PatternSlugRegex))
                throw new Exception($"Slug '{slug}' doesn't match with the pattern '{model.CheckerConfig.PatternSlugRegex}'");
        }
    }

    private void SpellCheckFileLanguage(ProcessingModel model, FileLanguageModel languageModel)
    {
        
    }

    private void CheckFileLanguage(ProcessingModel model, FileLanguageModel languageModel)
    {
        var detectedLanguage = DetectLanguage(languageModel.Body);
        if (detectedLanguage != languageModel.Language)
            throw new Exception($"File '{languageModel.FilePath}' language '{languageModel.Language}' doesn't match with the content language '{detectedLanguage}'");
    }

    private void CheckRequiredLists(ProcessingModel model, FileLanguageModel languageModel)
    {
        foreach(var pair in model.CheckerConfig.RequiredLists)
            CheckRequiredList(model, languageModel, pair.Key);
    }

    private void CheckRequiredList(ProcessingModel model, FileLanguageModel languageModel, string key)
    {
        var list = GetYamlList(languageModel.Yaml, key);
        if (!list.Any())
            throw new Exception($"There are no required list '{key}' in the file {languageModel.FilePath}");
        
        foreach(var item in list)
            CheckRequiredListItem(model, languageModel, key, item);
    }

    private void CheckRequiredListItem(ProcessingModel model, FileLanguageModel languageModel, string key, string value)
    {
        if (!model.CheckerConfig.RequiredLists[key].ContainsKey(languageModel.Language))
            throw new Exception($"There are no required list '{key}' in the file {languageModel.FilePath} for language {languageModel.Language}");

        if (!model.CheckerConfig.RequiredLists[key][languageModel.Language].Contains(value))
            throw new Exception($"There are no required '{value}' from the list '{key}' in the file {languageModel.FilePath} for language {languageModel.Language}. Check required-lists in the hugo-checker.yaml");
    }

    private void CheckRequiredHeaders(ProcessingModel model, FileLanguageModel languageModel)
    {
        foreach(var header in model.CheckerConfig.RequiredHeaders)
            CheckRequiredHeader(model, languageModel, header);
    }

    private void CheckRequiredHeader(ProcessingModel model, FileLanguageModel languageModel, string key)
    {
        if (model.CheckerConfig.RequiredLists.ContainsKey(key))
        {
            var list = GetYamlList(languageModel.Yaml, key);
            if (!list.Any())
                throw new Exception($"There are no required header key '{key}' (list) in the file {languageModel.FilePath}");
        }
        else
        {
            var value = GetYamlValue(languageModel.Yaml, key);
            if (string.IsNullOrWhiteSpace(value))
                throw new Exception($"There are no required header key '{key}' (value) in the file {languageModel.FilePath}");
            
        }
    }

    private void CheckFileNames(ProcessingModel model)
    {
        foreach (var subfolder in model.CheckedFolders)
        {
            if (subfolder.Value.Files.Count == 0)
                core.Warning($"Folder {subfolder.Value.SubFolder} doesn't have any markdown files");

            if (subfolder.Key != subfolder.Value.SubFolder)
                throw new Exception($"Folder {subfolder.Value.SubFolder} doesn't match with the key {subfolder.Key}");

            CheckFolder(model, subfolder.Value);
        }
    }

    private void CheckFolder(ProcessingModel model, FolderModel folder)
    {
        core.Info($"Folder {folder.SubFolder}");
        foreach (var file in folder.Files)
        {
            core.Info(
                $"File '{file.Value.RootFilePath}' found languages {string.Join(", ", file.Value.LanguageFiles.Keys)}");
            if (model.CheckerConfig.Languages != null)
                foreach (var language in model.CheckerConfig.Languages)
                    if (!file.Value.LanguageFiles.ContainsKey(language))
                        core.Warning($"File '{file.Value.RootFilePath}' doesn't have language '*.{language}.md'");
        }
    }

    private void CheckLanguages(ProcessingModel model)
    {
        core.Info($"languageCode in the config.yaml: {model.HugoConfig.LanguageCode}");
        CheckLanguage(model.HugoConfig.LanguageCode, model);

        core.Info($"Key default-language: {model.CheckerConfig.DefaultLanguage}, used for primary files *.md");
        CheckLanguage(model.CheckerConfig.DefaultLanguage, model);

        core.Info($"Key languages: {string.Join(",", model.CheckerConfig.Languages)}");
        foreach (var language in model.CheckerConfig.Languages)
            CheckLanguage(language, model);

        if (model.CheckerConfig.RequiredLists != null)
            foreach (var section in model.CheckerConfig.RequiredLists)
            {
                core.Info($"Key required.{section.Key}: {string.Join(",", section.Value.Keys)}");
                foreach (var language in model.CheckerConfig.Languages)
                    if (!section.Value.ContainsKey(language))
                        throw new Exception(
                            $"Undefined language in the key required.{section.Key}: {language}. Check languages key.");

                foreach (var language in section.Value.Keys)
                    if (!model.CheckerConfig.Languages.Contains(language))
                        throw new Exception(
                            $"Undefined language in the key required.{section.Key}: {language}. Check languages key.");
            }

        core.Info("All languages are valid");
    }

    private void CheckLanguage(string? languageCode, ProcessingModel model)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new Exception("Language code is required");

        if (languageCode.Length != 2)
            throw new Exception($"Language code: '{languageCode}' is invalid. It should be 2 characters long");

        if (!char.IsLower(languageCode[0]) || !char.IsLower(languageCode[1]))
            throw new Exception($"Language code: '{languageCode}' is invalid. It should be lower case");

        if (!model.CheckerConfig.Languages.Contains(languageCode))
            throw new Exception(
                $"Language code is not defined in hugo-checker.yaml file, expected {string.Join(", ", model.CheckerConfig.Languages)}.");

        var culture = CultureInfo.GetCultureInfo(languageCode);
        if (culture == null)
            throw new Exception($"Language code: '{languageCode}' is invalid. It should be a valid culture");

        core.Info($"Language code: '{languageCode}' is valid, culture = '{culture.DisplayName}'");
    }

    private string GetHugoFolder(string? hugoFolder = null)
    {
        hugoFolder = !string.IsNullOrWhiteSpace(hugoFolder) ? hugoFolder : core.GetInput("hugo-folder", true);

        if (string.IsNullOrWhiteSpace(hugoFolder))
            throw new Exception("Input: hugo-folder is required");

        if (!Directory.Exists(hugoFolder))
            throw new Exception($"Folder input:hugo-folder: '{hugoFolder}' doesn't exist");

        core.Info($"Hugo folder exists: {hugoFolder}");

        return hugoFolder;
    }

    private async Task<HugoConfig> ReadHugoConfigAsync(ProcessingModel model)
    {
        var hugoConfigFile = Path.Combine(model.HugoFolder, "config.yaml");

        if (!File.Exists(hugoConfigFile))
            throw new Exception($"Hugo configuration file '{hugoConfigFile}' doesn't exist");

        core.Info($"Hugo configuration file '{hugoConfigFile}' is loading...");

        var config = new HugoConfig();
        var mapping = await ReadYamlFile(hugoConfigFile);

        core.Info("Hugo configuration file is loaded");
        config.Title = GetYamlValue(mapping, "title");
        config.LanguageCode = GetYamlValue(mapping, "languageCode");
        core.Info($"Website title: {config.Title}, language code: {config.LanguageCode}");
        return config;
    }

    private string DetectLanguage(string text)
    {
        var language = languageDetector.Detect(text);
        if (string.IsNullOrWhiteSpace(language))
            throw new Exception($"Unable to detect language from text '{text}'");
        
        var culture = CultureInfo.GetCultureInfo(language);
        if (culture == null)
            throw new Exception($"Language code: '{language}' is invalid. It should be a valid culture");

        return culture.TwoLetterISOLanguageName.ToLower();
    }

    private YamlMappingNode GetYamlFromText(string text)
    {
        var reader = new StringReader(text);
        var yaml = new YamlStream();
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    private string GetFileHeaderAsText(string text)
    {
        var pattern = @"---\n([\s\S]*?)\n---";
        var matches = Regex.Matches(text, pattern, RegexOptions.Multiline);
        if (matches.Count == 0)
            throw new Exception("Unable to find header.");

        text = matches[0].Groups[1].Value.Trim();
        return text;
    }

    private async Task<YamlMappingNode> ReadYamlFile(string filePath)
    {
        var text = await File.ReadAllTextAsync(filePath);
        return GetYamlFromText(text);
    }

    private string GetYamlValue(YamlMappingNode mapping, string key)
    {
        try
        {
            return mapping[new YamlScalarNode(key)].ToString();
        }
        catch (Exception ex)
        {
            throw new Exception($"Unable to get tag '{key}' from yaml. ", ex);
        }
    }

    private List<string> GetYamlList(YamlMappingNode mapping, string key)
    {
        try
        {
            var list = new List<string>();
            var node = mapping[new YamlScalarNode(key)];
            foreach (var item in (YamlSequenceNode)node)
                list.Add(item.ToString());
            return list;
        }
        catch (Exception ex)
        {
            throw new Exception($"Unable to get tag '{key}' from yaml. ", ex);
        }
    }

    private async Task<HugoCheckerConfig> ReadCheckerConfigAsync(ProcessingModel model)
    {
        var checkerConfigFile = Path.Combine(model.HugoFolder, "hugo-checker.yaml");

        if (!File.Exists(checkerConfigFile))
            throw new Exception($"Hugo checker configuration file '{checkerConfigFile}' doesn't exist");

        core.Info($"Hugo checker configuration file '{checkerConfigFile}' is loading...");

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .Build();
        var config = deserializer.Deserialize<HugoCheckerConfig>(await File.ReadAllTextAsync(checkerConfigFile));

        core.Info("Hugo checker configuration file is loaded");
        return config;
    }

    private Dictionary<string, FolderModel> GetFileNames(ProcessingModel model)
    {
        var result = new Dictionary<string, FolderModel>();

        core.Info(
            $"Key folders-to-check: '{string.Join(", ", model.CheckerConfig.FoldersToCheck)}' - getting files...");
        foreach (var subfolder in model.CheckerConfig.FoldersToCheck)
        {
            var folderModel = new FolderModel
            {
                SubFolder = subfolder,
                Files = GetFileNames(model, subfolder)
            };
            core.Info($"Markdown files count in the folder {subfolder}: {folderModel.Files.Count}");
            result.Add(subfolder, folderModel);
        }

        return result;
    }

    private Dictionary<string, FileModel> GetFileNames(ProcessingModel model, string subfolder)
    {
        var result = new Dictionary<string, FileModel>();

        var folder = Path.Combine(model.HugoFolder, subfolder);
        if (!Directory.Exists(folder))
            throw new Exception($"Folder {folder} doesn't exist");

        core.Info($"Loading markdown file names from the folder '{folder}'");

        foreach (var filePath in Directory.GetFiles(folder, "*.md",
                     SearchOption.TopDirectoryOnly))
        {
            var rootFilePath = GetRootFilePath(model, filePath);
            var rootFileNameWithoutExtension = Path.GetFileNameWithoutExtension(rootFilePath);
            if (!result.ContainsKey(rootFileNameWithoutExtension))
                result.Add(rootFileNameWithoutExtension, new FileModel
                {
                    RootFilePath = rootFilePath,
                    LanguageFiles = new Dictionary<string, FileLanguageModel>()
                });
            var language = GetFileLanguage(model, filePath);
            if (!model.CheckerConfig.Languages.Contains(language))
                throw new Exception(
                    $"Language code is not defined in hugo-checker.yaml file, expected {string.Join(", ", model.CheckerConfig.Languages)}.");

            result[rootFileNameWithoutExtension].LanguageFiles[language] = new FileLanguageModel
            {
                Language = language,
                FilePath = filePath
            };
        }

        return result;
    }

    private string GetRootFilePath(ProcessingModel model, string filePath)
    {
        var language = GetFileLanguage(model, filePath);

        if (language == model.CheckerConfig.DefaultLanguage)
            return filePath;

        var folder = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        fileName = Path.GetFileNameWithoutExtension(fileName);
        fileName += ".md";
        filePath = Path.Combine(folder, fileName);

        if (!File.Exists(filePath))
            throw new Exception($"File '{filePath}' doesn't exist");

        return filePath;
    }

    private string GetFileLanguage(ProcessingModel model, string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var items = fileName.Split('.');
        var language = items[items.Length - 1];
        if (items.Length == 1)
            language = model.CheckerConfig.DefaultLanguage;

        if (!model.CheckerConfig.Languages.Contains(language))
            throw new Exception(
                $"Language code {filePath} is not defined in hugo-checker.yaml file, expected {string.Join(", ", model.CheckerConfig.Languages)}.");

        return language;
    }

    private void StartInformation()
    {
        core.Info($"HugoChecker version: {typeof(CheckerService).Assembly.GetName().Version}");
    }

    private void FinishInformation()
    {
        core.Info("Well done!");
    }
}