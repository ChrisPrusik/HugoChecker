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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotnetActionsToolkit;
using LanguageDetection;
using Markdig;
using Markdig.Syntax;
using SpellCheck;
using SpellCheck.Dictionaries;

namespace HugoChecker.Services;

public class CheckerService : ICheckerService
{
    private const string checkerConfigFileName = "hugo-checker.yaml";
    private const string hugoConfigFileName = "config.yaml";
    private const string ingoreSpellingWordsFileName = "ignore-spelling-words.txt";
    
    private readonly Core core;
    private readonly IYamlService yamlService;
    private readonly IChatGptService chatGptService;
    
    private LanguageDetector languageDetector;
    private SpellCheckFactory spellCheckFactory = new SpellCheckFactory();
    private Dictionary<string, SpellChecker> spellCheckers = new Dictionary<string, SpellChecker>();
   
    private string[] ignoreSpellingWords;
    
    private string? chatGptApiKey;

    public CheckerService(Core core, IYamlService yamlService, IChatGptService chatGptService)
    {
        this.core = core;
        this.yamlService = yamlService;
        this.chatGptService = chatGptService;

        languageDetector = new LanguageDetector();
        languageDetector.AddAllLanguages();
    }

    public async Task Check(string? hugoFolder = null, string? chatGptApiKey = null)
    {
        StartInformation();
        
        this.chatGptApiKey = chatGptApiKey ?? core.GetInput("chatgpt-api-key", false);

        var folder = GetHugoFolder(hugoFolder);

        spellCheckFactory.DictionaryDirectory = folder;
        
        var model = new ProcessingModel(folder, await ReadHugoConfig(folder));
        await ReadAllFiles(model);
        
        await LoadIgnoreSpellingWords(folder);
        
        CheckFileNames(model, model.Config.LanguageCode);
        await CheckAllFilesContent(model);

        FinishInformation();
    }

    private async Task LoadIgnoreSpellingWords(string folder)
    {
        var filePath = Path.Combine(folder, ingoreSpellingWordsFileName);
        if (File.Exists(filePath) is false)
        {
            core.Info($"File '{ingoreSpellingWordsFileName}' doesn't exist in the folder '{folder}'");
            return;
        }
        
        core.Info($"Loading '{ingoreSpellingWordsFileName}' file from the folder '{folder}'");
        var text = await File.ReadAllTextAsync(filePath);
        ignoreSpellingWords = text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
    }

    private async Task InitializeChatGpt(FolderModel model)
    {
        if (model.Config.ChatGptSpellCheck is not true)
            return;

        if (string.IsNullOrWhiteSpace(chatGptApiKey))
            throw new Exception("Undefined chatgpt-api-key. ChatGPT is not available.");

        await chatGptService.Initialise(chatGptApiKey, 
            model.Config.ChatGptPrompt,
            model.Config.ChatGptModel,
            model.Config.ChatGptTemperature,
            model.Config.ChatGptMaxTokens);

        core.Info($"Connected with OpenAI ChatGPT API");
    }

    private async Task CheckAllFilesContent(ProcessingModel model)
    {
        if (model.Folders is not {Count: > 0})
            throw new Exception($"There are no folders to check.");
        
        foreach (var pair in model.Folders) 
            await CheckFolderContent(pair.Value);
    }

    private async Task CheckFolderContent(FolderModel model)
    {
        core.Info($"Checking all files content in the folder '{model.FullFolderPath}'");
        ShowFolderSummary(model);

        await InitializeChatGpt(model);
        
        if (model.Files is not {Count: > 0})
            throw new Exception($"There are no files to check in the folder '{model.FullFolderPath}'");
        
        foreach (var file in model.Files) 
            await CheckFileContent(model, file.Value);
    }
    
    private void ShowFolderSummary(FolderModel model)
    {
        core.Info($"Default language used in the folder '{model.Config.DefaultLanguage}'.");
        core.Info($"All allowed languages '{ListToString(model.Config.Languages)}'.");
        core.Info($"Ignored files: '{ListToString(model.Config.IgnoreFiles)}'.");
        core.Info($"Required headers: '{ListToString(model.Config.RequiredHeaders)}'.");

        var list = new List<string>();
        if (model.Config.RequiredLists is not null) 
            foreach(var section in model.Config.RequiredLists)
                list.Add(section.Key);
        
        core.Info($"Required lists: '{ListToString(list)}' in languages '{ListToString(model.Config.Languages)}'.");
        core.Info($"Check header duplicates: '{ListToString(model.Config.CheckHeaderDuplicates)}'.");
        core.Info($"Check language structure: {model.Config.CheckLanguageStructure}");
        core.Info($"Check file language: {model.Config.CheckFileLanguage}");
        core.Info($"Check mark down: {model.Config.CheckMarkDown}");
        core.Info($"Check spelling: {model.Config.CheckSpelling}");
        core.Info($"Check slug regex: {model.Config.CheckSlugRegex}");
        core.Info($"Pattern for slug regex: '{model.Config.PatternSlugRegex}'");
        core.Info($"ChatGPT spell check: {model.Config.ChatGptSpellCheck}");
        
        if (model.Config.ChatGptSpellCheck is true)
        {
            core.Info($"ChatGPT model: '{model.Config.ChatGptModel}'");
            core.Info($"ChatGPT temperature: {model.Config.ChatGptTemperature}");
            core.Info($"ChatGPT max tokens: {model.Config.ChatGptMaxTokens}");
        }
    }

