using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rxns;
using Rxns.Cloud;
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
        public static StartUnitTest Cfg;
        private static int _workerCount;
        
        public static Func<StartUnitTest, string[], Action<IRxnLifecycle>> TestServer = (cfg, args) =>
        {
            RxnExtensions.DeserialiseImpl = (t, json) => JsonExtensions.FromJson(json, t);
            RxnExtensions.SerialiseImpl = (json) => JsonExtensions.ToJson(json);

            Cfg = cfg;

            return dd =>
            {
                //d(dd);
                dd.CreatesOncePerApp<theBfg>()
                    .CreatesOncePerApp<bfgCluster>()
                    .CreatesOncePerApp<SsdpDiscoveryService>()
                    .Includes<RxnsModule>()
                    .Includes<AppStatusClientModule>()
                    .Includes<AspNetCoreWebApiAdapterModule>()
                    .CreatesOncePerApp<bfgWorkerDoWorkOrchestrator>()
                    .CreatesOncePerApp(_ => theBFGDef.Cfg ?? new StartUnitTest(){Dll = "sda"})
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
            return dd =>
            {
                d(dd);
                dd.CreatesOncePerApp<SsdpDiscoveryService>()
                .CreatesOncePerApp<bfgCluster>()
                .CreatesOncePerApp<AppStatusClientModule>()
                .CreatesOncePerApp(_ => new AppServiceRegistry()
                {
                    AppStatusUrl = testHostUrl
                })
                .CreatesOncePerApp(_ => new DynamicStartupTask((log, resolver) =>
                {
                    SpawnTestaWorker(resolver);
                }));
            };
        };

        public static bfgWorker SpawnTestaWorker(IResolveTypes resolver)
        {
            $"Starting worker".LogDebug();
            Interlocked.Increment(ref _workerCount);

            var testWorker = new bfgWorker($"TestWorker#{_workerCount}", "local", resolver.Resolve<IAppServiceRegistry>(), resolver.Resolve<IAppServiceDiscovery>(), resolver.Resolve<IRxnManager<IRxn>>(), resolver.Resolve<IUpdateServiceClient>());

            if (Cfg == null)
                testWorker.DiscoverAndDoWork();

            return testWorker;
        }
    }
}
