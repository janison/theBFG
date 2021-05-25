using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using Rxns;
using Rxns.Interfaces;
using Rxns.Logging;

namespace theBFG.TestDomainAPI
{
    public abstract class ProcessBasedTestArena : ITestArena
    {
        public abstract IEnumerable<ITestDomainEvent> OnEnd(string dll);
        protected abstract void OnStart(StartUnitTest work);


        public abstract IEnumerable<IRxn> OnLog(string worker, StartUnitTest work, string msg);

        public IObservable<IRxn> Start(string name, StartUnitTest work, StreamWriter testLog, string logDir)
        {
            var testEventStream = new Subject<IRxn>();
            //https://github.com/dotnet/sdk/issues/5514
            var dotnetHack = PathToTestArenaProcess();

            var logName = $"{name}{work.RunThisTest.Substring(0, work.RunThisTest.Length > 25 ? 25 : work.RunThisTest.Length).IsNullOrWhiteSpace(new FileInfo(work.Dll).Name)}".LogDebug("Targeting");

            bool isreadingOutputMessage = false;
            var lastLine = false;

            OnStart(work);
            return Rxn.Create
            (
                dotnetHack,
                StartTestsCmd(work, logDir),
                i =>
                {
                    if (i == null) return;

                    foreach (var progress in OnLog(name, work, i))
                        testEventStream.OnNext(progress);

                    if(testLog.BaseStream.CanWrite)
                        testLog.WriteLine(i.LogDebug(logName));
                },
                e => testLog.WriteLine(e.LogDebug(logName))
            )
            .FinallyR(() =>
            {
                foreach(var e in OnEnd(work.Dll))
                    testEventStream.OnNext(e);
                
                testEventStream.OnCompleted();
            })
            .SelectMany(_ => testEventStream)
          ;
        }

        protected abstract string StartTestsCmd(StartUnitTest work, string logDir);

        protected string ToDuration(string s)
        {
            return s.TrimStart('[').TrimEnd(']');
        }

        protected abstract string PathToTestArenaProcess();

        public IObservable<IEnumerable<string>> ListTests(string dll)
        {
            var testArenaProcess = PathToTestArenaProcess();
            var tests = new List<string>();

            return Rxn.Create
            (
                testArenaProcess,
                ListTestsCmd(dll),
                i =>
                {
                    i.LogDebug();

                    foreach (var test in OnTestCmdLog(i))
                        tests.Add(test);
                },
                e => $"failed to parse test: {e}".LogDebug()
            )
            .Aggregate(tests.ToObservable(), (a, b) => a)
            .SelectMany(e => e)
            ;
        }

        protected abstract IEnumerable<string> OnTestCmdLog(string s);

        protected abstract string ListTestsCmd(string dll);

    }
}
