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
using System.Threading.Tasks;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Models;

namespace HugoChecker;

public class ChatGptService : IChatGptService
{
    private OpenAIAPI? openAIApi = null;
    
    private Conversation? languageDetector = null;
    public Conversation LanguageDetector => GetLanguageDetectConversation();

    public async Task Initialise(string? chatGptApiKey)
    {
        openAIApi = new OpenAIAPI(chatGptApiKey);
        if (!await openAIApi.Auth.ValidateAPIKey())
            throw new Exception("Invalid OpenAI ChatGPT API key");
    }

    public async Task<string> LanguageDetect(string text)
    {
        if (LanguageDetector == null)
            throw new Exception("ChatGPT is not available");

        LanguageDetector.AppendUserInput(text);
        var language = await LanguageDetector.GetResponseFromChatbotAsync();

        if (string.IsNullOrWhiteSpace(language) || language.Length != 2)
            throw new Exception($"Unable to detect language from text '{text}'");

        return language;
    }

    private Conversation GetLanguageDetectConversation()
    {
        if (languageDetector != null)
            return languageDetector;
        
        if (openAIApi == null)
            throw new Exception("ChatGPT is not available");

        var request = new ChatRequest()
        {
            Model = Model.ChatGPTTurbo,
            Temperature = 0.1,
            MaxTokens = 1000,
            Messages = new ChatMessage[]
            {
                new ChatMessage(ChatMessageRole.System, 
                    """
                    Your role is to identify language of the text message provided by the user.
                    Do not ask any questions - just make a judgement.
                    If you are not sure about the language, choose the most probable one.
                    As a result return two letter code of the language (ISO 639-1 code).
                    """)
            }
        };
        languageDetector = openAIApi.Chat.CreateConversation(request);

        if (languageDetector == null)
            throw new Exception("Unable to create ChatGPT conversation");

        return languageDetector;
    }               
}