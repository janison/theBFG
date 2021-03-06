using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Rxns.Cloud.Intelligence;
using Rxns.DDD.Commanding;
using Rxns.Hosting.Updates;
using Rxns.Interfaces;

namespace theBFG.RxnsAdapter
{
    public class bfgWorkerRxnManagerBridge<T, TR> : IClusterWorker<T, TR>
        where T : IServiceCommand where TR : IRxnResult
    {
        private readonly IRxnManager<IRxn> _repsonseChannel;
        private readonly IAppStatusStore _appCmds;
        private Func<IRxn, IObservable<Unit>> _publish;

        public bfgWorkerRxnManagerBridge(IAppStatusStore appCmds, IRxnManager<IRxn> repsonseChannel)
        {
            _repsonseChannel = repsonseChannel;
            _appCmds = appCmds;
        }

        public string Name { get; set; } = "RxnManagerBfgWorker";
        public string Route { get; set; }
        public IObservable<bool> IsBusy => _isBusy;
        private readonly ISubject<bool> _isBusy = new BehaviorSubject<bool>(false);

        public IObservable<TR> DoWork(T work)
        {
            _appCmds.Add(work.AsQuestion(Route));

            return _repsonseChannel.CreateSubscription<TR>()
                .Where(c => c.InResponseTo == work.Id)
                .FirstOrDefaultAsync();
        }
    }
}