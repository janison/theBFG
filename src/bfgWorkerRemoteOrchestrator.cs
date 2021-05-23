using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Rxns;
using Rxns.Cloud;
using Rxns.Cloud.Intelligence;
using Rxns.Commanding;
using Rxns.Health.AppStatus;
using Rxns.Hosting.Cluster;
using Rxns.Hosting.Updates;
using Rxns.Interfaces;
using theBFG.RxnsAdapter;
using theBFG.TestDomainAPI;
using RouteExtensions = Rxns.RouteExtensions;

namespace theBFG
{
    public class TestArenaWorkerHeadbeat : ITestDomainEvent
    {
        public DateTime At { get; } = DateTime.Now;

        public string Name { get; set; }
        public string Workers { get; set; }
        public string Route { get; set; }
        public string IpAddress { get; set; }
        public string ComputerName { get; set; }
        public string UserName { get; set; }
        public string Host { get; set; }
        public string Tags { get; set; }
    }

    public class bfgWorkerRemoteOrchestrator : IAppHeartBeatHandler
    {
        private readonly IRxnManager<IRxn> _rxnManager;
        private readonly IAppStatusStore _appCmds;
        private readonly IUpdateStorageClient _appUpdates;
        private readonly IRxnProcessor<WorkerDiscovered<StartUnitTest, UnitTestResult>> _workerPool;
        private readonly IClusterFanout<StartUnitTest, UnitTestResult> _cluster;
        private readonly IRxnProcessor<WorkerDisconnected> _workerPoolD;

        public bfgWorkerRemoteOrchestrator(IRxnManager<IRxn> rxnManager, IAppStatusStore appCmds, IUpdateStorageClient appUpdates, IRxnProcessor<WorkerDiscovered<StartUnitTest, UnitTestResult>> workerPool, IClusterFanout<StartUnitTest, UnitTestResult> cluster, IRxnProcessor<WorkerDisconnected> workerPoolD)
        {
            _rxnManager = rxnManager;
            _appCmds = appCmds;
            _appUpdates = appUpdates;
            _workerPool = workerPool;
            _cluster = cluster;
            _workerPoolD = workerPoolD;
        }

        /// <summary>
        /// Updates apps that connect to the 
        /// </summary>
        /// <param name="updates"></param>
        /// <param name="app"></param>
        /// <returns></returns>
        public IObservable<IRxn> OnNewAppDiscovered(IAppStatusManager updates, SystemStatusEvent app, object[] meta)
        {
            return OnAppHeartBeat(updates, app, meta);
        }

        private WorkerDiscovered<StartUnitTest, UnitTestResult> GenerateWorker(SystemStatusEvent app, object[] meta, string id)
        {
            return new WorkerDiscovered<StartUnitTest, UnitTestResult>() // 8-|
            {
                Worker = new bfgWorkerRxnManagerBridge(_appCmds, _rxnManager, _appUpdates, ToInfo(meta))
                {
                    Route = RouteExtensions.GetRoute(app),
                    Name = $"{app.SystemName}_{Guid.NewGuid().ToString().Split('-').FirstOrDefault()}",
                    Ip = app.IpAddress,
                }
            };
        }

        private IDictionary<string, string> ToInfo(object[] meta)
        {
            var info = new Dictionary<string, string>
            {
                {
                    bfgTagWorkflow.WorkerTag, ((AppStatusInfo[]) meta.FirstOrDefault())?.FirstOrDefault(a => a.Key.BasicallyEquals(bfgTagWorkflow.WorkerTag))?.Value.ToString() ?? string.Empty
                }
            };

            return info;
        }

        public IObservable<IRxn> OnAppHeartBeat(IAppStatusManager updates, SystemStatusEvent app, object[] meta)
        {
            var heartBeat = new TestArenaWorkerHeadbeat()
            {
                Route = RouteExtensions.GetRoute(app),
                Name = app.SystemName,
                IpAddress = app.IpAddress,
                Host = ParseValueFromMetaWithId("Id", meta),
                Workers = ParseValueFromMetaWithId("Free Workers", meta),
                ComputerName = ParseValueFromMetaWithId("ComputerName", meta),
                UserName = ParseValueFromMetaWithId("UserName", meta),
                Tags = ParseValueFromMetaWithId(bfgTagWorkflow.WorkerTag, meta)
            };

            //update info / tags
            var workerInfoUpdate = new WorkerInfoUpdated()
            {
                Name = app.SystemName,
                Info = ToInfo(meta)
            };

            BalanceRemoteWorkersWithCluster(heartBeat, app, meta);

            return new IRxn[] { heartBeat, workerInfoUpdate }.ToObservableSequence();
        }

        private void BalanceRemoteWorkersWithCluster(TestArenaWorkerHeadbeat next, SystemStatusEvent app, object[] meta)
        {
            if (app.SystemName.BasicallyContains("TestArena"))
                return; //dont track our node or bad things will happen!

            var theWorkersWeThinkTheNoteHas = _cluster.Workers.Where(c => c.Value.Worker.Route.Equals(Rxns.RouteExtensions.GetRoute(app)))
                .Select(r => r.Key)//this could potentially cause GC issues with large worker counts
                .ToArray();

            var theCurrentWorkersOnNode = next.Workers.Split("/").Last().AsInt();
            var workerExpectedVsCurrentDiff = theCurrentWorkersOnNode - theWorkersWeThinkTheNoteHas.Length;

            if (workerExpectedVsCurrentDiff > 0)
            {
                do
                {
                    _workerPool.Process(GenerateWorker(app, meta, workerExpectedVsCurrentDiff.ToString())).WaitR();
                } while (--workerExpectedVsCurrentDiff > 0);
            }

            else if (workerExpectedVsCurrentDiff < 0)
            {

                foreach (var worker in theWorkersWeThinkTheNoteHas)
                {
                    _workerPoolD.Process(new WorkerDisconnected() { Name = worker }).WaitR();

                    if (--workerExpectedVsCurrentDiff >= 0)
                        break;
                }
            }
        }

        public string ParseValueFromMetaWithId(string name, object[] meta)
        {
            return meta.Select(m => m as AppStatusInfo[]).FirstOrDefault()?.FirstOrDefault(w => w.Key.BasicallyEquals(name))?.Value?.ToString();
        }
    }

}