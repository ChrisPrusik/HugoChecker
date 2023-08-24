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
using LanguageDetection;

namespace HugoChecker;

public class CheckerService : ICheckerService
{
    private readonly Core core;
    private readonly IYamlService yamlService;
    private readonly IChatGptService chatGptService;
    
    private LanguageDetector languageDetector;

    public CheckerService(Core core, IYamlService yamlService, IChatGptService chatGptService)
    {
        this.core = core;
        this.yamlService = yamlService;
        this.chatGptService = chatGptService;
        this.languageDetector = new LanguageDetector();
        languageDetector.AddAllLanguages();
    }

    public async Task Check(string? hugoFolder = null, string? chatGptApiKey = null)
    {
        StartInformation();
        var folder = GetHugoFolder(hugoFolder);

        var model = new ProcessingModel(folder, 
            await ReadCheckerConfig(folder), 
            await ReadHugoConfig(folder));

        await InitializeChatGpt(model, chatGptApiKey);

        CheckLanguages(model);

        model.CheckedFolders = GetFileNames(model);
        CheckFileNames(model);
        
        await CheckAllFiles(model);

        FinishInformation();
    }

    private async Task InitializeChatGpt(ProcessingModel model, string? chatGptApiKey)
    {
        if (!model.CheckerConfig.ChatGptSpellCheck)
            return;

        if (string.IsNullOrWhiteSpace(chatGptApiKey))
            chatGptApiKey = core.GetInput("chatgpt-api-key");

        if (string.IsNullOrWhiteSpace(chatGptApiKey))
            throw new Exception("Undefined chatgpt-api-key. ChatGPT is not available.");

        await chatGptService.Initialise(chatGptApiKey, model.CheckerConfig.ChatGptPrompt,
            model.CheckerConfig.ChatGptModel, model.CheckerConfig.ChatGptTemperature,
            model.CheckerConfig.ChatGptMaxTokens);

        core.Info($"Connected with OpenAI ChatGPT API");
    }

    private async Task CheckAllFiles(ProcessingModel model)
    {
        core.Info("Checking all files content...");
        core.Info($"Check required lists: {model.CheckerConfig.RequiredLists is { Count: > 0 }}");
        core.Info($"Check spellCheck: {model.CheckerConfig.ChatGptSpellCheck}");
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
            await CheckFile(model, language.Value);
    }

    private async Task CheckFile(ProcessingModel model, FileLanguageModel languageModel)
    {
        if (IsFileIgnored(model, languageModel))
        {
            core.Warning($"Ignore file '{languageModel.FilePath}'");
            return;
        }

        core.Info($"Checking file '{languageModel.FilePath}' language '{languageModel.Language}'");
        await ReadLanguageFile(model, languageModel);
        
        await CheckLanguageFile(model, languageModel);
    }

    private bool IsFileIgnored(ProcessingModel model, FileLanguageModel languageModel)
    {
        var relativePath = Path.GetRelativePath(model.HugoFolder, languageModel.FilePath);
        return model.CheckerConfig.IgnoreFiles.Any() && 
               model.CheckerConfig.IgnoreFiles.Contains(relativePath);
    }

    private async Task ReadLanguageFile(ProcessingModel model, FileLanguageModel languageModel)
    {
        var text = await File.ReadAllTextAsync(languageModel.FilePath);
        languageModel.FileInfo = new FileInfo(languageModel.FilePath);
        languageModel.Header = GetFileHeaderAsText(text);
        languageModel.Yaml = yamlService.GetYamlFromText(languageModel.Header);
        languageModel.Body = text.Substring(text.IndexOf(languageModel.Header, StringComparison.Ordinal) + 
                                            languageModel.Header.Length).Trim();
    }

    private async Task CheckLanguageFile(ProcessingModel model, FileLanguageModel languageModel)
    {
        if (model.CheckerConfig.RequiredHeaders is { Count: > 0 })
            CheckRequiredHeaders(model, languageModel);
        
        if (model.CheckerConfig.RequiredLists is { Count: > 0 })
            CheckRequiredLists(model, languageModel);
        
        if (model.CheckerConfig.CheckFileLanguage || model.CheckerConfig.ChatGptSpellCheck)
            await CheckFileBody(model, languageModel);
        
        if (model.CheckerConfig.CheckSlugRegex)
            CheckSlugRegex(model, languageModel);
        
        if (model.CheckerConfig.CheckHeaderDuplicates.Any())
            CheckHeaderDuplicates(model, languageModel);
    }

