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
    public class DotNetTestArena : ITestArena
    {
        public IObservable<IRxn> Start(string name, StartUnitTest work, StreamWriter testLog)
        {
            var testEventStream = new Subject<IRxn>();
            //https://github.com/dotnet/sdk/issues/5514
            var dotnetHack = GetDotNetOnPlatform();

            var logName = $"{name}{work.RunThisTest.IsNullOrWhiteSpace(new FileInfo(work.Dll).Name)}";

            bool isreadingOutputMessage = false;
            var lastLine = false;
            var outputBuffer = new StringBuilder();
            //todo: make testrunner injectable/swapable
            return Rxn.Create
            (
                dotnetHack,
                $"test{FilterIfSingleTestOnly(work)} {work.Dll.EnsureRooted()} --results-directory {"logs/".EnsureRooted()} --collect:\"XPlat Code Coverage\" --logger trx --no-build --logger \"console;verbosity=detailed\"",
                i =>
                {
                    if(i == null) return;
                    
                    var cmd = i.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (cmd.Length > 0)
                    {
                        lastLine = false;

                        if (cmd[0] == "Passed")
                        {
                            testEventStream.OnNext(new UnitTestPartialResult(work.Id, cmd[0], cmd[1], ToDuration(cmd[2])));
                        }

                        if (isreadingOutputMessage)
                        {
                            outputBuffer.AppendLine(i);
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
                            testEventStream.OnNext(new UnitTestPartialLogResult(work.Id, outputBuffer.ToString()));
                        }

                        if (isreadingOutputMessage)
                        {
                            lastLine = true;
                        }
                    }

                    testLog.WriteLine(i.LogDebug(logName));
                },
                e => testLog.WriteLine(e.LogDebug(logName))
            ).SelectMany(_ => testEventStream);
        }

        private string ToDuration(string s)
        {
            return s.TrimStart('[').TrimEnd(']');
        }

        private string GetDotNetOnPlatform()
        {
            var dotnetHack = "c:/program files/dotnet/dotnet.exe";
            if (!File.Exists(dotnetHack))
                dotnetHack = "dotnet";

            return dotnetHack;
        }

        public IObservable<IEnumerable<string>> ListTests(StartUnitTest work)
        {
            var dotnet = GetDotNetOnPlatform();
            var tests = new List<string>();

            var startParsing = false;
            return Rxn.Create
            (
                dotnet,
                $"test {work.Dll.EnsureRooted()} --listtests",
                i =>
                {
                    i.LogDebug();

                    if (i != null && i.Contains("are available:"))
                    {
                        startParsing = true;
                        return;
                    }

                    if (startParsing && i.IsNullOrWhitespace())
                    {
                        startParsing = false;
                    }

                    if (startParsing)
                    {
                        tests.Add(i?.Trim());
                    }
                },
                e => $"failed to parse test: {e}".LogDebug()
            )
            .Aggregate(tests.ToObservable(), (a, b) => a)
            .SelectMany(e => e)
            ;
        }

        private string FilterIfSingleTestOnly(StartUnitTest work)
        {
            return work.RunThisTest.IsNullOrWhitespace() ? "" : $" --filter Name={work.RunThisTest.Replace(",", "|Name=")}";
        }
    }


}
