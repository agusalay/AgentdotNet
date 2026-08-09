// =============================================================================
// Property 5: Conversation history sliding window preserves recency
// Validates: Requirements 9.6
//
// For any sequence of N conversation turns added to the history provider,
// the provider SHALL retain exactly min(N, 10) turns, and those retained
// turns SHALL be the most recent ones in chronological order.
// =============================================================================

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using FluentAssertions;
using Tests.TestInfra;

namespace Tests.Properties;

/// <summary>
/// Property-based tests validating that ConversationHistoryProvider
/// implements a sliding window of 10 turns, always preserving the
/// most recent turns.
/// **Validates: Requirements 9.6**
/// </summary>
public class ConversationHistoryProperties
{
    /// <summary>
    /// Property 5: After adding N turns, the provider retains exactly min(N, 10) turns.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property SlidingWindow_RetainsMinNOr10Turns()
    {
        var gen = Gen.Choose(1, 50);

        return Prop.ForAll(gen.ToArbitrary(), n =>
        {
            var provider = new ConversationHistoryProvider();

            for (int i = 0; i < n; i++)
            {
                provider.StoreContextAsync($"user-msg-{i}", $"assistant-msg-{i}")
                    .GetAwaiter().GetResult();
            }

            var expected = Math.Min(n, 10);
            provider.TurnCount.Should().Be(expected,
                $"after adding {n} turns, provider should retain min({n}, 10) = {expected} turns");
            return true;
        });
    }

    /// <summary>
    /// Property 5: The retained turns are always the most recent ones.
    /// After adding N turns, the context should contain the last min(N, 10) turns.
    /// Uses bracket-delimited markers [turn:X] to avoid substring collision.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property SlidingWindow_PreservesMostRecentTurns()
    {
        var gen = Gen.Choose(1, 50);

        return Prop.ForAll(gen.ToArbitrary(), n =>
        {
            var provider = new ConversationHistoryProvider();

            for (int i = 0; i < n; i++)
            {
                provider.StoreContextAsync($"[userturn:{i}]", $"[assistturn:{i}]")
                    .GetAwaiter().GetResult();
            }

            var context = provider.ProvideContextAsync().GetAwaiter().GetResult();

            // The most recent turn should always be present
            var lastIndex = n - 1;
            context.Should().Contain($"[userturn:{lastIndex}]",
                "the most recent user message must be in context");
            context.Should().Contain($"[assistturn:{lastIndex}]",
                "the most recent assistant message must be in context");

            // The oldest retained turn should be at index max(0, N-10)
            var oldestRetainedIndex = Math.Max(0, n - 10);
            context.Should().Contain($"[userturn:{oldestRetainedIndex}]",
                $"the oldest retained turn (index {oldestRetainedIndex}) must be in context");

            // Turns older than the window should NOT be present
            if (n > 10)
            {
                var droppedIndex = n - 11; // This turn should have been dropped
                context.Should().NotContain($"[userturn:{droppedIndex}]",
                    $"turn at index {droppedIndex} should have been dropped from the window");
            }

            return true;
        });
    }

    /// <summary>
    /// Property 5: Sliding window with exactly 10 turns retains all of them.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property SlidingWindow_Exactly10Turns_RetainsAll()
    {
        var charGen = Gen.Elements('a', 'b', 'c', 'd', 'e', 'f');
        var msgGen = Gen.ArrayOf(charGen, 10).Select(c => new string(c));

        return Prop.ForAll(msgGen.ToArbitrary(), msgPrefix =>
        {
            var provider = new ConversationHistoryProvider();

            for (int i = 0; i < 10; i++)
            {
                provider.StoreContextAsync($"{msgPrefix}-user-{i}", $"{msgPrefix}-assist-{i}")
                    .GetAwaiter().GetResult();
            }

            provider.TurnCount.Should().Be(10,
                "exactly 10 turns should all be retained");

            var context = provider.ProvideContextAsync().GetAwaiter().GetResult();

            // All 10 turns should be present
            for (int i = 0; i < 10; i++)
            {
                context.Should().Contain($"{msgPrefix}-user-{i}");
                context.Should().Contain($"{msgPrefix}-assist-{i}");
            }

            return true;
        });
    }

    /// <summary>
    /// Property 5: ClearHistory resets TurnCount to zero regardless of previous state.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property ClearHistory_ResetsTurnCountToZero()
    {
        var gen = Gen.Choose(1, 50);

        return Prop.ForAll(gen.ToArbitrary(), n =>
        {
            var provider = new ConversationHistoryProvider();

            for (int i = 0; i < n; i++)
            {
                provider.StoreContextAsync($"msg-{i}", $"reply-{i}")
                    .GetAwaiter().GetResult();
            }

            provider.ClearHistory();

            provider.TurnCount.Should().Be(0,
                "after ClearHistory, TurnCount must be 0");

            var context = provider.ProvideContextAsync().GetAwaiter().GetResult();
            context.Should().BeEmpty(
                "after ClearHistory, context should be empty");

            return true;
        });
    }
}
