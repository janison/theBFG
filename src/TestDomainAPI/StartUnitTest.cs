using System;
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

        //of the format {StartTestNumber}:{EndTestNumber} as returned from "discover tests" command (dotnet test test.dll -discovertests)
        public string Range { get; set; }

        public StartUnitTest()
        {

        }

        /// <summary>
        /// StartUnitTest False  0 False C:/svn/bfg/theTestGimp/bin/Debug/netcoreapp3.1/theTestGimp.dll Test Latest  
        /// </summary>
        public StartUnitTest(string RunAllTest, string RepeatTests, string InParallel, string Dll, string UseAppUpdate, string UseAppVersion)
        {
            this.RunAllTest =  bool.Parse(RunAllTest.IsNullOrWhiteSpace("false"));
            this.RunThisTest = RunThisTest;
            this.RepeatTests = int.Parse(RepeatTests.IsNullOrWhiteSpace("0"));
            this.InParallel = bool.Parse(InParallel.IsNullOrWhiteSpace("false"));
            this.Dll = Dll;
            this.UseAppUpdate = UseAppUpdate;
            this.UseAppVersion = UseAppVersion;
//            this.AppStatusUrl = AppStatusUrl;
  //          this.Range = Range;
        }

        public StartUnitTest(string RunAllTest, string RepeatTests, string InParallel, string Dll, string UseAppUpdate)
        {
            this.RunAllTest = bool.Parse(RunAllTest.IsNullOrWhiteSpace("false"));
            this.RunThisTest = RunThisTest;
            this.RepeatTests = int.Parse(RepeatTests.IsNullOrWhiteSpace("0"));
            this.InParallel = bool.Parse(InParallel.IsNullOrWhiteSpace("false"));
            this.Dll = Dll;
            this.UseAppUpdate = UseAppUpdate;
            //this.UseAppVersion = UseAppVersion;
            //            this.AppStatusUrl = AppStatusUrl;
            //          this.Range = Range;
        }

    }

}