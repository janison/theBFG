using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using Rxns;
using Rxns.Hosting;

namespace theBFG.TestDomainAPI
{
    /// <summary>
    /// Exposes vstest.console.exe as a test arena
    /// 
    /// Not thread safe
    /// </summary>
    public class VsTestArena : DotNetTestArena
    {
        protected override string PathToTestArenaProcess()
        {
            var path = string.Empty;
            return Rxn.Create(@"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe", //should probbaly try other drives?
                    "-legacy -prerelease -format json",
                    i =>
                    {
                        if (!path.IsNullOrWhitespace()) return;

                        if (i.BasicallyContains("installationPath"))
                        {
                            //whoa yeh, nice hack here
                            var tokens = i.Split("\":");
                            path = tokens[1].Split('"')[1];
                        }
                    },
                    e => { })
                .LastOrDefaultAsync()
                .Select(_ => $@"{path}\Common7\IDE\Extensions\TestPlatform\vstest.console.exe")
                .WaitR();
        }

        protected override string StartTestsCmd(StartUnitTest work, string logDir)
        {
            return $"{FilterIfSingleTestOnly(work)} {work.Dll.EnsureRooted()} /resultsdirectory:{logDir.EnsureRooted()} /logger:\"console;verbosity=detailed\" /logger:\"trx;LogFileName={work.Id}.trx\"";
        }

        protected override string ListTestsCmd(string dll)
        {
            baddll = false;
            startParsing = false;
            return $"{dll.EnsureRooted()} --listtests";
        }
        
        protected override IEnumerable<string> OnTestCmdLog(string i)
        {
            if (baddll)
                yield break;

            if (i != null && i.Contains("are available:"))
            {
                startParsing = true;
                yield break;
            }
            
            if (startParsing && i != null && i.Contains("vstest") && i.EndsWith("exited"))
            {
                startParsing = false;
            }

            if (!i.IsNullOrWhitespace() && i.BasicallyContains("Exception discovering"))
            {
                baddll = true;
            }

            if (startParsing && i != null && i.StartsWith("  ") && !i.StartsWith("   at "))
            {
                yield return i.Trim();
            }
        }


        protected override string FilterIfSingleTestOnly(StartUnitTest work)
        {
            return work.RunThisTest.IsNullOrWhitespace() ? "" : $" /Tests:{work.RunThisTest}";
        }

        public VsTestArena(IRxnAppInfo appInfo) : base(appInfo)
        {
        }
    }

    public class UnitTestsStarted : ITestDomainEvent
    {
        public DateTime At { get; set; }

        public string TestId { get; set; }

        public string[] Tests { get; set; }

        public string Worker { get; set; }
        public string WorkerId { get; set; }

        public UnitTestsStarted()
        {
        }

    }
}
