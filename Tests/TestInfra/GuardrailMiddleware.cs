namespace Tests.TestInfra;

public class GuardrailMiddleware : IAgentMiddleware
{
    private const int MaxInputLength = 500;

    public bool IsEnabled { get; set; } = true;

    public string Name => "GuardrailMiddleware";

    public async Task InvokeAsync(MiddlewareContext context, Func<MiddlewareContext, Task> nextMiddleware)
    {
        if (!IsEnabled)
        {
            await nextMiddleware(context);
            return;
        }

        if (context.Input.Length > MaxInputLength)
        {
            context.Output = $"[BLOCKED] Input melebihi {MaxInputLength} karakter. " +
                             $"Input Anda: {context.Input.Length} karakter. " +
                             $"Harap kurangi panjang input.";
            return;
        }

        await nextMiddleware(context);
    }
}
