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
using System.Text.Json;
using System.Threading.Tasks;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Models;
using YamlDotNet.Serialization.NamingConventions;

namespace HugoChecker;

public class ChatGptService : IChatGptService
{
    private OpenAIAPI? openAIApi = null;
    
    private Conversation? languageDetector = null;
    public Conversation LanguageDetector => GetLanguageDetectConversation();

    private const int maxTextLength = 20000;
    
    public Double ChatGptTemperature { get; set; } = 0.9;
    public int ChatGptMaxTokens { get; set; } = 1000;
    public string ChatGptModel { get; set; } = "GPT4";

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

    public async Task Initialise(string? chatGptApiKey)
    {
        openAIApi = new OpenAIAPI(chatGptApiKey);
        if (!await openAIApi.Auth.ValidateAPIKey())
            throw new Exception("Invalid OpenAI ChatGPT API key");
    }

    public async Task<string> CheckText(string text)
    {
        if (LanguageDetector == null)
            throw new Exception("ChatGPT is not available");
        
        if (string.IsNullOrWhiteSpace(text))
            throw new Exception("Text to detect language is empty");

        if (text.Length > maxTextLength)
            text = text.Substring(0, maxTextLength);

        LanguageDetector.AppendMessage(ChatMessageRole.User, text);
        var response = await LanguageDetector.GetResponseFromChatbotAsync();
        var result = JsonSerializer.Deserialize<ChatGptResult>(response);
        if (result == null)
            throw new Exception("Unable to deserialize ChatGPT response");

        if (!result.SpellCheck)
            throw new Exception($"Spellcheck failed: {result.Comment}");
        
        var language = result.Language;

        if (string.IsNullOrWhiteSpace(language) || language.Length != 2)
            throw new Exception($"Unable to detect language from text '{text}'");

        return language.ToLower().Trim();
    }
    
    // private void string GetModel()
    // {
    //     switch (ChatGptModel.Trim().ToLower())
    //     {
    //         "gpt4" => Model.GPT4,
    //         "turbo" => Model.ChatGPTTurbo,
    //     }
    // }

    private Conversation GetLanguageDetectConversation()
    {
        if (languageDetector != null)
            return languageDetector;
        
        if (openAIApi == null)
            throw new Exception("ChatGPT is not available");

        var request = new ChatRequest()
        {
            Model = ChatGptModel,
            Temperature = ChatGptTemperature,
            MaxTokens = ChatGptMaxTokens,
        };
        languageDetector = openAIApi.Chat.CreateConversation(request);
        LanguageDetector.AppendMessage(ChatMessageRole.User, ChatGptPrompt);

        if (languageDetector == null)
            throw new Exception("Unable to create ChatGPT conversation");

        return languageDetector;
    }               
}