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
using Rxns.Windows;
using theBFG.TestDomainAPI;

namespace theBFG
{
    /// <summary>
    /// Reloads and fires at the last test session started
    /// </summary>
    public class Reload : ServiceCommand
    {

    }

    /// <summary>
    /// Unlike the Gatling Gun, the Bfg was always the top weapon in every game. It was accurate, responsive, rewarding to master and had your back in every situation.
    ///
    /// I wanted to create a C# testing tool that just brought the C# ecosystem out of the stone ages. Somethign that just worked. Something that would compliment the dev-test flow
    /// and compensate for the lack of creativety that my IDE vendor delivered. Something that let me leverage my unit test toolbox, yet execute advanced distributed test scenarios.
    /// Something that helped me iterate quickly and tell me exactly what went wrong and why.
    ///
    /// The main thing your have to know is:
    ///     - That theBFG is a gun.
    ///     - You target tests.dlls with are spwaned inside a test arena
    ///     - You fire to run the tests. You reload after every shot.
    ///     - You monitor everything in real-time via TestArena Web Portal.
    /// 
    /// This API gives you the keys to the castle. Use with caution. Eye protection recommended when using rapid fire mode for extended periods.
    /// </summary>
    public class theBfg : IDisposable, IServiceCommandHandler<Reload>
    {
        public static ISubject<Unit> IsCompleted = new ReplaySubject<Unit>(1);
        public IDisposable TestRunner { get; set; }
        public static string[] Args = new string[0];

        private Func<AppStatusInfo[]> _info = () => new AppStatusInfo[0];
        private DateTime? _started;
        private bfgCluster _testCluster;
        bool _notUpdating = true;

        public static IObservable<Unit> ReloadWithTestWorker(string url = "http://localhost:888/", params string[] args)
        {
            return Rxn.Create<Unit>(o =>
            {
                var apiName = args.Skip(1).FirstOrDefault();
                var testHostUrl = args.Skip(2).FirstOrDefault().IsNullOrWhiteSpace("http://localhost:888");
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

        /// <summary>
        /// Tnis function will parse an arg list for a set of firing targets.
        /// Depending on the firing mode, the function will return a series of
        /// startunittest sessions that represent the target as it grows over time.
        /// </summary>
        /// <param name="args">The startup commands for the bfg</param>
        /// <param name="arena">When compete mode is detected, the area will be used to list tests to fire on</param>
        /// <returns></returns>
        public static IObservable<StartUnitTest[]> DetectAndWatchTargets(string[] args, ITestArena arena)
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
            

            //need to modify GetTagets to watch for changes and potentially return a stream?
            // it can then augment that with compete to create a CI/CD on compile?
            //var file = new FileInfo(work.Dll);
            //var watchForCompiles = Rxn.Create<string>(o => Files.WatchForChanges(file.DirectoryName, file.Name, () => o.OnNext(work.Dll))).StartWith(work.Dll);
            // GetTargets(dll).Do(_ => tests.Add(_))

            //todo: fix, need to get from container
            
            return GetTargets(dll).SelectMany(target =>
            {
                var test = new StartUnitTest()
                {
                    UseAppUpdate = appUpdateDllSource ?? "Test",
                    UseAppVersion = appUpdateVersion,
                    Dll = target,
                    RunThisTest = testName,
                };

                if (args.Contains("compete"))
                {

                    var userSize = args.Last();
                    int batchSize = 0;
                    Int32.TryParse(userSize, out batchSize);

                    batchSize = batchSize == 0 ? 25 : batchSize;

                    $"Reloading with {batchSize} tests per/batch".LogDebug();

                    return
                        arena.ListTests(test).SelectMany(s => s)
                            .Buffer(batchSize)
                            .Select(tests => new StartUnitTest()
                            {
                                UseAppUpdate = appUpdateDllSource ?? "Test",
                                UseAppVersion = appUpdateVersion,
                                Dll = dll,
                                RunThisTest = tests.ToStringEach()
                            });
                }

                return test.ToObservable();
            })
            .Buffer(TimeSpan.FromSeconds(1))
            .Where(l => l.Count > 0)
            .Select(l => l.ToArray())
            .Publish()
            .RefCount()
            ;

        }

        private static IObservable<string> GetTargets(string dll)
        {
            return Rxn.Create<string>(o =>
            {
                var watchers = new CompositeDisposable();
                var pattern = dll.Split('/', '\\');
                var dir = pattern.Take(pattern.Length - 1).ToStringEach("/");
                var filePattern = pattern.Last();

                if (!dll.Contains("*"))
                {
                    
                    Files.WatchForChanges(dir, filePattern, () => o.OnNext(dll), true, false, false).DisposedBy(watchers);
                    o.OnNext(dll);
                }
                else
                {

                    foreach (var file in Directory.GetFileSystemEntries(dir, filePattern))
                    {
                        Files.WatchForChanges(dir, file, () => o.OnNext(file)).DisposedBy(watchers);
                    }
                }

                return watchers;
            });
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
                        "fire rapidly {{threadCount | max}} | will fire on multiple threads simultatiously".LogDebug();
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
                theBfg.Args = args;

                "Configuring App".LogDebug();
                
                
                theBFGAspNetCoreAdapter.Appcfg = RxnAppCfg.Detect(args);
                
                return AspNetCoreWebApiAdapter.StartWebServices<theBFGAspNetCoreAdapter>(theBFGAspNetCoreAdapter.Cfg, args).ToObservable()
                    .LastAsync()
                    .Select(_ => new Unit())
                    .Subscribe(o);
            });
        }

