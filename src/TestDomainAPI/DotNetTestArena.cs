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
        bool isreadingOutputMessage = false;
        bool lastLine = false;
        StringBuilder outputBuffer = new StringBuilder();
        private bool startParsing;

        public override IEnumerable<IRxn> OnLog(string worker, StartUnitTest work, string msg)
        {
            var cmd = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (cmd.Length > 0)
            {
                lastLine = false;

                if (cmd[0] == "Passed" || cmd[0] == "Failed")
                {
                    yield return new UnitTestPartialResult(work.Id, cmd[0], cmd[1], cmd.Length > 2 ? ToDuration(cmd[2]) : "0", worker);
                }

                if (isreadingOutputMessage)
                {
                    outputBuffer.AppendLine(msg);
                }

                if (cmd[0] == "Standard")
                {
                    isreadingOutputMessage = true;
                }
            }
            else
            {
                if (lastLine)
                {
                    isreadingOutputMessage = false;
                    yield return new UnitTestPartialLogResult(work.Id, worker, outputBuffer.ToString());
                    outputBuffer.Clear();
                }

                if (isreadingOutputMessage)
                {
                    lastLine = true;
                }
            }
        }
        
        protected override string StartTestsCmd(StartUnitTest work)
        {
            return $"test{FilterIfSingleTestOnly(work)} {work.Dll.EnsureRooted()} --results-directory {"logs/".EnsureRooted()} --collect:\"XPlat Code Coverage\" --logger trx --no-build --logger \"console;verbosity=detailed\"";
        }

        private string ToDuration(string s)
        {
            return s.TrimStart('[').TrimEnd(']');
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

        protected string FilterIfSingleTestOnly(StartUnitTest work)
        {
            return work.RunThisTest.IsNullOrWhitespace() ? "" : $" --filter Name={work.RunThisTest.Replace(",", "|Name=")}";
        }
    }
}