    private void CheckHeaderDuplicates(ProcessingModel model, FileLanguageModel languageModel)
    {
        foreach(var header in model.CheckerConfig.CheckHeaderDuplicates)
            if (yamlService.ContainsChild(languageModel.Yaml, header))
            {
                var value = yamlService.GetStringValue(languageModel.Yaml, header);
                
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
        if (yamlService.ContainsChild(languageModel.Yaml, "slug"))
        {
            var slug = yamlService.GetStringValue(languageModel.Yaml, "slug");
            if (!Regex.IsMatch(slug, model.CheckerConfig.PatternSlugRegex))
                throw new Exception($"Slug '{slug}' doesn't match with the pattern '{model.CheckerConfig.PatternSlugRegex}'");
        }
    }


    private async Task CheckFileBody(ProcessingModel model, FileLanguageModel languageModel)
    {
        if (!model.CheckerConfig.ChatGptSpellCheck)
        {
            if (model.CheckerConfig.CheckFileLanguage)
                CheckFileLanguageLocally(languageModel.Body, languageModel.Language);

            return;
        }

        try
        {
            if (model.CheckerConfig.CheckFileLanguage)
                await chatGptService.SpellCheck(languageModel.Body, languageModel.Language);
            else
                await chatGptService.SpellCheck(languageModel.Body);
        }
        catch (Exception ex)
        {
            throw new Exception($"File '{languageModel.FilePath}' failed spellcheck.", ex);
        }
    }

    private void CheckFileLanguageLocally(string text, string expectedLanguage)
    {
        var language = languageDetector.Detect(text);
        var culture = new CultureInfo(language);
        if (string.Compare(culture.TwoLetterISOLanguageName, expectedLanguage, StringComparison.OrdinalIgnoreCase) != 0)
            throw new Exception($"Language '{language}' is not expected '{expectedLanguage}'");
    }

    private void CheckRequiredLists(ProcessingModel model, FileLanguageModel languageModel)
    {
        foreach(var pair in model.CheckerConfig.RequiredLists)
            CheckRequiredList(model, languageModel, pair.Key);
    }

    private void CheckRequiredList(ProcessingModel model, FileLanguageModel languageModel, string key)
    {
        var list = yamlService.GetListValue(languageModel.Yaml, key);
        if (!list.Any())
            throw new Exception($"There are no required list '{key}' in the file {languageModel.FilePath}");
        
        foreach(var item in list)
            CheckRequiredListItem(model, languageModel, key, item);
    }

    private void CheckRequiredListItem(ProcessingModel model, FileLanguageModel languageModel, string key, string value)
    {
        if (model.CheckerConfig.RequiredLists != null && !model.CheckerConfig.RequiredLists.ContainsKey(key))
            throw new Exception($"There are no required list '{key}' in the file {languageModel.FilePath}. Check required-lists in the hugo-checker.yaml");

        if (model.CheckerConfig.RequiredLists != null && !model.CheckerConfig.RequiredLists[key].ContainsKey(languageModel.Language))
            throw new Exception($"There are no required list '{key}' in the file {languageModel.FilePath} for language {languageModel.Language}");

        if (model.CheckerConfig.RequiredLists != null && !model.CheckerConfig.RequiredLists[key][languageModel.Language].Contains(value))
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
            var list = yamlService.GetListValue(languageModel.Yaml, key);
            if (!list.Any())
                throw new Exception($"There are no required header key '{key}' (list) in the file {languageModel.FilePath}");
        }
        else
        {
            var value = yamlService.GetStringValue(languageModel.Yaml, key);
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
        IsLanguageValid(model, model.HugoConfig.LanguageCode);

        core.Info($"Default language used for primary files *.md: '{model.CheckerConfig.DefaultLanguage}'");
        IsLanguageValid(model, model.CheckerConfig.DefaultLanguage);

        core.Info($"All used languages: {string.Join(",", model.CheckerConfig.Languages)}");
        foreach (var language in model.CheckerConfig.Languages)
            IsLanguageValid(model, language);

        foreach (var section in model.CheckerConfig.RequiredLists)
        {
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

    private void IsLanguageValid(ProcessingModel model, string? languageCode)
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
    }

    private string GetHugoFolder(string? hugoFolder = null)
    {
        var inputHugoFolder = core.GetInput("hugo-folder", false);
        hugoFolder = !string.IsNullOrWhiteSpace(inputHugoFolder) ? inputHugoFolder : hugoFolder; 

        if (string.IsNullOrWhiteSpace(hugoFolder))
            throw new Exception("Input: hugo-folder is required");

        if (!Directory.Exists(hugoFolder))
            throw new Exception($"Folder input:hugo-folder: '{hugoFolder}' doesn't exist");

        hugoFolder = Path.GetFullPath(hugoFolder);
        core.Info($"Hugo folder exists: {hugoFolder}");

        return hugoFolder;
    }

    private async Task<HugoConfig> ReadHugoConfig(string? hugoFolder)
    {
        if (string.IsNullOrWhiteSpace(hugoFolder))
            throw new Exception("Hugo folder is required");
        
        var hugoConfigFile = Path.Combine(hugoFolder, "config.yaml");

        if (!File.Exists(hugoConfigFile))
            throw new Exception($"Hugo configuration file '{hugoConfigFile}' doesn't exist");

        core.Info($"Hugo configuration file '{hugoConfigFile}' is loading...");

        var text = await File.ReadAllTextAsync(hugoConfigFile);
        var mapping = yamlService.GetYamlFromText(text);

        core.Info("Hugo configuration file is loaded");
        var config = new HugoConfig()
        {
            Title = yamlService.GetStringValue(mapping, "title"),
            LanguageCode = yamlService.GetStringValue(mapping, "languageCode")
        };
        core.Info($"Website title: {config.Title}, language code: {config.LanguageCode}");
        return config;
    }

    private string GetFileHeaderAsText(string text)
    {
        int firstPosition = text.IndexOf("---", StringComparison.Ordinal);
        if (firstPosition < 0)
            throw new Exception("Unable to find header in the file");
        
        int secondPosition = text.IndexOf("---", firstPosition + 3, StringComparison.Ordinal);
        if (secondPosition < 0)
            throw new Exception("Unable to find header in the file");

        var result = text.Substring(firstPosition + 3, secondPosition - firstPosition);
        
        return result.Trim();
    }
                
    private async Task<HugoCheckerConfig> ReadCheckerConfig(string hugoFolder)
    {
        var checkerConfigFile = Path.Combine(hugoFolder, "hugo-checker.yaml");

        if (!File.Exists(checkerConfigFile))
            throw new Exception($"Hugo checker configuration file '{checkerConfigFile}' doesn't exist");

        core.Info($"Hugo checker configuration file '{checkerConfigFile}' is loading...");

        var config = await yamlService.ReadFromFile(checkerConfigFile);
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
                    LanguageFiles = new()
                });
            var language = GetFileLanguage(model, filePath);
            if (!model.CheckerConfig.Languages.Contains(language))
                throw new Exception(
                    $"Language code is not defined in hugo-checker.yaml file, expected {string.Join(", ", model.CheckerConfig.Languages)}.");

            result[rootFileNameWithoutExtension].LanguageFiles[language] = new FileLanguageModel(language, filePath);
        }

        return result;
    }

    private string GetRootFilePath(ProcessingModel model, string filePath)
    {
        filePath = Path.GetFullPath(filePath);
        var language = GetFileLanguage(model, filePath);

        if (language == model.CheckerConfig.DefaultLanguage)
            return filePath;

        var folder = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        fileName = Path.GetFileNameWithoutExtension(fileName);
        fileName += ".md";
        if(!string.IsNullOrWhiteSpace(folder))
            filePath = Path.Combine(folder, fileName);

        if (!File.Exists(filePath))
            throw new Exception($"File '{filePath}' doesn't exist");

        return filePath;
    }

    private string GetFileLanguage(ProcessingModel model, string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var items = fileName.Split('.');
        var language = items[^1];
        if (items.Length == 1)
            language = model.CheckerConfig.DefaultLanguage;

        IsLanguageValid(model, language);

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