        public theBfg(bfgCluster cluster)
        {
            _testCluster = cluster;
        }

        public IDisposable Fire(IObservable<StartUnitTest[]> unitTests = null)
        {
            return (unitTests ?? _lastFired.ToObservable())
                .Do(tests =>
                {
                    startedAt = Stopwatch.StartNew();
                    _lastFired = tests;
                    "Test".LogDebug(++iteration);
                    startedAt.Restart();

                    foreach (var test in tests)
                    {
                        _testCluster.Publish(test);
                    }
                }).Until();
        }

        public IDisposable DoWorkContiniously(IRxnManager<IRxn> rxnManage, IObservable<StartUnitTest[]> unitTestToRun,  Action fireStyle)
        {
            return unitTestToRun.Select(tests =>
            {
                var stopFiringContiniously = rxnManage.CreateSubscription<CommandResult>()
                    .Where(c => c.InResponseTo.Equals(tests[0].Id))
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

                return stopFiringContiniously;
            })
            .Until();
        }

        public IDisposable WatchForCompletion(IRxnManager<IRxn> rxnManage, IObservable<StartUnitTest[]> unitTestToRun, Action onComplete)
        {
            //stopAdvertising?.Dispose();
            return unitTestToRun.Select(tests =>
            {
                var stopDOingWork = rxnManage.CreateSubscription<CommandResult>()
                    .Where(_ => unitTestToRun != null)
                    .Where(c => c.InResponseTo.Equals(tests[0]
                        .Id)) //this code is wrong, need to fix, could be a response to any msg
                    .Do(_ =>
                    {
                        $"Duration: {startedAt.Elapsed}".LogDebug();

                        if (iteration <= 0) onComplete?.Invoke();
                    })
                    .Until();

                return stopDOingWork;

            })
            .Until();
        }

        public void FireRapidly(IObservable<StartUnitTest[]> tests)
        {
            Enumerable.Range(0, Environment.ProcessorCount).ToObservable().ObserveOn(NewThreadScheduler.Default).Do(_ => { Fire(tests); }).Until();
        }

        public IObservable<bfgWorker> StartRapidWorkers(IResolveTypes resolver, IObservable<StartUnitTest[]> unitTestToRun)
        {
            return unitTestToRun.SelectMany(tests => Enumerable.Range(0, Environment.ProcessorCount).ToObservable().SelectMany(_ => SpawnTestWorker(resolver, unitTestToRun)));
        }

        public void WatchForTestUpdates(IRxnManager<IRxn> rxnManage)
        {
            rxnManage.CreateSubscription<UpdateSystemCommand>()
                .Do(_ => { _notUpdating = false; })
                .Until(); }


        int iteration = 0;
        private Stopwatch startedAt = new Stopwatch();


        public IDisposable StartTestArena(string[] args, IObservable<StartUnitTest[]> allUnitTests, IResolveTypes resolver)
        {

            "Starting up TestArena".LogDebug();

            if (_started.HasValue)
            {
                _started = DateTime.Now;
            }

            var stopAdvertising = bfgTestApi.AdvertiseForWorkers(resolver.Resolve<SsdpDiscoveryService>(), "all", $"http://{RxnApp.GetIpAddress()}:888");
            var stopAutoLaunchingTestsIntoArena = LaunchUnitTestsToTestArenaDelayed(allUnitTests);
            var stopFiring = StartFiringWorkflow(args, allUnitTests, resolver.Resolve<IRxnManager<IRxn>>());

            var broadCaste = allUnitTests.FirstAsync()
                .Do(unitTests => BroadcasteStatsToTestArena(_testCluster, unitTests[0], resolver.Resolve<SystemStatusPublisher>(), resolver.Resolve<IManageReactors>()))
                .Until();

            return new CompositeDisposable(stopAdvertising, stopAutoLaunchingTestsIntoArena, stopFiring, broadCaste);

        }

        public IObservable<bfgWorker> StartTestArenaWorkers(string[] args, IObservable<StartUnitTest[]> unitTestToRun, IResolveTypes resolver)
        {
            _testCluster = resolver.Resolve<bfgCluster>();

            if (args.Contains("rapidly"))
            {
                return StartRapidWorkers(resolver, unitTestToRun);
            }
            
            if (args.Contains("fire"))
            {
                return SpawnTestWorker(resolver, unitTestToRun);
            }

            return Rxn.Empty<bfgWorker>();
        }


