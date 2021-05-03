using System;
using System.Reactive;
using System.Reactive.Linq;
using Rxns;
using Rxns.Cloud.Intelligence;
using Rxns.Commanding;
using Rxns.Health.AppStatus;
using Rxns.Hosting.Updates;
using Rxns.Interfaces;
using theBFG.RxnsAdapter;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public class bfgWorkerDoWorkOrchestrator : IAppHeartBeatHandler
    {
        private readonly IRxnManager<IRxn> _rxnManager;
        private readonly IAppStatusStore _appCmds;
        private readonly IUpdateStorageClient _appUpdates;
        private readonly IRxnProcessor<WorkerDiscovered<StartUnitTest, UnitTestResult>> _workerPool;

        public bfgWorkerDoWorkOrchestrator(IRxnManager<IRxn> rxnManager, IAppStatusStore appCmds, IUpdateStorageClient appUpdates, IRxnProcessor<WorkerDiscovered<StartUnitTest, UnitTestResult>> workerPool)
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
        public IObservable<IRxn> OnNewAppDiscovered(IAppStatusManager updates, SystemStatusEvent app)
        {
            if(app.SystemName.BasicallyContains("worker"))
                return _workerPool.Process(new WorkerDiscovered<StartUnitTest, UnitTestResult>() // 8-|
                {
                    Worker = new bfgWorkerRxnManagerBridge(_appCmds, _rxnManager, _appUpdates)
                    {
                        Route = app.GetRoute(),
                        Name = app.SystemName,
                        Ip = app.IpAddress,
                    }
                });

            return Rxn.Empty();
        }

        public IObservable<IRxn> OnAppHeartBeat(IAppStatusManager updates, SystemStatusEvent app)
        {
            return Rxn.Empty();
        }
    }
}