    private async Task CheckFileContent(FolderModel model, FileModel file)
    {
        if (file.LanguageFiles is not {Count: > 0})
            throw new Exception($"There are no language files to check '{file.RootFilePath}'");
        
        foreach (var language in file.LanguageFiles)
            await CheckLanguageFile(model, language.Value);
    }

    private bool IsFileIgnored(FolderModel model, string languageFullFilePath)
    {
        var fileName = Path.GetFileName(languageFullFilePath);
        var ignored = model.Config.IgnoreFiles is {Count: > 0} && 
               (model.Config.IgnoreFiles.Contains(fileName));
        if (ignored)
            core.Warning($"Ignore file '{languageFullFilePath}'");
        
        return ignored;
    }

    private async Task ReadLanguageFileContent(FileLanguageModel languageModel)
    {
        var text = await File.ReadAllTextAsync(languageModel.FullFilePath);
        languageModel.FileInfo = new FileInfo(languageModel.FullFilePath);
        languageModel.Header = GetFileHeaderAsText(text);
        languageModel.Yaml = yamlService.GetYamlFromText(languageModel.Header);
        languageModel.Body = text.Substring(text.IndexOf(languageModel.Header, StringComparison.Ordinal) + 
                                            languageModel.Header.Length).Trim();
        languageModel.MarkDown = Markdown.Parse(languageModel.Body);
    }

    private async Task CheckLanguageFile(FolderModel model, FileLanguageModel languageModel)
    {
        core.Info($"Checking file '{languageModel.FullFilePath}' language '{languageModel.Language}'");

        if (model.Config.CheckMarkDown is true)
            CheckMarkDown(model, languageModel);
        
        if (model.Config.RequiredHeaders != null && model.Config.RequiredHeaders.Any())
            CheckRequiredHeaders(model, languageModel);
        
        if (model.Config.RequiredLists != null && model.Config.RequiredLists.Any())
            CheckRequiredLists(model, languageModel);
        
        if (model.Config.CheckFileLanguage is true || 
            model.Config.CheckSpelling is true || 
            model.Config.ChatGptSpellCheck is true)
            await CheckFileBody(model, languageModel);
        
        if (model.Config.CheckSlugRegex is true)
            CheckSlugRegex(model, languageModel);
        
        if (model.Config.CheckHeaderDuplicates != null && model.Config.CheckHeaderDuplicates.Any())
            CheckHeaderDuplicates(model, languageModel);
    }
    
    private async Task CheckSpelling(string text, string language)
    {
        if (spellCheckers.ContainsKey(language) is false)
            spellCheckers[language] = await FindSpellChecker(language);

        spellCheckers[language].CheckText(text);
    }

    private async Task<SpellChecker> FindSpellChecker(string language) =>
        spellCheckFactory.Contains(language)
            ? await spellCheckFactory.CreateSpellChecker(language, ignoreSpellingWords)
            : await spellCheckFactory.CreateSpellChecker(language.FindCulture(), ignoreSpellingWords);

    private void CheckMarkDown(FolderModel model, FileLanguageModel languageModel)
    {
        var blocks = languageModel.MarkDown
            .Select(x => x)
            .Count(x => x is not null);
        var titles = languageModel.MarkDown
            .Select(x => x as HeadingBlock)
            .Count(x => x is not null);
        core.Info($"File '{languageModel.FullFilePath}' has {titles} titles, {blocks} blocks, {languageModel.MarkDown.LineCount} lines.");
        if (blocks == 0)
            throw new Exception($"File '{languageModel.FullFilePath}' has no blocks.");
    }
    
