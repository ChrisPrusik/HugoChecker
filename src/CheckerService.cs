using System;
using System.IO;
using System.Threading.Tasks;
using DotnetActionsToolkit;
using YamlDotNet;

namespace ArghulHugoChecker;

public class CheckerService
{
    private readonly Core core;

    public string HugoFolder => core.GetInput("hugo-folder");
    
    public CheckerService(Core core)
    {
        this.core = core;
    }

    public async Task Check()
    {
        StartInformation();
        CheckInputs();
        await ReadConfig();
        await Task.Delay(1000);    
        FinishInformation();
    }

    private async Task ReadConfig()
    {
        var fileName = Path.Combine(HugoFolder, "arghul-hugo-checker.yaml");
        if (!File.Exists(fileName))
            throw new Exception($"File '{fileName}' does not exist");
    }
    
    private void CheckInputs()
    {
        if (string.IsNullOrWhiteSpace(HugoFolder))
            throw new Exception("Input: hugo-folder is required");
        
        if (!Directory.Exists(HugoFolder))
            throw new Exception($"Directory input:hugo-folder: '{HugoFolder}' does not exist");
    }

    private void StartInformation()
    {
        core.Info($"ArghulHugoChecker hugo-folder: '{HugoFolder}'");
    }

    private void FinishInformation()
    {
        core.Info("Well done!");
    }
}