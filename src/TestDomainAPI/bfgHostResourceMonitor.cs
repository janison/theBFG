using System;
using System.Reactive.Linq;
using Rxns;
using Rxns.Health;
using Rxns.Hosting;
using Rxns.Interfaces;

namespace theBFG.TestDomainAPI
{
    public class bfgHostResourceMonitor : IRxnPublisher<IRxn>, IDisposable
    {
        private readonly ISystemResourceService _sr;
        private readonly IRxnAppInfo _appInfo;
        private IDisposable _resourceWatcher;

        public bfgHostResourceMonitor(ISystemResourceService sr, IRxnAppInfo appInfo)
        {
            _sr = sr;
            _appInfo = appInfo;
        }

        public void ConfigiurePublishFunc(Action<IRxn> publish)
        {
            _resourceWatcher = _sr.AppUsage.Sample(TimeSpan.FromSeconds(5)).Select(a => a.ForHost(bfgWorkerManager.ClientId, _appInfo.Name)).OfType<IRxn>().Do(publish).Until();
        }

        public void Dispose()
        {
            _resourceWatcher?.Dispose();
        }
    }
}
