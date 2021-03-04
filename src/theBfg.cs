using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Autofac;
using Rxns;
using Rxns.Cloud;
using Rxns.Cloud.Intelligence;
using Rxns.Commanding;
using Rxns.DDD.Commanding;
using Rxns.Health;
using Rxns.Hosting;
using Rxns.Hosting.Updates;
using Rxns.Interfaces;
using Rxns.Logging;
using Rxns.Metrics;
using Rxns.NewtonsoftJson;
using Rxns.WebApiNET5;
using Rxns.WebApiNET5.NET5WebApiAdapters;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public class theBfg : IContainerPostBuildService, IDisposable
    {
        public static ISubject<Unit> IsCompleted = new ReplaySubject<Unit>(1);
        public IDisposable TestRunner { get; set; }
        public static string[] Args = new string[0];
        
        private static int _workerCount = 0;
        
        public static IObservable<Unit> ReloadWithTestWorker(string url = "http://localhost:888/", params string[] args)
        {
            return Rxn.Create<Unit>(o =>
            {
                var apiName = args.Skip(1).FirstOrDefault();
                var testHostUrl = args.Skip(2).FirstOrDefault().IsNullOrWhiteSpace("http://localhost:888");
                theBFGDef.Cfg = DetectIfTargetMode(url, args);

                RxnExtensions.DeserialiseImpl = (t, json) => JsonExtensions.FromJson(json, t);
                RxnExtensions.SerialiseImpl = (json) => JsonExtensions.ToJson(json);

                var cfg = RxnAppCfg.Detect(args);
                //var appStore = new CurrentDirectoryAppUpdateStore();
                //var clusterHost = OutOfProcessFactory.CreateClusterHost(args, appStore, cfg);

                return theBFGDef.TestWorker(apiName, testHostUrl, theBFGDef.Cfg, RxnApp.SpareReator(testHostUrl)).ToRxns()
                    .Named(new ClusteredAppInfo("bfgWorker", "1.0.0", args, false))
                    .OnHost(new ConsoleHostedApp(), cfg)
                    .SelectMany(h => h.Run())
                    .Select(app => new Unit())
                    .Subscribe(o);
            });
        }

        public static StartUnitTest DetectIfTargetMode(string url, string[] args)
        {
            var cfg = RxnAppCfg.Detect(args);
            var testCfg = theBigCfg.Detect();

            var dll = args.Skip(1).FirstOrDefault().IsNullOrWhiteSpace(testCfg.Dll);
            var fire = args.Reverse().Skip(1).FirstOrDefault().IsNullOrWhiteSpace(testCfg.Dll);
            if(fire != "fire")
                fire = args.Reverse().Skip(2).FirstOrDefault().IsNullOrWhiteSpace(testCfg.Dll);

            var appUpdateDllSource = dll == null ? null : dll.Contains("@") ? dll.Split('@').Reverse().FirstOrDefault().IsNullOrWhiteSpace(testCfg.RunThisTest) : null;
            var testName = string.Empty;// args.Skip(3).FirstOrDefault().IsNullOrWhiteSpace(testCfg.UseAppUpdate);
            var appUpdateVersion = args.Skip(4).FirstOrDefault().IsNullOrWhiteSpace(testCfg.UseAppVersion);
            url = args.Skip(4).FirstOrDefault().IsNullOrWhiteSpace(url).IsNullOrWhiteSpace(testCfg.AppStatusUrl)
                .IsNullOrWhiteSpace(cfg.AppStatusUrl);

            if (fire.BasicallyEquals("fire"))
                return new StartUnitTest()
                {
                    UseAppUpdate = appUpdateDllSource,
                    UseAppVersion = appUpdateVersion,
                    Dll = dll,
                    RunThisTest = testName,
                };

            return null;
        }

        public static IObservable<Unit> ReloadAnd(string url = "http://localhost:888/", params string[] args)
        {
            Args = args;
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
                    case null:

                        "theBFG instructions:".LogDebug();
                        "1. Take aim at a target".LogDebug();
                        "2. Fire".LogDebug();
                        "".LogDebug();
                        "".LogDebug();
                        "Usage:".LogDebug();
                        "target sut@sut.dll".LogDebug();
                        "target sut@sut.dll and fire".LogDebug();
                        "fire".LogDebug();
                        "fire @sut".LogDebug();
                        "fire @url".LogDebug();
                        "fire rapid {{threadCount | max}} | will fire on multiple threads simultatiously".LogDebug();
                        "fire coop | shard test-suite execution across multiple nodes".LogDebug();
                        "".LogDebug();
                        "launch sut@sut.dll | deploy apps to worker nodes automatically during CI/CD. Worker integration via complementary C# api: theBfgApi.launch(\"app\", \"dir\")".LogDebug();
                        "".LogDebug();
                        "".LogDebug();
                        "<<USE WITH CAUTION>>".LogDebug();
                        "".LogDebug();
                        break;
                }

                return Disposable.Empty;
            });
        }

        public static IObservable<Unit> ReloadWithTestServer(string url = "http://localhost:888/", params string[] args)
        {
            return Rxn.Create<Unit>(o =>
            {
                ReportStatus.StartupLogger = ReportStatus.Log.ReportToConsole();

                "Configuring App".LogDebug();
                
                
                theBFGAspnetCoreAdapter.Appcfg = RxnAppCfg.Detect(args);
                
                return AspNetCoreWebApiAdapter.StartWebServices<theBFGAspnetCoreAdapter>(theBFGAspnetCoreAdapter.Cfg, args).ToObservable()
                    .LastAsync()
                    .Select(_ => new Unit())
                    .Subscribe(o);
            });
        }

        public theBfg()
        {
        }

        public void Run(IReportStatus logger, IResolveTypes container)
        {
            logger.OnInformation("Starting unit TestArea");

            Start(container);
            //todo: need to fix ordering of services, this needs to start before the appstatusservicce otherwise it will miss the infoprovdiderevent
            container.Resolve<SystemStatusPublisher>().Process(new AppStatusInfoProviderEvent()
            {
                Info = _info
            }).Until();
        }

        private Func<AppStatusInfo[]> _info = () => new AppStatusInfo[0];
        private DateTime _started;
        private bfgCluster _testCluster;
        private Func<IEnumerable<IMonitorAction<IRxn>>> _before;

        private void Start(IResolveTypes resolver)
        {
            var args = Args;
            var unitTestToRun = theBFGDef.Cfg;
            _started = DateTime.Now;
            "Starting up TestServer".LogDebug();

            //todo fix all the below hacks and put into proper organised classes
            //move fire etc methods to testdomainapi
            //implement real cluster mode?

            _testCluster = resolver.Resolve<bfgCluster>();

            var reactorMgr = resolver.Resolve<IManageReactors>();
            var bfgReactor = reactorMgr.GetOrCreate("bfg").Reactor;
            //need to fix health which will allow this to be viewed on the appstatus portal. should monitor health of fanout stratergy
            //
            //IMonitorActionFactory<IRxn> health =MonitorHealth
            RxnCreator.MonitorHealth<IRxn>(bfgReactor, "theBFG", out _before, () => Rxn.Empty()).SelectMany(bfgReactor.Output).Until();

            var rxnManage = resolver.Resolve<IRxnManager<IRxn>>();
            Action<IRxn> _publish = e => rxnManage.Publish(e).Until();


            var notUpdating = true;

            var iteration = 0;
            var workerId = 0;
            var startedAt = Stopwatch.StartNew();
            Action fire = () =>
            {
                "Test".LogDebug(++iteration);
                startedAt.Restart();

                _testCluster.Queue(unitTestToRun);
            };

            Action<Action> doWorkContiniously = (fireStyle) =>
            {
                var stopDOingWor1k = rxnManage
                    .CreateSubscription<CommandResult>()
                    .Where(c => c.InResponseTo.Equals(unitTestToRun.Id))
                    .Select(_ => --iteration)
                    .If(e => e <= 0,
                        _ =>
                        {
                            if (notUpdating)
                            {
                                CurrentThreadScheduler.Instance.Run(() =>
                                {
                                    fireStyle();
                                });
                            }
                        }).Until();


                fireStyle();
            };

            _testCluster.ConfigiurePublishFunc(e => _publish(e));

            BroadcasteStatsToAppStatus(_testCluster, unitTestToRun);

            var rxnManager = resolver.Resolve<IRxnManager<IRxn>>();

            $"Heartbeating".LogDebug();
            rxnManager.Publish(new PerformAPing()).Until();

            var stopAdvertising = bfgTestApi.AdvertiseForWorkers(resolver.Resolve<SsdpDiscoveryService>(), "all", $"http://{RxnApp.GetIpAddress()}:888");
            
            Action<Action> watchForCompletion = (onComplete) =>
            {
                var stopDOingWork = rxnManage
                    .CreateSubscription<CommandResult>()
                    .Where(_ => unitTestToRun != null)
                    .Where(c => c.InResponseTo.Equals(unitTestToRun.Id))
                    .Do(
                        _ =>
                        {
                            $"Duration: {startedAt.Elapsed}".LogDebug();

                            if (iteration <= 0)
                                onComplete?.Invoke();
                        }).Until();
            };

            Action spawnWorker = () =>
            {
                "Spawning worker".LogDebug(++workerId);

                var worker = theBFGDef.SpawnTestaWorker(resolver);
                _testCluster.Process(new WorkerDiscovered<StartUnitTest, UnitTestResult>() { Worker = worker }).Do(e => _publish(e)).Until();
            };

            Action fireRapidly = () =>
            {
                Enumerable.Range(0, Environment.ProcessorCount).ToObservable().ObserveOn(NewThreadScheduler.Default).Do(_ =>
                {
                    fire();
                }).Until();
            };

            Action startRapidWorkers = () =>
            {
                Enumerable.Range(0, Environment.ProcessorCount).ToObservable().Do(_ =>
                {
                    spawnWorker();
                }).Until();
            };

            Action watchUpdating = () => rxnManage
                .CreateSubscription<UpdateSystemCommand>()
                .Do(
                    _ =>
                    {
                        notUpdating = false;
                    }).Until();

            if (args.Contains("rapidly"))
            {
                startRapidWorkers();
            }
            else if (args.Contains("fire"))
            {
                spawnWorker();
            }

            if (args.Contains("continuously"))
            {
                watchUpdating();
                watchForCompletion(null);
                
                if (args.Contains("rapidly"))
                {
                    doWorkContiniously(fireRapidly);
                }
                else
                {
                    doWorkContiniously(fire);
                }

            }
            else
            {
                if (args.Contains("rapidly"))
                {
                    fireRapidly();
                }
                else
                {
                    fire();
                }

                watchForCompletion(() =>
                {
                    theBfg.IsCompleted.OnNext(new Unit());
                    theBfg.IsCompleted.OnCompleted();
                });
            }

            //need to active webapi inside of this testserver
            //need to test the appstatus worker tunnel
            //need to push rxns webapi to nuget
            //need to allow the webapi to startup in isolation or with config options to turn off rxns services, allow appstatus portal to be overriden?
        }

        private void BroadcasteStatsToAppStatus(bfgCluster testCluster, StartUnitTest unitTestToRun)
        {
            _info = () =>
            {
                if (unitTestToRun == null)
                    return new[]
                    {
                        new AppStatusInfo("Test", $"Waiting for work"),
                        new AppStatusInfo("Duration", (DateTime.Now - _started).TotalMilliseconds),
                        new AppStatusInfo("Workers", testCluster.Workflow.Workers.Count)
                    };

                return new[]
                {
                    new AppStatusInfo("Test",
                        $"Running {(unitTestToRun.RunAllTest ? "All" : unitTestToRun.RunThisTest)}"),
                    new AppStatusInfo("Duration", (DateTime.Now - _started).TotalMilliseconds),
                    new AppStatusInfo("Workers", testCluster.Workflow.Workers.Count)
                };
            };

            _info();
        }

        public void Dispose()
        {
            TestRunner?.Dispose();
        }
    }

    
}