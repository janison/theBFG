using Rxns;

namespace theBFG.TestDomainAPI
{
    public class VsTestArena : DotNetTestArena
    {
        protected override string PathToTestArenaProcess()
        {
            return @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe";
        }

        protected override string StartTestsCmd(StartUnitTest work)
        {
            return $"{FilterIfSingleTestOnly(work)} {work.Dll.EnsureRooted()} /resultsdirectory:{"logs/".EnsureRooted()}";
        }
    }
}
