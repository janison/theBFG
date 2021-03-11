using Rxns.DDD.Commanding;

namespace theBFG.TestDomainAPI
{
    public class UnitTestResult : CommandResult, ITestDomainEvent
    {
        public bool WasSuccessful { get; set; }
    }
}