using System.Reactive;
using System.Reactive.Linq;
using Rxns;
using Rxns.Hosting;

namespace theBFG
{
    class bfgMain
    {
        private static void Main(string[] args)
        {
            theBfg.ReloadAnd(args: args).Until();
            var appContext = theBfg.IsReady.FirstOrDefaultAsync().WaitR()?.AppCmdService;
            if(appContext != null)
                ConsoleHostedApp.StartREPL(appContext);
        }
    }
}