    private void CheckHeaderDuplicates(FolderModel model, FileLanguageModel languageModel)
    {
        if (model.Config.CheckHeaderDuplicates is { Count: > 0 })
            foreach(var header in model.Config.CheckHeaderDuplicates)
                if (yamlService.ContainsChild(languageModel.Yaml, header))
                {
                    var value = yamlService.GetStringValue(languageModel.Yaml, header);
                    
                    if (model.ProcessedDuplicates.ContainsKey(header) && 
                        model.ProcessedDuplicates[header].ContainsKey(value))
                        throw new Exception($"Detected duplicates {header}: '{value}' in two files '{model.ProcessedDuplicates[header][value]}' and '{languageModel.FullFilePath}'");

                    if (!model.ProcessedDuplicates.ContainsKey(header))
                        model.ProcessedDuplicates.Add(header, new Dictionary<string, string>());
                    
                    model.ProcessedDuplicates[header][value] = languageModel.FullFilePath;
                }
    }

    private void CheckSlugRegex(FolderModel model, FileLanguageModel languageModel)
    {
        if (yamlService.ContainsChild(languageModel.Yaml, "slug"))
        {
            var slug = yamlService.GetStringValue(languageModel.Yaml, "slug");
            if (string.IsNullOrWhiteSpace(model.Config.PatternSlugRegex))
                throw new Exception("Undefined slug regex pattern.");

            if (!Regex.IsMatch(slug, model.Config.PatternSlugRegex))
                throw new Exception($"Slug '{slug}' doesn't match with the pattern '{model.Config.PatternSlugRegex}'");
        }
    }


    private async Task CheckFileBody(FolderModel model, FileLanguageModel languageModel)
    {
        if (string.IsNullOrWhiteSpace(languageModel.Body))
            return;
        
        if (model.Config.CheckFileLanguage is true)
            CheckFileLanguageLocally(languageModel.Body, languageModel.Language);

        if (model.Config.CheckSpelling is true)
            await CheckSpelling(languageModel.Body, languageModel.Language);

        if (model.Config.ChatGptSpellCheck is true)
            await CheckChatGpt(model, languageModel);
    }

    private async Task CheckChatGpt(FolderModel model, FileLanguageModel languageModel)
    {
        try
        {
            if (model.Config.CheckFileLanguage is true)
                await chatGptService.SpellCheck(languageModel.Body, languageModel.Language);
            else
                await chatGptService.SpellCheck(languageModel.Body);
        }
        catch (Exception ex)
        {
            throw new Exception($"File '{languageModel.FullFilePath}' failed spellcheck.", ex);
        }
    }

    private void CheckFileLanguageLocally(string text, string expectedLanguage)
    {
        var language = languageDetector.Detect(text);
        var culture = new CultureInfo(language);
        if (string.Compare(culture.TwoLetterISOLanguageName, expectedLanguage, StringComparison.OrdinalIgnoreCase) != 0)
            throw new Exception($"Language '{language}' is not expected '{expectedLanguage}'");
    }

    private void CheckRequiredLists(FolderModel model, FileLanguageModel languageModel)
    {
        if (model.Config.RequiredLists is not null)
            foreach(var pair in model.Config.RequiredLists)
                CheckRequiredList(model, languageModel, pair.Key);
    }

    private void CheckRequiredList(FolderModel model, FileLanguageModel languageModel, string key)
    {
        var list = yamlService.GetListValue(languageModel.Yaml, key);
        if (!list.Any())
            throw new Exception($"There are no required list '{key}' in the file {languageModel.FullFilePath}");
        
        foreach(var item in list)
            CheckRequiredListItem(model, languageModel, key, item);
    }

    private void CheckRequiredListItem(FolderModel model, FileLanguageModel languageModel, string key, string value)
    {
        if (model.Config.RequiredLists != null && !model.Config.RequiredLists.ContainsKey(key))
            throw new Exception($"There are no required list '{key}' in the file {languageModel.FullFilePath}. Check required-lists in the {checkerConfigFileName}");

        if (model.Config.RequiredLists != null && !model.Config.RequiredLists[key].ContainsKey(languageModel.Language))
            throw new Exception($"There are no required list '{key}' in the file {languageModel.FullFilePath} for language {languageModel.Language}");

        if (model.Config.RequiredLists != null && !model.Config.RequiredLists[key][languageModel.Language].Contains(value))
            throw new Exception($"There are no required '{value}' from the list '{key}' in the file {languageModel.FullFilePath} for language {languageModel.Language}. Check required-lists in the {checkerConfigFileName}");
    }

    private void CheckRequiredHeaders(FolderModel model, FileLanguageModel languageModel)
    {
        if (model.Config.RequiredHeaders is not null)
            foreach(var header in model.Config.RequiredHeaders)
                CheckRequiredHeader(model, languageModel, header);
    }

