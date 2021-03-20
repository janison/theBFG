using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using Rxns.Collections;
using Rxns.Commanding;
using Rxns.DDD.Commanding;
using Rxns.DDD.CQRS;
using Rxns.Health;
using Rxns.Hosting;
using Rxns.Hosting.Updates;
using Rxns.Interfaces;
using Rxns.Logging;
using Rxns.NewtonsoftJson;
using Rxns.WebApiNET5.NET5WebApiAdapters;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public class Reload : ServiceCommand
    {

    }

    public class theBfg : IDisposable, IServiceCommandHandler<Reload>
    {
        public static ISubject<Unit> IsCompleted = new ReplaySubject<Unit>(1);
        public IDisposable TestRunner { get; set; }
        public static string[] Args = new string[0];

        private Func<AppStatusInfo[]> _info = () => new AppStatusInfo[0];
        private DateTime _started;
        private bfgCluster _testCluster;
        bool _notUpdating = true;

        public static IObservable<Unit> ReloadWithTestWorker(string url = "http://localhost:888/", params string[] args)
        {
            return Rxn.Create<Unit>(o =>
            {
                var apiName = args.Skip(1).FirstOrDefault();
                var testHostUrl = args.Skip(2).FirstOrDefault().IsNullOrWhiteSpace("http://localhost:888");
                theBFGDef.Cfg = DetectIfTargetMode(url, args).WaitR();
                theBfg.Args = args;


                RxnExtensions.DeserialiseImpl = (t, json) => JsonExtensions.FromJson(json, t);
                RxnExtensions.SerialiseImpl = (json) => JsonExtensions.ToJson(json);

                var cfg = RxnAppCfg.Detect(args);
                //var appStore = new CurrentDirectoryAppUpdateStore();
                //var clusterHost = OutOfProcessFactory.CreateClusterHost(args, appStore, cfg);

                return theBFGDef.TestWorker(apiName, testHostUrl, RxnApp.SpareReator(testHostUrl)).ToRxns()
                    .Named(new ClusteredAppInfo("bfgWorker", "1.0.0", args, true))
                    .OnHost(new ConsoleHostedApp(), cfg)
                    .SelectMany(h => h.Run())
                    .Select(app => new Unit())
                    .Subscribe(o);
            });
        }

        public static IObservable<StartUnitTest[]> DetectIfTargetMode(string url, string[] args)
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
            url = args.Skip(4).FirstOrDefault().IsNullOrWhiteSpace(url).IsNullOrWhiteSpace("http://localhost:888")//testCfg.AppStatusUrl)
                .IsNullOrWhiteSpace(cfg.AppStatusUrl);

            List<StartUnitTest> tests = new List<StartUnitTest>();

            foreach(var target in GetTargets(dll))
                tests.Add( new StartUnitTest()
                {
                    UseAppUpdate = appUpdateDllSource ?? "Test",
                    UseAppVersion = appUpdateVersion,
                    Dll = target,
                    RunThisTest = testName,
                });


            var mode = new DotNetTestArena();

            if (args.Contains("compete"))
            {
                
                var userSize = args.Last();
                int batchSize = 0;
                Int32.TryParse(userSize, out batchSize);

                batchSize = batchSize == 0 ? 25 : batchSize;

                $"Reloading with {batchSize} tests per/batch".LogDebug();

                return tests.ToObservableSequence().SelectMany(t =>  
                     mode.ListTests(t).SelectMany(s => s)
                    .Buffer(batchSize)
                    .Select(tests => new StartUnitTest()
                {
                    UseAppUpdate = appUpdateDllSource ?? "Test",
                    UseAppVersion = appUpdateVersion,
                    Dll = dll,
                    RunThisTest = tests.ToStringEach()
                })
                .ToArray());
            }
            else
            {
                return Observable.Return(tests.ToArray());
            }
        }

        private static IEnumerable<string> GetTargets(string dll)
        {
            if (!dll.Contains("*")) yield return dll;

            var pattern = dll.Split('/', '\\');

            foreach (var file in Directory.GetFileSystemEntries(pattern.Take(pattern.Length - 1).ToStringEach("/"), pattern.Last()))
                yield return file;
        }

        public static IObservable<Unit> ReloadAnd(string url = "http://localhost:888/", params string[] args)
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
                        return ReloadWithTestArena(url, args).Subscribe(o);
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

        public static IObservable<Unit> ReloadWithTestArena(string url = "http://localhost:888/", params string[] args)
        {
            return Rxn.Create<Unit>(o =>
            {
                ReportStatus.StartupLogger = ReportStatus.Log.ReportToConsole();
                theBFGDef.Cfg = DetectIfTargetMode(url, args).WaitR();
                theBfg.Args = args;

                "Configuring App".LogDebug();
                
                
                theBFGAspNetCoreAdapter.Appcfg = RxnAppCfg.Detect(args);
                
                return AspNetCoreWebApiAdapter.StartWebServices<theBFGAspNetCoreAdapter>(theBFGAspNetCoreAdapter.Cfg, args).ToObservable()
                    .LastAsync()
                    .Select(_ => new Unit())
                    .Subscribe(o);
            });
        }

        public theBfg()
        {
        }

        public void Fire()
        {
            startedAt = Stopwatch.StartNew();
          
            "Test".LogDebug(++iteration);
            startedAt.Restart();

            foreach (var test in theBFGDef.Cfg)
            {
                _testCluster.Publish(test);
            }
        }

        public IDisposable DoWorkContiniously(IRxnManager<IRxn> rxnManage, StartUnitTest[] unitTestToRun,  Action fireStyle)
        {
            
            var stopDOingWor1k = rxnManage.CreateSubscription<CommandResult>()
                .Where(c => c.InResponseTo.Equals(unitTestToRun[0].Id))
                .Select(_ => --iteration)
                .If(e => e <= 0, _ =>
                {
                    if (_notUpdating)
                    {
                        CurrentThreadScheduler.Instance.Run(() => { fireStyle(); });
                    }
                })
                .Until();
            
            fireStyle();

            return stopDOingWor1k;
        }

        public IDisposable WatchForCompletion(IRxnManager<IRxn> rxnManage, StartUnitTest[] unitTestToRun, Action onComplete)
        {
            //stopAdvertising?.Dispose();

            var stopDOingWork = rxnManage.CreateSubscription<CommandResult>()
                .Where(_ => unitTestToRun != null)
                .Where(c => c.InResponseTo.Equals(unitTestToRun[0].Id)) //this code is wrong, need to fix, could be a response to any msg
                .Do(_ =>
                {
                    $"Duration: {startedAt.Elapsed}".LogDebug();

                    if (iteration <= 0) onComplete?.Invoke();
                })
                .Until();

            return stopDOingWork;
        }

        public void FireRapidly()
        {
            Enumerable.Range(0, Environment.ProcessorCount).ToObservable().ObserveOn(NewThreadScheduler.Default).Do(_ => { Fire(); }).Until();
        }

        public IObservable<bfgWorker> StartRapidWorkers(IResolveTypes resolver, StartUnitTest[] unitTestToRun)
        {
            return Enumerable.Range(0, Environment.ProcessorCount).ToObservable().Select(_ => SpawnTestWorker(resolver, unitTestToRun));
        }

        public void WatchForTestUpdates(IRxnManager<IRxn> rxnManage)
        {
            rxnManage.CreateSubscription<UpdateSystemCommand>()
                .Do(_ => { _notUpdating = false; })
                .Until(); }


        int iteration = 0;
        private Stopwatch startedAt = new Stopwatch();


        public IDisposable StartTestArena(string[] args, StartUnitTest[] unitTests, IResolveTypes resolver)
        {
            "Starting up TestArena".LogDebug();

            _started = DateTime.Now;

            _testCluster = resolver.Resolve<bfgCluster>();
            BroadcasteStatsToTestArena(_testCluster, unitTests[0], resolver.Resolve<SystemStatusPublisher>(), resolver.Resolve<IManageReactors>());

            LaunchUnitTestsToTestArenaDelayed(unitTests);
            var stopAdvertising = bfgTestApi.AdvertiseForWorkers(resolver.Resolve<SsdpDiscoveryService>(), "all", $"http://{RxnApp.GetIpAddress()}:888");
            var stopFiring = StartFiringWorkflow(args, unitTests,  resolver.Resolve<IRxnManager<IRxn>>());
            
            return new CompositeDisposable(stopAdvertising, stopFiring);
        }

        public IObservable<bfgWorker> StartTestArenaWorkers(string[] args, StartUnitTest[] unitTestToRun, IResolveTypes resolver)
        {
            _testCluster = resolver.Resolve<bfgCluster>();

            if (args.Contains("rapidly"))
            {
                return StartRapidWorkers(resolver, unitTestToRun);
            }
            
            if (args.Contains("fire"))
            {
                return SpawnTestWorker(resolver, unitTestToRun).ToObservable();
            }

            return Rxn.Empty<bfgWorker>();
        }


        public void LaunchUnitTestsToTestArenaDelayed(StartUnitTest[] unitTestToRun)
        {
            TimeSpan.FromSeconds(2).Then().Do(_ =>
            {
                RxnApps.CreateAppUpdate(unitTestToRun[0].UseAppUpdate, Scrub(unitTestToRun[0].UseAppVersion),
                        new FileInfo(unitTestToRun[0].Dll).DirectoryName, true, "http://localhost:888")
                    .Catch<Unit, Exception>(
                        e =>
                        {
                            ReportStatus.Log.OnError("Could not download test. Cant join cluster :(", e);
                            throw e;
                        }).WaitR();
            }).Until();
        }

        /// <summary>
        /// Implements the firing sematics of the bfg, reponding to rapidly condintiously etc
        
        /// </summary>
        /// <param name="args"></param>
        /// <param name="unitTestToRun"></param>
        /// <param name="resolver"></param>
        /// <param name="rxnManager"></param>
        /// <returns></returns>
        public IDisposable StartFiringWorkflow(string[] args, StartUnitTest[] unitTestToRun,  IRxnManager<IRxn> rxnManager)
        {

            if (args.Contains("continuously"))
            {
                WatchForTestUpdates(rxnManager);

                if (args.Contains("rapidly"))
                {
                    DoWorkContiniously(rxnManager, unitTestToRun, (Action)FireRapidly);
                }
                else
                {
                    DoWorkContiniously(rxnManager, unitTestToRun, Fire);
                }
            }
            else
            {
                if (args.Contains("rapidly"))
                {
                    FireRapidly();
                }
                else
                {
                    Fire();
                }
            }

            return WatchForCompletion(rxnManager, unitTestToRun, () =>
            {
                theBfg.IsCompleted.OnNext(new Unit());
                theBfg.IsCompleted.OnCompleted();
            });
        }

        private string Scrub(string useAppVersion)
        {
            return new[] {"rapidly", "continuously", "fire"}.FirstOrDefault(i => i == useAppVersion) == null
                ? useAppVersion
                : null;
        }

        private static int workerId;
        private static int _workerCount;
        private Func<IEnumerable<IMonitorAction<IRxn>>> _before;

        public static bfgWorker SpawnTestWorker(IResolveTypes resolver, StartUnitTest[] Cfg)
        {
            "Spawning worker".LogDebug(++workerId);

            var testCluster = resolver.Resolve<bfgCluster>();
            var rxnManager = resolver.Resolve<IRxnManager<IRxn>>();

            //$"Streaming logs".LogDebug();
            //rxnManager.Publish(new StreamLogs(TimeSpan.FromMinutes(60))).Until();

            $"Starting worker".LogDebug();
            Interlocked.Increment(ref _workerCount);

            var testWorker = new bfgWorker($"TestWorker#{_workerCount}", "local", resolver.Resolve<IAppServiceRegistry>(), resolver.Resolve<IAppServiceDiscovery>(), resolver.Resolve<IZipService>(), resolver.Resolve<IAppStatusServiceClient>(), resolver.Resolve<IRxnManager<IRxn>>(), resolver.Resolve<IUpdateServiceClient>(), resolver.Resolve<ITestArena>());

            if (!Cfg.AnyItems() || Cfg[0].AppStatusUrl.IsNullOrWhitespace() && !Directory.Exists(Cfg[0].UseAppVersion))
                testWorker.DiscoverAndDoWork();

            testCluster.Process(new WorkerDiscovered<StartUnitTest, UnitTestResult>() { Worker = testWorker }).SelectMany(e => rxnManager.Publish(e)).Until();

            return testWorker;
        }

        private void BroadcasteStatsToTestArena(bfgCluster testCluster, StartUnitTest unitTestToRun, SystemStatusPublisher publusher, IManageReactors reactorMgr)
        {
            var bfgReactor = reactorMgr.GetOrCreate("bfg").Reactor;
            //need to fix health which will allow this to be viewed on the appstatus portal. should monitor health of fanout stratergy
            //
            //IMonitorActionFactory<IRxn> health =MonitorHealth
            RxnCreator.MonitorHealth<IRxn>(bfgReactor, "theBFG", out _before, () => Rxn.Empty()).SelectMany(bfgReactor.Output).Until();

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
                    new AppStatusInfo("Test", $"Running {(unitTestToRun.RunAllTest ? "All" : unitTestToRun.RunThisTest)}"),
                    new AppStatusInfo("Duration", (DateTime.Now - _started).TotalMilliseconds),
                    new AppStatusInfo("Workers", testCluster.Workflow.Workers.Count)
                };
            };

            //todo: need to fix ordering of services, this needs to start before the appstatusservicce otherwise it will miss the infoprovdiderevent
            publusher.Process(new AppStatusInfoProviderEvent()
            {
                Info = _info
            }).Until();

            $"Heartbeating".LogDebug();
            bfgReactor.Output.Publish(new PerformAPing()).Until();
        }

        public void Dispose()
        {
            TestRunner?.Dispose();
        }

        public IObservable<CommandResult> Handle(Reload command)
        {
            return Rxn.Create(() =>
            {
                "Reloading".LogDebug();
                Fire();

                return CommandResult.Success().AsResultOf(command);
            });
        }
    }

    
}