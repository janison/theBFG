using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Rxns;
using Rxns.Cloud;
using Rxns.Cloud.Intelligence;
using Rxns.DDD.Commanding;
using Rxns.Interfaces;
using Rxns.Logging;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public class bfgCluster : ElasticQueue<StartUnitTest, UnitTestResult>, IServiceCommandHandler<StartUnitTest>, IServiceCommandHandler<StopUnitTest>
    {
        private static CompeteFanout<StartUnitTest, UnitTestResult> FanoutStratergy = new CompeteFanout<StartUnitTest, UnitTestResult>();

        public void Publish(IRxn rxn)
        {
            _publish(rxn);
        }

        public bfgCluster() : base(FanoutStratergy)
        {
            TimeSpan.FromSeconds(5).Then().Do(_ =>
            {
                _publish?.Invoke(new AppStatusInfoProviderEvent()

                {
                    ReporterName = "TestArena",
                    Info = () => new[]
                    {
                        new AppStatusInfo("Workers", $"{Workflow.Workers.Count}{Workflow.Workers.Values.Count(v => v.Worker.IsBusy.Value())}"),
                    }
                });

            }).Until();

            
        }

        public IObservable<CommandResult> Handle(StartUnitTest command)
        {
            Queue(command);
            //the result will be broadcast when the queue processes the command
            return Rxn.Empty<CommandResult>();
        }

        public IObservable<CommandResult> Handle(StopUnitTest command)
        {
            return Rxn.Create(() =>
            {
                "Stopping all unit tests".LogDebug();
                foreach (var worker in FanoutStratergy.Workers.Values)
                {
                    try
                    {
                        worker.DoWork?.Dispose();
                    }
                    catch (Exception e)
                    {
                        $"Failed to stop {worker.Worker.Name} : {e}".LogDebug();
                    }
                }

                return CommandResult.Success().AsResultOf(command);
            });
        }
    }

    
}