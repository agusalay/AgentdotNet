using FluentAssertions;
using Tests.TestInfra;
using Xunit;

namespace Tests.Unit;

/// <summary>
/// Unit tests for middleware pipeline execution order and short-circuit behavior.
/// Validates Requirements: 8.8
/// Uses GuardrailMiddleware and MiddlewareContext from TestInfra.
/// </summary>
public class MiddlewarePipelineTests
{
    [Fact]
    public async Task GuardrailMiddleware_ShortInput_ShouldPassThrough()
    {
        // Arrange
        var middleware = new GuardrailMiddleware();
        var context = new MiddlewareContext { Input = "Hello, how are you?" };
        var nextCalled = false;

        // Act
        await middleware.InvokeAsync(context, ctx =>
        {
            nextCalled = true;
            ctx.Output = "I'm fine, thanks!";
            return Task.CompletedTask;
        });

        // Assert
        nextCalled.Should().BeTrue("short input should pass through guardrail to next middleware");
        context.Output.Should().Be("I'm fine, thanks!");
    }

    [Fact]
    public async Task GuardrailMiddleware_LongInput_ShouldBlockAndNotCallNext()
    {
        // Arrange
        var middleware = new GuardrailMiddleware();
        var longInput = new string('x', 501); // exceeds 500 char limit
        var context = new MiddlewareContext { Input = longInput };
        var nextCalled = false;

        // Act
        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Assert
        nextCalled.Should().BeFalse("guardrail should short-circuit pipeline for invalid input");
        context.Output.Should().Contain("[BLOCKED]", "output should indicate blocked request");
        context.Output.Should().Contain("501", "output should show actual input length");
    }

    [Fact]
    public async Task GuardrailMiddleware_ExactlyAtLimit_ShouldPassThrough()
    {
        // Arrange
        var middleware = new GuardrailMiddleware();
        var exactInput = new string('a', 500); // exactly at the limit
        var context = new MiddlewareContext { Input = exactInput };
        var nextCalled = false;

        // Act
        await middleware.InvokeAsync(context, ctx =>
        {
            nextCalled = true;
            ctx.Output = "Processed";
            return Task.CompletedTask;
        });

        // Assert
        nextCalled.Should().BeTrue("input at exactly the limit should pass through");
    }

    [Fact]
    public async Task GuardrailMiddleware_WhenDisabled_ShouldAlwaysPassThrough()
    {
        // Arrange
        var middleware = new GuardrailMiddleware { IsEnabled = false };
        var longInput = new string('x', 1000); // way over limit but middleware disabled
        var context = new MiddlewareContext { Input = longInput };
        var nextCalled = false;

        // Act
        await middleware.InvokeAsync(context, ctx =>
        {
            nextCalled = true;
            ctx.Output = "Processed despite long input";
            return Task.CompletedTask;
        });

        // Assert
        nextCalled.Should().BeTrue("disabled middleware should not block any input");
        context.Output.Should().Be("Processed despite long input");
    }

    [Fact]
    public async Task MiddlewarePipeline_ShouldExecuteInOrder()
    {
        // Arrange — two middleware in sequence
        var executionOrder = new List<string>();

        var middleware1 = new TrackingMiddleware("First");
        var middleware2 = new TrackingMiddleware("Second");

        var context = new MiddlewareContext { Input = "test input" };

        // Act — chain middleware1 → middleware2 → terminal
        await middleware1.InvokeAsync(context, async ctx =>
        {
            executionOrder.Add("First");
            await middleware2.InvokeAsync(ctx, innerCtx =>
            {
                executionOrder.Add("Second");
                innerCtx.Output = "Final output";
                return Task.CompletedTask;
            });
        });

        // Assert
        executionOrder.Should().ContainInOrder("First", "Second");
        context.Output.Should().Be("Final output");
    }

    [Fact]
    public async Task MiddlewarePipeline_ShortCircuit_ShouldStopExecution()
    {
        // Arrange — guardrail blocks, second middleware never runs
        var guardrail = new GuardrailMiddleware();
        var longInput = new string('z', 600);
        var context = new MiddlewareContext { Input = longInput };
        var secondMiddlewareCalled = false;

        // Act — guardrail first, then tracking
        await guardrail.InvokeAsync(context, async ctx =>
        {
            // This is "next" for guardrail — it would call second middleware
            secondMiddlewareCalled = true;
            await Task.CompletedTask;
        });

        // Assert
        secondMiddlewareCalled.Should().BeFalse(
            "when guardrail blocks, no subsequent middleware should execute");
        context.Output.Should().Contain("[BLOCKED]");
    }

    [Fact]
    public async Task MiddlewarePipeline_RuntimeToggle_ShouldChangeMiddlewareBehavior()
    {
        // Arrange — Requirement 8.10: toggle middleware at runtime
        var guardrail = new GuardrailMiddleware();
        var longInput = new string('x', 600);

        // First call: enabled — should block
        var context1 = new MiddlewareContext { Input = longInput };
        await guardrail.InvokeAsync(context1, ctx =>
        {
            ctx.Output = "Passed";
            return Task.CompletedTask;
        });
        context1.Output.Should().Contain("[BLOCKED]");

        // Toggle off at runtime
        guardrail.IsEnabled = false;

        // Second call: disabled — should pass
        var context2 = new MiddlewareContext { Input = longInput };
        await guardrail.InvokeAsync(context2, ctx =>
        {
            ctx.Output = "Passed";
            return Task.CompletedTask;
        });
        context2.Output.Should().Be("Passed");
    }
}

/// <summary>
/// Simple tracking middleware for verifying execution order.
/// </summary>
internal class TrackingMiddleware : IAgentMiddleware
{
    public string Name { get; }
    public bool IsEnabled { get; set; } = true;

    public TrackingMiddleware(string name) => Name = name;

    public async Task InvokeAsync(MiddlewareContext context, Func<MiddlewareContext, Task> nextMiddleware)
    {
        // Simply delegate to next
        await nextMiddleware(context);
    }
}
