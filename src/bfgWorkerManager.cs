using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Rxns;
using Rxns.Cloud;
using Rxns.Cloud.Intelligence;
using Rxns.Collections;
using Rxns.DDD.Commanding;
using Rxns.Health;
using Rxns.Health.AppStatus;
using Rxns.Hosting;
using Rxns.Hosting.Updates;
using Rxns.Interfaces;
using Rxns.Logging;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public class SpawnWorker : ServiceCommand
    {

        public SpawnWorker(string tags)
        {
            Tags = tags;
        }

        public SpawnWorker()
        {

        }

        public string Tags { get; set; }
        public string Route { get; set; }
    }


    public class bfgWorkerManager : IServiceCommandHandler<SpawnWorker>
    {
        private readonly bfgCluster _workerCluster;
        private readonly IObservable<StartUnitTest[]> _cfg;
        private readonly IResolveTypes _resolver;
        private readonly IRxnManager<IRxn> _eventManager;
        private readonly IAppStatusCfg _appStatusCfg;
        private readonly IAppServiceRegistry _appServiceRegistry;
        private readonly IZipService _zipService;
        private readonly IAppStatusStore _appCmds;
        private readonly IAppStatusServiceClient _appStatus;
        private readonly IAppServiceDiscovery _serviceDiscovery;
        private readonly IUpdateServiceClient _appUpdates;
        public static string ClientId = Guid.NewGuid().ToString(); //get from cfg file if exists

        public bfgWorkerManager(bfgCluster workerCluster, IObservable<StartUnitTest[]> cfg, SystemStatusPublisher systemStatus, IResolveTypes resolver, IRxnManager<IRxn> eventManager, IAppStatusCfg appStatusCfg, IAppServiceRegistry appServiceRegistry, IZipService zipService, IAppStatusStore appCmds, IAppStatusServiceClient appStatus, IAppServiceDiscovery serviceDiscovery, IUpdateServiceClient appUpdates)
        {
            _workerCluster = workerCluster;
            _cfg = cfg;
            _resolver = resolver;
            _eventManager = eventManager;
            _appStatusCfg = appStatusCfg;
            _appServiceRegistry = appServiceRegistry;
            _zipService = zipService;
            _appCmds = appCmds;
            _appStatus = appStatus;
            _serviceDiscovery = serviceDiscovery;
            _appUpdates = appUpdates;

            systemStatus.Process(new AppStatusInfoProviderEvent()
            {
                ReporterName = nameof(bfgWorkerManager),
                Component = "Status",
                Info = WorkerManagerStatus
            }).Until();
        }

        private AppStatusInfo[] WorkerManagerStatus()
        {
            return new[]
            {
                new AppStatusInfo("Workers", _workerCluster.Workflow.Workers),
                new AppStatusInfo("ComputerName", Environment.MachineName),
                new AppStatusInfo("Username", Environment.UserName),
                new AppStatusInfo("Id", ClientId),
            };
        }

        public IObservable<bfgWorker> SpawnTestWorker(string[] tags = null)
        {
            var instanceId = _workerCluster.Workflow.Workers.Count + 1;
            "Spawning worker".LogDebug(instanceId);

            //$"Streaming logs".LogDebug();
            //rxnManager.Publish(new StreamLogs(TimeSpan.FromMinutes(60))).Until();

            $"Starting worker".LogDebug();

            var testWorker = new bfgWorker(
                $"{ClientId}/TestWorker#{instanceId}",
                "local",
                tags ?? theBfg.Args.Where(w => w.StartsWith("#")).ToArray(),
                _appServiceRegistry, _serviceDiscovery,
                _zipService, _appStatus,
                _eventManager, _appUpdates,
                _appStatusCfg,
                _resolver.Resolve<Func<ITestArena[]>>()
            );

            _workerCluster.Process(new WorkerDiscovered<StartUnitTest, UnitTestResult>() { Worker = testWorker })
                .SelectMany(e => _eventManager.Publish(e)).Until();

            return StartDiscoveringTestsIf(_cfg, testWorker).Select(_ => testWorker);
        }

        private IObservable<Unit> StartDiscoveringTestsIf(IObservable<StartUnitTest[]> cfg, bfgWorker testWorker)
        {
            return cfg.Take(1).Select(tests =>
            {
                //todo: cleanup this code and decide
                if (!tests.AnyItems() || tests[0].AppStatusUrl.IsNullOrWhitespace() && !Directory.Exists(tests[0].UseAppVersion))
                    testWorker.DiscoverAndDoWork();

                return new Unit();
            });
        }

        public IObservable<CommandResult> Handle(SpawnWorker command)
        {
            if (command.Route.IsNullOrWhitespace())
            {
                return SpawnTestWorker(bfgTagWorkflow.TagsFromString(command.Tags).ToArray()).Select(_ => CommandResult.Success());
            }

            return SpawnTestWorkerOnRoute(command);
        }

        private IObservable<CommandResult> SpawnTestWorkerOnRoute(SpawnWorker command)
        {
            return Rxn.Create(() =>
            {
                _appCmds.Add(command.AsQuestion());

                "Queued command for worker".LogDebug();

                return CommandResult.Success();
            });
        }
    }

    public class NoAppCmdsOnWorker : IAppStatusStore
    {
        public IDictionary<string, Dictionary<SystemStatusEvent, object[]>> GetSystemStatus()
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public void ClearSystemStatus(string route)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> GetLog()
        {
            throw new NotImplementedException();
        }

        public IObservable<string> SaveLog(string tenant, Stream log, string file)
        {
            throw new NotImplementedException();
        }

        public IObservable<AppLogInfo[]> ListLogs(string tenantId, int top = 3)
        {
            throw new NotImplementedException();
        }

        public IObservable<Stream> GetLogs(string tenantId, string file)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IRxnQuestion> FlushCommands(string route)
        {
            throw new NotImplementedException();
        }

        public void Add(IRxnQuestion cmds)
        {
            throw new Exception("Cant queue work to other workers sorry. Must be done via the Test Arena".LogDebug());
        }

        public void Add(LogMessage<string> message)
        {
            throw new NotImplementedException();
        }

        public void Add(LogMessage<Exception> message)
        {
            throw new NotImplementedException();
        }

        public IDictionary<object, object> Cache { get; }
    }
}
