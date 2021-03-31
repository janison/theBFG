using System;
using Rxns.DDD.Commanding;

namespace theBFG.TestDomainAPI
{
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
        
        public string InResponseTo { get; set; }
    }
}