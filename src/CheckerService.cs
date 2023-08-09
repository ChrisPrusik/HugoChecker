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
    public string HugoConfigFile => Path.Combine(HugoFolder, "config.yaml");
    public string CheckerConfigFile => Path.Combine(HugoFolder, "arghul-hugo-checker.yaml");
    
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
    }
    
    private void CheckInputs()
    {
        if (string.IsNullOrWhiteSpace(HugoFolder))
            throw new Exception("Input: hugo-folder is required");
        
        if (!Directory.Exists(HugoFolder))
            throw new Exception($"Directory input:hugo-folder: '{HugoFolder}' does not exist");
        
        if (!File.Exists(HugoConfigFile))
            throw new Exception($"Hugo config file: '{HugoConfigFile}' does not exist");

        if (!File.Exists(CheckerConfigFile))
            throw new Exception($"Hugo checker config file: '{CheckerConfigFile}' does not exist");
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