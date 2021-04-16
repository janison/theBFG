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
using System.Reflection;
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

/// <summary>
///
/// Backlog
///
/// todo:
///         - show discovered tests in test arena when .dll selected
/// ///         - need to work out how to deal with multiple test-disvcovered.. should alert on a diff? 
///             -   "new" tests added tests
///         - thebfg launch @localhost //need to implement, docoed but not working
///         - thebfg target myinttest.bfc // list of bfg commands in a file that can be executed in a single unit
///             -   allow startintegrationtest command which is basically like
///                 allows sets of startunittests to be specified. maybe something also to do wtih amount
///                 of workers needed for each test? or will that just be an extra parama for startunitest?
///         - thebfg servicecmd to allow same sytax to be used via test arena console as from commandline?
///                 - show it launch new processes or same? or cfgable?
/// 
///          thebfg target all // monitors dirs and auto-executes 
///   
///         -   allow workers to be associated with tags
/// -           -   allow targeting of tests at specific tag'd workers with startunittest
/// -           -   show workers resource ussage as graphs which are  ----------- this big each and they scroll after laying
///                 out and overflowing the container. Like the taskmgr  layout. only need to show stats per machine, not worker so we dont repeat whats on t
///                 the same machine
/// 
///             - need to fix saving / persistance of data.
///             -   thebfg self destruct to clear the metric cache and reset everything




