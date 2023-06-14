using Rxns;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Xml.Linq;
using Rxns.Logging;
using Rxns.Hosting;
using Rxns.Interfaces;
using System.Reactive.Linq;
using Rxns.Collections;
using Rxns.Health;
using Rxns.Hosting.Updates;

namespace theBFG.TestDomainAPI
{
    public class UnitTestWorkflow
    {
        private static int _runId;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="work"></param>
        /// <param name="isBusy"></param>
        /// <param name="_rxnManager">Fix: this should not depend on rxnmgr, should return stream of unit test events instead</param>
        /// <param name="_updateService"></param>
        /// <param name="_cfg"></param>
        /// <param name="_appStatus"></param>
        /// <param name="_arena"></param>
        /// <param name="_zipService"></param>
        /// <returns></returns>
        public static IObservable<ITestDomainEvent> Run(string Name, StartUnitTest work, Action<bool> isBusy, IUpdateServiceClient _updateService, IAppStatusCfg _cfg, IAppStatusServiceClient _appStatus, Func<ITestArena[]> _arena, IZipService _zipService)
        {
            return Rxn.DfrCreate<ITestDomainEvent>(o =>
            {
                work.Dll = work.Dll.EnsureRooted();

                var logId = $"{Name.Replace("#", "")}_{++_runId}_{DateTime.Now:dd-MM-yy-hhmmssfff}";
                var logDir = Path.Combine(_cfg.AppRoot, "logs", logId).AsCrossPlatformPath();
                StreamWriter testLog = null;
                FileStream logFile = null;

                return Rxn.Create(() => //setup dirs for test
                {
                    if (!Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }

                    logFile = File.Create(Path.Combine(logDir, "testArena.log"));
                    testLog = new StreamWriter(logFile, leaveOpen: true);
                })
                .SelectMany(_ =>  //keep the test updated while we are running it
                {
                    isBusy(true);

                    return RunTestSuiteInTestArena(Name, work, testLog, logDir, _updateService, _cfg, _arena).Do(msg => o.OnNext(msg));
                })
                .LastOrDefaultAsync()
                .Delay(TimeSpan.FromSeconds(1))//test is finished, wait for log file to unlock
                .SelectMany(_ =>
                {
                    try
                    {
                        testLog?.Dispose();
                        logFile?.Dispose();
                    }
                    catch (Exception e)
                    {
                        $"While closing log {e}".LogDebug();
                    }

                    //send the log to the
                    return ShipLogForTest(Name, logDir, logId, work.Id, _appStatus, _cfg, _zipService).Do(msg => o.OnNext(msg)); //send logs to testArena
                })
                .Select(_ => //return result of process
                {
                    return (UnitTestResult)new UnitTestResult()
                    {
                        WasSuccessful = true
                    }.AsResultOf(work);
                })
                .Catch<UnitTestResult, Exception>(e =>
                {
                    return ((UnitTestResult)new UnitTestResult()
                    {
                        WasSuccessful = false
                    }.AsResultOf(work)).ToObservable();
                })
                .Do(o.OnNext)
                .FinallyR(() =>
                {
                    isBusy(false);
                    o.OnCompleted();
                })
                .Until();
            });

        }

