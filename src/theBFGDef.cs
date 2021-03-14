using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections;
using Rxns;
using Rxns.Cloud;
using Rxns.DDD;
using Rxns.Health.AppStatus;
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
    //todo:
    //need to push bfg to nuget
    //need to push rxns webapi to nuget
    //need to allow the webapi to startup in isolation or with config options to turn off rxns services, allow appstatus portal to be overriden?
    public static class theBFGDef
    {
        public static StartUnitTest[] Cfg;

        public static Func<string[], Action<IRxnLifecycle>> TestArena = (args) =>
        {
            RxnExtensions.DeserialiseImpl = (t, json) => JsonExtensions.FromJson(json, t);
            RxnExtensions.SerialiseImpl = (json) => JsonExtensions.ToJson(json);

            return dd =>
            {
                //DistributedBackingChannel.For(typeof(ITestDomainEvent))(dd);
                if (Cfg.AnyItems())
                    dd.CreatesOncePerApp(_ => Cfg[0]);

                dd.CreatesOncePerApp(_ => Cfg);

                //d(dd);
                dd.CreatesOncePerApp<theBfg>()
                    .CreatesOncePerApp<bfgCluster>()
                    .CreatesOncePerApp<SsdpDiscoveryService>()
                    .Includes<RxnsModule>()
                    .Includes<AppStatusClientModule>()
                    .CreatesOncePerApp<NestedInAppDirAppUpdateStore>()
                    .Includes<AspNetCoreWebApiAdapterModule>()
                    .CreatesOncePerApp<bfgWorkerDoWorkOrchestrator>()
                    .CreatesOncePerApp<DotNetTestArena>()
                    .CreatesOncePerApp<bfgTestArenaProgressView>()
                    .RespondsToSvcCmds<Reload>()

                    .Emits<UnitTestResult>()
                    .Emits<UnitTestPartialResult>()
                    .Emits<UnitTestPartialLogResult>()
                    .CreatesOncePerApp<RxnManagerCommandService>() //fixes svccmds
                    .CreatesOncePerApp(_ => new AspnetCoreCfg()
                    {
                        Cfg = aspnet =>
                        {
                            aspnet.UseEndpoints(e =>
                            {
                                e.MapHub<bfgTestArenaProgressHub>("/testArena", o =>
                                {
                                    o.Transports =
                                        HttpTransportType.WebSockets |
                                        HttpTransportType.LongPolling;
                                });
                            });
                        }
                    })
                    //cfg specific
                    .CreatesOncePerApp(() => new AggViewCfg()
                    {
                        ReportDir = "reports"
                    })
                    .CreatesOncePerApp(() => new AppServiceRegistry()
                    {
                        AppStatusUrl = $"http://{RxnApp.GetIpAddress()}:888"
                    })
                    .CreatesOncePerApp(_ => new RxnDebugLogger("bfgCluster"))
                    .CreatesOncePerApp<INSECURE_SERVICE_DEBUG_ONLY_MODE>()
                    .CreatesOncePerApp<UseDeserialiseCodec>()
                    .CreatesOncePerApp(_ => new DynamicStartupTask((log, resolver) =>
                    {
                        var theBfg = new theBfg();
                        var stopArena = theBfg.StartTestArena(args, Cfg, resolver);
                        var stopWorkers = theBfg.StartTestArenaWorkers(args, Cfg, resolver).Until();
                    }));
                ;
            };
        };

        public static Func<string, string, Action<IRxnLifecycle>, Action<IRxnLifecycle>> TestWorker = (apiName, testHostUrl, d) =>
        {
            return dd =>
            {
                d(dd);

                dd.CreatesOncePerApp<SsdpDiscoveryService>()
                    .CreatesOncePerApp<TaggedServiceRxnManagerRegistry>()
                    .CreatesOncePerApp<bfgCluster>()
                    .RespondsToSvcCmds<StartUnitTest>()
                    .CreatesOncePerApp<AppStatusClientModule>()
                    .CreatesOncePerApp<NestedInAppDirAppUpdateStore>()
                    .CreatesOncePerApp<DotNetTestArena>()
                    .Emits<UnitTestResult>()
                    .Emits<UnitTestPartialResult>()
                    .Emits<UnitTestPartialLogResult>()
                    .CreatesOncePerApp(_ => new RxnDebugLogger("bfgWorker"))
                    .CreatesOncePerApp(_ => new AppServiceRegistry()
                    {
                        AppStatusUrl = testHostUrl
                    })
                    .CreatesOncePerApp(_ => new DynamicStartupTask((log, resolver) =>
                    {
                        var theBfg = new theBfg();
                        var stopWorkers = theBfg.StartTestArenaWorkers(theBfg.Args, Cfg, resolver).Until();
                    }));
                
                DistributedBackingChannel.For(typeof(ITestDomainEvent))(dd);
            };
        };
    }
}
