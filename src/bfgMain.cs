using System.Reactive;
using Rxns;

namespace theBFG
{
    class bfgMain
    {
        static void Main(string[] args)
        {
            theBfg.ReloadAnd(args: args).Until();
            theBfg.IsCompleted.WaitR();
        }
    }
}