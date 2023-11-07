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
using System.Text.Json;
using System.Threading.Tasks;
using OpenAI_API;
using OpenAI_API.Chat;

namespace HugoChecker.Services;

public class ChatGptService : IChatGptService
{
    private OpenAIAPI? openAiApi;
    
    private Conversation? languageDetector;
    
    public async Task Initialise(string? apiKey, string? prompt, 
        string? model = null, double? temperature = null, int? maxTokens = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new Exception("The ChatGPT API key is empty");
        
        if (string.IsNullOrWhiteSpace(prompt))
            throw new Exception("The ChatGPT prompt is empty");
        
        if (openAiApi == null)
        {
            openAiApi = new OpenAIAPI(apiKey);
            if (!await openAiApi.Auth.ValidateAPIKey())
                throw new Exception("Invalid OpenAI ChatGPT API key");
        }
        
        CreateLanguageDetectConversation(prompt, model, temperature, maxTokens);
    }

    private void CreateLanguageDetectConversation(string? prompt, 
        string? model = null, double? temperature = null, int? maxTokens = null)
    {
        if (openAiApi == null)
            throw new Exception("OpenAI ChatGPT API is not initialised");

        var request = new ChatRequest()
        {
            Model = model ?? "gpt-4",
            Temperature = temperature ?? 0.9,
            MaxTokens = maxTokens ?? 2000,
        };
        languageDetector = openAiApi.Chat.CreateConversation(request);
        languageDetector.AppendMessage(ChatMessageRole.User, prompt);

        if (languageDetector == null)
            throw new Exception("Unable to create ChatGPT conversation");
    }               

    public async Task SpellCheck(string? text, string? expectedLanguage = null)
    {
        if (languageDetector == null)
            throw new Exception("ChatGPT is not available");
        
        if (string.IsNullOrWhiteSpace(text))
            throw new Exception("Text to detect language is empty");

        languageDetector.AppendMessage(ChatMessageRole.User, text);
        var response = await GetResponse();
        var result = JsonSerializer.Deserialize<ChatGptResult>(response);
        if (result == null)
            throw new Exception("Unable to deserialize ChatGPT response");

        if (!result.SpellCheck)
            throw new Exception($"Spellcheck failed: {result.Comment}");
        
        var language = result.Language;

        if (string.IsNullOrWhiteSpace(language) || language.Length != 2)
            throw new Exception($"Unable to detect language from text '{text}'");

        if (language != expectedLanguage)
            throw new Exception($"Language detected '{language}' is different than expected '{expectedLanguage}'");
    }

    private async Task<string> GetResponse(int level = 4, int delay = 1000)
    {
        if (languageDetector == null)
            throw new NotImplementedException("languageDetector is not initialised");

        try
        {
            return await languageDetector.GetResponseFromChatbotAsync();
        }
        catch (Exception ex)
        {
            if (ex.Source != "OpenAI_API" || level < 0)
                throw;
            
            await Task.Delay(delay);
            return await GetResponse(level -1, delay * 2);
        }
    }
}