using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Autofac;
using Rxns;
using Rxns.Cloud;
using Rxns.Commanding;
using Rxns.DDD.Commanding;
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

    public class FocusOn : ServiceCommand
    {
        public string TestName { get; set; }

        public FocusOn(string testName)
        {
            TestName = testName;
        }

        public FocusOn()
        {

        }
    }

    public class StopFocusing : ServiceCommand
    {

    }

    public class DiscoverUnitTests : ServiceCommand
    {
        public string TestDllOrPattern { get; }

        public DiscoverUnitTests(string testDllOrPattern)
        {
            TestDllOrPattern = testDllOrPattern;
        }

        public DiscoverUnitTests()
        {

        }
    }

    public class StartRecording : ServiceCommand, IReactiveEvent
    {

    }

    public class StopRecording : ServiceCommand, IReactiveEvent
    {

    }

    public class bfgAppInfo : IRxnAppInfo
    {
        public string Name { get; set; }
        public string Version { get; set; } = typeof(bfgAppInfo).Assembly.GetName().Version?.ToString();
        public string Url { get; } = RxnApp.GetIpAddress();
        public string Id { get; } = bfgWorkerManager.ClientId;
        public bool KeepUpdated { get; set;  } = true;

        public bfgAppInfo(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// The Gatling Gun evolved into the Bfg in the games I played. Accurate, responsive, rewarding to master, having your back in every situation with deadly response.
    ///
    /// I wanted to create a C# testing tool that brought the C# ecosystem out of the gatling stone ages. Somethign that just worked with the tooling and test frameorks we already used. Something that would compliment the dev-test flow
    /// and compensate for the lack of creativety that my IDE vendor delivered. Something that let me leverage my existing toolbox to orchestrate advanced distributed test scenarios.
    /// A tool which helped me iterate quickly and tell me exactly what went wrong and why without giving me RSI.
    ///
    /// Key concepts:
    ///     - That theBFG is a gun that likes to punish tests.
    ///     - Each deathmatch test takes place inside test arena.
    ///     - You fire to run the tests. Reloading and Firing as normal.
    ///     - You monitor everything in real-time via TestArena Portal.
    ///     - Either theBfg wins, or your test wins. Goodluck.
    /// 
    /// This API gives you the keys to the castle. Use with caution. Eye protection recommended when using rapid fire mode for extended periods.
    /// </summary>
    public class theBfg : IDisposable, IServiceCommandHandler<Reload>, IServiceCommandHandler<FocusOn>, IServiceCommandHandler<StopFocusing>, IServiceCommandHandler<DiscoverUnitTests>, IRxnPublisher<IRxn>
    {
        private readonly Func<ITestArena[]> _testArena;
        public static ISubject<Unit> IsCompleted = new ReplaySubject<Unit>(1);
        public static IDisposable TestRunner { get; set; }
        public static string[] Args = new string[0];

        private static Func<AppStatusInfo[]> _info = () => new AppStatusInfo[0];
        private static DateTime? _started;
        private static bfgCluster _testCluster;
        private static bool  _notUpdating = true;

        public static string DataDir = ".bfg";

        static theBfg()
        {
            if (!Directory.Exists(DataDir))
                Directory.CreateDirectory(DataDir);
        }
        
        public static IObservable<Unit> ReloadWithTestWorker(params string[] args)
        {
            return Rxn.Create<Unit>(o =>
            {
                var apiName = args.Skip(1).FirstOrDefault();
                var testHostUrl = GetTestarenaUrlFromArgs(args);
                theBfg.Args = args;


                RxnExtensions.DeserialiseImpl = (t, json) => JsonExtensions.FromJson(json, t);
                RxnExtensions.SerialiseImpl = (json) => JsonExtensions.ToJson(json);

                var cfg = RxnAppCfg.Detect(args);
                //var appStore = new CurrentDirectoryAppUpdateStore();
                //var clusterHost = OutOfProcessFactory.CreateClusterHost(args, appStore, cfg);

                return theBfgDef.TestWorker(apiName, testHostUrl, RxnApp.SpareReator(testHostUrl)).ToRxns()
                    .Named(new bfgAppInfo("bfgWorker"))
                .OnHost(new ConsoleHostedApp(), cfg)
                    .SelectMany(h => h.Run().Do(app =>
                    {
                        theBfg.IsReady.OnNext(app);
                    }))
                    .Select(app =>
                    {
                        return new Unit();
                    })
                    .Subscribe(o);
            });
        }

        public static ISubject<IRxnAppContext> IsReady = new ReplaySubject<IRxnAppContext>(1);

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

            var dllOrTestSynax = GetDllFromArgs(args);//heck to remove the unwated tokens

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
            return testSyntax.IsNullOrWhitespace() ? 
                Rxn.Empty<StartUnitTest>() :
                Rxn.DfrCreate<StartUnitTest>(() =>
                {
                    if (!(testSyntax.Contains(".dll", StringComparison.InvariantCultureIgnoreCase) ||
                          testSyntax.Contains(".csproj", StringComparison.InvariantCultureIgnoreCase) ||
                          testSyntax.Contains(".bfc", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        "Target must be either .dll or .csproj or .bfc".LogDebug();
                        return Rxn.Empty<StartUnitTest>();
                    }
                    
                    if (testSyntax.Contains(".bfc"))
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
                })
                .Select(e =>
                {
                    e.Dll = e.Dll.AsCrossPlatformPath();

                    if(!FocusedTest.IsNullOrWhitespace())
                        e.RunThisTest = FocusedTest;

                    return e;
                });
        }

        /// <summary>
        /// When set, all Targets will be filtered to include this as
        /// the test to run only. Good for situations where you are working on large
        /// slow test unit test suites, or for integration testing
        /// </summary>
        public static string FocusedTest { get; set; } //Hmm this kind of state is bad. i should ditch static on everything?

        private static IObservable<StartUnitTest> GetTargetsFromPath(string[] args, string testSyntax, Func<ITestArena[]> arena)
        {
            var pattern = testSyntax.Split('/', '\\');

            var dir = pattern.Take(pattern.Length - 1).ToStringEach("/");
            var filePattern = pattern.Last();
            var watchers = new CompositeDisposable();


            return Rxn.Create<StartUnitTest>(o =>
            {
                foreach (var dll in Directory.GetFileSystemEntries(dir, filePattern, SearchOption.AllDirectories).Select(d => d.AsCrossPlatformPath()).Where(NotAFrameworkFile))
                {
                    GetTargetsFromDll(args, dll, arena).Do(t => o.OnNext(t)).Until();
                }

                return watchers;
            });
        }


        public static string GetAppVersionFromDllSyntax(string[] args, string dll)
        {
            var testCfg = bfgCfg.Detect();

            var appUpdateDllSource = args.Skip(4).FirstOrDefault().IsNullOrWhiteSpace(testCfg.UseAppVersion);
            
            return appUpdateDllSource.IsNullOrWhiteSpace(DateTime.Now.ToString("s").Replace(":", ""));
        }

        public static string GetAppNameFromDllSyntax(string dll)
        {
            var testCfg = bfgCfg.Detect();

            var appUpdateDllSource = dll == null ? null : dll.Contains("@") ? dll.Split('@').Reverse().FirstOrDefault().IsNullOrWhiteSpace(testCfg.RunThisTest) : null;
            var dlld = new FileInfo(dll);

            return appUpdateDllSource.IsNullOrWhiteSpace(dlld.Name.Split(dlld.Extension).FirstOrDefault().TrimEnd('.'));
        }

        public static IObservable<StartUnitTest> GetTargetsFromDll(string[] args, string dll, Func<ITestArena[]> arena)
        {
            var appUpdateDllSource = GetAppNameFromDllSyntax(dll);
            var testName = GetTestNameFromArgs(args);
            var appUpdateVersion = GetAppVersionFromDllSyntax(args, dll);
            var watchDllUpdatesOverTime = GetOrCreateWatcher(dll);

            return watchDllUpdatesOverTime
                .StartWith(dll)
                .SelectMany(d =>
            {
                if (args.Contains("compete"))
                {
                    var userSize = args.Last();
                    int batchSize = 0;
                    Int32.TryParse(userSize, out batchSize);

                    batchSize = batchSize == 0 ? 25 : batchSize;

                    $"Reloading with {batchSize} tests per/batch".LogDebug();

                    return ListTests(d, arena).SelectMany(s => s)
                        .Buffer(batchSize)
                        .Select(tests => new StartUnitTest()
                        {
                            UseAppUpdate = appUpdateDllSource,
                            UseAppVersion = appUpdateVersion,
                            Dll = d,
                            RunThisTest = tests.ToStringEach()
                        });
                }

                return new StartUnitTest()
                {
                    UseAppUpdate = appUpdateDllSource,
                    UseAppVersion = appUpdateVersion,
                    Dll = d,
                    RunThisTest = testName,
                }.ToObservable();
            });
        }

        private static string GetTestNameFromArgs(string[] dll)
        {
            var testCfg = bfgCfg.Detect();

            return dll.FirstOrDefault(a => a.Contains("$"))?.Split('$').Reverse().FirstOrDefault().IsNullOrWhiteSpace(testCfg.RunThisTest);
        }

        private static readonly IDictionary<string, IObservable<string>> _watchers = new ConcurrentDictionary<string, IObservable<string>>();
        private static IObservable<string> GetOrCreateWatcher(string testDll)
        {
            var dlld = new FileInfo(testDll);
            if (_watchers.ContainsKey(dlld.FullName))
                return Rxn.Empty<string>(); //already subscribed to updates

            var watcher = Files.WatchForChanges(dlld.DirectoryName, dlld.Name, true, false, false);
            _watchers.Add(dlld.FullName, watcher);
            return watcher;
        }

        public static IObservable<StartUnitTest> GetTargetsFromBfc(string bfcFile, IServiceCommandFactory parseTestSyntax, IObservable<UnitTestResult> forParallelExection)
        {
            var pattern = bfcFile.Split('/', '\\');

            var dir = pattern.Take(pattern.Length - 1).ToStringEach("/");
            var filePattern = pattern.Last();
            var watchers = new CompositeDisposable();

            return Rxn.Create<StartUnitTest>(o =>
            {
                Files.WatchForChanges(dir, filePattern, true, false, false).SelectMany(_ => TestWorkflow.StartIntegrationTest(File.ReadAllText(bfcFile), parseTestSyntax, forParallelExection).Do(dll => o.OnNext(dll)))
                    .Until().DisposedBy(watchers);

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
            })
            ;
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
                        return ReloadWithTestWorker(args).Subscribe(o);
                        
                    case "target":
                        return ReloadWithTestArena(args).Subscribe(o);
                    
                    case "launch":
                        return LaunchToTestArenaIf(args, url) ?? ReloadWithTestArena(args).Subscribe(o);

                    case "self":
                        return SelfDestructIf(args).Subscribe(o);

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

        private static IObservable<Unit> SelfDestructIf(string[] args)
        {
            return Rxn.Create(() =>
            {
                var destruct = args.Skip(1).FirstOrDefault().IsNullOrWhiteSpace("fail");
                var quiteMode = args.Last().IsNullOrWhiteSpace("nup");

                if (!destruct.BasicallyEquals("destruct")) return;

                if (!quiteMode.BasicallyEquals("quite"))
                {
                    "Please type 'y' to delete ALL BFG DATA under this root".LogDebug();

                    if (Console.Read() != 'y')
                    {
                        "Aborting".LogDebug();
                        return;
                    }
                }

                Directory.Delete(DataDir, true);
                "RESET !!!".LogDebug();
            })
            .FinallyR(() =>
            {
                theBfg.IsCompleted.OnNext(new Unit());
                theBfg.IsCompleted.OnCompleted();
            });
        }

        public static IObservable<Unit> ReloadWithTestArena(params string[] args)
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

        private static IDisposable LaunchToTestArenaIf(string[] args, string url)
        {
            var dll = GetDllFromArgs(args);

            if (dll.IsNullOrWhitespace() || dll.Contains("*"))
            {
                //not launching a specific .dll, abort
                return null;
            }

            var name = GetAppNameFromDllSyntax(dll);
            var appUpdateVersion = GetAppVersionFromDllSyntax(args, dll);
            url = GetTestarenaUrlFromArgs(args) ?? url;

            return LaunchAppToTestArena(name, appUpdateVersion, dll, url, new AppStatusCfg()).FinallyR(() => theBfg.IsCompleted.OnCompleted()).Until();
        }

        /// <summary>
        ///  updatename@dll:version 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static string GetDllFromArgs(string[] args)
        {
            var testCfg = bfgCfg.Detect();
            var appsyntax = args.Skip(1).FirstOrDefault().IsNullOrWhiteSpace(testCfg.Dll)?.Split('$')[0].AsCrossPlatformPath();

            return appsyntax;
        }

        public static IDisposable Fire(IObservable<StartUnitTest[]> unitTests = null)
        {
            return (unitTests ?? _lastFired.ToObservable())
                .Where(t => t != null)//hmm sometimes this happens for various reasons
                .Do(tests =>
                {
                    startedAt = Stopwatch.StartNew();
                    _lastFired = tests;
                    $"{tests.Length} Tests about to run".LogDebug(++iteration);
                    startedAt.Restart();

                    foreach (var test in tests)
                    {
                        if (!theBfg.FocusedTest.IsNullOrWhitespace())
                        {
                            test.RunThisTest = FocusedTest;
                        }

                        _testCluster.Handle(test).Until(); //todo: fix hanging resource
                    }
                }).Until();
        }

        public static IDisposable DoWorkContiniously(IRxnManager<IRxn> rxnManage, IObservable<StartUnitTest[]> unitTestToRun, Action fireStyle)
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
                .FinallyR(() =>
                {
                    "Continuous mode is stopping".LogDebug();
                })
            .Until();
        }

        public static IDisposable WatchForCompletion(IRxnManager<IRxn> rxnManage, IObservable<StartUnitTest[]> unitTestToRun, Action onComplete)
        {
            return unitTestToRun.Select(tests =>
            {
                var stopDOingWork = rxnManage.CreateSubscription<CommandResult>()
                    .Where(_ => unitTestToRun != null)
                    .Where(c => c.InResponseTo.BasicallyEquals(tests[0].Id)) //todo this this, should look at all unit tests
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

        public static void FireCompeteRapidly(IObservable<StartUnitTest[]> unitTestToRun)
        {
            Enumerable.Range(0, Environment.ProcessorCount).ForEach(_ =>
            {
                SpawnWorker();
            });

            Fire(unitTestToRun);
        }

        public static IObservable<bfgWorker> SpawnWorker()
        {
            return TestArenaWorkerManager.SpawnTestWorker(bfgTagWorkflow.GetTagsFromString(theBfg.Args.ToStringEach(" ")).ToArray());
        }


        public static void FireRapidly(IObservable<StartUnitTest[]> tests)
        {
            Enumerable.Range(0, Environment.ProcessorCount).ToObservable().ObserveOn(NewThreadScheduler.Default).Do(_ => { Fire(tests); }).Until();
        }

        public static IObservable<bfgWorker> StartRapidWorkers(IObservable<StartUnitTest[]> unitTestToRun)
        {
            return unitTestToRun.SelectMany(tests => Enumerable.Range(0, Environment.ProcessorCount).ToObservable().SelectMany(_ => TestArenaWorkerManager.SpawnTestWorker()));
        }

        public static void WatchForTestUpdates(IRxnManager<IRxn> rxnManage)
        {
            rxnManage.CreateSubscription<UpdateSystemCommand>()
                .Do(_ => { _notUpdating = false; })
                .Until();
        }


        private static int iteration = 0;
        private static Stopwatch startedAt = new Stopwatch();


        public static IDisposable StartTestArena(string[] args, IObservable<StartUnitTest[]> testCfg, bfgCluster cluster, bfgWorkerManager workerManager, IRxnManager<IRxn> toFireOn, IAppServiceDiscovery discovery)
        {
            "Starting up TestArena".LogDebug();

            if (_started.HasValue)
            {
                _started = DateTime.Now;
            }

            var stopAdvertising = bfgTestArenaApi.AdvertiseForWorkers(discovery, "all", $"http://{RxnApp.GetIpAddress()}:888");

            theBfg.Use(cluster,workerManager);

            var stopWorkers = Disposable.Empty;
            if (args.Any(a => a.BasicallyEquals("fire")))
            {
                stopWorkers = theBfg.StartTestArenaWorkers(args, testCfg).Until();
            }

            var stopFiring = StartFiringWorkflow(args, testCfg, toFireOn);
            

            return new CompositeDisposable(stopAdvertising, stopWorkers, stopFiring);
        }

        public IDisposable LaunchUnitTests(string[] args, IObservable<StartUnitTest[]> allUnitTests, IResolveTypes resolver)
        {
            var stopAutoLaunchingTestsIntoArena = LaunchUnitTestsToTestArenaDelayed(allUnitTests, GetTestarenaUrlFromArgs(args), resolver.Resolve<IAppStatusCfg>()).Until();

            var broadCaste = allUnitTests.FirstAsync()
                .Do(unitTests => BroadcasteStatsToTestArena(resolver.Resolve<IManageReactors>()))
                .Until();

            return new CompositeDisposable(stopAutoLaunchingTestsIntoArena, broadCaste);
        }

        public static string GetTestarenaUrlFromArgs(string[] args)
        {
            var url = args.FirstOrDefault(w => w.StartsWith('@')).IsNullOrWhiteSpace("http://localhost:888").TrimStart('@');

            return url;
        }

        public static void Use(bfgCluster testCluster, bfgWorkerManager workerManager)
        {
            if (testCluster != null)
                _testCluster = testCluster;

            if (workerManager != null)
                TestArenaWorkerManager = workerManager; // new bfgWorkerManager(_testCluster);
        }

        public static IObservable<Unit> StartTestArenaWorkers(string[] args, IObservable<StartUnitTest[]> unitTestToRun)
        {

            Action<string> setInfo = (testsInQueue) =>
            {
                _info = () =>
                {
                    return new[]
                    {
                        unitTestToRun == null
                            ? new AppStatusInfo("Test", $"Running {testsInQueue} tests")
                            : new AppStatusInfo("Test", $"Waiting for work"),
                        new AppStatusInfo("Duration", (DateTime.Now - (_started ?? DateTime.Now)).TotalMilliseconds),

                    };
                };
            };

            setInfo(0.ToString());

            //todo: need to fix ordering of services, this needs to start before the appstatusservicce otherwise it will miss the infoprovdiderevent
            _testCluster.Publish(new AppStatusInfoProviderEvent()
            {
                Info = _info
            });

            if (args.Contains("rapidly"))
            {
                return StartRapidWorkers(unitTestToRun).Select(_ => new Unit());
            }

            if (args.Contains("fire") || args.Contains("launch"))
            {
                return SpawnWorker().Select(_ => new Unit());
            }

            return new Unit().ToObservable();
        }


        /// <summary>
        /// todo: fix this method, it should use a delay, thats a hack!
        /// </summary>
        /// <param name="unitTestToRun"></param>
        /// <param name="testArenaAddress"></param>
        /// <param name="cfg"></param>
        /// <returns></returns>
        public static IObservable<string> LaunchUnitTestsToTestArenaDelayed(IObservable<StartUnitTest[]> unitTestToRun, string testArenaAddress, IAppStatusCfg cfg)
        {
            return unitTestToRun.Delay(TimeSpan.FromSeconds(1.5))
                .SelectMany(t => t)
                .SelectMany(runTheseUnitTest => LaunchAppToTestArena(runTheseUnitTest.UseAppUpdate, runTheseUnitTest.UseAppVersion, runTheseUnitTest.Dll, testArenaAddress, cfg))
                ;
        }

        public static IObservable<string> LaunchAppToTestArena(string appName, string appVersion, string appDll, string testArenaAddress, IAppStatusCfg cfg)
        {
            return RxnApps.CreateAppUpdate(
                    appName,
                    Scrub(appVersion),
                    new FileInfo(appDll).DirectoryName,
                    false,
                    cfg,
                    testArenaAddress,
                    new string[] {DataDir}
                )
                .Catch<Unit, Exception>(
                    e =>
                    {
                        ReportStatus.Log.OnError("Could not upload test. Cant join cluster :(", e);
                        throw e;
                })
                .Select(_ => appVersion);
        }

        /// <summary>
        /// Implements the firing sematics of the bfg, reponding to rapidly condintiously etc
        /// </summary>
        /// <param name="args"></param>
        /// <param name="unitTestToRun"></param>
        /// <param name="rxnManager"></param>
        /// <param name="resolver"></param>
        /// <returns></returns>
        public static IDisposable StartFiringWorkflow(string[] args, IObservable<StartUnitTest[]> unitTestToRun, IRxnManager<IRxn> rxnManager)
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
                        FireCompeteRapidly(unitTestToRun);
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

        private static string Scrub(string useAppVersion)
        {
            return new[] { "rapidly", "continuously", "fire" }.FirstOrDefault(i => i == useAppVersion) == null
                ? useAppVersion
                : null;
        }

      

        /// <summary>
        /// todo: fix issue with not broadcasting all test stats to appstatus, only first time
        /// </summary>
        /// <param name="testCluster"></param>
        /// <param name="unitTestToRun"></param>
        /// <param name="publusher"></param>
        /// <param name="reactorMgr"></param>
        private void BroadcasteStatsToTestArena(IManageReactors reactorMgr)
        {
            var bfgReactor = reactorMgr.GetOrCreate("bfg").Reactor;
            //need to fix health which will allow this to be viewed on the appstatus portal. should monitor health of fanout stratergy
            //
            //IMonitorActionFactory<IRxn> health =MonitorHealth
            RxnCreator.MonitorHealth<IRxn>(bfgReactor, "theBFG", out var _before, () => Rxn.Empty()).SelectMany(bfgReactor.Output).Until();


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

        //todo: write unit test for this
        public static IObservable<UnitTestDiscovered> DiscoverUnitTests(string testDllSelector, string[] args, Func<ITestArena[]> arenas)
        {
            return Rxn.Create<UnitTestDiscovered>(o =>
            {
                return GetTargets(testDllSelector, args, null, null, null)
                    .Do(t =>
                    {
                        ListTests(t.Dll, arenas)
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
            .Publish()
            .RefCount();
        }

        private static string[] frameworkFileExclusions = new[] { "packages/", "obj/", "packages\\", "obj\\", ".TestPlatform.", ".nunit.", "testhost.dll", "\\publish\\", "/publish/", theBfg.DataDir, "%%" };
        public static bfgWorkerManager TestArenaWorkerManager;
        private static StartUnitTest[] _lastFired;
        private Action<IRxn> _publish;

        private static bool NotAFrameworkFile(string file)
        {
            return !frameworkFileExclusions.Any(e => file.BasicallyContains(e));
        }

        public static IDisposable ExitAfter(StartUnitTest[] testsToWatch, IObservable<UnitTestResult> testResults)
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

        public static string GetTestSuiteDir(StartUnitTest work)
        {
            return Path.Combine(theBfg.DataDir, "TestSuites",  work.UseAppUpdate, $"{work.UseAppUpdate}%%{work.UseAppVersion}").AsCrossPlatformPath();
        }

        public static string GetTestSuiteDir(string systemName, string version)
        {
            return Path.Combine(theBfg.DataDir, "TestSuites", systemName, $"{systemName}%%{version}").AsCrossPlatformPath();
        }

        public IObservable<CommandResult> Handle(FocusOn command)
        {
            theBfg.FocusedTest = command.TestName;

            return CommandResult.Success().AsResultOf(command).ToObservable();

        }

        public IObservable<CommandResult> Handle(StopFocusing command)
        {
            theBfg.FocusedTest = null;

            return CommandResult.Success().AsResultOf(command).ToObservable();
        }

        public IObservable<CommandResult> Handle(DiscoverUnitTests command)
        {
            return Rxn.Create<CommandResult>(o =>
            {
                return DiscoverUnitTests(command.TestDllOrPattern, new string[0], _testArena)
                    .Do(t => _publish(t))
                    .LastOrDefaultAsync().Select(_ => CommandResult.Success().AsResultOf(command))
                    .Subscribe(o);
            });
        }

        public theBfg(Func<ITestArena[]> testArena)
        {
            _testArena = testArena;
        }

        public void ConfigiurePublishFunc(Action<IRxn> publish)
        {
            _publish = publish;
        }

        public static string GetSearchPatternFromArgs(string[] args)
        {
            return GetDllFromArgs(args);
        }
    }

    public static class XplatExtensions
    {
    }
}