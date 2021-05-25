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
        public override string ReportName => "TestArena";

        public bfgTestArenaProgressView(ITapeRepository reportRepo, AggViewCfg cfg) : base(reportRepo, cfg)
        {
            //
        }

        public override IObservable<IRxn> GetOrCreateStream()
        {
            return this.OnReactionTo<ITestDomainEvent>().OfType<IRxn>()
                .Merge(this.OnReactionTo<AppResourceInfo>()); //broadcast from workers
        }
    }

}
