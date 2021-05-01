using System;
using System.Reactive.Linq;
using Rxns;
using Rxns.Health;
using Rxns.Hosting;
using Rxns.Interfaces;
using Rxns.Metrics;
using Rxns.Playback;

namespace theBFG.TestDomainAPI
{


    public class bfgTestArenaProgressView : AggregatedView<IRxn>, IReactTo<IRxn>
    {
        private readonly ISystemResourceService _sr;
        private readonly IRxnAppInfo _appInfo;
        public override string ReportName => "TestArena";

        public bfgTestArenaProgressView(ITapeRepository reportRepo, ISystemResourceService sr, IRxnAppInfo appInfo, AggViewCfg cfg) : base(reportRepo, cfg)
        {
            _sr = sr;
            _appInfo = appInfo;
        }

        public override IObservable<IRxn> GetOrCreateStream()
        {
            return this.OnReactionTo<ITestDomainEvent>()
                .Merge(_sr.AppUsage.Sample(TimeSpan.FromSeconds(5)).Select(a => a.ForHost(_appInfo.Id, _appInfo.Name)).OfType<IRxn>())
                .Merge(this.OnReactionTo<AppResourceInfo>()); //broadcast from workers
        }
    }

}
