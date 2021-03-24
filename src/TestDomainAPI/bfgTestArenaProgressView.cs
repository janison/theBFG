using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Rxns;
using Rxns.DDD;
using Rxns.WebApiNET5.NET5WebApiAdapters;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public interface ITestArenaApi
    {
        void OnUpdate(ITestDomainEvent e);
    }

    public class bfgTestArenaProgressHub : ReportsStatusEventsHub<ITestArenaApi>
    {
        private readonly bfgTestArenaProgressView _testArena;
        private readonly IHubContext<bfgTestArenaProgressHub> _context;
        private bool callOnce = true;
        private IAppCommandService _cmdService;

        public bfgTestArenaProgressHub(bfgTestArenaProgressView testArena, IHubContext<bfgTestArenaProgressHub> context, IAppCommandService cmdService)
        {
            _testArena = testArena;
            _context = context;
            _cmdService = cmdService;
        }

        private void SendInitalMetricsTo(IClientProxy user)
        {
            _testArena.GetHistory().Where(v => v != null).Buffer(TimeSpan.FromSeconds(2), 50).Where(v => v.AnyItems())
                .SelectMany(s => s).Do(s => user.SendAsync("onUpdate", s)).Subscribe();
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
                    }).Until();
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
                                        .Buffer(TimeSpan.FromMilliseconds(100), 20)
                                        .Where(ms => ms.Count > 0)
                                        .SelectMany(s => s) //i think i need to reimplement the batched event passing using a delimiter - couldnt find the code in the frontend to deserilise
                                        .Subscribe(this, metric =>
                                        {
                                            _context.Clients.All.SendAsync("onUpdate", metric);
                                        })
                                        .DisposedBy(this);
        }
    }
}
