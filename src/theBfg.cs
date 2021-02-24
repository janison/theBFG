using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Autofac;

using RxnCreate;
using Rxns;
using Rxns.Cloud;
using Rxns.DDD.BoundedContext;
using Rxns.DDD.CQRS;
using Rxns.Health;
using Rxns.Hosting;
using Rxns.Hosting.Updates;
using Rxns.Interfaces;
using Rxns.Logging;
using Rxns.Metrics;
using Rxns.NewtonsoftJson;
using Rxns.WebApiNET5;
using Rxns.WebApiNET5.NET5WebApiAdapters;
using Rxns.WebApiNET5.NET5WebApiAdapters.RxnsApiAdapters;
using RxnsDemo.Micro.App.AggRoots;
using RxnsDemo.Micro.App.Api;
using RxnsDemo.Micro.App.Cmds;
using RxnsDemo.Micro.App.Events;
using RxnsDemo.Micro.App.Qrys;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public class theBfg : IContainerPostBuildService, IDisposable
    {
        private readonly IUpdateServiceClient _testUpdateProvider;
        private static StartUnitTest testcfg;
        public IDisposable TestRunner { get; set; }

        public static Func<StartUnitTest,  Action<IRxnLifecycle>> TestServer = (cfg) =>
        {
            RxnExtensions.DeserialiseImpl = (t, json) => JsonExtensions.FromJson(json, t);
            RxnExtensions.SerialiseImpl = (json) => JsonExtensions.ToJson(json);

            theBfg.testcfg = cfg;
            return dd =>
            {
                //d(dd);
                dd.CreatesOncePerApp<theBfg>()
                .CreatesOncePerApp<SsdpDiscoveryService>()
                .CreatesOncePerApp(_ => new DynamicStartupTask((log, resolver) =>
                {
                    "Starting up TestServer".LogDebug();
                    var rxnManager = resolver.Resolve<IRxnManager<IRxn>>();
                    
                    $"Heartbeating".LogDebug();
                    rxnManager.Publish(new PerformAPing()).Until();

                    var stopAdvertising = BfgTestApi.AdvertiseForWorkers(resolver.Resolve<SsdpDiscoveryService>(), "all", $"http://{RxnApp.GetIpAddress()}:888");
                    
                    //need to active webapi inside of this testserver
                    //need to test the appstatus worker tunnel
                    //need to push rxns webapi to nuget
                    //need to allow the webapi to startup in isolation or with config options to turn off rxns services, allow appstatus portal to be overriden?
                }))
                //cfg specific
                .CreatesOncePerApp(() => new AggViewCfg()
                {
                    ReportDir = "reports"
                })
                .CreatesOncePerApp(() => new AppServiceRegistry()
                {
                    AppStatusUrl = $"http://{RxnApp.GetIpAddress()}:888"
                })
                .CreatesOncePerApp<RxnDebugLogger>()
                .CreatesOncePerApp<INSECURE_SERVICE_DEBUG_ONLY_MODE>()
                .CreatesOncePerApp<UseDeserialiseCodec>();
            };
        };
        
        public static Func<string, string, StartUnitTest, Action<IRxnLifecycle>, Action<IRxnLifecycle>> TestWorker = (apiName, testHostUrl, testcfg, d) =>
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
                    
                    if (testcfg == null)
                        testWorker.DoWork(testcfg).Until();
                    else
                        testWorker.DiscoverAndDoWork();
                }));
            };
        };
        
        public static IObservable<Unit> ReloadWithTestWorker(string url = "http://192.168.1.2:888/", params string[] args)
        {
            return Rxn.Create<Unit>(o =>
            {
                var apiName = args.Skip(1).FirstOrDefault(); 
                var testHostUrl = args.Skip(2).FirstOrDefault().IsNullOrWhiteSpace("http://localhost:888");


                return theBfg.TestWorker(apiName, testHostUrl, DetectIfTargetMode(url, args), RxnApp.SpareReator(testHostUrl))   .ToRxns()
                    .Named(new ClusteredAppInfo("bfgWorker", "1.0.0", args, false))
                    .OnHost(new ConsoleHostedApp(), RxnAppCfg.Detect(args))
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
            var testName = args.Skip(2).FirstOrDefault().IsNullOrWhiteSpace(testCfg.RunThisTest);
            var appUpdateDllSource = args.Skip(3).FirstOrDefault().IsNullOrWhiteSpace(testCfg.UseAppUpdate);
            var appUpdateVersion = args.Skip(4).FirstOrDefault().IsNullOrWhiteSpace(testCfg.UseAppVersion);
            url = args.Skip(4).FirstOrDefault().IsNullOrWhiteSpace(url).IsNullOrWhiteSpace(testCfg.AppStatusUrl)
                .IsNullOrWhiteSpace(cfg.AppStatusUrl);

            return new StartUnitTest()
            {
                UseAppUpdate = appUpdateDllSource,
                UseAppVersion = appUpdateVersion,
                Dll = dll,
                RunThisTest = testName
            };
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
                        "launch sut@sut.dll | deploy auto-updated apps to worker nodes on demand via complementary C# api: theBfgApi.launch(\"app\", \"dir\")".LogDebug();
                        "".LogDebug();
                        "".LogDebug();
                        "<<USE WITH CAUTION>>".LogDebug();
                        "".LogDebug();
                        break;
                }
                
                return Disposable.Empty;
            });
        }

        public static IObservable<Unit> ReloadWithTestServer(string url = "http://192.168.1.2:888/", params string[] args)
        {
            return Rxn.Create<Unit>(o =>
            {
                ReportStatus.StartupLogger = ReportStatus.Log.ReportToConsole();

                "Configuring App".LogDebug();



                theBFGAspnetCoreAdapter.Appcfg = RxnAppCfg.Detect(args);
                return AspNetCoreWebApiAdapter.StartWebServices<theBFGAspnetCoreAdapter>( theBFGAspnetCoreAdapter.Cfg, args).ToObservable()
                    .LastAsync()
                    .Select(_ => new Unit())
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
            TestRunner?.Dispose();
        }
    }

    public class theBFGAspnetCoreAdapter : ConfigureAndStartAspnetCore
    {
        public static IRxnAppCfg Appcfg = null;
        public static IWebApiCfg Cfg = new WebApiCfg()
        {
            BindingUrl = "http://*:888",
            Html5IndexHtml = "index.html",
            Html5Root = @"C:\jan\Rxns\Rxns.AppSatus\Web\dist" // @"/Users/janison/rxns/Rxns.AppSatus/Web/dist/" //the rxns appstatus portal
        };

        public theBFGAspnetCoreAdapter()
        {
            var args = Appcfg?.Args ?? new string[0];
            var url = string.Empty;

            WebApiCfg = Cfg;
            AppInfo = new ClusteredAppInfo("bfgTestServer", "1.0.0", args, false);
            App = e =>
            {
                

                return theBfg.TestServer(theBfg.DetectIfTargetMode(url, args)); //, RxnApp.SpareReator(url)

            };
        }

        public override Func<string, Action<IRxnLifecycle>> App { get; }
        public override IRxnAppInfo AppInfo { get; }
        public override IWebApiCfg WebApiCfg { get; }
    }

    public class MicroServiceBoostrapperAspNetCore : ConfigureAndStartAspnetCore
    {
        public MicroServiceBoostrapperAspNetCore()
        {
        }

        public override Func<string, Action<IRxnLifecycle>> App { get; } = url => HostSurveyDomainFeatureModule(url);
        public override IRxnAppInfo AppInfo { get; } = new AppVersionInfo("Survey Micro Service", "1.0", true);

        public override IWebApiCfg WebApiCfg => Cfg;

        public static IWebApiCfg Cfg { get; set; } = new WebApiCfg()
        {
            BindingUrl = "http://*:888",
            Html5IndexHtml = "index.html",
            Html5Root = @"C:\jan\Rxns\Rxns.AppSatus\Web\dist"// @"/Users/janison/rxns/Rxns.AppSatus/Web/dist/" //the rxns appstatus portal
        };

        //todo:
        //create supervir which uses the CPU stats to suggest increasing the process count (scale signals)
        //such as max file handlers per process is around ~16m

        //log sample 20 as an appcommand should return the last 20 log messages
        //we can enable and disable logs via a static global prop
        public static Func<string, Action<IRxnLifecycle>> HostSurveyDomainFeatureModule = appStatusUrl => SurveyRoom =>
        {
            SurveyRoom
                //the services to the api
                .CreatesOncePerApp<SurveyAnswersDomainService>()
                //.CreatesOncePerApp(() => new SurveyProgressView(new DictionaryKeyValueStore<string, SurveyProgressModel>()))
                .CreatesOncePerApp<Func<ISurveyAnswer, string>>(_ => s => $"{s.userId}%{s.AttemptId}")
                .CreatesOncePerApp<TapeArrayTenantModelRepository<SurveyAnswers, ISurveyAnswer>>()
                //api
                .RespondsToCmd<BeginSurveyCmd>()
                .RespondsToCmd<RecordAnswerForSurveyCmd>()
                .RespondsToCmd<FinishSurveyCmd>()
                .RespondsToQry<LookupProgressInSurveyQry>()
                //events
                .Emits<UserAnsweredQuestionEvent>()
                .Emits<UserSurveyStartedEvent>()
                .Emits<UserSurveyEndedEvent>()
                //cfg specific
                .CreatesOncePerApp(() => new AggViewCfg()
                {
                    ReportDir = "reports"
                })
                .CreatesOncePerApp(() => new AppServiceRegistry()
                {
                    AppStatusUrl = "http://localhost:888"
                })
                .CreatesOncePerApp<RxnDebugLogger>()

                .CreatesOncePerApp<INSECURE_SERVICE_DEBUG_ONLY_MODE>()
                //setup OS abstractions
                //test sim to exercise api
                //.CreatesOncePerApp<Basic30UserSurveySimulation>()
                //serilaisation of models
                .CreatesOncePerApp<UseDeserialiseCodec>()
                //.CreatesOncePerApp(_ => new AutoScaleoutReactorPlan(new ScaleoutToEverySpareReactor(), "ReplayMacOS", version: "Latest"))
                .CreatesOncePerApp(_ => new DynamicStartupTask((r, c) =>
                {
           
                }))
                ;

            //setup static object.Serialise() & string.Deserialise() methods
            RxnExtensions.DeserialiseImpl = (t, json) => JsonExtensions.FromJson(json, t);
            RxnExtensions.SerialiseImpl = (json) => JsonExtensions.ToJson(json);
        };
    }
}