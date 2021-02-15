using Rxns.DDD.Commanding;

namespace theBFG.TestDomainAPI
{
    public class StartUnitTest : ServiceCommand
    {
        public bool RunAllTest { get; set; }
        public string RunThisTest { get; set; }
        public int RepeatTests { get; set; }
        public bool InParallel { get; set; }
        public string Dll { get; set; }
        public string UseAppUpdate { get; set; }
        public string UseAppVersion { get; set; }
        public string AppStatusUrl { get; set; }
    }

}