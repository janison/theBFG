using System;
using System.Collections.Generic;
using Rxns;
using Rxns.Interfaces;

namespace theBFG.TestDomainAPI
{
    public class VsTestArena : DotNetTestArena
    {
        private bool _freshTest;

        protected override string PathToTestArenaProcess()
        {
            return @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe";
        }

        protected override string StartTestsCmd(StartUnitTest work)
        {
            _freshTest = true;
            return $"{FilterIfSingleTestOnly(work)} {work.Dll.EnsureRooted()} /resultsdirectory:{"logs/".EnsureRooted()}";
        }


        public override IEnumerable<IRxn> OnLog(string worker, StartUnitTest work, string msg)
        {
            var cmd = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (cmd.Length > 0)
            {
                lastLine = false;

                if (cmd[0] == "Passed" || cmd[0] == "Failed")
                {
                    if (outputBuffer.Length > 0)
                    {
                        if (_freshTest)
                        {
                            _freshTest = false;
                            outputBuffer.Clear();
                        }

                        var withResultONFirstLine = outputBuffer.ToString(0, outputBuffer.Length < 2 ? 0 : outputBuffer.Length - 2);
                        var outputResultMarker = (int)withResultONFirstLine?.IndexOf(Environment.NewLine);
                        
                        yield return new UnitTestPartialLogResult(work.Id, worker, outputResultMarker > 0 ? withResultONFirstLine.Substring(outputResultMarker + 2, withResultONFirstLine.Length - outputResultMarker - 2) : "-");

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
    }
}
