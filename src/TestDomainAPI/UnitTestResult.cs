using Rxns.DDD.Commanding;

namespace theBFG.TestDomainAPI
{
    public class UnitTestResult : CommandResult
    {
        public bool WasSuccessful { get; set; }
    }
}