using Rxns;

namespace theBFG
{
    class bfgMain
    {
        static void Main(string[] args)
        {
            theBfg.ReloadWith(args: args).Until();
        }
    }
}