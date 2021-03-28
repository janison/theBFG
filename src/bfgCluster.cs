using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Rxns;
using Rxns.Cloud;
using Rxns.Cloud.Intelligence;
using Rxns.DDD.Commanding;
using Rxns.Interfaces;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public class bfgCluster : ElasticQueue<StartUnitTest, UnitTestResult>
    {
        public void Publish(IRxn rxn)
        {
            _publish(rxn);
        }

        public bfgCluster() : base(new CompeteFanout<StartUnitTest, UnitTestResult>())
        {
            TimeSpan.FromSeconds(5).Then().Do(_ =>
            {
                _publish(new AppStatusInfoProviderEvent()
                {
                    ReporterName = "TestArena",
                    Info = () => new AppStatusInfo[]
                    {
                        new AppStatusInfo("Workers", $"{Workflow.Workers.Count}{Workflow.Workers.Values.Count(v => v.IsBusy.Value())}"),
                    }
                });

            }).Until();
        }
    }
}