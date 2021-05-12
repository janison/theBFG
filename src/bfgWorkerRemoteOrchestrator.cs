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
    }

    public class bfgWorkerRemoteOrchestrator : IAppHeartBeatHandler
    {
        private readonly IRxnManager<IRxn> _rxnManager;
        private readonly IAppStatusStore _appCmds;
        private readonly IUpdateStorageClient _appUpdates;
        private readonly IRxnProcessor<WorkerDiscovered<StartUnitTest, UnitTestResult>> _workerPool;

        public bfgWorkerRemoteOrchestrator(IRxnManager<IRxn> rxnManager, IAppStatusStore appCmds, IUpdateStorageClient appUpdates, IRxnProcessor<WorkerDiscovered<StartUnitTest, UnitTestResult>> workerPool)
        {
            _rxnManager = rxnManager;
            _appCmds = appCmds;
            _appUpdates = appUpdates;
            _workerPool = workerPool;
        }

        /// <summary>
        /// Updates apps that connect to the 
        /// </summary>
        /// <param name="updates"></param>
        /// <param name="app"></param>
        /// <returns></returns>
        public IObservable<IRxn> OnNewAppDiscovered(IAppStatusManager updates, SystemStatusEvent app, object[] meta)
        {
            var info = new Dictionary<string, string>();
            info.Add("tags", meta.Skip(3).FirstOrDefault()?.ToString());

            if (app.SystemName.BasicallyContains("worker"))
                return _workerPool.Process(new WorkerDiscovered<StartUnitTest, UnitTestResult>() // 8-|
                {
                    Worker = new bfgWorkerRxnManagerBridge(_appCmds, _rxnManager, _appUpdates, info)
                    {
                        Route = RouteExtensions.GetRoute(app),
                        Name = app.SystemName,
                        Ip = app.IpAddress,
                    }
                });

            return OnAppHeartBeat(updates, app, meta);
        }

        public IObservable<IRxn> OnAppHeartBeat(IAppStatusManager updates, SystemStatusEvent app, object[] meta)
        {
            return new TestArenaWorkerHeadbeat()
            {
                Route = RouteExtensions.GetRoute(app),
                Name = app.SystemName,
                IpAddress = app.IpAddress,
                Host = ParseFromMeta("Id", meta),
                Workers = ParseFromMeta("Free Workers", meta),
                ComputerName = ParseFromMeta("ComputerName", meta),
                UserName = ParseFromMeta("UserName", meta)
            }.ToObservable();
        }

        public string ParseFromMeta(string name, object[] meta)
        {
            return meta.Select(m => m as AppStatusInfo[]).FirstOrDefault()?.FirstOrDefault(w => w.Key.BasicallyEquals(name))?.Value?.ToString();
        }
    }

}