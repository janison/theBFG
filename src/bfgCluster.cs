using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Rxns;
using Rxns.Cloud;
using Rxns.DDD.Commanding;
using Rxns.Interfaces;
using theBFG.TestDomainAPI;
using ObservableExtensions = Rxns.Scheduling.ObservableExtensions;

namespace theBFG
{
    public class bfgCluster : ElasticQueue<StartUnitTest, UnitTestResult>, IServiceCommandHandler<StartUnitTest>
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
                        new AppStatusInfo("Workers", $"{Workflow.Workers.Count}{Workflow.Workers.Values.Count(v => ObservableExtensions.Value(v.IsBusy))}"),
                    }
                }).Until();

            }).Until();
        }

        public IObservable<CommandResult> Handle(StartUnitTest command)
        {
            Queue(command);

            return IObservableExtensions.ToObservable(CommandResult.Success("queued unit test run"));
        }
    }
}