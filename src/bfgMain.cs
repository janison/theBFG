using System.Reactive;
using System.Reactive.Linq;
using Rxns;
using Rxns.Hosting;

namespace theBFG
{
    class bfgMain
    {
        static void Main(string[] args)
        {
            theBfg.ReloadAnd(args: args).Until();
            ConsoleHostedApp.StartREPL(theBfg.IsReady.FirstAsync().WaitR().AppCmdService);
        }
    }
}