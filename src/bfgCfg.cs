using System.IO;
using Rxns.NewtonsoftJson;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public class bfgCfg : StartUnitTest
    {
        public static string CfgFile = Path.Combine(theBfg.DataDir, "bfg.json");
        public bfgCfg()
        {

        }

        public static bfgCfg Detect()
        {
            return File.Exists(CfgFile) ? Read(CfgFile) : new bfgCfg().Save();
        }

        private static bfgCfg Read(string cfgFile)
        {
            return File.ReadAllText(cfgFile).FromJson<bfgCfg>();
        }

        public bfgCfg Save()
        {
            File.WriteAllText(CfgFile, this.ToJson());

            return this;
        }
    }
}