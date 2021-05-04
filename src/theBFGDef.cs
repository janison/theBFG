using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections;
using Rxns;
using Rxns.Cloud;
using Rxns.DDD;
using Rxns.DDD.Commanding;
using Rxns.Health;
using Rxns.Health.AppStatus;
using Rxns.Hosting;
using Rxns.Hosting.Updates;
using Rxns.Interfaces;
using Rxns.Logging;
using Rxns.Metrics;
using Rxns.NewtonsoftJson;
using Rxns.Playback;
using Rxns.WebApiNET5;
using Rxns.WebApiNET5.NET5WebApiAdapters;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public static class theBfgDef
    {
        public static IObservable<StartUnitTest[]> Cfg;

        public static string DetectTestFramework(string[] args)
        {
            return args.ToStringEach(",").Split("using")[1].Split(',')[1];
        }

        public static Func<string[], Action<IRxnLifecycle>> TestArena = (args) =>
        {
            RxnExtensions.DeserialiseImpl = (t, json) => JsonExtensions.FromJson(json, t);
            RxnExtensions.SerialiseImpl = (json) => JsonExtensions.ToJson(json);
            string ver = string.Empty;

            if (args.Contains("using"))
            {
                ver = DetectTestFramework(args);
            }

            return dd =>
            {
                //DistributedBackingChannel.For(typeof(ITestDomainEvent))(dd);
                if (Cfg != null)
                    dd.CreatesOncePerApp(_ => Cfg);

                dd.CreatesOncePerApp(_ => theBFGAspNetCoreAdapter.AspnetCfg);

                //d(dd);
                dd.CreatesOncePerApp<theBfg>()
                    .CreatesOncePerApp<bfgCluster>()
                    .CreatesOncePerApp<SsdpDiscoveryService>()
                    .Includes<RxnsModule>()
                    .Includes<AppStatusClientModule>()
                    .CreatesOncePerApp<NestedInAppDirAppUpdateStore>()
                    .Includes<AspNetCoreWebApiAdapterModule>()
                    .CreatesOncePerApp<bfgWorkerDoWorkOrchestrator>()
                    .CreatesOncePerApp<bfgTestArenaProgressView>()
                    .CreatesOncePerApp<bfgTestArenaProgressHub>()
                    .CreatesOncePerRequest<DotNetTestArena>()
                    .CreatesOncePerRequest<VsTestArena>()
                    .RespondsToSvcCmds<Reload>()
                    .RespondsToSvcCmds<StartUnitTest>()
                    .Emits<UnitTestsStarted>()
                    .Emits<UnitTestDiscovered>()
                    .Emits<UnitTestOutcome>()
                    .Emits<UnitTestResult>()
                    .Emits<UnitTestPartialResult>()
                    .Emits<UnitTestAssetResult>()
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
                    .CreatesOncePerApp(_ => new AppStatusCfg()
                    {
                        ShouldAutoUnzipLogs = true,
                        AppRoot = theBfg.DataDir
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
                        TimeSpan.FromSeconds(1).Then().Do(_ =>
                        {

                            if (theBfgDef.Cfg == null)
                                theBfgDef.Cfg = theBFG.theBfg.DetectAndWatchTargets(args, resolver.Resolve<Func<ITestArena[]>>(), resolver.Resolve<IServiceCommandFactory>(), resolver.Resolve<IRxnManager<IRxn>>().CreateSubscription<UnitTestResult>());
                            
                            var theBfg = resolver.Resolve<theBfg>();
                            var stopArena = theBfg.StartTestArena(resolver);
                            var stopWorkers = theBfg.StartTestArenaWorkers(args, Cfg, resolver).Until();


                            if (args.LastOrDefault().BasicallyEquals("exit") || args.LastOrDefault().BasicallyEquals("quit"))
                            {
                                var calledOnce = false;
                                Cfg = Cfg.Do(testInSession =>
                                {
                                    if(calledOnce) return;

                                    calledOnce = true;
                                    theBfg.ExitAfter(testInSession, resolver.Resolve<IRxnManager<IRxn>>().CreateSubscription<UnitTestResult>());
                                });
                            }

                            var searchPattern = args.Skip(1).FirstOrDefault();

                            if (args.FirstOrDefault().BasicallyEquals("launch"))
                            {
                                searchPattern = searchPattern ??  $"{Directory.GetCurrentDirectory()}\\*.test*.dll";
                            }
                            else
                            {
                                var launchUnitTests = theBfg.LaunchUnitTests(args, Cfg, resolver);
                            }

                            if (searchPattern != null)
                            {
                                $"Discovering unit tests with: {searchPattern}".LogDebug();
                                theBfg.DiscoverUnitTests(searchPattern, args, resolver.Resolve<Func<ITestArena[]>>())
                                    .SelectMany(t => resolver.Resolve<IRxnManager<IRxn>>().Publish(t))
                                    .Until();
                            }
                        }).Until();

                    }))
                    .CreatesOncePerApp<InMemoryTapeRepo>()
                    ;
                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    dd.CreatesOncePerApp<MacOSSystemInformationService>();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    dd.CreatesOncePerApp<WindowsSystemInformationService>();
            };
        };

        public static Func<string, string, Action<IRxnLifecycle>, Action<IRxnLifecycle>> TestWorker = (apiName, testHostUrl, d) =>
        {
            return dd =>
            {
                d(dd);

                dd
                    .CreatesOncePerApp<theBfg>()
                    .CreatesOncePerApp<SsdpDiscoveryService>()
                    .CreatesOncePerApp<TaggedServiceRxnManagerRegistry>()
                    .CreatesOncePerApp<bfgCluster>()
                    .RespondsToSvcCmds<StartUnitTest>()
                    .RespondsToSvcCmds<Target>()
                    .RespondsToSvcCmds<StartIntegrationTest>()
                    .CreatesOncePerApp<AppStatusClientModule>()
                    .CreatesOncePerApp<NestedInAppDirAppUpdateStore>()
                    .CreatesOncePerRequest<DotNetTestArena>()
                    .CreatesOncePerRequest<VsTestArena>()
                    .CreatesOncePerRequest<bfgHostResourceMonitor>()
                    .Emits<UnitTestsStarted>()
                    .Emits<UnitTestDiscovered>()
                    .Emits<UnitTestOutcome>()
                    .Emits<UnitTestResult>()
                    .Emits<UnitTestPartialResult>()
                    .Emits<UnitTestAssetResult>()
                    .Emits<UnitTestPartialLogResult>()
                    .Emits<UnitTestOutcome>()
                    .Emits<UnitTestsStarted>()
                    .CreatesOncePerApp(_ => new AppStatusCfg()
                    {
                        AppRoot = theBfg.DataDir
                    })
                    .CreatesOncePerApp(_ => new RxnDebugLogger("bfgWorker"))
                    .CreatesOncePerApp(_ => new AppServiceRegistry()
                    {
                        AppStatusUrl = testHostUrl
                    })
                    .CreatesOncePerApp(_ => new DynamicStartupTask((log, resolver) =>
                    {
                        var theBfg = resolver.Resolve<theBfg>();
                        var stopWorkers = theBfg.StartTestArenaWorkers(theBfg.Args, Cfg, resolver).Until();
                    }));

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    dd.CreatesOncePerApp<MacOSSystemInformationService>();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    dd.CreatesOncePerApp<WindowsSystemInformationService>();

                //forward all test events to the test arena
                DistributedBackingChannel.For(typeof(AppResourceInfo), typeof(ITestDomainEvent))(dd);
            };
        };
    }
}
