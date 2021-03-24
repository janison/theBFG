using System;
using System.Reactive.Linq;
using Rxns;
using Rxns.Health;
using Rxns.Interfaces;
using Rxns.Metrics;
using Rxns.Playback;

namespace theBFG.TestDomainAPI
{
    public class bfgTestArenaProgressView : AggregatedView<IRxn>, IReactTo<IRxn>
    {
        private readonly ISystemResourceService _sr;
        public override string ReportName => "TestArena";

        public bfgTestArenaProgressView(ITapeRepository reportRepo, ISystemResourceService sr, AggViewCfg cfg) : base(reportRepo, cfg)
        {
            _sr = sr;
        }

        public override IObservable<IRxn> GetOrCreateStream()
        {
            return this.OnReactionTo<ITestDomainEvent>().Merge(this._sr.AppUsage.Sample(TimeSpan.FromSeconds(5)).OfType<IRxn>());
        }
    }
}
