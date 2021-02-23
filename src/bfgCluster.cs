using System;
using System.Collections.Generic;
using Rxns.Cloud;
using Rxns.DDD.Commanding;
using Rxns.Interfaces;
using theBFG;
using theBFG.TestDomainAPI;

namespace RxnCreate
{
    public class QueueWorkDone : CommandResult
    {
    }

    public class bfgCluster : ElasticQueue<StartUnitTest, UnitTestResult>, IRxnProcessor<StartUnitTest>, IManageResources
    {
        public ClusterFanOut<StartUnitTest, UnitTestResult> Workflow = new ClusterFanOut<StartUnitTest, UnitTestResult>();

        public IObservable<IRxn> Process(StartUnitTest @event)
        {
            return Workflow.Fanout(@event);
        }

        public void Dispose()
        {
        }

        public void OnDispose(IDisposable obj)
        {
        }
    }


    


}