using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Rxns;
using Rxns.Collections;
using Rxns.DDD.Commanding;
using Rxns.Interfaces;
using Rxns.Logging;

namespace theBFG.TestDomainAPI
{
    /// <summary>
    /// Sets up a worker to watch for tests marked with certain tags then collect execution path coverage data of a specific App.
    /// </summary>
    public class Cover : ServiceCommand
    {
        /// <summary>
        /// The App to cover; currnetly only supports dotnet compatible targets and in this case it should be the full path to the app
        /// </summary>
        public string App { get; set; }
        /// <summary>
        /// The tags that will trigger this cover command to run
        /// </summary>
        public string Tag { get; set; }
        
        protected bool Equals(Cover other)
        {
            return App == other.App && Tag == other.Tag;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(App, Tag);
        }
    }

    public class CoverageWatching : Cover, ITestDomainEvent
    {
        public DateTime At { get; } = DateTime.Now;
    }
    
    public class CoverEventWorkflow
    {
        public class CoverageContext : Cover
        {
            public IDisposable CoverageProcess { get; set; }

        }

        private readonly IDictionary<Cover, CoverageContext> _watchers = new UseConcurrentReliableOpsWhenCastToIDictionary<Cover, CoverageContext>(new ConcurrentDictionary<Cover, CoverageContext>());

        public CoverEventWorkflow()
        {
            
        }

        public IObservable<IRxn> Run(Cover command, IObservable<ITestDomainEvent> unitTestEvents)
        {
            IsActive = true;
            return Rxn.Create<IRxn>(o =>
            {
                $"Coverage setup for {command.Tag}".LogDebug();

                //can use this event to update the UI later that the worker has configured itself for the command
                o.OnNext(new CoverageWatching()
                {
                    App = command.App,
                    Tag = command.Tag
                });

                _watchers.Add(command, new CoverageContext()
                {
                    App = command.App,
                    CoverageProcess = Rxn.Create(() =>
                    {
                        UnitTestsStarted currentTest = null;
                        IObservable<IDisposable> process = null;

                        unitTestEvents.OfType<UnitTestsStarted>()
                            .Where(t => IsTagged(t, command.Tag))
                            .Do(test =>
                            {
                                currentTest = test;

                                var watcher = unitTestEvents
                                    .OfType<UnitTestsStarted>()
                                    .Where(t => IsTagged(t, command.Tag))
                                    // .Where(t => t.tags) todo: need to find out how we get alerted on tags. probably not this event.. should we add it though?
                                    .Select(test =>
                                    {
                                        return process = StartCoverageForApp(command);
                                    })
                                    .Switch()
                                    .Until();

                                var finishTest = unitTestEvents.OfType<UnitTestOutcome>()
                                    .Where(t => t.UnitTestId == currentTest?.TestId)
                                    .FirstOrDefaultAsync()
                                    .Do(test =>
                                    {
                                        watcher?.Dispose();
                                    })
                                    .Until();
                            });
                    }).Until()
                });

                
                return Disposable.Create(() =>
                {
                    //this is teardown logic, used to "stop watching"
                    _watchers[command].CoverageProcess.Dispose();
                    _watchers.Remove(command);
                });
            });
        }

        public bool IsActive { get; set; }

        private IObservable<IDisposable> StartCoverageForApp(Cover command)
        {
            var cmd = GetCommandForPlatform(command);
            var args = "";

            return Rxn.Create(cmd, args, 
                onInfo =>
                {
                    onInfo.LogDebug("Cover");

                }, 
                onError =>
                {
                    onError.LogDebug("Cover");
                    //not sure if we should throw here?
                    //should we just continue to stream reactions?
                    //start app again and let it fail again?
                    //o.OnError(new Exception(onError)); 
                });
        }

        private string GetCommandForPlatform(Cover command)
        {
            var localisedCommand = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : "/bin/bash";

            //more needs to be done here

            return localisedCommand;

        }

        private bool IsTagged(UnitTestsStarted test, string commandTag)
        {

            //return test.tag == commandtag

            return false;
        }
    }
}
