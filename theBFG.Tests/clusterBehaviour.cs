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
        public void should_compete_to_drain_test_queue()
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
            var workerLifetime = new Subject<Unit>();
            theBfg.ReloadWithTestWorker().LastAsync().Subscribe(workerLifetime);

            theBfg.ReloadWithTestServer().LastAsync().Subscribe(workerLifetime);

            "waiting for test to complete".LogDebug();
            workerLifetime.WaitR();
        }
        
    }
}