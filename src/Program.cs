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
            if (args.Length > 0)
                await checkerService.Check(args[0]);
            else
                await checkerService.Check();
        }
        catch (Exception ex)
        {
            core.SetFailed(ex.Message);
        }
    }
}