    private void CheckRequiredHeader(FolderModel model, FileLanguageModel languageModel, string key)
    {
        if (model.Config.RequiredLists is not null && model.Config.RequiredLists.ContainsKey(key))
        {
            var list = yamlService.GetListValue(languageModel.Yaml, key);
            if (!list.Any())
                throw new Exception($"There are no required header key '{key}' (list) in the file {languageModel.FullFilePath}");
        }
        else
        {
            var value = yamlService.GetStringValue(languageModel.Yaml, key);
            if (string.IsNullOrWhiteSpace(value))
                throw new Exception($"There are no required header key '{key}' (value) in the file {languageModel.FullFilePath}");
            
        }
    }

    private void CheckFileNames(ProcessingModel model, string languageCode)
    {
        foreach (var folderPair in model.Folders)
            CheckFolderFileNames(folderPair.Value, languageCode);
    }

    private void CheckFolderFileNames(FolderModel folder, string languageCode)
    {
        if (folder.Files.Count == 0)
            core.Warning($"Folder {folder.FullFolderPath} doesn't have any markdown files");

        core.Info($"Folder {folder.FullFolderPath}");
        CheckLanguageStructure(folder, languageCode);
        
        foreach (var file in folder.Files)
        {
            core.Info(
                $"File '{file.Value.RootFilePath}' found languages {string.Join(", ", file.Value.LanguageFiles.Keys)}");

            if (folder.Config.Languages is null)
                throw new Exception($"Languages are not defined in the {checkerConfigFileName} file.");
            
            if (folder.Config.CheckLanguageStructure is true)
                foreach (var language in folder.Config.Languages)
                    if (!file.Value.LanguageFiles.ContainsKey(language))
                        throw new Exception($"File '{file.Value.RootFilePath}' doesn't have language '*.{language}.md'");
        }
    }

    private void CheckLanguageStructure(FolderModel model, string languageCode)
    {
        if (model.Config.CheckLanguageStructure is not true)
            return;
        
        if (model.Config.Languages is null)
            throw new Exception($"Languages are not defined in the {checkerConfigFileName} file");
        
        core.Info($"languageCode in the {hugoConfigFileName}: {languageCode}");
        IsLanguageValid(model, languageCode);

        core.Info($"Default language used for primary files *.md: '{model.Config.DefaultLanguage}'");
        IsLanguageValid(model, model.Config.DefaultLanguage);

        core.Info($"All used languages: {string.Join(",", model.Config.Languages)}");
        foreach (var language in model.Config.Languages)
            IsLanguageValid(model, language);

        if (model.Config.RequiredLists is not null)
            foreach (var section in model.Config.RequiredLists)
            {
                if (section.Value is null)
                    throw new Exception($"Undefined language in the list required.{section.Key}. Check required-lists key.");
                
                foreach (var language in model.Config.Languages)
                    if (!section.Value.ContainsKey(language))
                        throw new Exception(
                            $"Undefined language in the key required.{section.Key}: {language}. Check languages key.");

                foreach (var language in section.Value.Keys)
                    if (!model.Config.Languages.Contains(language))
                        throw new Exception(
                            $"Undefined language in the key required.{section.Key}: {language}. Check languages key.");
            }

        core.Info("All languages are valid");
    }

    private void IsLanguageValid(FolderModel model, string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new Exception("Language code is required");

        if (languageCode.Length != 2)
            throw new Exception($"Language code: '{languageCode}' is invalid. It should be 2 characters long");

        if (!char.IsLower(languageCode[0]) || !char.IsLower(languageCode[1]))
            throw new Exception($"Language code: '{languageCode}' is invalid. It should be lower case");

        if (model.Config.Languages is null || !model.Config.Languages.Contains(languageCode))
            throw new Exception(
                $"Language code is not defined in hugo-checker.yaml file, expected {ListToString(model.Config.Languages)}.");

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
        
        var hugoConfigFile = Path.Combine(hugoFolder, hugoConfigFileName);

        if (!File.Exists(hugoConfigFile))
            throw new Exception($"Hugo configuration file '{hugoConfigFile}' doesn't exist");

        core.Info($"Hugo configuration file '{hugoConfigFile}' is loading...");

        var text = await File.ReadAllTextAsync(hugoConfigFile);
        var mapping = yamlService.GetYamlFromText(text);

        core.Info("Hugo configuration file is loaded");
        var config = new HugoConfig(
            yamlService.GetStringValue(mapping, "languageCode"),
            yamlService.GetStringValue(mapping, "title"));

        core.Info($"Website title: {config.Title}, language code: {config.LanguageCode}");
        return config;
    }

