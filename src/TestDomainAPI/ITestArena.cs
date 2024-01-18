using System;
using System.Collections.Generic;
using System.IO;
using Rxns.Interfaces;

namespace theBFG.TestDomainAPI
{
    public interface ITestArena
    {
        IObservable<ITestDomainEvent> Start(string name, StartUnitTest work, StreamWriter testLog, string logDir);
        IObservable<IEnumerable<string>> ListTests(string dll);
    }
}
