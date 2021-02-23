using System;
using Rxns;
using Rxns.Cloud;
using Rxns.DDD.Commanding;
using Rxns.Health.AppStatus;
using Rxns.Hosting.Updates;
using Rxns.Interfaces;
using theBFG.RxnsAdapter;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public class TestWorkerDiscoveryService : LocalAppStatusServer, IRxnPublisher<IRxn>
    {
        private Action<IRxn> _publish;

        public TestWorkerDiscoveryService(IAppErrorManager errorMgr, IAppStatusManager appStatusMgr) : base(errorMgr, appStatusMgr)
        {
        }

        public override IObservable<RxnQuestion[]> PublishSystemStatus(SystemStatusEvent status, AppStatusInfo[] meta)
        {
            if (status.SystemName.StartsWith("TestWorker#"))
            {
                _publish(new WorkerDiscovered<StartUnitTest, UnitTestResult>() {Worker = new RxnManagerWorkerTunnel<StartUnitTest, UnitTestResult>()});
                
            }
            return base.PublishSystemStatus(status, meta);
        }

        public void ConfigiurePublishFunc(Action<IRxn> publish)
        {
            _publish = publish;
        }
    }
}