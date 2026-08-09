namespace Tests.TestInfra;

public interface IContextProvider
{
    string Name { get; }
    Task<string> ProvideContextAsync();
    Task StoreContextAsync(string userMessage, string assistantMessage);
}
