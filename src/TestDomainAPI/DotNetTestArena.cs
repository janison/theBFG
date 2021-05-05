using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using Rxns;
using Rxns.Hosting;
using Rxns.Interfaces;
using Rxns.Logging;

namespace theBFG.TestDomainAPI
{
    /// <summary>
    /// To use coverage, add this to your test project
    /// > dotnet add package coverlet.collector
    ///
    /// Not thread safe
    /// </summary>
    public class DotNetTestArena : ProcessBasedTestArena
    {
        private readonly IRxnAppInfo _appInfo;
        protected bool isreadingOutputMessage = true;
        protected bool lastLine = false;
        protected  StringBuilder outputBuffer = new StringBuilder();
        protected StartUnitTest _work;
        private int _passed;
        private int _failed;
        private string _unitTestId = string.Empty;
        private bool _freshTest;

        private string _worker;

        //todo: fix state, this class is not multi-threadable
        protected bool startParsing;
        protected bool baddll;

        public DotNetTestArena(IRxnAppInfo appInfo)
        {
            _appInfo = appInfo;
        }

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
                        Tests = work.RunThisTest.IsNullOrWhiteSpace("").Split(',', StringSplitOptions.RemoveEmptyEntries),
                        Worker = _appInfo.Name,
                        WorkerId = worker
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

                    if(cmd.Length > 0)
                        yield return new UnitTestPartialResult(work.Id, cmd[0], cmd[1], cmd.Length > 2 ? ToDuration(cmd[2]) : "0", worker) { UnitTestId = _unitTestId };

                    yield break;
                }

                if (cmd[0] == ">>SCREENSHOT<<")
                {
                    yield return new UnitTestAssetResult() { Worker = _worker, LogUrl = "screenshot", TestId = _work.Id, UnitTestId = _unitTestId };
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

        protected override string StartTestsCmd(StartUnitTest work, string logDir)
        {
            return $"test{FilterIfSingleTestOnly(work)} {work.Dll.EnsureRooted()} --results-directory {logDir.EnsureRooted()} --collect:\"XPlat Code Coverage\" --no-build --logger \"console;verbosity=detailed;\" --logger \"trx;LogFileName={work.Id}.trx\"";
        }

        protected override string PathToTestArenaProcess()
        { 
            var dotnetHack = "c:/program files/dotnet/dotnet.exe";
            if (!File.Exists(dotnetHack))
                dotnetHack = "dotnet";

            return dotnetHack;
        }
        
        protected override IEnumerable<string> OnTestCmdLog(string i)
        {
            if (baddll || i.BasicallyContains("failed to discover tests"))
                yield break;

            if (i != null && i.Contains("are available:"))
            {
                startParsing = true;
                yield break;
            }

            if (startParsing && i != null && i.Contains("vstest") && i.EndsWith("exited"))
            {
                startParsing = false;
            }

            if (!i.IsNullOrWhitespace() && i.BasicallyContains("Exception discovering") && i.BasicallyContains("TestPlatformException"))
            {
                baddll = true;
            }

            if (startParsing && i != null && i.StartsWith("  ") && !i.StartsWith("   at "))
            {
                yield return i.Trim();
            }
        }


        protected override string ListTestsCmd(string dll)
        {
            baddll = false;
            startParsing = true;

            return $"test {dll.EnsureRooted()} --listtests";
        }

        protected virtual string FilterIfSingleTestOnly(StartUnitTest work)
        {
            return work.RunThisTest.IsNullOrWhitespace() ? "" : $" --filter Name={work.RunThisTest.Replace(",", "|Name=")}";
        }
    }
}
