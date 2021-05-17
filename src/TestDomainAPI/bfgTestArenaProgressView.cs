using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
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
    public interface ITestArenaApi
    {
        void OnUpdate(ITestDomainEvent e);
        void SendCommand(string route, string command);
    }

    public class TestArenaCfg : IRxn
    {
        /// <summary>
        /// The threshhold for a test to be considered "slow" and appear in the pie graph
        /// </summary>
        public int SlowTestMs { get; set; } = 100;

        /// <summary>
        /// This controls the resolution of the graph which displays each test outcome. The max value on the graph
        /// will be this value in milliseconds
        /// </summary>
        public int TestDurationMax { get; set; } = 100;
        /// <summary>
        /// If the test arena will be in lights out mode by default
        /// </summary>
        public bool IsLightsOut { get; set; }
        /// <summary>
        /// If the test arena metics will default to persistint between sessions.
        /// This is usually controlled with the "save" syntax in a command 
        /// </summary>
        public bool AlwaysSave { get; set; }

        /// <summary>
        /// If sounds will fire to indicate the outcome of a each test
        /// </summary>
        public bool SoundsOn { get; set; } = false;

        public static string CfgFile = Path.Combine(theBfg.DataDir, "testArena.json");
        public TestArenaCfg()
        {

        }

        public static TestArenaCfg Detect()
        {
            return File.Exists(CfgFile) ? Read(CfgFile) : new TestArenaCfg().Save();
        }

        private static TestArenaCfg Read(string cfgFile)
        {
            return File.ReadAllText(cfgFile).FromJson<TestArenaCfg>();
        }

        public TestArenaCfg Save()
        {
            File.WriteAllText(CfgFile, this.ToJson());
            return this;
        }
    }

    /// <summary>
    /// Indicates a new test arena cfg has set in the system
    /// </summary>
    public class TestArenaCfgUpdated : IRxn
    {
        public TestArenaCfg Cfg { get; set; }
    }

    public class bfgTestArenaProgressHub : ReportsStatusEventsHub<ITestArenaApi>, IRxnProcessor<TestArenaCfgUpdated>
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
                .StartWith(LatestTestArenaCfg())
                .SelectMany(s => s).Do(s => user.SendAsync("onUpdate", s))
                .Until();
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

        public void SaveCfg(TestArenaCfg cfg)
        {
            cfg?.Save();
        }

        public IObservable<IRxn> Process(TestArenaCfgUpdated @event)
        {
            @event.Cfg?.Save();

            return Rxn.Empty<IRxn>();
        }
    }
}
