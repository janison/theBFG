using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Rxns.Cloud.Intelligence;
using Rxns.DDD.Commanding;
using Rxns.Hosting;
using Rxns.Hosting.Updates;
using Rxns.Interfaces;
using theBFG.TestDomainAPI;

namespace theBFG.RxnsAdapter
{
    public class bfgWorkerRxnManagerBridge : IClusterWorker<StartUnitTest, UnitTestResult>
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
        public string Ip { get; set; }

        private readonly ISubject<bool> _isBusy = new BehaviorSubject<bool>(false);

        public IObservable<UnitTestResult> DoWork(StartUnitTest work)
        {
            //setup the command to call back to us
            //and setup the client to run the downloaded version
            //instead of the original target existing on the machine (local fire) 
            work.AppStatusUrl = $"http://{RxnApp.GetIpAddress()}:888";
            var workDll = new FileInfo(work.Dll);
            work.Dll = Path.Combine(theBfg.GetTestSuiteDir(work), workDll.Name);

            _appCmds.Add(work.AsQuestion(Route));

            return _repsonseChannel.CreateSubscription<UnitTestResult>()
                .Where(c => c.InResponseTo == work.Id)
                .FirstOrDefaultAsync()
                .FinallyR(() =>
                {
                    _isBusy.OnNext(false);
                });
        }
    }
}