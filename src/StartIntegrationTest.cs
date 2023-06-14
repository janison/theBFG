using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Rxns;
using Rxns.DDD;
using Rxns.DDD.Commanding;
using Rxns.Logging;
using theBFG.TestDomainAPI;

namespace theBFG
{
    public class StartIntegrationTest : ServiceCommand, ITestDomainEvent
    {
        public string Expression { get; }
        public DateTime At { get; set; } = DateTime.Now;
        public string UseAppUpdate { get; set; }
        public string UseAppVersion { get; set; }
        public string AppStatusUrl { get; set; }

        public StartIntegrationTest()
        {

        }



        public StartIntegrationTest(string expression)
        {
            Expression = expression;
        }
        
        public override string ToString()
        {
            return $"{GetType().Name} {GetType().GetProperties().Where(p => p.Name != "Id" || p.Name != "At").Select(p => this.GetProperty(p.Name)).ToStringEach(" ")}";
        }
    }

    public class TestWorkflow
    {
        public static IObservable<StartUnitTest> StartIntegrationTest(string testExpression, IServiceCommandFactory cmds, IObservable<UnitTestResult> results)
        {
            var serial = testExpression.Split("\n", StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => !t.IsNullOrWhitespace()).ToArray();
            
            var stageNo = 0;

            return Rxn.Create<StartUnitTest>(o =>
            {
                var resources = new CompositeDisposable();

                return serial
                        .SelectMany(cmd =>
                        {
                            var stageTrigger = new Subject<Unit>();
                            
                            var parallel = cmd.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .SelectMany(t =>
                            {
                                var testSytax = t.Split(';', StringSplitOptions.RemoveEmptyEntries);

                                return Enumerable.Range(0, Int32.Parse(testSytax.Skip(1).FirstOrDefault().IsNullOrWhiteSpace("1"), NumberStyles.Any)).Select(_ => testSytax.Length > 0 ? testSytax[0].Trim() : string.Empty);
                            })
                            .Select(action =>
                            {
                                var tokens = action.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                return cmds.Get(tokens.First(), tokens.Skip(1));
                            })
                            .ToArray();

                            if (parallel.Length < 1) return new Unit().ToObservable();
                        
                            $"Starting stage {++stageNo} with {parallel.Length} tests in parallel".LogDebug();

                            var expectedResultIds = parallel.Select(t => t.Id).ToList();
                            
                            results.Synchronize().Where(tr =>
                            {

                                if (expectedResultIds.Contains(tr.InResponseTo))
                                {
                                    expectedResultIds.Remove(tr.InResponseTo);
                                }

                                if (expectedResultIds.Count < 1)
                                {
                                    $"Stage {stageNo} complete".LogDebug();
                                    stageTrigger.OnNext(new Unit());
                                    stageTrigger.OnCompleted();
                                    return true;
                                }

                                return false;
                            })
                            .FirstOrDefaultAsync()
                            .Until()
                            .DisposedBy(resources);

                            parallel.ForEach(t => o.OnNext((StartUnitTest)t));

                            return stageTrigger;
                        })
                        .Where(_ => stageNo >= serial.Length)
                        .FirstOrDefaultAsync()
                        .Do(_ => o.OnCompleted())
                        .FinallyR(() => resources.Dispose())
                        .Until()
                        ;
            });
        }
    }

    public class IntegrationTestResult : ICommandResult
    {
        public string InResponseTo { get; set; }
        public CmdResult Result { get; set;  }
    }
}
