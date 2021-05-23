using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Rxns;
using Rxns.DDD;
using Rxns.Interfaces;
using Rxns.NewtonsoftJson;
using Rxns.WebApiNET5.NET5WebApiAdapters;
using theBFG.TestDomainAPI;

namespace theBFG
{
    /// <summary>
    /// Indicates a new test arena cfg has set in the system
    /// </summary>
    public class TestArenaCfgUpdated : IRxn
    {
        public TestArenaCfg Cfg { get; set; }
    }

    public class bfgTestArenaProgressHub : ReportsStatusEventsHub<ITestArenaApi>, IRxnProcessor<TestArenaCfgUpdated>, IRxnPublisher<IRxn>
    {
        private readonly bfgTestArenaProgressView _testArena;
        private readonly IHubContext<bfgTestArenaProgressHub> _context;
        private bool callOnce = true;
        private IAppCommandService _cmdService;
        private Action<IRxn> _publish;

        public bfgTestArenaProgressHub(bfgTestArenaProgressView testArena, IHubContext<bfgTestArenaProgressHub> context, IAppCommandService cmdService)
        {
            _testArena = testArena;
            _context = context;
            _cmdService = cmdService;
        }

        private void SendInitalMetricsTo(IClientProxy user)
        {
            _testArena.GetHistory().Where(v => v != null).Buffer(TimeSpan.FromSeconds(0.5), 50).Where(v => v.AnyItems())
                .StartWith(LatestTestArenaCfg())
                .SelectMany(s => Send(user, s, "onUpdate").ToObservable())
                .Until();
        }

        private Task Send(IClientProxy user, IEnumerable<IRxn> @events, string method)
        {
            return user.SendAsync(method, @events.ToJson());
        }

        private IEnumerable<IRxn> LatestTestArenaCfg()
        {
            yield return TestArenaCfg.Detect();
        }

        public override Task OnConnectedAsync()
        {
            this.ReportExceptions(() =>
            {
                OnVerbose("{0} connected", Context.ConnectionId);
                var connected = _context.Clients.Client(Context.ConnectionId);

                SendInitalMetricsTo(connected);

                if (callOnce)
                {
                    callOnce = false;
                    ActiveMetricsUpdates();
                }
                Observable.Timer(TimeSpan.FromSeconds(1)).Subscribe(this, _ => SendInitalMetricsTo(connected));
            });

            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception stopCalled)
        {
            this.ReportExceptions(() =>
            {
                OnVerbose("{0} disconnected", Context.ConnectionId);
            });

            return base.OnDisconnectedAsync(stopCalled);
        }

        public void SendCommand(string route, string command)
        {
            try
            {
                if (Context.User == null)
                {
                    OnWarning("Not logged in. Fix bypass!");
                    //return;
                }

                if (String.IsNullOrWhiteSpace(command))
                {
                    OnWarning("How am I supposed to execute an empty command buddy?");
                    return;
                }

                _cmdService.ExecuteCommand(route, command).Do(result =>
                    {
                        OnInformation("{0}", result);
                    })
                    .Catch<object, Exception>(e =>
                    {
                        OnWarning("x {0}", e.Message);
                        return new object().ToObservable();
                    }).Until(OnError);
            }
            catch (ArgumentException e)
            {
                OnWarning(e.Message);
            }
            catch (Exception e)
            {
                OnError(e);
            }
        }

        private void ActiveMetricsUpdates()
        {
            _testArena.GetUpdates()
                                        .Buffer(TimeSpan.FromMilliseconds(500), 50)
                                        .Where(ms => ms.Count > 0)
                                        .SelectMany(metric => Send(_context.Clients.All, metric, "onUpdate").ToObservable())
                                        .Until(OnError)
                                        .DisposedBy(this);
        }

        public void SaveCfg(TestArenaCfg cfg)
        {
            cfg?.Save();
        }

        public void Publish(IRxn cfg)
        {
            _publish(cfg);
        }

        public IObservable<IRxn> Process(TestArenaCfgUpdated @event)
        {
            @event.Cfg?.Save();

            return Rxn.Empty<IRxn>();
        }

        public void ConfigiurePublishFunc(Action<IRxn> publish)
        {
            _publish = publish;
        }
    }
}
