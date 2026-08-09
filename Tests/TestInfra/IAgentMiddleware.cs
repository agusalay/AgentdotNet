namespace Tests.TestInfra;

public interface IAgentMiddleware
{
    string Name { get; }
    bool IsEnabled { get; set; }
    Task InvokeAsync(MiddlewareContext context, Func<MiddlewareContext, Task> nextMiddleware);
}

public class MiddlewareContext
{
    public string Input { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
}
