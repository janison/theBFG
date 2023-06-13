﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Rxns;
using Rxns.DDD.Commanding;
using Rxns.Interfaces;

namespace theBFG.TestDomainAPI
{
    //event flows for the test arena
    public interface ITestDomainEvent : IRxn
    {
        DateTime At { get; }
    }

    /// <summary>
    /// A test potential unit test session has been located.
    /// </summary>
    public class UnitTestDiscovered : ITestDomainEvent
    {
        public DateTime At { get; set; } = DateTime.Now;

        public string Dll { get; set; }
        public string[] DiscoveredTests { get; set; }
    }

    /// <summary>
    /// An alias for StartUnitTest
    /// </summary>
    public class Target : StartUnitTest
    {

    }

    /// <summary>
    /// Reloads and fires at the last test session started
    /// </summary>
    public class Reload : ServiceCommand
    {

    }

    /// <summary>
    /// Stops all running tests on a worker
    /// </summary>
    public class StopUnitTest : ServiceCommand
    {
        public string UnitTestId { get; set; }

        public StopUnitTest()
        {

        }

        public StopUnitTest(string unitTestId)
        {
            UnitTestId = unitTestId;
        }
    }

    /// <summary>
    /// Starts a new unit test session.
    /// </summary>
    public class StartUnitTest : ServiceCommand, ITestDomainEvent
    {
        public DateTime At { get; set; } = DateTime.Now;

        public bool RunAllTest { get; set; }
        public string RunThisTest { get; set; }
        public int RepeatTests { get; set; }
        public bool InParallel { get; set; }
        public string Dll { get; set; }
        public string UseAppUpdate { get; set; }
        public string UseAppVersion { get; set; }
        public string AppStatusUrl { get; set; }
        public string Tags { get; set; } = string.Empty;

        public StartUnitTest()
        {

        }

        public StartUnitTest(string dll)
        {
            var tokens = dll.Split('$');
            Dll = tokens.FirstOrDefault();
            RunThisTest = tokens.Skip(1).FirstOrDefault();

            var fn = new FileInfo(dll);
            UseAppUpdate = $"{fn.Name}".Replace(fn.Extension, "");
        }

        public override string ToString()
        {
            if (!Dll.IsNullOrWhitespace() && UseAppVersion.IsNullOrWhitespace())
                return $"{GetType().Name} {Dll}{(RunThisTest.IsNullOrWhitespace() ? "" : $"${RunThisTest}")}";
            else
            {
                return $"{GetType().Name} {GetType().GetProperties().Where(p => p.Name != "Id" && p.Name != "At").Select(p => this.GetProperty(p.Name)).ToStringEach(" ")}";
            }

        }

        /// <summary>
        /// these overloads are optimised for text/based calling via servicecommands
        ///
        /// ie.
        /// StartUnitTest False  0 False C:/svn/bfg/theTestGimp/bin/Debug/netcoreapp3.1/theTestGimp.dll Test Latest  
        /// </summary>
        public StartUnitTest(string RunAllTest, string RepeatTests, string InParallel, string Dll, string UseAppUpdate, string UseAppVersion)
        {
            this.RunAllTest = bool.Parse(RunAllTest.IsNullOrWhiteSpace("false"));

            this.RepeatTests = int.Parse(RepeatTests.IsNullOrWhiteSpace("0"));
            this.InParallel = bool.Parse(InParallel.IsNullOrWhiteSpace("false"));
            this.Dll = Dll;
            this.RunThisTest = Dll.Split('$').Skip(1).FirstOrDefault();
            this.UseAppUpdate = UseAppUpdate;
            this.UseAppVersion = UseAppVersion;
            //            this.AppStatusUrl = AppStatusUrl;
            //          this.Range = Range;
        }

