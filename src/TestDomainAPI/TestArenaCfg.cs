using System.IO;
using Rxns.Interfaces;
using Rxns.NewtonsoftJson;

namespace theBFG.TestDomainAPI
{
    public class TestArenaCfg : IRxn
    {
        /// <summary>
        /// The threshhold for a test to be considered "slow" and appear in the pie graph
        /// </summary>
        public int SlowTestMs { get; set; } = 100;

        /// <summary>
        /// This controls the resolution of the graph which displays each test outcome. The max value on the graph
        /// will be this value in milliseconds
        /// </summary>
        public int TestDurationMax { get; set; } = 100;
        /// <summary>
        /// If the test arena will be in lights out mode by default
        /// </summary>
        public bool IsLightsOut { get; set; }
        /// <summary>
        /// If the test arena metics will default to persistint between sessions.
        /// This is usually controlled with the "save" syntax in a command 
        /// </summary>
        public bool AlwaysSave { get; set; }

        /// <summary>
        /// If sounds will fire to indicate the outcome of a each test
        /// </summary>
        public bool SoundsOn { get; set; } = false;

        public static string CfgFile = Path.Combine(theBfg.DataDir, "testArena.json");
        public TestArenaCfg()
        {

        }

        public static TestArenaCfg Detect()
        {
            return File.Exists(CfgFile) ? Read(CfgFile) : new TestArenaCfg().Save();
        }

        private static TestArenaCfg Read(string cfgFile)
        {
            return File.ReadAllText(cfgFile).FromJson<TestArenaCfg>();
        }

        public TestArenaCfg Save()
        {
            File.WriteAllText(CfgFile, this.ToJson());
            return this;
        }
    }
}
