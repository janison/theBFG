namespace theBFG.TestDomainAPI
{
    public interface ITestArenaApi
    {
        void OnUpdate(ITestDomainEvent e);
        void SendCommand(string route, string command);
    }

}
