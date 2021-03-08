using System;
using System.Linq;
using System.Reactive.Linq;
using Rxns;
using Rxns.Cloud;
using Rxns.Hosting;
using Rxns.Logging;

namespace theBFG
{
    public class bfgTestApi
    {
        public static IDisposable AdvertiseForWorkers(SsdpDiscoveryService discovery, string apiName = null, string hostUrl = "http://localhost:888/")
        {
            return discovery.Advertise("bfg-worker-queue", apiName, hostUrl).Until();
        }

        //to load a range of tests with dotnet test i cant use filters
        //will need to discover_tests in the dll first, parse the list of tests and divide them up
        //and stitch a list together
        public static IObservable<ApiHeartbeat> DiscoverWork(IAppServiceDiscovery services, string apiName = null)
        {
            return services.Discover()
                .Where(msg => msg.Name.Contains("bfg-worker-queue") &&
                              (apiName.IsNullOrWhitespace() || msg.Name.BasicallyContains(apiName)))
                .Select(m =>
                {
                    var tokens = m.Name.Split(':');
                    m.Name = tokens.Reverse().Skip(1).FirstOrDefault();
                    return m;
                })
                .Do(msg =>
                {
                    $"Discovered target {msg.Name} @ {msg.Url}".LogDebug();
                });
        }
    }
}