        public static IObservable<ITestDomainEvent> RunTestSuiteInTestArena(string Name, StartUnitTest work, StreamWriter testLog, string logDir, IUpdateServiceClient _updateService, IAppStatusCfg appStatusCfg, Func<ITestArena[]> _arena)
        {
            return Rxn.DfrCreate<ITestDomainEvent>(o =>
            {
                $"Preparing to run {(work.RunAllTest ? "All" : work.RunThisTest)} in {work.Dll}".LogDebug();

                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                var keepTestUpdatedIfRequested =
                    work.UseAppUpdate.ToObservable(); //if not using updates, the dest folder is our root

                if (!File.Exists(work.Dll))
                {
                    if (work.UseAppUpdate.IsNullOrWhitespace())
                    {
                        o.OnNext((UnitTestResult)new UnitTestResult() { WasSuccessful = false, Message = $"Cannot find target @{work.Dll}".LogDebug() }.AsResultOf(work));
                        o.OnCompleted();

                        return Disposable.Empty;
                    }

                    keepTestUpdatedIfRequested = _updateService.KeepUpdated(work.UseAppUpdate, work.UseAppVersion, theBfg.GetTestSuiteDir(work.UseAppUpdate, work.UseAppVersion), new RxnAppCfg()
                    {
                        AppStatusUrl = work.AppStatusUrl.IsNullOrWhiteSpace("http://localhost:888"),
                        SystemName = work.UseAppUpdate,
                        KeepUpdated = true
                    }, true);
                }
                else
                {
                    work.Dll = FindIfNotExists(work.Dll);
                }

                return keepTestUpdatedIfRequested //run the test
                    .Select(testPath =>
                    {
                        $"Running {work.Dll}".LogDebug();

                        foreach (var arena in _arena())
                        {
                            try
                            {
                                var tests = arena.ListTests(work.Dll).WaitR();
                                if (tests.AnyItems())
                                    return arena.Start(Name, work, testLog, logDir).Do(msg => o.OnNext(msg));
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                        }

                        "Argh, couldnt find a test arena to run this test".LogDebug();
                        o.OnNext((UnitTestResult)new UnitTestResult() { WasSuccessful = false, Message = $"No test arena on host is compatible with {work.Dll}" }.AsResultOf(work));
                        return Rxn.Empty<ITestDomainEvent>();
                    })
                    .Switch()
                    .Catch<ITestDomainEvent, Exception>(e =>
                    {
                        o.OnError(e);
                        return Rxn.Empty<ITestDomainEvent>();
                    })
                    .FinallyR(() =>
                    {
                        o.OnCompleted();
                    })
                    .Until();


            });
        }


        /// <summary>
        /// Sends a set of logs to the test arena, returning a url to access the test via
        /// </summary>
        /// <param name="logDir"></param>
        /// <param name="logId"></param>
        /// <param name="testId"></param>
        /// <returns></returns>
        public static IObservable<ITestDomainEvent> ShipLogForTest(string Name, string logDir, string logId, string testId, IAppStatusServiceClient _appStatus, IAppStatusCfg cfg, IZipService zipService)
        {
            return Rxn.Create<ITestDomainEvent>(o =>
            {
                var logsZip = ZipAndTruncate(logDir, $"{logId}_{DateTime.Now:dd-MM-yy-hhmmssfff}", cfg, zipService);
                var sendingLogs = File.OpenRead(logsZip);
                return _appStatus.PublishLog(sendingLogs).Do(logUrl =>
                {
                    o.OnNext(new UnitTestAssetResult()
                    {
                        LogUrl = logUrl,
                        TestId = testId,
                        Worker = Name
                    });

                    sendingLogs.Dispose();
                    File.Delete(logsZip);
                })
                .FinallyR(() =>
                {
                    o.OnCompleted();
                }).Until();
            });
        }

        private static string ZipAndTruncate(string dir, string logId, IAppStatusCfg _cfg, IZipService _zipService)
        {
            if (dir.IsNullOrWhitespace() || (dir.Length < 3 && dir[0] == '/' || dir[1] == ':'))
            {
                throw new Exception($"Cant achieve {dir}");
            }


            var logFile = Path.Combine(_cfg.AppRoot, "logs", $"{logId}.zip");

            if (!Directory.Exists(dir))
            {
                return logFile;
            }



            using (var file = File.Create(logFile))
                _zipService.Zip(dir).CopyTo(file);

            Directory.Delete(dir, true);

            return logFile;
        }

        private static string FindIfNotExists(string workDll, string parent = null)
        {
            if (File.Exists(workDll))
                return workDll;

            throw new Exception($"Test Not Found: {workDll}");
        }
    }
}
