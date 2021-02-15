using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using Rxns;
using Rxns.Cloud;
using Rxns.Hosting;
using Rxns.Hosting.Updates;
using Rxns.Logging;
using theBFG.TestDomainAPI;

namespace theBFG
{
    /// <summary>
    /// 
    /// </summary>
    public class bfgWorker : IClusterWorker<StartUnitTest, UnitTestResult>
    {
        private readonly IUpdateServiceClient _updateService;
        public string Name { get; }
        public string Route { get; }

        public bfgWorker(string name, string route, IUpdateServiceClient updateService)
        {
            _updateService = updateService;
            Name = name;
            Route = route;
        }

        public IObservable<UnitTestResult> DoWork(StartUnitTest work)
        {
            $"Preparing to run {(work.RunAllTest ? "All" : work.RunThisTest)}".LogDebug();

            if (!Directory.Exists("logs"))
            {
                Directory.CreateDirectory("logs");
            }

            var file = File.Create($"logs/testLog_{DateTime.Now.ToString("s").Replace(":", "").LogDebug("LOG")}");
            var testLog = new StreamWriter(file, leaveOpen: true);
            var keepTestUpdatedIfRequested =
                work.UseAppUpdate.ToObservable(); //if not using updates, the dest folder is out root

            if (!work.UseAppUpdate.IsNullOrWhitespace())
            {
                //todo: update need
                keepTestUpdatedIfRequested = _updateService.KeepUpdated(work.UseAppUpdate, work.UseAppVersion,
                    work.UseAppUpdate, new RxnAppCfg()
                    {
                        AppStatusUrl = work.AppStatusUrl,
                        SystemName = work.UseAppUpdate,
                        KeepUpdated = true
                    }, true);
            }

            return keepTestUpdatedIfRequested
                .Select(testPath =>
                {
                    $"Running {(work.RunAllTest ? "All" : work.RunThisTest)}".LogDebug();

                    //todo: make testrunner injectable/swapable
                    return Rxn.Create("dotnet",
                        $"test{FilterIfSingleTestOnly(work)} {Path.Combine(testPath, work.Dll)}",
                        i => testLog.WriteLine(i.LogDebug(work.RunThisTest ?? work.Dll)),
                        e => testLog.WriteLine(e.LogDebug(work.RunThisTest ?? work.Dll))
                    );
                })
                .Switch()
                .LastOrDefaultAsync()
                .Select(_ =>
                {
                    return (UnitTestResult) new UnitTestResult()
                    {
                        WasSuccessful = true
                    }.AsResultOf(work);
                });
        }

        private string FilterIfSingleTestOnly(StartUnitTest work)
        {
            return work.RunThisTest.IsNullOrWhitespace() ? "" : $" --filter Name={work.RunThisTest}";
        }
    }
}