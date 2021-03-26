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
        
        public bfgWorkerDoWorkOrchestrator(IRxnManager<IRxn> rxnManager, IAppStatusStore appCmds)
        {
            _rxnManager = rxnManager;
            _appCmds = appCmds;
        }

        /// <summary>
        /// Updates apps that connect to the 
        /// </summary>
        /// <param name="updates"></param>
        /// <param name="app"></param>
        /// <returns></returns>
        public IObservable<IRxn> OnNewAppDiscovered(IAppStatusManager updates, SystemStatusEvent app)
        {
            if(app.SystemName.Contains("worker", StringComparison.InvariantCultureIgnoreCase))
                return new WorkerDiscovered<StartUnitTest, UnitTestResult>() // 8-|
                {
                    Worker = new bfgWorkerRxnManagerBridge<StartUnitTest, UnitTestResult>(_appCmds, _rxnManager)
                    {
                        Route = app.GetRoute(),
                        Name = app.SystemName
                    }
                }.ToObservable();

            return Rxn.Empty();
        }

        public IObservable<IRxn> OnAppHeartBeat(IAppStatusManager updates, SystemStatusEvent app)
        {
            return Rxn.Empty();
        }
    }
}