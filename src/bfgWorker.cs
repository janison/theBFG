using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using Rxns;
using Rxns.Cloud;
using Rxns.Cloud.Intelligence;
using Rxns.Collections;
using Rxns.DDD.Commanding;
using Rxns.Health;
using Rxns.Hosting;
using Rxns.Hosting.Updates;
using Rxns.Interfaces;
using Rxns.Logging;
using theBFG.TestDomainAPI;

namespace theBFG
{
    /// <summary>
    /// 
    /// </summary>
    public class bfgWorker : IClusterWorker<StartUnitTest, UnitTestResult>,
        IServiceCommandHandler<TagWorker>,
        IServiceCommandHandler<UntagWorker>,
        IServiceCommandHandler<Run>,
        IServiceCommandHandler<Cover>
    {
        private readonly IAppServiceDiscovery _services;
        private readonly IAppStatusServiceClient _appStatus;
        private readonly IRxnManager<IRxn> _rxnManager;
        private readonly IUpdateServiceClient _updateService;
        private readonly IAppStatusCfg _cfg;
        private readonly Func<ITestArena[]> _arena;
        private readonly IAppServiceRegistry _registry;
        public void Update(IDictionary<string, string> eventInfo)
        {
            Info = eventInfo;
        }

        public string Name { get; }
        public string Route { get; }
        public IDictionary<string, string> Info { get; set; } = new Dictionary<string, string>();
        public IObservable<bool> IsBusy => _isBusy;
        private readonly ISubject<bool> _isBusy = new BehaviorSubject<bool>(false);
        private readonly IZipService _zipService;
        private readonly CoverEventWorkflow _cover;

        public IEnumerable<string> Tags => bfgTagWorkflow.GetTagsFromString(Info[bfgTagWorkflow.WorkerTag]);

        public bfgWorker(string name, string route, string[] tags, IAppServiceRegistry registry, IAppServiceDiscovery services, IZipService zipService, IAppStatusServiceClient appStatus, IRxnManager<IRxn> rxnManager, IUpdateServiceClient updateService, IAppStatusCfg cfg, Func<ITestArena[]> arena)
        {
            _registry = registry;
            _services = services;
            _zipService = zipService;
            _appStatus = appStatus;
            _rxnManager = rxnManager;
            _updateService = updateService;
            _cfg = cfg;
            _arena = arena;
            Name = name;
            Route = route;
            Info.Add(bfgTagWorkflow.WorkerTag, tags.ToStringEach());
            _cover = new CoverEventWorkflow();
        }

        public IObservable<CommandResult> Handle(TagWorker command)
        {
            return Rxn.Create(() =>
            {
                Info[bfgTagWorkflow.WorkerTag] = $"{Info[bfgTagWorkflow.WorkerTag]},{command.Tags.ToStringEach()}";

                return CommandResult.Success().AsResultOf(command).ToObservable();
            });
        }

        public IObservable<CommandResult> Handle(UntagWorker command)
        {
            return Rxn.Create(() =>
            {
                Info[bfgTagWorkflow.WorkerTag] = Info[bfgTagWorkflow.WorkerTag].Split(',').Except(command.Tags).ToStringEach();

                return CommandResult.Success().AsResultOf(command).ToObservable();
            });
        }

        public IDisposable DiscoverAndDoWork(string apiName = null, string testHostUrl = null)
        {
            $"Attempting to discover work {apiName ?? "any"}@{testHostUrl ?? "any"}".LogDebug();

            var allDiscoveredApiRequests = bfgTestArenaApi.DiscoverWork(_services).Do(apiFound =>
            {
                $"Discovered Api Hosting: {apiFound.Name}@{apiFound.Url}".LogDebug();
                _registry.AppStatusUrl = apiFound.Url;

                TimeSpan.FromSeconds(1).Then().Do(_ =>
                {
                    _rxnManager.Publish(new PerformAPing()).Until();

                });

                //from here on, heartbeats will return work for us to do
            });

            return apiName.IsNullOrWhitespace()
                ? allDiscoveredApiRequests.Until()
                : allDiscoveredApiRequests.Where(foundApi => apiName == null || foundApi.Name.BasicallyEquals(apiName)).Until();
        }

        public IObservable<UnitTestResult> DoWork(StartUnitTest work)
        {
            //right now we may need to hack fanout stratergies to discount duplicate and stream unit test events across workers so 
            //the cover flow works out correctly otherwise there may not be a worker actually running the unit test.
            //we want the worker to either be a coverage worker, or a unit test worker. not exactly sure how to transfer this info, maybe i just use Info?
            return !_cover.IsActive ?
                    UnitTestWorkflow.Run(Name, work, isBusy => _isBusy.OnNext(isBusy), _updateService, _cfg, _appStatus, _arena, _zipService)
                                    .Do(msg => _rxnManager.Publish(msg))
                                    .LastOrDefaultAsync()
                                    .OfType<UnitTestResult>()
                    : Rxn.Empty<UnitTestResult>();
        }

        public IObservable<CommandResult> Handle(Run command)
        {
            return UnitTestEventWorkflow.Run(Name, command)
                .Do(msg => _rxnManager.Publish(msg).Until())
                .LastOrDefaultAsync()
                .Select(_ => CommandResult.Success().AsResultOf(command));
        }

        public IObservable<CommandResult> Handle(Cover command)
        {
            return _cover.Run(command, _rxnManager.CreateSubscription<ITestDomainEvent>())
                .Do(msg => _rxnManager.Publish(msg).Until())
                .LastOrDefaultAsync()
                .Select(_ => CommandResult.Success().AsResultOf(command));
        }
    }
}