/// </summary>
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
        public static IObservable<StartUnitTest[]> DetectAndWatchTargets(string[] args, Func<ITestArena[]> forCompete, IServiceCommandFactory integrationTestParsing, IObservable<UnitTestResult> integrationTestResults)
        {
            var testCfg = theBigCfg.Detect();

            var dllOrTestSynax = args.Skip(1).FirstOrDefault().IsNullOrWhiteSpace(testCfg.Dll).Split('$')[0];//heck to remove the unwated tokens

            return GetTargets(dllOrTestSynax, args, forCompete, integrationTestParsing, integrationTestResults)
                    .Buffer(TimeSpan.FromSeconds(1))
                    .Where(l => l.Count > 0)
                    .Select(l => l.ToArray())
                    .Replay(1)
                    .RefCount()
                    ;
        }
        
        public static IObservable<StartUnitTest> GetTargets(string testSyntax, string[] args, Func<ITestArena[]> forCompete, IServiceCommandFactory parseTestSyntax, IObservable<UnitTestResult> forParallelExection)
        {
            return testSyntax.IsNullOrWhitespace() ? Rxn.Empty<StartUnitTest>() :
            Rxn.DfrCreate<StartUnitTest>(() =>
            {
                if (!(testSyntax.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) ||
                      testSyntax.EndsWith(".csproj", StringComparison.InvariantCultureIgnoreCase) ||
                      testSyntax.EndsWith(".bfc", StringComparison.InvariantCultureIgnoreCase)))
                {
                    "Target must be either .dll or .csproj or .bfc".LogDebug();
                    return Rxn.Empty<StartUnitTest>();
                }
                
                if (testSyntax.EndsWith(".bfc"))
                {
                    return GetTargetsFromBfc(testSyntax, parseTestSyntax, forParallelExection);
                }

                if (!testSyntax.Contains("*"))
                {
                    return GetTargetsFromDll(args, testSyntax, forCompete);
                }
                else
                {

                    return GetTargetsFromPath(args, testSyntax, forCompete);
                }
            });
        }

        private static IObservable<StartUnitTest> GetTargetsFromPath(string[] args, string testSyntax, Func<ITestArena[]> arena)
        {
            var pattern = testSyntax.Split('/', '\\');

            var dir = pattern.Take(pattern.Length - 1).ToStringEach("/");
            var filePattern = pattern.Last();
            var watchers = new CompositeDisposable();


            return Rxn.Create<StartUnitTest>(o =>
            {
                foreach (var dll in Directory.GetFileSystemEntries(dir, filePattern, SearchOption.AllDirectories))
                {
                    Files.WatchForChanges(dir, dll, () => GetTargetsFromDll(args, dll, arena).Do(t => o.OnNext(t)).Until()).DisposedBy(watchers);
                }

                return watchers;
            });
        }

        private static IObservable<StartUnitTest> GetTargetsFromDll(string[] args, string dll, Func<ITestArena[]> arena)
        {
            var testCfg = theBigCfg.Detect();

            var appUpdateDllSource = dll == null ? null : dll.Contains("@") ? dll.Split('@').Reverse().FirstOrDefault().IsNullOrWhiteSpace(testCfg.RunThisTest) : null;
            var testName = dll == null ? string.Empty : dll.Contains("$") ? dll.Split('$').Reverse().FirstOrDefault().IsNullOrWhiteSpace(testCfg.RunThisTest) : null;
            var appUpdateVersion = args.Skip(4).FirstOrDefault().IsNullOrWhiteSpace(testCfg.UseAppVersion);
            //todo fix this, should embed version inside dll expression

            if (args.Contains("compete"))
            {
                var userSize = args.Last();
                int batchSize = 0;
                Int32.TryParse(userSize, out batchSize);

                batchSize = batchSize == 0 ? 25 : batchSize;

                $"Reloading with {batchSize} tests per/batch".LogDebug();

                return ListTests(dll, arena).SelectMany(s => s)
                    .Buffer(batchSize)
                    .Select(tests => new StartUnitTest()
                    {
                        UseAppUpdate = appUpdateDllSource,
                        UseAppVersion = appUpdateVersion,
                        Dll = dll,
                        RunThisTest = tests.ToStringEach()
                    });
            }

            return new StartUnitTest()
            {
                UseAppUpdate = appUpdateDllSource,
                UseAppVersion = appUpdateVersion,
                Dll = dll,
                RunThisTest = testName,
            }.ToObservable();
        }

        public static IObservable<StartUnitTest> GetTargetsFromBfc(string bfcFile, IServiceCommandFactory parseTestSyntax, IObservable<UnitTestResult> forParallelExection)
        {
            var pattern = bfcFile.Split('/', '\\');

            var dir = pattern.Take(pattern.Length - 1).ToStringEach("/");
            var filePattern = pattern.Last();
            var watchers = new CompositeDisposable();

            return Rxn.Create<StartUnitTest>(o =>
            {
                Files.WatchForChanges(dir, filePattern, () =>
                {
                    TestWorkflow.StartIntegrationTest(File.ReadAllText(bfcFile), parseTestSyntax, forParallelExection).Do(dll => o.OnNext(dll)).Until();
                }, true, false, false)
                .DisposedBy(watchers);

                if (bfcFile.EndsWith(".bfc", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!File.Exists(bfcFile))
                    {
                        $"Could not find theBFG command file: {bfcFile}".LogDebug();
                        o.OnCompleted();
                    }

                    TestWorkflow.StartIntegrationTest(File.ReadAllText(bfcFile), parseTestSyntax, forParallelExection).Do(dll => o.OnNext(dll)).Until();
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

                    case "launch":
                        return ReloadWithTestArena(url, args).Subscribe(o);
                    case null:

                        "theBFG instructions:".LogDebug();
                        "1. Take aim at a target".LogDebug();
                        "2. Fire".LogDebug();
                        "".LogDebug();
                        "".LogDebug();
                        "Usage:".LogDebug();
                        "target test.dll".LogDebug();
                        "target Test@test.dll and fire".LogDebug();
                        "fire".LogDebug();
                        "fire @url".LogDebug();
                        "fire rapidly {{threadCount | max}} | will fire on multiple threads simultatiously".LogDebug();
                        "fire compete | shard test-suite execution across multiple nodes".LogDebug();
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
                    $"{tests.Length} Tests about to run".LogDebug(++iteration);
                    startedAt.Restart();

                    foreach (var test in tests)
                    {
                        _testCluster.Publish(test);
                    }
                }).Until();
        }

        public IDisposable DoWorkContiniously(IRxnManager<IRxn> rxnManage, IObservable<StartUnitTest[]> unitTestToRun, Action fireStyle)
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

        public void FireCompeteRapidly(IObservable<StartUnitTest[]> unitTestToRun, IResolveTypes resolver)
        {
            Enumerable.Range(0, Environment.ProcessorCount).ForEach(_ =>
            {
                SpawnTestWorker(resolver, unitTestToRun).Until();
            });

            Fire(unitTestToRun);
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
                .Until();
        }


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

            return new CompositeDisposable(stopAdvertising);
        }

        public IDisposable LaunchUnitTests(string[] args, IObservable<StartUnitTest[]> allUnitTests, IResolveTypes resolver)
        {
            var stopAutoLaunchingTestsIntoArena = LaunchUnitTestsToTestArenaDelayed(allUnitTests, resolver.Resolve<IAppStatusCfg>());
            var stopFiring = StartFiringWorkflow(args, allUnitTests, resolver.Resolve<IRxnManager<IRxn>>(), resolver);

            var broadCaste = allUnitTests.FirstAsync()
                .Do(unitTests => BroadcasteStatsToTestArena(_testCluster, unitTests[0], resolver.Resolve<SystemStatusPublisher>(), resolver.Resolve<IManageReactors>()))
                .Until();

            return new CompositeDisposable(stopAutoLaunchingTestsIntoArena, stopFiring, broadCaste);
        }

        public IObservable<bfgWorker> StartTestArenaWorkers(string[] args, IObservable<StartUnitTest[]> unitTestToRun, IResolveTypes resolver)
        {
            _testCluster = resolver.Resolve<bfgCluster>();

            if (args.Contains("rapidly"))
            {
                return StartRapidWorkers(resolver, unitTestToRun);
            }

            if (args.Contains("fire") || args.Contains("launch"))
            {
                return SpawnTestWorker(resolver, unitTestToRun);
            }

            return Rxn.Empty<bfgWorker>();
        }


        public IDisposable LaunchUnitTestsToTestArenaDelayed(IObservable<StartUnitTest[]> unitTestToRun, IAppStatusCfg cfg)
        {
            return unitTestToRun.Delay(TimeSpan.FromSeconds(2))
                .Do(runTheseUnitTests =>
                {
                    RxnApps.CreateAppUpdate(runTheseUnitTests[0].UseAppUpdate,
                            Scrub(runTheseUnitTests[0].UseAppVersion),
                            new FileInfo(runTheseUnitTests[0].Dll).DirectoryName, false, cfg, "http://localhost:888")
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
        /// <param name="rxnManager"></param>
        /// <param name="resolver"></param>
        /// <returns></returns>
        public IDisposable StartFiringWorkflow(string[] args, IObservable<StartUnitTest[]> unitTestToRun, IRxnManager<IRxn> rxnManager, IResolveTypes resolver)
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
                    DoWorkContiniously(rxnManager, unitTestToRun, () => Fire(unitTestToRun));
                }
            }
            else
            {
                if (args.Contains("rapidly"))
                {
                    if (args.Contains("compete"))
                    {
                        FireCompeteRapidly(unitTestToRun, resolver);
                    }
                    else
                    {
                        FireRapidly(unitTestToRun);
                    }

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
            return new[] { "rapidly", "continuously", "fire" }.FirstOrDefault(i => i == useAppVersion) == null
                ? useAppVersion
                : null;
        }

        private static int _workerCount;
        private Func<IEnumerable<IMonitorAction<IRxn>>> _before;
        private StartUnitTest[] _lastFired = new StartUnitTest[0];

        public static IObservable<bfgWorker> SpawnTestWorker(IResolveTypes resolver, IObservable<StartUnitTest[]> cfg)
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
                resolver.Resolve<IAppStatusCfg>(),
                resolver.Resolve<Func<ITestArena[]>>()
            );

            testCluster.Process(new WorkerDiscovered<StartUnitTest, UnitTestResult>() { Worker = testWorker })
                .SelectMany(e => rxnManager.Publish(e)).Until();

            return cfg.Take(1).Select(tests =>
            {
                if (!tests.AnyItems() || tests[0].AppStatusUrl.IsNullOrWhitespace() && !Directory.Exists(tests[0].UseAppVersion))
                    testWorker.DiscoverAndDoWork();

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

        public static IObservable<IEnumerable<string>> ListTests(string testDll, Func<ITestArena[]> arenas)
        {
            return arenas().SelectMany(a => a.ListTests(testDll)).FirstAsync(w => w.AnyItems());
        }

        public static IObservable<UnitTestDiscovered> DiscoverUnitTests(string testDllSelector, string[] args, Func<ITestArena[]> arenas)
        {
            return Rxn.Create<UnitTestDiscovered>(o =>
            {
                return GetTargets(testDllSelector, args, null, null, null)
                    .Where(d => NotAFrameworkFile(d))
                    .Do(t =>
                    {
                        ListTests(testDllSelector, arenas)
                            .Select(
                            tests =>
                            {
                                return new UnitTestDiscovered()
                                {
                                    Dll = t.Dll,
                                    DiscoveredTests = tests.ToArray()
                                };
                            })
                            .Do(o.OnNext)
                            .Until();
                    })
                    .Subscribe();//todo fix hanging
            })
                .Publish().RefCount();
        }

        private static bool NotAFrameworkFile(StartUnitTest d)
        {
            return !d.Dll.BasicallyContains("packages/") && !d.Dll.BasicallyContains("packages\\") &&
                   !d.Dll.BasicallyContains("obj/") && !d.Dll.BasicallyContains("obj\\") &&
                   !d.Dll.BasicallyContains(".TestPlatform.") && !d.Dll.BasicallyContains(".xunit.") &&
                   !d.Dll.BasicallyContains(".nunit.");
        }

        public IDisposable ExitAfter(StartUnitTest[] testsToWatch, IObservable<UnitTestResult> testResults)
        {
            var allFinished = testsToWatch.Length;
            var passed = 0;
            var failed = 0;

            //todo: break down this function into small bits
            return testResults.Where(r =>
            {
                var resultWeAreTnterestedIn = testsToWatch.FirstOrDefault(t => t.Id == r.InResponseTo);

                if (resultWeAreTnterestedIn != null)
                {
                    --allFinished;

                    if (r.WasSuccessful)
                        passed++;
                    else
                        failed++;

                    $@"
===============================================================================
-------------------------------------------------------------------------------
{(r.WasSuccessful ? @" (                              
 )\ )                     (     
(()/(    )            (   )\ )  
 /(_))( /(  (   (    ))\ (()/(  
(_))  )(_)) )\  )\  /((_) ((_)) 
| _ \((_)_ ((_)((_)(_))   _| |  
|  _// _` |(_-<(_-</ -_)/ _` |  
|_|  \__,_|/__//__/\___|\__,_|  
                                " : @" _______  _______  ___   ___      _______  ______  
|       ||   _   ||   | |   |    |       ||      | 
|    ___||  |_|  ||   | |   |    |    ___||  _    |
|   |___ |       ||   | |   |    |   |___ | | |   |
|    ___||       ||   | |   |___ |    ___|| |_|   |
|   |    |   _   ||   | |       ||   |___ |       |
|___|    |__| |__||___| |_______||_______||______|")} 

----------------------------------------------------------------------------------
{resultWeAreTnterestedIn.Dll}
----------------------------------------------------------------------------------".LogDebug();
                    }
                        

                    return allFinished < 1;
                })
                .Take(1)
                .Do(result =>
            {

                @$"
-------------------------------------------------------------------------------
-------------------------------------------------------------------------------
  __  .__          __________   _____       
_/  |_|  |__   ____\______   \_/ ____\____  
\   __\  |  \_/ __ \|    |  _/\   __\/ ___\ 
 |  | |   Y  \  ___/|    |   \ |  | / /_/  >
 |__| |___|  /\___  >______  / |__| \___  / 
           \/     \/       \/      /_____/

-------------------------------------------------------------------------------
{(failed > 0 ? $"Failed {failed} of {failed + passed}" : $"{passed} Passed")}
-------------------------------------------------------------------------------

{(failed > 0 ? "Chin up! Next time!" 
            : "Good work! Keep it up!")}                
----------------------------------------------------------------------------------
------------------------------------------------------------------ CaptainJono ---
==================================================================================
".LogDebug();

                theBfg.IsCompleted.OnNext(new Unit());
                theBfg.IsCompleted.OnCompleted();

                return;
            })
            .FirstAsync()
            .Until();
        }
    }
}