using System;
using Rxns.DDD.Commanding;

namespace theBFG.TestDomainAPI
{
    public class UnitTestResult : CommandResult, ITestDomainEvent
    {
        public DateTime At { get; set; } = DateTime.Now;

        public bool WasSuccessful { get; set; }
    }
}