    private string GetFileHeaderAsText(string text)
    {
        int firstPosition = text.IndexOf("---", StringComparison.Ordinal);
        if (firstPosition < 0)
            throw new Exception($"Starting string '--' not found for header in text {text}");
        
        int secondPosition = text.IndexOf("---", firstPosition + 3, StringComparison.Ordinal);
        if (secondPosition < 0)
            throw new Exception($"Ending string '--' not found for header in text: {text}");

        var result = text.Substring(firstPosition + 3, secondPosition - firstPosition);
        
        return result.Trim();
    }

    private async Task<HugoCheckerConfig> ReadCheckerConfig(string fileName)
    {
        if (!File.Exists(fileName))
            throw new Exception($"Hugo checker configuration file '{fileName}' doesn't exist");
        core.Info($"Hugo checker configuration file '{fileName}' is loading...");

        var config = await yamlService.ReadFromFile(fileName);
        core.Info("Hugo checker configuration file is loaded");
        return config;
    }

    private async Task ReadAllFiles(ProcessingModel model)
    {
        var result = new Dictionary<string, FolderModel>();
        var configFileNames = Directory.GetFiles(model.HugoFolder, 
            checkerConfigFileName, SearchOption.AllDirectories);
        if (!configFileNames.Any())
            throw new Exception($"'{checkerConfigFileName}' file doesn't exist in any subdirectory of {model.HugoFolder}");
        
        foreach (var fileName in configFileNames)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(fileName));
            if (string.IsNullOrWhiteSpace(folder))
                throw new Exception($"Folder for the file {fileName} doesn't exist");

            var folderModel = new FolderModel(folder, await ReadCheckerConfig(fileName));
            await ReadAllFilesFromFolder(folderModel);
            result.Add(folderModel.FullFolderPath, folderModel);
        }

        model.Folders = result;
    }

    private async Task ReadAllFilesFromFolder(FolderModel folderModel)
    {
        var result = new Dictionary<string, FileModel>();

        if (!Directory.Exists(folderModel.FullFolderPath))
            throw new Exception($"Folder {folderModel.FullFolderPath} doesn't exist");

        core.Info($"Loading markdown file names from the folder '{folderModel.FullFolderPath}'");

        foreach (var filePath in Directory.GetFiles(folderModel.FullFolderPath, "*.md",
                     SearchOption.TopDirectoryOnly))
        {
            if (IsFileIgnored(folderModel, filePath))
                continue;

            core.Info($"Reading markdown file '{filePath}'.");

            var rootFilePath = GetRootFilePath(folderModel, filePath);
            if (!result.ContainsKey(rootFilePath))
                result.Add(rootFilePath, new FileModel(rootFilePath));
            
            var language = GetFileLanguage(folderModel, filePath);

            if (folderModel.Config.Languages is null || !folderModel.Config.Languages.Contains(language))
                throw new Exception(
                    $"Language code is not defined in {checkerConfigFileName} file, expected {ListToString(folderModel.Config.Languages)}.");

            var fileLanguageModel = new FileLanguageModel(language, filePath);
            
            await ReadLanguageFileContent(fileLanguageModel);

            result[rootFilePath].LanguageFiles[language] = fileLanguageModel;
        }
        core.Info($"Markdown files count in the folder {folderModel.FullFolderPath}: {folderModel.Files.Count}");

        folderModel.Files = result;
    }

    private string GetRootFilePath(FolderModel model, string filePath)
    {
        filePath = Path.GetFullPath(filePath);
        var language = GetFileLanguage(model, filePath);

        if (language == model.Config.DefaultLanguage)
            return filePath;

        var folder = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        fileName = Path.GetFileNameWithoutExtension(fileName);
        fileName += ".md";
        if(!string.IsNullOrWhiteSpace(folder))
            filePath = Path.Combine(folder, fileName);

        return filePath;
    }

    private string GetFileLanguage(FolderModel model, string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var items = fileName.Split('.');
        var language = items[^1];
        if (items.Length == 1)
        {
            if (model.Config.DefaultLanguage is null)
                throw new Exception(
                    $"Language code is not defined in '{hugoConfigFileName}' file, expected {ListToString(model.Config.Languages)}.");

            language = model.Config.DefaultLanguage;
        }

        IsLanguageValid(model, language);

        return language;
    }

    private string ListToString(List<string>? list) => 
        list is null ? string.Empty : string.Join(", ", list);

    private void StartInformation() => 
        core.Info($"HugoChecker version: {typeof(CheckerService).Assembly.GetName().Version}");

    private void FinishInformation() => 
        core.Info("Well done!");
}