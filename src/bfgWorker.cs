using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Rxns;
using Rxns.Cloud.Intelligence;
using Rxns.DDD.Commanding;
using Rxns.DDD.CQRS;
using Rxns.Health;
using Rxns.Hosting;
using Rxns.Hosting.Updates;
using Rxns.Interfaces;
using Rxns.Logging;
using theBFG.TestDomainAPI;

namespace theBFG
{   
    /// <summary>
    /// 
    /// </summary>
    public class bfgWorker : IClusterWorker<StartUnitTest, UnitTestResult>
    {
        private readonly IAppServiceDiscovery _services;
        private readonly IRxnManager<IRxn> _rxnManager;
        private readonly IUpdateServiceClient _updateService;
        private readonly IAppServiceRegistry _registry;
        private int _runId;
        public string Name { get; }
        public string Route { get; }
        public IObservable<bool> IsBusy => _isBusy;
        private readonly ISubject<bool> _isBusy = new BehaviorSubject<bool>(false);

        public bfgWorker(string name, string route, IAppServiceRegistry registry, IAppServiceDiscovery services, IRxnManager<IRxn> rxnManager, IUpdateServiceClient updateService)
        {
            _registry = registry;
            _services = services;
            _rxnManager = rxnManager;
            _updateService = updateService;
            Name = name;
            Route = route;
        }

        public IObservable<UnitTestResult> DoWork(StartUnitTest work)
        {
            _isBusy.OnNext(true);
            $"Preparing to run {(work.RunAllTest ? "All" : work.RunThisTest)}".LogDebug();

            if (!Directory.Exists("logs"))
            {
                Directory.CreateDirectory("logs");
            }

            var file = File.Create($"logs/testLog_{Name.Replace("#", "")}_{++_runId}_{DateTime.Now.ToString("s").Replace(":", "").LogDebug("LOG")}");
            var testLog = new StreamWriter(file, leaveOpen: true);
            var keepTestUpdatedIfRequested = work.UseAppUpdate.ToObservable(); //if not using updates, the dest folder is our root

            var workDll = new FileInfo(work.Dll).Name;
            workDll = $"{work.UseAppUpdate}/{workDll}"; //{work.UseAppVersion}/
            work.Dll = workDll;

            if (!File.Exists(work.Dll) || !work.UseAppUpdate.IsNullOrWhitespace())
            {
                //todo: update need
                keepTestUpdatedIfRequested = _updateService.KeepUpdated(work.UseAppUpdate, work.UseAppVersion,
                    work.UseAppUpdate ?? "Test", new RxnAppCfg()
                    {
                        AppStatusUrl = "http://localhost:888",// work.AppStatusUrl,
                        SystemName = work.UseAppUpdate,
                        KeepUpdated = true
                    }, true);
            }
            else
            {
                work.Dll = FindIfNotExists(work.Dll);
            }

            return keepTestUpdatedIfRequested
                .Select(testPath =>
                {
                    $"Running {(work.RunAllTest ? "All" : work.RunThisTest)}".LogDebug();

                    var logName = $"{Name}{work.RunThisTest.IsNullOrWhiteSpace(new FileInfo(work.Dll).Name)}";

                    //https://github.com/dotnet/sdk/issues/5514
                    var dotnetHack = "c:/program files/dotnet/dotnet.exe";

                    if (!File.Exists(dotnetHack))
                        dotnetHack = "dotnet";

                    //todo: make testrunner injectable/swapable
                    return Rxn.Create
                    (
                        dotnetHack,
                        $"test{FilterIfSingleTestOnly(work)} {Path.Combine(Environment.CurrentDirectory, work.Dll)}",
                        i => testLog.WriteLine(i.LogDebug(logName)),
                        e => testLog.WriteLine(e.LogDebug(logName)
                    ));
                })
                .Switch()
                .LastOrDefaultAsync()
                .Catch<IDisposable, Exception>(e =>
                {
                    $"Failed running test {e}".LogDebug();
                    return Disposable.Empty.ToObservable();
                })
                .Select(_ =>
                {
                    return (UnitTestResult) new UnitTestResult()
                    {
                        WasSuccessful = true
                    }.AsResultOf(work);
                })
                .FinallyR(() =>
                {
                    _isBusy.OnNext(false);
                });
        }

        private string FindIfNotExists(string workDll, string parent = null)
        {
            if (File.Exists(workDll))
                return workDll;

            throw new Exception($"Test Not Found: {workDll}");
        }

        private string FilterIfSingleTestOnly(StartUnitTest work)
        {
            return work.RunThisTest.IsNullOrWhitespace() ? "" : $" --filter Name={work.RunThisTest}";
        }

        public IDisposable DiscoverAndDoWork(string apiName = null, string testHostUrl = null)
        {
            $"Attempting to discover work {apiName ?? "any"}@{testHostUrl ?? "any"}".LogDebug();
            
            var allDiscoveredApiRequests = bfgTestApi.DiscoverWork(_services).Do(apiFound =>
            {
                $"Discovered Api Hosting: {apiFound.Name}@{apiFound.Url}".LogDebug();
                _registry.AppStatusUrl = apiFound.Url;
                _rxnManager.Publish(new PerformAPing()).Until();

                $"Streaming logs".LogDebug();
                _rxnManager.Publish(new StreamLogs(TimeSpan.FromMinutes(60))).Until();

                //from here on, heartbeats will return work for us to do
            });

            return apiName.IsNullOrWhitespace()
                ? allDiscoveredApiRequests.Until()
                : allDiscoveredApiRequests.Where(foundApi => apiName == null || foundApi.Name.BasicallyEquals(apiName)).Until();
        }
    }
}