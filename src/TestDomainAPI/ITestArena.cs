using System;
using System.Collections.Generic;
using System.IO;

namespace theBFG.TestDomainAPI
{
    public interface ITestArena
    {
        IObservable<IDisposable> Start(string name, StartUnitTest work, StreamWriter testLog);
        IObservable<IEnumerable<string>> ListTests(StartUnitTest work);
    }
}
