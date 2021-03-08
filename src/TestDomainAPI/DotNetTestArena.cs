using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using Rxns;
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
        public IObservable<IDisposable> Start(string name, StartUnitTest work, StreamWriter testLog)
        {
            //https://github.com/dotnet/sdk/issues/5514
            var dotnetHack = GetDotNetOnPlatform();

            var logName = $"{name}{work.RunThisTest.IsNullOrWhiteSpace(new FileInfo(work.Dll).Name)}";


            //todo: make testrunner injectable/swapable
            return Rxn.Create
            (
                dotnetHack,
                $"test{FilterIfSingleTestOnly(work)} {EnsureRooted(work.Dll)} --results-directory {EnsureRooted("logs/")} --collect:\"XPlat Code Coverage\" --logger trx --no-build",
                i => testLog.WriteLine(i.LogDebug(logName)),
                e => testLog.WriteLine(e.LogDebug(logName)
                ));
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
                $"test {EnsureRooted(work.Dll)} --listtests",
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

        private string EnsureRooted(string workDll)
        {
            if (workDll.Length < 1) return workDll;

            if (workDll.StartsWith("/") || workDll[1] == ':')
                return workDll;

            return $"{Path.Combine(Environment.CurrentDirectory, workDll)}";
        }


        private string FilterIfSingleTestOnly(StartUnitTest work)
        {
            return work.RunThisTest.IsNullOrWhitespace() ? "" : $" --filter Name={work.RunThisTest}";
        }

    }
}