        public IDisposable LaunchUnitTestsToTestArenaDelayed(IObservable<StartUnitTest[]> unitTestToRun)
        {
            return unitTestToRun.Delay(TimeSpan.FromSeconds(2))
                .Do(runTheseUnitTests =>
                {
                    RxnApps.CreateAppUpdate(runTheseUnitTests[0].UseAppUpdate,
                            Scrub(runTheseUnitTests[0].UseAppVersion),
                            new FileInfo(runTheseUnitTests[0].Dll).DirectoryName, true, "http://localhost:888")
                        .Catch<Unit, Exception>(
                            e =>
                            {
                                ReportStatus.Log.OnError("Could not download test. Cant join cluster :(", e);
                                throw e;
                            }).WaitR();
                })
                .Until();
        }

        /// <summary>
        /// Implements the firing sematics of the bfg, reponding to rapidly condintiously etc
        
        /// </summary>
        /// <param name="args"></param>
        /// <param name="unitTestToRun"></param>
        /// <param name="resolver"></param>
        /// <param name="rxnManager"></param>
        /// <returns></returns>
        public IDisposable StartFiringWorkflow(string[] args, IObservable<StartUnitTest[]> unitTestToRun,  IRxnManager<IRxn> rxnManager)
        {
            //todo: should be implemented with a rxnmediator, can seperate firestyle into injected logic classes
            if (args.Contains("continuously"))
            {
                WatchForTestUpdates(rxnManager);

                if (args.Contains("rapidly"))
                {
                    DoWorkContiniously(rxnManager, unitTestToRun, () => FireRapidly(unitTestToRun));
                }
                else
                {
                    DoWorkContiniously(rxnManager, unitTestToRun,() => Fire(unitTestToRun));
                }
            }
            else
            {
                if (args.Contains("rapidly"))
                {
                    FireRapidly(unitTestToRun);
                }
                else
                {
                    Fire(unitTestToRun);
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

        private static int _workerCount;
        private Func<IEnumerable<IMonitorAction<IRxn>>> _before;
        private StartUnitTest[] _lastFired = new StartUnitTest[0];

        public static IObservable<bfgWorker> SpawnTestWorker(IResolveTypes resolver, IObservable<StartUnitTest[]> Cfg)
        {
            return Cfg.FirstAsync().Select(tests =>
            {
                "Spawning worker".LogDebug(++_workerCount);

                var testCluster = resolver.Resolve<bfgCluster>();
                var rxnManager = resolver.Resolve<IRxnManager<IRxn>>();

                //$"Streaming logs".LogDebug();
                //rxnManager.Publish(new StreamLogs(TimeSpan.FromMinutes(60))).Until();

                $"Starting worker".LogDebug();

                var testWorker = new bfgWorker(
                    $"TestWorker#{_workerCount}", "local",
                    resolver.Resolve<IAppServiceRegistry>(), resolver.Resolve<IAppServiceDiscovery>(),
                    resolver.Resolve<IZipService>(), resolver.Resolve<IAppStatusServiceClient>(),
                    resolver.Resolve<IRxnManager<IRxn>>(), resolver.Resolve<IUpdateServiceClient>(),
                    resolver.Resolve<ITestArena>()
                    );

                if (!tests.AnyItems() || tests[0].AppStatusUrl.IsNullOrWhitespace() && !Directory.Exists(tests[0].UseAppVersion))
                    testWorker.DiscoverAndDoWork();

                testCluster.Process(new WorkerDiscovered<StartUnitTest, UnitTestResult>() {Worker = testWorker})
                    .SelectMany(e => rxnManager.Publish(e)).Until();

                return testWorker;
            });
        }

        /// <summary>
        /// todo: fix issue with not broadcasting all test stats to appstatus, only first time
        /// </summary>
        /// <param name="testCluster"></param>
        /// <param name="unitTestToRun"></param>
        /// <param name="publusher"></param>
        /// <param name="reactorMgr"></param>
        private void BroadcasteStatsToTestArena(bfgCluster testCluster, StartUnitTest unitTestToRun, SystemStatusPublisher publusher, IManageReactors reactorMgr)
        {
            var bfgReactor = reactorMgr.GetOrCreate("bfg").Reactor;
            //need to fix health which will allow this to be viewed on the appstatus portal. should monitor health of fanout stratergy
            //
            //IMonitorActionFactory<IRxn> health =MonitorHealth
            RxnCreator.MonitorHealth<IRxn>(bfgReactor, "theBFG", out _before, () => Rxn.Empty()).SelectMany(bfgReactor.Output).Until();

            _info = () =>
            {
                return new[]
                {
                    unitTestToRun == null
                        ? new AppStatusInfo("Test", $"Running {(unitTestToRun.RunAllTest ? "All" : unitTestToRun.RunThisTest)}")
                        : new AppStatusInfo("Test", $"Waiting for work"),
                    new AppStatusInfo("Duration", (DateTime.Now - (_started ?? DateTime.Now)).TotalMilliseconds),
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