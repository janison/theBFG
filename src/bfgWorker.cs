using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Rxns;
using Rxns.Cloud.Intelligence;
using Rxns.Collections;
using Rxns.Health;
using Rxns.Hosting;
using Rxns.Hosting.Updates;
using Rxns.Interfaces;
using Rxns.Logging;
using Rxns.Windows;
using theBFG.TestDomainAPI;

namespace theBFG
{
    /// <summary>
    /// 
    /// </summary>
    public class bfgWorker : IClusterWorker<StartUnitTest, UnitTestResult>
    {
        private readonly IAppServiceDiscovery _services;
        private readonly IAppStatusServiceClient _appStatus;
        private readonly IRxnManager<IRxn> _rxnManager;
        private readonly IUpdateServiceClient _updateService;
        private readonly ITestArena _arena;
        private readonly IAppServiceRegistry _registry;
        private int _runId;
        public string Name { get; }
        public string Route { get; }
        public IObservable<bool> IsBusy => _isBusy;
        private readonly ISubject<bool> _isBusy = new BehaviorSubject<bool>(false);
        private IZipService _zipService;

        public bfgWorker(string name, string route, IAppServiceRegistry registry, IAppServiceDiscovery services, IZipService zipService, IAppStatusServiceClient appStatus, IRxnManager<IRxn> rxnManager, IUpdateServiceClient updateService, ITestArena arena)
        {
            _registry = registry;
            _services = services;
            _zipService = zipService;
            _appStatus = appStatus;
            _rxnManager = rxnManager;
            _updateService = updateService;
            _arena = arena;
            Name = name;
            Route = route;
        }

        public IObservable<UnitTestResult> DoWork(StartUnitTest work)
        {
            _isBusy.OnNext(true);
            var logId = $"{Name.Replace("#", "")}_{++_runId}_{DateTime.Now:dd-MM-yy-hhmmssfff}";
            var logDir = $"logs/{logId}";
            $"Preparing to run {(work.RunAllTest ? "All" : work.RunThisTest)}".LogDebug();

            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            var logFile = File.Create($"{logDir}/testArena.log");
            var testLog = new StreamWriter(logFile, leaveOpen: true);
            var keepTestUpdatedIfRequested = work.UseAppUpdate.ToObservable(); //if not using updates, the dest folder is our root

            if (!File.Exists(work.Dll))
            {
                if (work.UseAppUpdate.IsNullOrWhitespace())
                {
                    throw new ArgumentException($"Cannot find target @{work.Dll}");
                }

                var workDll = new FileInfo(work.Dll).Name;
                workDll = $"{work.UseAppUpdate}/{workDll}"; //{work.UseAppVersion}/
                work.Dll = workDll;

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

                    return _arena.Start(Name, work, testLog).SelectMany(_ => _rxnManager.Publish(_));
                })
                .Switch()
                .Catch<Unit, Exception>(e =>
                {
                    $"Failed running test {e}".LogDebug();
                    return Rxn.Empty<Unit>();
                })
                .LastOrDefaultAsync()
                .Delay(TimeSpan.FromSeconds(1))
                .SelectMany(_ =>
                {
                    try
                    {
                        testLog.Dispose();
                        logFile.Dispose();
                    }
                    catch (Exception e)
                    {
                        $"While closing log {e}".LogDebug();
                    }

                    var logsZip = ZipAndTruncate(logDir, $"{logId}_{DateTime.Now:dd-MM-yy-hhmmssfff}");
                    var sendingLogs = File.OpenRead(logsZip);
                    return _appStatus.PublishLog(sendingLogs).Do(_ =>
                    {
                        sendingLogs.Dispose();
                        File.Delete(logsZip);
                    });
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

        private string ZipAndTruncate(string dir, string logId)
        {
            if (dir.IsNullOrWhitespace() || (dir.Length < 3 && dir[0] == '/' || dir[1] == ':'))
            {
                throw new Exception($"Cant achieve {dir}");
            }

            var logFile = $"logs_{logId}.zip";
            using (var file = File.Create(logFile))
                _zipService.Zip(dir).CopyTo(file);

            Directory.Delete(dir, true);

            return logFile;
        }

        private string FindIfNotExists(string workDll, string parent = null)
        {
            if (File.Exists(workDll))
                return workDll;

            throw new Exception($"Test Not Found: {workDll}");
        }

        public IDisposable DiscoverAndDoWork(string apiName = null, string testHostUrl = null)
        {
            $"Attempting to discover work {apiName ?? "any"}@{testHostUrl ?? "any"}".LogDebug();
            
            var allDiscoveredApiRequests = bfgTestApi.DiscoverWork(_services).Do(apiFound =>

            {
                $"Discovered Api Hosting: {apiFound.Name}@{apiFound.Url}".LogDebug();
                _registry.AppStatusUrl = apiFound.Url;

                TimeSpan.FromSeconds(1).Then().Do(_ =>
                {
                    _rxnManager.Publish(new PerformAPing()).Until();

                });

                //from here on, heartbeats will return work for us to do
            });

            return apiName.IsNullOrWhitespace()
                ? allDiscoveredApiRequests.Until()
                : allDiscoveredApiRequests.Where(foundApi => apiName == null || foundApi.Name.BasicallyEquals(apiName)).Until();
        }
    }
}