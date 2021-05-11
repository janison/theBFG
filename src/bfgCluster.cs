using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Rxns;
using Rxns.Cloud;
using Rxns.Cloud.Intelligence;
using Rxns.DDD.Commanding;
using Rxns.Health;
using Rxns.Hosting.Updates;
using Rxns.Interfaces;
using Rxns.Logging;
using Rxns.Playback;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public class bfgTagWorkflow
    {
        public static string WorkerTag = "tag";

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
            
            var workTags = TagsFromString(work.Tags);
            return tags.Any(tag => workTags.Any(w => w.BasicallyEquals(tag)));
        }

        public static IEnumerable<string> TagsFromString(string tagSyntax)
        {
            return tagSyntax.IsNullOrWhitespace() ? new string[0] : tagSyntax.Split(' ').Where(t => t.Contains("#")).Select(t => t.Trim('#'));
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
            Tags = bfgTagWorkflow.TagsFromString(tagSyntax).ToArray();
        }
    }

    public class UntagWorker : TagWorker
    {
    }

    public class bfgCluster : ElasticQueue<StartUnitTest, UnitTestResult>, IServiceCommandHandler<StartUnitTest>, IServiceCommandHandler<StopUnitTest>
    {
        private readonly IRxnManager<IRxn> _rxnManager;
        private static CompeteFanout<StartUnitTest, UnitTestResult> FanoutStratergy = new CompeteFanout<StartUnitTest, UnitTestResult>(bfgTagWorkflow.FanoutIfNotBusyAndHasMatchingTag);

        public void Publish(IRxn rxn)
        {
            _rxnManager.Publish(rxn).Until();
        }

        public bfgCluster(SystemStatusPublisher appStatus, IRxnManager<IRxn> rxnManager) : base(FanoutStratergy)
        {
            _rxnManager = rxnManager;
            appStatus.Process(new AppStatusInfoProviderEvent()

            {
                ReporterName = "TestArena",
                Info = () => new[]
                {
                    new AppStatusInfo("Workers", $"{Workflow.Workers.Count}{Workflow.Workers.Values.Count(v => v.Worker.IsBusy.Value())}"),
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