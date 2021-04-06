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
    /// <summary>
    /// To use coverage, add this to your test project
    /// > dotnet add package coverlet.collector
    /// 
    /// </summary>
    public class DotNetTestArena : ProcessBasedTestArena
    {
        protected bool isreadingOutputMessage = true;
        protected bool lastLine = false;
        protected  StringBuilder outputBuffer = new StringBuilder();
        protected bool startParsing;
        protected StartUnitTest _work;
        private int _passed;
        private int _failed;
        private string _unitTestId = string.Empty;
        private bool _freshTest;

        private string _worker;
        //^ yep im abusing inhertance without apology

        public override IEnumerable<IRxn> OnLog(string worker, StartUnitTest work, string msg)
        {
            var cmd = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            _worker = worker;

            if (cmd.Length > 0)
            {
                if (_freshTest)
                {
                    yield return new UnitTestsStarted()
                    {
                        TestId = work.Id,
                        At = DateTime.Now,
                        Tests = work.RunThisTest.IsNullOrWhiteSpace("").Split(',', StringSplitOptions.RemoveEmptyEntries)
                    };

                    _freshTest = false;
                    outputBuffer.Clear();
                }

                lastLine = false;

                if (cmd[0] == "Passed" || cmd[0] == "Failed")
                {
                    if(_unitTestId != string.Empty)
                        yield return new UnitTestPartialLogResult
                        {
                            LogMessage = outputBuffer.ToString(),
                            TestId = work.Id,
                            UnitTestId = _unitTestId,
                            Worker = worker
                        };

                    outputBuffer.Clear();

                    _unitTestId = Guid.NewGuid().ToString();

                    if (cmd[0].StartsWith('P'))
                        _passed++;
                    else if (cmd[0].StartsWith('F'))
                        _failed++;


                    yield return new UnitTestPartialResult(work.Id, cmd[0], cmd[1], cmd.Length > 2 ? ToDuration(cmd[2]) : "0", worker) { UnitTestId = _unitTestId };
                    yield break;
                }

                if (isreadingOutputMessage)
                {
                    outputBuffer.AppendLine(msg);
                }

                if (cmd[0] == "Standard" || cmd[0] == "Error")
                {
                    isreadingOutputMessage = true;
                }
            }
        }

        public override IEnumerable<ITestDomainEvent> OnEnd(string dll)
        {
            yield return new UnitTestPartialLogResult() { Worker = _worker, TestId = _work.Id, LogMessage = outputBuffer.ToString(), UnitTestId = _unitTestId };

            yield return new UnitTestOutcome()
            {
                Passed = _passed,
                Failed = _failed,
                InResponseTo = _work.Id,
                UnitTestId = _unitTestId,
                Dll = dll
            };
        }

        protected override void OnStart(StartUnitTest work)
        {
            _work = work;
            _passed = 0;
            _freshTest = true;
            _failed = 0;
            _unitTestId = string.Empty;
        }

        protected override string StartTestsCmd(StartUnitTest work)
        {
            return $"test{FilterIfSingleTestOnly(work)} {work.Dll.EnsureRooted()} --results-directory {"logs/".EnsureRooted()} --collect:\"XPlat Code Coverage\" --logger:\" --no-build --logger \"console;verbosity=detailed;trx;LogFileName={work.Id}.trx\"";
        }

        protected override string PathToTestArenaProcess()
        { 
            var dotnetHack = "c:/program files/dotnet/dotnet.exe";
            if (!File.Exists(dotnetHack))
                dotnetHack = "dotnet";

            return dotnetHack;
        }
        
        protected override IEnumerable<string> OnTestCmdLog(StartUnitTest work, string i)
        {
            if (i != null && i.Contains("are available:"))
            {
                startParsing = true;
                yield break;
            }

            if (startParsing && i.IsNullOrWhitespace())
            {
                startParsing = false;
            }

            if (startParsing)
            {
                yield return i?.Trim();
            }
        }

        protected override string ListTestsCmd(StartUnitTest work)
        {
            return $"test {work.Dll.EnsureRooted()} --listtests";
        }

        protected virtual string FilterIfSingleTestOnly(StartUnitTest work)
        {
            return work.RunThisTest.IsNullOrWhitespace() ? "" : $" --filter Name={work.RunThisTest.Replace(",", "|Name=")}";
        }
    }
}
