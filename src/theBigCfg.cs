using System.IO;
using Rxns.NewtonsoftJson;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public class theBigCfg : StartUnitTest
    {
        public static theBigCfg Detect()
        {
            var cfg = new theBigCfg();
            if (File.Exists("bfg.cfg"))
            {
                cfg = File.ReadAllText("unittest.cfg").FromJson<theBigCfg>();
            }

            return cfg;
        }
    }
}