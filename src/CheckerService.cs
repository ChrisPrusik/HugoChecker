using System;
using System.IO;
using System.Threading.Tasks;
using DotnetActionsToolkit;

namespace HugoChecker;

public class CheckerService
{
    private readonly Core core;

    private string hugoFolder;

    public string HugoFolder => hugoFolder;

    private string HugoConfigFile => Path.Combine(HugoFolder, "config.yaml");
    
    private string CheckerConfigFile => Path.Combine(HugoFolder, "hugo-checker.yaml");
    
    public CheckerService(Core core)
    {
        this.core = core;
    }

    public async Task Check(string? hugoFolder = null)
    {
        this.hugoFolder = !string.IsNullOrWhiteSpace(hugoFolder) ? 
            hugoFolder : core.GetInput("hugo-folder", true);

        StartInformation();
        CheckInputs();
        var config = await ReadConfig();
        await Task.Delay(1000);    
        FinishInformation();
    }
    
    private async Task<HugoCheckerConfig> ReadConfig()
    {
        var config = new HugoCheckerConfig();

        return config;
    }
    
    private void CheckInputs()
    {
        if (string.IsNullOrWhiteSpace(HugoFolder))
            throw new Exception("Input: hugo-folder is required");
        
        if (!Directory.Exists(HugoFolder))
            throw new Exception($"Folder input:hugo-folder: '{HugoFolder}' does not exist");
        
        if (!File.Exists(HugoConfigFile))
            throw new Exception($"Hugo config file: '{HugoConfigFile}' does not exist");

        if (!File.Exists(CheckerConfigFile))
            throw new Exception($"Hugo checker config file: '{CheckerConfigFile}' does not exist");
        
        core.Info($"Found Hugo config file: '{HugoConfigFile}'");
        core.Info($"Found Hugo-checker config file: '{CheckerConfigFile}'");
    }

    private void StartInformation()
    {
        core.Info($"HugoChecker hugo-folder: '{HugoFolder}'");
    }

    private void FinishInformation()
    {
        core.Info("Well done!");
    }
}