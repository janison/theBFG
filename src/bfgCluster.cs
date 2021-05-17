using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using Rxns;
using Rxns.Cloud;
using Rxns.Cloud.Intelligence;
using Rxns.DDD.Commanding;
using Rxns.Health;
using Rxns.Interfaces;
using Rxns.Logging;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public class bfgTagWorkflow
    {
        public static string WorkerTag = "tags";

        public static IEnumerable<string> GetTagsFromWorker(WorkerConnection<StartUnitTest, UnitTestResult> worker)
        {
            return worker.Worker.Info.ContainsKey(WorkerTag) ? worker.Worker.Info[WorkerTag].Split(' ').Select(t => t.Trim('#')) : new string[0];
        }

        public static bool FanoutIfNotBusyAndHasMatchingTag(WorkerConnection<StartUnitTest, UnitTestResult> worker, StartUnitTest work)
        {
            return !worker.Worker.IsBusy.Value() && 
                   HasMatchingTag(GetTagsFromWorker(worker), work);
        }

        public static bool HasMatchingTag(IEnumerable<string> tags, StartUnitTest work)
        {
            if (work.Tags.IsNullOrWhitespace() ||work.Tags.Length < 1)
                return true; //no tags requested on work
            
            var workTags = GetTagsFromString(work.Tags);
            return tags.Any(workerTag => workTags.Any(workTag => workTag.BasicallyEquals(workerTag) || DoesMatchWildCard(workTag, workerTag)));
        }

        public static bool DoesMatchWildCard(string workTag, string workerTag)
        {
            if (workerTag.EndsWith(":"))
                workerTag += "*";

            var workTagTokens = workTag.Split(':');

            //match all workers that are in the category
            return workTagTokens.Length == 2 && workTagTokens[1] == "*"  //#os:* 
                                             && workTagTokens[0] == workerTag.Split(":")[0]; //#os:win #os:mac

        }

        public static IEnumerable<string> GetTagsFromString(string tagSyntax)
        {
            return tagSyntax.IsNullOrWhitespace() ? new string[0] : tagSyntax.Split(' ').Where(t => t.Contains("#")).Select(t => t.Trim('#'));
        }

        public static IEnumerable<string> GetAllTagsRunnableByCluster(bfgCluster workerCluster)
        {
            return workerCluster.Workflow.Workers.Values.SelectMany(w => GetTagsFromWorker(w)).Distinct();
        }
    }


    /// <summary>
    /// Adds a tag to a worker in order to slice the worker pool up into
    /// discrete units
    /// </summary>
    public class TagWorker : ServiceCommand
    {
        public string[] Tags { get; set; }

        public TagWorker()
        {

        }
        public TagWorker(string tagSyntax)
        {
            Tags = bfgTagWorkflow.GetTagsFromString(tagSyntax).ToArray();
        }
    }

    public class UntagWorker : TagWorker
    {
    }

    public class bfgCluster : ElasticQueue<StartUnitTest, UnitTestResult>, IServiceCommandHandler<StartUnitTest>, IServiceCommandHandler<StopUnitTest>, IRxnProcessor<WorkerInfoUpdated>
    {
        private readonly IRxnManager<IRxn> _rxnManager;
        private readonly IClusterFanout<StartUnitTest, UnitTestResult> _fanoutStrategy;

        public void Publish(IRxn rxn)
        {
            _rxnManager.Publish(rxn).Until();
        }

        public bfgCluster(SystemStatusPublisher appStatus, IRxnManager<IRxn> rxnManager, IClusterFanout<StartUnitTest, UnitTestResult> fanoutStrategy) : base(fanoutStrategy)
        {
            _rxnManager = rxnManager;
            _fanoutStrategy = fanoutStrategy;
            appStatus.Process(new AppStatusInfoProviderEvent()
            {
                Info = () => new[]
                {
                    new AppStatusInfo("Free Workers", $"{Workflow.Workers.Count - Workflow.Workers.Count(w => w.Value.Worker.IsBusy.Value())} / {Workflow.Workers.Count}")
                }
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
                foreach (var worker in _fanoutStrategy.Workers.Values)
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