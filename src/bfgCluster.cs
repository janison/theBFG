using System;
using System.Linq;
using System.Reactive.Linq;
using Rxns;
using Rxns.Cloud;
using Rxns.Interfaces;
using Rxns.Scheduling;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public class bfgCluster : ElasticQueue<StartUnitTest, UnitTestResult>
    {
        public bfgCluster(IRxnManager<IRxn> rxns)
        {

            TimeSpan.FromSeconds(5).Then().Do(_ =>
            {

                rxns.Publish(new AppStatusInfoProviderEvent()
                {
                    ReporterName = "TestArena",
                    Info = () => new AppStatusInfo[]
                    {
                        new AppStatusInfo("Workers", $"{Workflow.Workers.Count}{Workflow.Workers.Values.Count(v => v.IsBusy.Value())}"),
                    }
                }).Until();

            }).Until();
        }
    }
}