using System;
using System.Collections.Generic;
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
        private readonly IUpdateStorageClient _updates;
        private readonly IAppStatusStore _appCmds;

        public bfgWorkerRxnManagerBridge(IAppStatusStore appCmds, IRxnManager<IRxn> repsonseChannel, IUpdateStorageClient updates, IDictionary<string, string> info = null)
        {
            _repsonseChannel = repsonseChannel;
            _updates = updates;
            _appCmds = appCmds;

            if (info != null)
                Info = info;
        }

        public string Name { get; set; } = "RxnManagerBfgWorker";
        public string Route { get; set; }
        public IDictionary<string, string> Info { get; set;  } = new Dictionary<string, string>();
        public IObservable<bool> IsBusy => _isBusy;
        public string Ip { get; set; }

        private readonly ISubject<bool> _isBusy = new BehaviorSubject<bool>(false); 

        public void Update(IDictionary<string, string> info)
        {
            Info = info;
        }

        public IObservable<UnitTestResult> DoWork(StartUnitTest work)
        {
            //setup the command to call back to us
            //and setup the client to run the downloaded version
            //instead of the original target existing on the machine (local fire) 
            work.AppStatusUrl = $"http://{RxnApp.GetIpAddress()}:888";
            var workDll = new FileInfo(work.Dll);
            work.Dll = Path.Combine(theBfg.GetTestSuiteDir(work), workDll.Name).AsCrossPlatformPath();

            
            return WhenUpdateExists(work)
                .SelectMany(u =>
                {
                    //send the command to the worker to run the test
                    _appCmds.Add(work.AsQuestion(Route));

                    //wait for the test response
                    return _repsonseChannel.CreateSubscription<UnitTestResult>()
                        .Where(c => c.InResponseTo == work.Id)
                        .FirstOrDefaultAsync()
                        .FinallyR(() => { _isBusy.OnNext(false); });
                });
        }

        private IObservable<bool> WhenUpdateExists(StartUnitTest work)
        {
            return _updates.GetUpdate(work.UseAppUpdate) //ensure we have an update
                .Select(_ =>
                {
                    _.Dispose();
                    return true;
                }).Catch<bool, Exception>(e =>
                {
                    //wait for the update if we dont
                    return _repsonseChannel.CreateSubscription<NewAppVersionReleased>()
                        .Where(e => e.SystemName.BasicallyEquals(work.UseAppUpdate) &&
                                    e.Version.BasicallyEquals(work.UseAppVersion))
                        .Select(_ => true);
                });
        }
    }
}