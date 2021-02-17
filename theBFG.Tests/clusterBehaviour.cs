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
        [TestMethod]
        public void should_compete_to_drain_test_queue()
        {
            //todo:
            theBfg.ReloadWith("launch testApp ../../../../theBfgtestApp/bin/debug/testApp.dll").WaitR();
        }

        
        [TestMethod]
        public void should_auto_detect_workers()
        {
            ReportStatus.Log.ReportToConsole();
            var foundWorker = new Subject<Unit>();
        
            var server = new SsdpDiscoveryService();
            server.Advertise("bfg-worker-queue","compete", "http://localhost:888/").Until();

            var worker = new SsdpDiscoveryService();
            worker.Discover()
                .Where(msg => msg.Name.Contains("bfg-worker-queue"))
                .Select(m =>
                {
                    var tokens = m.Name.Split(':');
                    m.Name = tokens.Reverse().Skip(1).FirstOrDefault();
                    return m;
                })
                .Do(msg =>
                {
                    $"Discovered {msg.Name} @ {msg.Url}".LogDebug();
                })
                .FirstAsync(w => w.Name.Contains("compete"))
                .Select(_ => new Unit())
                .Subscribe(foundWorker);


            foundWorker.FirstAsync().WaitR();
        }
        
    }
}