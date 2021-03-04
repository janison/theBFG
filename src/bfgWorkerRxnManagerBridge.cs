using System;
using System.Reactive;
using Rxns;
using Rxns.Cloud.Intelligence;
using Rxns.DDD.Commanding;
using Rxns.Interfaces;

namespace theBFG.RxnsAdapter
{
    public class bfgWorkerRxnManagerBridge<T, TR> : IClusterWorker<T, TR>
        where T : IUniqueRxn where TR : IRxnResult
    {
        private readonly IRxnManager<IRxn> _tunnel;
        private Func<IRxn, IObservable<Unit>> _publish;

        public bfgWorkerRxnManagerBridge(IRxnManager<IRxn> tunnel)
        {
            _tunnel = tunnel;
        }

        public string Name { get; set; } = "RxnManagerBfgWorker";
        public string Route { get; set; }
        public IObservable<bool> IsBusy { get; }//TODO::::::::::::::::::::

        public IObservable<TR> DoWork(T work)
        {
            return _tunnel.Ask<TR>(work);
        }
    }
}