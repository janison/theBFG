using System;
using System.Collections.Generic;
using System.Text;
using Rxns;
using Rxns.Interfaces;

namespace theBFG.TestDomainAPI
{
    public class VsTestArena : ProcessBasedTestArena
    {
        private bool _freshTest;
        protected bool isreadingOutputMessage = false;
        protected bool lastLine = false;
        protected StringBuilder outputBuffer = new StringBuilder();
        protected bool startParsing;
        private int _passed;
        private int _failed;
        private StartUnitTest _work;

        protected override string PathToTestArenaProcess()
        {
            return @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe";
        }

        protected override string StartTestsCmd(StartUnitTest work)
        {
            _work = work;
            _freshTest = true;
            _passed = 0;
            _failed = 0;

            return $"{FilterIfSingleTestOnly(work)} {work.Dll.EnsureRooted()} /resultsdirectory:{"logs/".EnsureRooted()}";
        }
        
        protected override string ListTestsCmd(StartUnitTest work)
        {
            return $"{work.Dll.EnsureRooted()} --listtests";
        }

        public override IEnumerable<IRxn> OnLog(string worker, StartUnitTest work, string msg)
        {
            var cmd = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (cmd.Length > 0)
            {
                lastLine = false;

                if (cmd[0] == "Passed" || cmd[0] == "Failed")
                {
                    if (cmd[0].StartsWith('P'))
                        _passed++;
                    else if (cmd[0].StartsWith('F'))
                        _failed++;

                    if (outputBuffer.Length > 0)
                    {
                        if (_freshTest)
                        {
                            _freshTest = false;
                            outputBuffer.Clear();
                        }

                        var withResultONFirstLine = outputBuffer.ToString(0, outputBuffer.Length < 2 ? 0 : outputBuffer.Length - 2);
                        var outputResultMarker = (int)withResultONFirstLine?.IndexOf(Environment.NewLine);
                        
                        if(outputResultMarker > 0)
                            yield return new UnitTestPartialLogResult(work.Id, worker, withResultONFirstLine.Substring(outputResultMarker + 2));
                        else
                        {
                            yield return new UnitTestOutcome()
                            {
                                Passed = _passed,
                                Failed = _failed,
                                InResponseTo = _work.Id
                            };
                        }

                        outputBuffer.Clear();
                    }

                    yield return new UnitTestPartialResult(work.Id, cmd[0], cmd[1], cmd.Length > 2 ? ToDuration(cmd[2]) : "0", worker);
                    outputBuffer.Clear();
                    isreadingOutputMessage = true;
                }

                if (isreadingOutputMessage)
                {
                    outputBuffer.AppendLine(msg);
                }
            }
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

            if (startParsing && i != null && i.Contains("vstest") && i.EndsWith("exited"))
            {
                startParsing = false;
            }

            if (startParsing)
            {
                yield return i?.Trim();
            }
        }


        protected string FilterIfSingleTestOnly(StartUnitTest work)
        {
            return work.RunThisTest.IsNullOrWhitespace() ? "" : $" /Tests:{work.RunThisTest}";
        }

    }
}
