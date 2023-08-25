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
using Microsoft.Extensions.DependencyInjection;
using DotnetActionsToolkit;
using HugoChecker;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var serviceProvider = new ServiceCollection()
    .AddSingleton<Core>()
    .AddSingleton<IYamlService, YamlService>()
    .AddSingleton<IChatGptService, ChatGptService>()
    .AddSingleton<ICheckerService, CheckerService>()
    .BuildServiceProvider();

var checkerService = serviceProvider.GetRequiredService<ICheckerService>();

try
{
    var hugoFolder = config["HUGO_FOLDER"];
    if (args.Length > 0)
    {
        if (args[0] == "--help" || args[0] == "-h")
        {
            Console.WriteLine("Usage: hugochecker hugoFolder [chatgptApiKey]");
            Console.WriteLine(("Whereas:"));
            Console.WriteLine(("  hugoFolder - the root folder of the Hugo project"));
            Console.WriteLine(("  chatgptApiKey - the API key for the ChatGPT service, optional"));
            Console.WriteLine();
            Console.WriteLine("More info: https://github.com/marketplace/actions/hugochecker");
            return;
        }
        hugoFolder = args[0];
    }

    await checkerService.Check(hugoFolder, config["CHATGPT_API_KEY"]);
}
catch (Exception ex)
{
    var core = new Core();
    core.SetFailed(ex.Message);
    while (ex.InnerException != null)
    {
        ex = ex.InnerException;
        core.SetFailed(ex.Message);
    }
}