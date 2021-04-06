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
}
