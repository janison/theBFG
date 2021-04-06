using System;
using System.Collections.Generic;
using System.Text;
using Rxns;
using Rxns.Interfaces;

namespace theBFG.TestDomainAPI
{
    public class VsTestArena : DotNetTestArena
    {
        protected bool startParsing;
      
        protected override string PathToTestArenaProcess()
        {
            return @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe";
        }

        protected override string StartTestsCmd(StartUnitTest work, string logDir)
        {
            return $"{FilterIfSingleTestOnly(work)} {work.Dll.EnsureRooted()} /resultsdirectory:{logDir.EnsureRooted()} /logger:\"console;verbosity=detailed\" /logger:\"trx;LogFileName={work.Id}.trx\"";
        }

        protected override string ListTestsCmd(StartUnitTest work)
        {
            return $"{work.Dll.EnsureRooted()} --listtests";
        }
        
        protected override IEnumerable<string> OnTestCmdLog(StartUnitTest work, string i)
        {
            if (i != null && i.Contains("are available:"))
            {
                startParsing = true;
                yield break;
            }

            if (startParsing && i.IsNullOrWhitespace())
            {
                startParsing = false;
            }

            if (startParsing && i != null && i.Contains("vstest") && i.EndsWith("exited"))
            {
                startParsing = false;
            }

            if (startParsing)
            {
                yield return i?.Trim();
            }
        }


        protected override string FilterIfSingleTestOnly(StartUnitTest work)
        {
            return work.RunThisTest.IsNullOrWhitespace() ? "" : $" /Tests:{work.RunThisTest}";
        }

    }

    public class UnitTestsStarted : ITestDomainEvent
    {
        public DateTime At { get; set; }

        public string TestId { get; set; }

        public string[] Tests { get; set; }

        public UnitTestsStarted()
        {
        }

    }
}
