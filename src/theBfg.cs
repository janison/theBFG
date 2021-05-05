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
using Rxns.Cloud.Intelligence;
using Rxns.Collections;
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

/// <summary>
///
/// Backlog
///
///
/// 
/// todo:
///
///         - support focus mode, where a single test can be given focus and only CI happens on that 1 test, even though the whole dll is built
///         - add visal effect to test table that quickly flashes the test as it launches after watching
///                 - make gun glow with a flash to simulate firing
///         -fix not launching if **.test.dll is used. need to use gettargets istead
///         - fix issue with remote worker tests not run due to not being uploaded to appstatus correctly when using wildcards *.tests.dll
///                 - need to fix download to remote worker and downloading to correct dir also
/// 
///         - need to fix packing, dont think correct app is being deployed - sorry everyone! u can get dist from the main repo
///         - allow settings to be configured via UI and saved between restarts
///             - html5 storage? or .cfg file?
///             - lights out
///             - threadholds for slow for flakey tests
///             - cmd history?
///         - show discovered or new tests when tests topic selected
///                - need to work out how to deal with multiple test-disvcovered.. should alert on a diff? 
///         - allow way to use servicecmd to launch a worker in a new processes
///         - thebfg target all // monitors dirs and auto-executes 
///   
///         -   allow workers to be associated with tags
/// -           -   allow targeting of tests at specific tag'd workers with startunittest
/// 
///         - add worker meta info to graph
///             - ip, os, threads, status, hostname
///             - indicate worker count per host
///             - indicate active work on each worker?
///                 - visually show the gun firing with a glow outline, then trigger a sound also
///                  - play sounds on pass or fail of test suite so you dont need to switch windows
/// 
///             - need to fix saving / persistance historical data.
///                 - add a "history" flag to the playback allow the UI to skip/know these are historical so it doesnt double count                 
///                 - use "save" keyword to indicate filesystem mode instead of inmemory?
///
///
/// </summary>
namespace theBFG
{
    /// <summary>
    /// Unlike the Gatling Gun, the Bfg was always the top weapon in the games I played. It was accurate, responsive, rewarding to master and had your back in every situation.
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
    public class theBfg : IDisposable, IServiceCommandHandler<Reload>
    {
        public static ISubject<Unit> IsCompleted = new ReplaySubject<Unit>(1);
        public static IDisposable TestRunner { get; set; }
        public static string[] Args = new string[0];

        private static Func<AppStatusInfo[]> _info = () => new AppStatusInfo[0];
        private static DateTime? _started;
        private static bfgCluster _testCluster;
        private static bool  _notUpdating = true;

        public static string DataDir = ".bfg";
        
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
                    return e;
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
                foreach (var dll in Directory.GetFileSystemEntries(dir, filePattern, SearchOption.AllDirectories).Select(d => d.AsCrossPlatformPath()).Where(NotAFrameworkFile))
                {
                    GetTargetsFromDll(args, dll, arena).Do(t => o.OnNext(t)).Until();
                }

