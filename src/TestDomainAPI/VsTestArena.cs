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
            //lookup path from registry
            //HKEY_CURRENT_USER\Software\Microsoft\VisualStudio\12.0_Config
            //<VisualStudioFolder>$(Registry:HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\12.0@ShellFolder)</VisualStudioFolder> <VsTestFolder>$([System.IO.Path]::Combine("$(VisualStudioFolder)", "Common7", "IDE", "CommonExtensions", "Microsoft", "TestWindow"))\</VsTestFolder> <VSTestExe>$(VsTestFolder)vstest.console.exe</VSTestExe> 
            return @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe";
        }

        protected override string StartTestsCmd(StartUnitTest work, string logDir)
        {
            return $"{FilterIfSingleTestOnly(work)} {work.Dll.EnsureRooted()} /resultsdirectory:{logDir.EnsureRooted()} /logger:\"console;verbosity=detailed\" /logger:\"trx;LogFileName={work.Id}.trx\"";
        }

        protected override string ListTestsCmd(string dll)
        {
            return $"{dll.EnsureRooted()} --listtests";
        }
        
        protected override IEnumerable<string> OnTestCmdLog(string i)
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
