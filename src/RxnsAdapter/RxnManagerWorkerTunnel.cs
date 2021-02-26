using System;
using System.Reactive;
using Rxns;
using Rxns.Cloud.Intelligence;
using Rxns.DDD.Commanding;
using Rxns.Interfaces;

namespace theBFG.RxnsAdapter
{
    public class RxnManagerWorkerTunnel<T, TR> : IClusterWorker<T, TR>, IRxnCfg, IRxnPublisher<IRxn>
        where T : IUniqueRxn where TR : IRxnResult
    {
        IObservable<IRxn> _rxnManager;
        private Func<IRxn, IObservable<Unit>> _publish;

        public string Name { get; set; } = "RxnManagerBfgWorker";
        public string Route { get; set; }
        public IObservable<bool> IsBusy { get; }//TODO::::::::::::::::::::

        public IObservable<TR> DoWork(T work)
        {
            return _rxnManager.Ask<TR>(work, _publish);
        }

        public string Reactor { get; set; } = "Workers";

        public IObservable<IRxn> ConfigureInput(IObservable<IRxn> pipeline)
        {
            return _rxnManager = pipeline;
        }

        public IDeliveryScheme<IRxn> InputDeliveryScheme { get; set; }
        public bool MonitorHealth { get; set; }
        public RxnMode Mode { get; set; }

        public void ConfigiurePublishFunc(Action<IRxn> publish)
        {
            _publish = e =>
            {
                publish(e);
                return new Unit().ToObservable();
            };
        }
    }
}