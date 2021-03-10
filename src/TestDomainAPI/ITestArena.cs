using System;
using System.Collections.Generic;
using System.IO;
using Rxns.Interfaces;

namespace theBFG.TestDomainAPI
{
    public interface ITestArena
    {
        IObservable<IRxn> Start(string name, StartUnitTest work, StreamWriter testLog);
        IObservable<IEnumerable<string>> ListTests(StartUnitTest work);
    }

    //event flows for the test arena

    public class UnitTestPartialResult : IRxn
    {
        public string Duration { get; set; }
        public string Result { get; }
        public string TestName { get; }
        public string TestId { get; }

        public UnitTestPartialResult(string testId, string result, string testName, string duration)
        {
            Result = result;
            TestName = testName;
            Duration = duration;
            TestId = testId;
        }
    }

    public class UnitTestPartialLogResult : IRxn
    {
        public string LogMessage { get; set; }
        public string TestId { get; set; }
        
        public UnitTestPartialLogResult(string forTest, string logMessage)
        {
            TestId = forTest;
            LogMessage = logMessage;
        }
    }
}
