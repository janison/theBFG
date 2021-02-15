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

        public static Func<StartUnitTest, Action<IRxnLifecycle>, Action<IRxnLifecycle>> TestAgent = (cfg, d) =>
        {
            theBfg.testcfg = cfg;
            return dd =>
            {
                d(dd);
                dd.CreatesOncePerApp<theBfg>();
            };
        };

        public static IObservable<IRxnAppContext> Reload(string url = "http://192.168.1.2:888/", params string[] args)
        {
            return Rxns.Rxn.Create<IRxnAppContext>(o =>
            {
                //todo: fix rapid fire mode which uses the cluster host to create parallel processes
                // if (args.Contains("reactor"))
                // {
                //     OutOfProcessFactory.CreateNamedPipeClient(args.SkipWhile(a => a != "reactor").Skip(1).FirstOrDefault() ?? "spare");
                // }
                // else
                // {
                //     OutOfProcessFactory.CreateNamedPipeServer();
                // }

                //setup static object.Serialise() & string.Deserialise() methods
                RxnExtensions.DeserialiseImpl = (t, json) => JsonExtensions.FromJson(json, t);
                RxnExtensions.SerialiseImpl = (json) => JsonExtensions.ToJson(json);

                var cfg = RxnAppCfg.Detect(args);
                var testCfg = theBigCfg.Detect();

                var dll = args.Skip(0).FirstOrDefault().IsNullOrWhiteSpace(testCfg.Dll); // ??  "/Users/janison/replay/windows/JanisonReplay.Integration.Tests/bin/Debug/netcoreapp3.1/JanisonReplay.Integration.Tests.dll"; 
                var testName = args.Skip(1).FirstOrDefault().IsNullOrWhiteSpace(testCfg.RunThisTest);
                var appUpdateDllSource = args.Skip(2).FirstOrDefault().IsNullOrWhiteSpace(testCfg.UseAppUpdate);
                var appUpdateVersion = args.Skip(3).FirstOrDefault().IsNullOrWhiteSpace(testCfg.UseAppVersion);
                url = args.Skip(4).FirstOrDefault().IsNullOrWhiteSpace(url).IsNullOrWhiteSpace(testCfg.AppStatusUrl)
                    .IsNullOrWhiteSpace(cfg.AppStatusUrl);


                if (dll.IsNullOrWhitespace())
                {
                    o.OnError(new Exception(
                        $"'{dll}','{testName}','{appUpdateDllSource}','{appUpdateVersion}','{url}'\\r\\n" +
                        "Usage: {testDll} {testName|all} {appUpdateTestDllSource} {appUpdateTestDllSourceVersion} {appStatusUrl}"));
                    return Disposable.Empty;
                }

                return theBfg.TestAgent(new StartUnitTest()
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
                    .Do(app =>
                    {
                        $"Heartbeating to {url}".LogDebug();
                        app.RxnManager.Publish(new PerformAPing()).Until();

                        $"Streaming logs".LogDebug();
                        app.RxnManager.Publish(new StreamLogs(TimeSpan.FromMinutes(60))).Until();
                    })
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

        private void Start()
        {
            var unitTestToRun = testcfg;
            _started = DateTime.Now;

            var testCluster = new bfgCluster();
            
            //todo: fix clustering mode
            var testWorker = new bfgWorker("TestWorker#1", "local", _testUpdateProvider);

            testCluster.Process(new WorkerDiscovered<StartUnitTest, UnitTestResult>() {Worker = testWorker}).WaitR();
            testCluster.Queue(unitTestToRun);

            BoardcastStatsToAppStatus(testCluster, unitTestToRun);
        }

        private void BoardcastStatsToAppStatus(bfgCluster testCluster, StartUnitTest unitTestToRun)
        {
            _info = () => new[]
            {
                new AppStatusInfo("Test", $"Running {(unitTestToRun.RunAllTest ? "All" : unitTestToRun.RunThisTest)}"),
                new AppStatusInfo("Duration", (DateTime.Now - _started).TotalMilliseconds),
                new AppStatusInfo("Workers", testCluster.Workers.Count)
            };

            _info();
        }

        public void Dispose()
        {
            TestRunner.Dispose();
        }
    }
}