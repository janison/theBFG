using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rxns;
using Rxns.Cloud;
using Rxns.Logging;
using Rxns.NewtonsoftJson;

namespace theBFG.Tests
{
    [TestClass]
    public class clusterBehaviour
    {
        [TestInitialize]
        public void Setup()
        {
            
        }
        
        [TestMethod]
        public void should_keep_updated()
        {
            //todo:
            theBfg.ReloadAnd("launch testApp ../../../../theBfgtestApp/bin/debug/testApp.dll").WaitR();
        }

        [TestMethod]
        public void should_auto_detect_workers()
        {
            ReportStatus.Log.ReportToConsole();
            var foundWorker = new Subject<Unit>();

            var autoDiscovery = new SsdpDiscoveryService();
            BfgTestApi.AdvertiseForWorkers(autoDiscovery, "compete");

            BfgTestApi
                .DiscoverWork(autoDiscovery, "compete")
                .FirstAsync()
                .Select(_ => new Unit())
                .Subscribe(foundWorker);

            foundWorker.FirstAsync().WaitR();
        }

        [TestMethod]
        public void should_detect_testagents()
        {
            try
            {
                var workerLifetime = new Subject<Unit>();
                theBfg.ReloadWithTestWorker().LastAsync().Until();

                theBfg.ReloadWithTestServer().LastAsync().Until();

                "waiting for test to complete".LogDebug();
                workerLifetime.WaitR();
            }
            catch (Exception e)
            {
               Assert.Fail(e.ToString());
            }
        }

        [TestMethod]
        public void should_support_rapid_fire_mode()
        {
            theBfg.ReloadWithTestWorker(args: "target testApp theBGF.testApp.dll and fire rapidly").LastAsync().Until();

            "waiting for test to cimplete".LogDebug();
            new Subject<int>().Wait();
        }
        
    }
}