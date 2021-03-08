﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rxns;
using Rxns.Cloud;
using Rxns.Cloud.Intelligence;
using Rxns.Collections;
using Rxns.DDD;
using Rxns.DDD.CQRS;
using Rxns.Health;
using Rxns.Health.AppStatus;
using Rxns.Hosting;
using Rxns.Hosting.Updates;
using Rxns.Interfaces;
using Rxns.Logging;
using Rxns.Metrics;
using Rxns.NewtonsoftJson;
using Rxns.WebApiNET5.NET5WebApiAdapters;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public static class theBFGDef
    {
        public static StartUnitTest[] Cfg;
        private static int _workerCount;
        
        public static Func<string[], Action<IRxnLifecycle>> TestArena = (args) =>
        {
            RxnExtensions.DeserialiseImpl = (t, json) => JsonExtensions.FromJson(json, t);
            RxnExtensions.SerialiseImpl = (json) => JsonExtensions.ToJson(json);
            
            return dd =>
            {
                if (Cfg != null)
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
                    .CreatesOncePerApp<UseDeserialiseCodec>();


            };
        };

        public static Func<string, string, Action<IRxnLifecycle>, Action<IRxnLifecycle>> TestWorker = (apiName, testHostUrl, d) =>
        {
            return dd =>
            {
                d(dd);
                dd.CreatesOncePerApp<SsdpDiscoveryService>()
                .CreatesOncePerApp<bfgCluster>()
                .RespondsToSvcCmds<StartUnitTest>()
                .CreatesOncePerApp<AppStatusClientModule>()
                .CreatesOncePerApp<NestedInAppDirAppUpdateStore>()
                .CreatesOncePerApp<DotNetTestArena>()
                .CreatesOncePerApp(_ => new RxnDebugLogger("bfgWorker"))
                .CreatesOncePerApp(_ => new AppServiceRegistry()
                {
                    AppStatusUrl = testHostUrl
                })
                .CreatesOncePerApp(_ => new DynamicStartupTask((log, resolver) =>
                {
                    SpawnTestWorker(resolver);
                }));
            };
        };

        private static int workerId;

        public static bfgWorker SpawnTestWorker(IResolveTypes resolver)
        {
            "Spawning worker".LogDebug(++workerId);

            var testCluster = resolver.Resolve<bfgCluster>();
            var rxnManager = resolver.Resolve<IRxnManager<IRxn>>();

            $"Streaming logs".LogDebug();
            rxnManager.Publish(new StreamLogs(TimeSpan.FromMinutes(60))).Until();

            $"Starting worker".LogDebug();
            Interlocked.Increment(ref _workerCount);

            var testWorker = new bfgWorker($"TestWorker#{_workerCount}", "local", resolver.Resolve<IAppServiceRegistry>(), resolver.Resolve<IAppServiceDiscovery>(), resolver.Resolve<IZipService>(), resolver.Resolve<IAppStatusServiceClient>(), resolver.Resolve<IRxnManager<IRxn>>(), resolver.Resolve<IUpdateServiceClient>(), resolver.Resolve<ITestArena>());

            if (Cfg == null)
                testWorker.DiscoverAndDoWork();

            testCluster.Process(new WorkerDiscovered<StartUnitTest, UnitTestResult>() { Worker = testWorker }).SelectMany(e => rxnManager.Publish(e)).Until();

            return testWorker;
        }
    }
}