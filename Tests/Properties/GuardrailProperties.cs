// =============================================================================
// Property 4: Guardrail middleware validates input length correctly
// Validates: Requirements 8.7, 8.9
//
// For any input string, if its length exceeds 500 characters the guardrail
// middleware SHALL reject the request without forwarding to the agent, and if
// its length is 500 characters or fewer the middleware SHALL allow the request
// to pass through.
// =============================================================================

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using FluentAssertions;
using Tests.TestInfra;

namespace Tests.Properties;

/// <summary>
/// Property-based tests validating that GuardrailMiddleware correctly
/// validates input length: reject > 500 chars, allow &lt;= 500 chars.
/// **Validates: Requirements 8.7, 8.9**
/// </summary>
public class GuardrailProperties
{
    /// <summary>
    /// Property 4: Input exceeding 500 characters is always rejected
    /// (short-circuited) without forwarding to the next middleware.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property InputExceeding500Chars_IsRejected()
    {
        var charGen = Gen.Elements(
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't');
        var gen = Gen.Choose(501, 1500).SelectMany(length =>
            Gen.ArrayOf(charGen, length)
            .Select(chars => new string(chars)));

        return Prop.ForAll(gen.ToArbitrary(), input =>
        {
            var middleware = new GuardrailMiddleware();
            var context = new MiddlewareContext { Input = input };
            var nextCalled = false;

            middleware.InvokeAsync(context, _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();

            nextCalled.Should().BeFalse(
                "next should NOT be called for input > 500 chars");
            context.Output.Should().Contain("BLOCKED");
            return true;
        });
    }

    /// <summary>
    /// Property 4: Input of 500 characters or fewer is always
    /// allowed through to the next middleware.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property InputAtOrBelow500Chars_IsAllowed()
    {
        var charGen = Gen.Elements(
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't');
        var gen = Gen.Choose(1, 500).SelectMany(length =>
            Gen.ArrayOf(charGen, length)
            .Select(chars => new string(chars)));

        return Prop.ForAll(gen.ToArbitrary(), input =>
        {
            var middleware = new GuardrailMiddleware();
            var context = new MiddlewareContext { Input = input };
            var nextCalled = false;

            middleware.InvokeAsync(context, _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();

            nextCalled.Should().BeTrue(
                "next should be called for input <= 500 chars");
            context.Output.Should().NotContain("BLOCKED");
            return true;
        });
    }

    /// <summary>
    /// Property 4: Boundary - input of exactly 500 characters passes.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property InputExactly500Chars_IsAllowed()
    {
        var charGen = Gen.Elements(
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j');
        var gen = Gen.ArrayOf(charGen, 500)
            .Select(chars => new string(chars));

        return Prop.ForAll(gen.ToArbitrary(), input =>
        {
            var middleware = new GuardrailMiddleware();
            var context = new MiddlewareContext { Input = input };
            var nextCalled = false;

            middleware.InvokeAsync(context, _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();

            input.Length.Should().Be(500);
            nextCalled.Should().BeTrue(
                "input of exactly 500 chars must pass through");
            return true;
        });
    }

    /// <summary>
    /// Property 4: Boundary - input of exactly 501 characters is rejected.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property InputExactly501Chars_IsRejected()
    {
        var charGen = Gen.Elements(
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j');
        var gen = Gen.ArrayOf(charGen, 501)
            .Select(chars => new string(chars));

        return Prop.ForAll(gen.ToArbitrary(), input =>
        {
            var middleware = new GuardrailMiddleware();
            var context = new MiddlewareContext { Input = input };
            var nextCalled = false;

            middleware.InvokeAsync(context, _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();

            input.Length.Should().Be(501);
            nextCalled.Should().BeFalse(
                "input of exactly 501 chars must be blocked");
            context.Output.Should().Contain("BLOCKED");
            return true;
        });
    }

    /// <summary>
    /// Property 4: When middleware is disabled, all inputs pass through
    /// regardless of length.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property DisabledMiddleware_AllInputsPassThrough()
    {
        var charGen = Gen.Elements(
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j');
        var gen = Gen.Choose(501, 1000).SelectMany(length =>
            Gen.ArrayOf(charGen, length)
            .Select(chars => new string(chars)));

        return Prop.ForAll(gen.ToArbitrary(), input =>
        {
            var middleware = new GuardrailMiddleware { IsEnabled = false };
            var context = new MiddlewareContext { Input = input };
            var nextCalled = false;

            middleware.InvokeAsync(context, _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();

            nextCalled.Should().BeTrue(
                "disabled middleware should always pass through");
            return true;
        });
    }
}