        /// <summary>
        /// these overloads are optimised for text/based calling via servicecommands
        /// </summary>
        public StartUnitTest(string RunAllTest, string RepeatTests, string InParallel, string Dll, string UseAppUpdate)
        {
            this.RunAllTest = bool.Parse(RunAllTest.IsNullOrWhiteSpace("false"));
            this.RepeatTests = int.Parse(RepeatTests.IsNullOrWhiteSpace("0"));
            this.InParallel = bool.Parse(InParallel.IsNullOrWhiteSpace("false"));
            this.Dll = Dll;
            this.RunThisTest = Dll.Split('$').Skip(1).FirstOrDefault();
            this.UseAppUpdate = UseAppUpdate;
            //this.UseAppVersion = UseAppVersion;
            //            this.AppStatusUrl = AppStatusUrl;
            //          this.Range = Range;
        }
    }

    public class UnitTestPartialResult : ITestDomainEvent
    {
        public string Duration { get; set; }
        public string Result { get; set; }
        public string TestName { get; set; }
        public string Worker { get; set; }
        public string TestId { get; set; }
        public string UnitTestId { get; set; }

        public UnitTestPartialResult()
        {
        }

        public UnitTestPartialResult(string testId, string result, string testName, string duration, string worker)
        {
            Result = result;
            TestName = testName;
            Duration = duration;
            TestId = testId;
            Worker = worker;
        }

        public DateTime At { get; set; } = DateTime.Now;
    }

    public class UnitTestPartialLogResult : ITestDomainEvent
    {
        public DateTime At { get; set; } = DateTime.Now;

        public string LogMessage { get; set; }
        public string Worker { get; set; }
        public string TestId { get; set; }
        public string UnitTestId { get; set; }

        public UnitTestPartialLogResult()
        {
        }
    }

    public class UnitTestResult : CommandResult, ITestDomainEvent
    {
        public DateTime At { get; set; } = DateTime.Now;

        public bool WasSuccessful { get; set; }
    }

    public class UnitTestAssetResult : ITestDomainEvent
    {
        public DateTime At { get; set; } = DateTime.Now;

        public string Worker { get; set; }
        public string TestId { get; set; }
        public string UnitTestId { get; set; }
        public string LogUrl { get; set; }
    }

    public class UnitTestOutcome : ITestDomainEvent
    {
        public DateTime At { get; set; } = DateTime.Now;

        public int Failed { get; set; }
        public int Passed { get; set; }
        public string UnitTestId { get; set; }

        public string InResponseTo { get; set; }
        public string Dll { get; set; }
    }

    public class UnitTestEventWorkflow
    {
        public static IObservable<IRxn> Run(string Name, Run command)
        {
            return Rxn.DfrCreate<IRxn>(o =>
            {
                var tokens = command.Cmd.Split(' ');
                var cmd = tokens[0];
                var worker = Name;

                o.OnNext(new UnitTestsStarted() {
                    TestId = command.Id,
                    At = DateTime.Now,
                    Tests = new[] { cmd },
                    Worker = "", //_appInfo.Name,
                    WorkerId = worker
                });

                var unitTestId = Guid.NewGuid().ToString();

                o.OnNext(new UnitTestPartialResult(command.Id, "passed", cmd, "0", worker) {
                    UnitTestId = unitTestId
                });

                return Rxn.Create
                    (
                        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : "/bin/bash",
                        $"{(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "/c" : "-c")} {command.Cmd}",
                        i =>
                        {
                            if (i == null) return;


                            o.OnNext(new UnitTestPartialLogResult
                            {
                                LogMessage = i.ToString(),
                                TestId = command.Id,
                                UnitTestId = unitTestId,
                                Worker = worker
                            });

                        },
                        e =>
                        {
                            o.OnNext(new UnitTestPartialLogResult
                            {
                                LogMessage = e.ToString(),
                                TestId = command.Id,
                                UnitTestId = unitTestId,
                                Worker = worker
                            });
                        }
                    )
                    .FinallyR(() =>
                    {
                        o.OnNext(new UnitTestOutcome()
                        {
                            Passed = 1,
                            Failed = 0,
                            InResponseTo = command.Id,
                            UnitTestId = unitTestId,
                            Dll = cmd
                        });
                        o.OnCompleted();
                    }).Until();
            });
            
        }
    }
}
