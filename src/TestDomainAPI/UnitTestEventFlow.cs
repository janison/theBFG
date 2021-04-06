using System;
using Rxns.DDD.Commanding;
using Rxns.Interfaces;

namespace theBFG.TestDomainAPI
{
    //event flows for the test arena
    public interface ITestDomainEvent : IRxn
    {
        DateTime At { get; }
    }
    
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

        public StartUnitTest()
        {

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

    public class UnitTestOutcome : ITestDomainEvent
    {
        public DateTime At { get; set; } = DateTime.Now;

        public int Failed { get; set; }
        public int Passed { get; set; }
        public string UnitTestId { get; set; }

        public string InResponseTo { get; set; }
        public string Dll { get; set; }
    }
}
