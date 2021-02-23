using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Autofac;
using RxnCreate;
using Rxns;
using Rxns.Cloud;
using Rxns.DDD.CQRS;
using Rxns.Health;
using Rxns.Hosting;
using Rxns.Hosting.Updates;
using Rxns.Interfaces;
using Rxns.Logging;
using Rxns.NewtonsoftJson;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public class theBfg : IContainerPostBuildService, IDisposable
    {
        private readonly IUpdateServiceClient _testUpdateProvider;
        private static StartUnitTest testcfg;
        public IDisposable TestRunner { get; set; }

        public static Func<StartUnitTest, Action<IRxnLifecycle>, Action<IRxnLifecycle>> TestServer = (cfg, d) =>
        {
            theBfg.testcfg = cfg;
            return dd =>
            {
                d(dd);
                dd.CreatesOncePerApp<theBfg>();
                dd.CreatesOncePerApp<TestWorkerDiscoveryService>();
                dd.CreatesOncePerApp(_ => new DynamicStartupTask((log, resolver) =>
                {
                    "Starting up TestServer".LogDebug();
                    var rxnManager = resolver.Resolve<IRxnManager<IRxn>>();
                    
                    $"Heartbeating".LogDebug();
                    rxnManager.Publish(new PerformAPing()).Until();
                    
                    //need to active webapi inside of this testserver
                    //need to test the appstatus worker tunnel
                    //need to push rxns webapi to nuget
                    //need to allow the webapi to startup in isolation or with config options to turn off rxns services, allow appstatus portal to be overriden?
                }));
            };
        };
        
        public static Func<string, string, Action<IRxnLifecycle>, Action<IRxnLifecycle>> TestWorker = (apiName, testHostUrl, d) =>
        {
            IUpdateServiceClient testUpdateProvider;
            //todo: fix clustering mode
            return dd =>
            {
                d(dd);
                dd.CreatesOncePerApp<SsdpDiscoveryService>();
                dd.CreatesOncePerApp(_ => new DynamicStartupTask((log, resolver) =>
                {
                    $"Starting worker".LogDebug();
                    var testWorker = new bfgWorker("TestWorker#1", "local",resolver.Resolve<IAppServiceRegistry>(),  resolver.Resolve<IAppServiceDiscovery>(), resolver.Resolve<IRxnManager<IRxn>>(), resolver.Resolve<IUpdateServiceClient>());
                    testWorker.DiscoverAndDoWork();
                }));
            };
        };
        
        public static IObservable<Unit> ReloadWithTestWorker(string url = "http://192.168.1.2:888/", params string[] args)
        {
            return Rxn.Create<Unit>(o =>
            {
                var apiName = args.Skip(1).FirstOrDefault(); 
                var testHostUrl = args.Skip(2).FirstOrDefault().IsNullOrWhiteSpace("http://localhost:869");

                return theBfg.TestWorker(apiName, testHostUrl, RxnApp.SpareReator(testHostUrl))   .ToRxns()
                    .Named(new ClusteredAppInfo("bfgWorker", "1.0.0", args, false))
                    .OnHost(new ConsoleHostedApp(), RxnAppCfg.Detect(args))
                    .SelectMany(h => h.Run())
                    .Select(app => new Unit())
                    .Subscribe(o);
            });
        }

        public static IObservable<Unit> ReloadAnd(string url = "http://192.168.1.2:888/", params string[] args)
        {
            return Rxn.Create<Unit>(o =>
            {
                RxnExtensions.DeserialiseImpl = (t, json) => JsonExtensions.FromJson(json, t);
                RxnExtensions.SerialiseImpl = (json) => JsonExtensions.ToJson(json);

                switch (args.FirstOrDefault()?.ToLower())
                {
                    case "fire":
                        return ReloadWithTestWorker(url, args).Subscribe(o);
                        break;
                    case "target":
                        return ReloadWithTestServer(url, args).Subscribe(o);
                        break;
                    case null :

                        ReportStatus.Log.OnInformation("theBFG instructions:");
                        ReportStatus.Log.OnInformation("1. Take aim at a target");
                        ReportStatus.Log.OnInformation("2. Fire");
                        ReportStatus.Log.OnInformation("");
                        ReportStatus.Log.OnInformation("");
                        ReportStatus.Log.OnInformation("Usage:");
                        ReportStatus.Log.OnInformation("target sut@sut.dll");
                        ReportStatus.Log.OnInformation("target sut@sut.dll and fire");
                        ReportStatus.Log.OnInformation("fire");
                        ReportStatus.Log.OnInformation("fire @sut");
                        ReportStatus.Log.OnInformation("fire @url");
                        ReportStatus.Log.OnInformation("fire rapid {threadCount | max} | will fire on multiple threads simultatiously");
                        ReportStatus.Log.OnInformation("fire coop | shard test-suite execution across multiple nodes");
                        ReportStatus.Log.OnInformation("");
                        ReportStatus.Log.OnInformation("launch sut@sut.dll | deploy auto-updated apps to worker nodes on demand via complementary C# api: theBfgApi.launch(\"app\", \"dir\")");
                        ReportStatus.Log.OnInformation("");
                        ReportStatus.Log.OnInformation("");
                        ReportStatus.Log.OnInformation("<<USE WITH CAUTION>>");
                        ReportStatus.Log.OnInformation("");
                        break;
                }
                
                return Disposable.Empty;
            });
        }

        public static IObservable<Unit> ReloadWithTestServer(string url = "http://192.168.1.2:888/", params string[] args)
        {
            return Rxn.Create<Unit>(o =>
            {
                var cfg = RxnAppCfg.Detect(args);
                var testCfg = theBigCfg.Detect();

                var dll = args.Skip(1).FirstOrDefault().IsNullOrWhiteSpace(testCfg.Dll);
                var testName = args.Skip(2).FirstOrDefault().IsNullOrWhiteSpace(testCfg.RunThisTest);
                var appUpdateDllSource = args.Skip(3).FirstOrDefault().IsNullOrWhiteSpace(testCfg.UseAppUpdate);
                var appUpdateVersion = args.Skip(4).FirstOrDefault().IsNullOrWhiteSpace(testCfg.UseAppVersion);
                url = args.Skip(4).FirstOrDefault().IsNullOrWhiteSpace(url).IsNullOrWhiteSpace(testCfg.AppStatusUrl)
                    .IsNullOrWhiteSpace(cfg.AppStatusUrl);

                return theBfg.TestServer(new StartUnitTest()
                    {
                        UseAppUpdate = appUpdateDllSource,
                        UseAppVersion = appUpdateVersion,
                        Dll = dll,
                        RunThisTest = testName
                    }, RxnApp.SpareReator(url))
                    .ToRxns()
                    .Named(new ClusteredAppInfo("DotNetTestWorker", "1.0.0", args, false))
                    .OnHost(new ConsoleHostedApp(), cfg)
                    .SelectMany(h => h.Run())
                    .Select(app => new Unit())
                    .Subscribe(o);
            });
        }

        public theBfg(IUpdateServiceClient testUpdateProvider)
        {
            _testUpdateProvider = testUpdateProvider;
        }

        public void Run(IReportStatus logger, IResolveTypes container)
        {
            logger.OnInformation("Starting unit test agent");

            Start();
            //todo: need to fix ordering of services, this needs to start before the appstatusservicce otherwise it will miss the infoprovdiderevent
            container.Resolve<SystemStatusPublisher>().Process(new AppStatusInfoProviderEvent()
            {
                Info = _info
            }).Until();
        }

        private Func<AppStatusInfo[]> _info = () => new AppStatusInfo[0];
        private DateTime _started;
        private bfgCluster _testCluster;

        private void Start()
        {
            var unitTestToRun = testcfg;
            _started = DateTime.Now;

            _testCluster = new bfgCluster();
            BoardcastStatsToAppStatus(_testCluster, unitTestToRun);
        }

        private void BoardcastStatsToAppStatus(bfgCluster testCluster, StartUnitTest unitTestToRun)
        {
            _info = () => new[]
            {
                new AppStatusInfo("Test", $"Running {(unitTestToRun.RunAllTest ? "All" : unitTestToRun.RunThisTest)}"),
                new AppStatusInfo("Duration", (DateTime.Now - _started).TotalMilliseconds),
                new AppStatusInfo("Workers", testCluster.Workflow.Workers.Count)
            };

            _info();
        }

        public void Dispose()
        {
            TestRunner.Dispose();
        }
    }
}