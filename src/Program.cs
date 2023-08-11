using DotnetActionsToolkit;
using System;
using System.Collections;
using System.Threading.Tasks;

namespace HugoChecker;

public class Program
{
    static async Task Main(string[] args)
    {
        var core = new Core();
        var checkerService = new CheckerService(core);
        try
        {
            await checkerService.Check();
        }
        catch (Exception ex)
        {
            core.SetFailed(ex.Message);
        }
    }
}