                return watchers;
            });
        }


        public static string GetAppVersionFromDllSyntax(string[] args, string dll)
        {
            var testCfg = theBigCfg.Detect();

            var appUpdateDllSource = args.Skip(4).FirstOrDefault().IsNullOrWhiteSpace(testCfg.UseAppVersion);
            
            return appUpdateDllSource.IsNullOrWhiteSpace(DateTime.Now.ToString("s").Replace(":", ""));
        }

        public static string GetAppNameFromDllSyntax(string dll)
        {
            var testCfg = theBigCfg.Detect();

            var appUpdateDllSource = dll == null ? null : dll.Contains("@") ? dll.Split('@').Reverse().FirstOrDefault().IsNullOrWhiteSpace(testCfg.RunThisTest) : null;
            var dlld = new FileInfo(dll);

            return appUpdateDllSource.IsNullOrWhiteSpace(dlld.Name.Split(dlld.Extension).FirstOrDefault().TrimEnd('.'));
        }

        public static IObservable<StartUnitTest> GetTargetsFromDll(string[] args, string dll, Func<ITestArena[]> arena)
        {
            var appUpdateDllSource = GetAppNameFromDllSyntax(dll);
            var testName = GetTestNameFromDllSyntax(dll);
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

        private static string GetTestNameFromDllSyntax(string dll)
        {
            var testCfg = theBigCfg.Detect();

            return dll == null ? string.Empty : dll.Contains("$") ? dll.Split('$').Reverse().FirstOrDefault().IsNullOrWhiteSpace(testCfg.RunThisTest) : null;
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
            var testCfg = theBigCfg.Detect();
            var appsyntax = args.Skip(1).FirstOrDefault().IsNullOrWhiteSpace(testCfg.Dll)?.Split('$')[0].AsCrossPlatformPath();

            return appsyntax;
        }

        public static IDisposable Fire(IObservable<StartUnitTest[]> unitTests = null)
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
                .FinallyR(() =>
                {
                    "Continuous mode is stopping".LogDebug();
                })
            .Until();
        }

        public IDisposable WatchForCompletion(IRxnManager<IRxn> rxnManage, IObservable<StartUnitTest[]> unitTestToRun, Action onComplete)
        {
            return unitTestToRun.Select(tests =>
            {
                var stopDOingWork = rxnManage.CreateSubscription<CommandResult>()
                    .Where(_ => unitTestToRun != null)
                    .Where(c => c.InResponseTo.BasicallyEquals(tests[0].Id))
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

        public static void FireCompeteRapidly(IObservable<StartUnitTest[]> unitTestToRun, IResolveTypes resolver)
        {
            Enumerable.Range(0, Environment.ProcessorCount).ForEach(_ =>
            {
                SpawnTestWorker(resolver, unitTestToRun).Until();
            });

            Fire(unitTestToRun);
        }


        public static void FireRapidly(IObservable<StartUnitTest[]> tests)
        {
            Enumerable.Range(0, Environment.ProcessorCount).ToObservable().ObserveOn(NewThreadScheduler.Default).Do(_ => { Fire(tests); }).Until();
        }

        public static IObservable<bfgWorker> StartRapidWorkers(IResolveTypes resolver, IObservable<StartUnitTest[]> unitTestToRun)
        {
            return unitTestToRun.SelectMany(tests => Enumerable.Range(0, Environment.ProcessorCount).ToObservable().SelectMany(_ => SpawnTestWorker(resolver, unitTestToRun)));
        }

        public static void WatchForTestUpdates(IRxnManager<IRxn> rxnManage)
        {
            rxnManage.CreateSubscription<UpdateSystemCommand>()
                .Do(_ => { _notUpdating = false; })
                .Until();
        }


        private static int iteration = 0;
        private static Stopwatch startedAt = new Stopwatch();


        public static IDisposable StartTestArena(IResolveTypes resolver)
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
            var stopAutoLaunchingTestsIntoArena = LaunchUnitTestsToTestArenaDelayed(allUnitTests, GetTestarenaUrlFromArgs(args), resolver.Resolve<IAppStatusCfg>()).Until();
            var stopFiring = StartFiringWorkflow(args, allUnitTests, resolver.Resolve<IRxnManager<IRxn>>(), resolver);

            var broadCaste = allUnitTests.FirstAsync()
                .Do(unitTests => BroadcasteStatsToTestArena(_testCluster, unitTests[0], resolver.Resolve<SystemStatusPublisher>(), resolver.Resolve<IManageReactors>()))
                .Until();

            return new CompositeDisposable(stopAutoLaunchingTestsIntoArena, stopFiring, broadCaste);
        }

        public static string GetTestarenaUrlFromArgs(string[] args)
        {
            var url = args.FirstOrDefault(w => w.StartsWith('@')).IsNullOrWhiteSpace("http://localhost:888").TrimStart('@');

            return url;
        }

        public static IObservable<bfgWorker> StartTestArenaWorkers(string[] args, IObservable<StartUnitTest[]> unitTestToRun, IResolveTypes resolver)
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

        private static string Scrub(string useAppVersion)
        {
            return new[] { "rapidly", "continuously", "fire" }.FirstOrDefault(i => i == useAppVersion) == null
                ? useAppVersion
                : null;
        }

        private static int _workerCount;
        private static StartUnitTest[] _lastFired = new StartUnitTest[0];

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

            if (cfg == null)
            {
                testWorker.DiscoverAndDoWork();
                return testWorker.ToObservable();
            }

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
            RxnCreator.MonitorHealth<IRxn>(bfgReactor, "theBFG", out var _before, () => Rxn.Empty()).SelectMany(bfgReactor.Output).Until();

            _info = () =>
            {
                return new[]
                {
                    unitTestToRun == null
                        ? new AppStatusInfo("Test", $"Running {unitTestToRun.Dll}")
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
            return arenas().SelectMany(a => a.ListTests(testDll)).FirstAsync(w => w.AnyItems()).Select(d => d.Where(NotAFrameworkFile));
        }

        public static IObservable<UnitTestDiscovered> DiscoverUnitTests(string testDllSelector, string[] args, Func<ITestArena[]> arenas)
        {
            return Rxn.Create<UnitTestDiscovered>(o =>
            {
                return GetTargets(testDllSelector, args, null, null, null)
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
            .Publish()
            .RefCount();
        }

        private static string[] frameworkFileExclusions = new[] { "packages/", "obj/", "packages\\", "obj\\", ".TestPlatform.", ".nunit.", "testhost.dll", "\\publish\\", "/publish/", theBfg.DataDir, "%%" };
        private static bool NotAFrameworkFile(string file)
        {
            return !frameworkFileExclusions.Any(e => file.BasicallyContains(e));
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

        public static string GetTestSuiteDir(StartUnitTest work)
        {
            return Path.Combine(work.UseAppUpdate, $"{work.UseAppUpdate}%%{work.UseAppVersion}").AsCrossPlatformPath();
        }
    }

    public static class XplatExtensions
    {
    }
}