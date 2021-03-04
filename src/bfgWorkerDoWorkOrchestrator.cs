using System;
using System.Reactive;
using System.Reactive.Linq;
using Rxns;
using Rxns.Cloud.Intelligence;
using Rxns.Commanding;
using Rxns.Health.AppStatus;
using Rxns.Interfaces;
using theBFG.RxnsAdapter;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public class bfgWorkerDoWorkOrchestrator : IAppHeartBeatHandler
    {
        private readonly IRxnManager<IRxn> _rxnManager;
        private readonly StartUnitTest _cfg;

        public bfgWorkerDoWorkOrchestrator(IRxnManager<IRxn> rxnManager, StartUnitTest cfg)
        {
            _rxnManager = rxnManager;
            _cfg = cfg;
        }

        /// <summary>
        /// Updates apps that connect to the 
        /// </summary>
        /// <param name="updates"></param>
        /// <param name="app"></param>
        /// <returns></returns>
        public IObservable<IRxn> OnNewAppDiscovered(IAppStatusManager updates, SystemStatusEvent app)
        {
            return new WorkerDiscovered<StartUnitTest, UnitTestResult>() // 8-|
            {
                Worker = new bfgWorkerRxnManagerBridge<StartUnitTest, UnitTestResult>(_rxnManager)
            }.ToObservable();
        }

        public IObservable<IRxn> OnAppHeartBeat(IAppStatusManager updates, SystemStatusEvent app)
        {
            return Rxn.Empty();
        }
    }
}