using System;
using System.IO;
using System.Threading.Tasks;
using DotnetActionsToolkit;

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
        await Task.Delay(1000);    
        FinishInformation();
    }

    private void CheckInputs()
    {
        if (string.IsNullOrEmpty(HugoFolder))
            throw new Exception("Input: hugo-folder is required");
        
        if (!Directory.Exists(HugoFolder))
            throw new Exception($"Directory input:hugo-folder: '{HugoFolder}' does not exist");
    }

    private void StartInformation()
    {
        core.Info($"ArghulHugoChecker hugo-folder: {HugoFolder}");
    }

    private void FinishInformation()
    {
        core.Info("Well done!");
    }
}