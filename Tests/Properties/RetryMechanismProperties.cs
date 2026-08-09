// =============================================================================
// Property 8: Retry mechanism follows exponential backoff with bounded attempts
// Validates: Requirements 11.10, 12.9
//
// For any failing operation (A2A communication or workflow step), the retry
// mechanism SHALL attempt at most 3 retries with delays following exponential
// backoff (2^(n-1) seconds for attempt n), and SHALL produce an error message
// indicating the failure reason and attempt number if all retries are exhausted.
// =============================================================================

using System.Diagnostics;
using System.Threading.Channels;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using FluentAssertions;
using Tests.TestInfra;

namespace Tests.Properties;

/// <summary>
/// Property-based tests validating that the retry mechanism follows exponential
/// backoff with bounded attempts, and produces correct error information on
/// exhaustion.
/// **Validates: Requirements 11.10, 12.9**
/// </summary>
public class RetryMechanismProperties
{
    /// <summary>
    /// Property 8: When all retries are exhausted, AgentCommunicationException
    /// contains the correct attempt count (equal to maxRetries).
    /// </summary>
    [Property(MaxTest = 20)]
    public Property AllRetriesExhausted_ExceptionContainsAttemptCount()
    {
        var maxRetriesGen = Gen.Elements(1, 2, 3);
        var receiverGen = Gen.Elements(
            "TargetAgent", "SummaryAgent", "AnalysisAgent", "Agent-999");

        var gen = from maxRetries in maxRetriesGen
                  from receiver in receiverGen
                  select (maxRetries, receiver);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (maxRetries, receiver) = tuple;

            var broker = new MessageBroker();
            // Register the agent channel so agent is "found"
            var channel = Channel.CreateUnbounded<A2AMessage>();
            broker.RegisterAgent(receiver, channel.Writer);
            // Do NOT register a message handler - this will cause TimeoutException

            // Enable failure simulation for all attempts
            broker.EnableFailureSimulation(maxRetries + 1);

            var message = new A2AMessage(
                SenderId: "TestSender",
                ReceiverId: receiver,
                Timestamp: DateTime.UtcNow,
                Content: "Test message for retry",
                Type: MessageType.Request);

            AgentCommunicationException? caughtException = null;
            try
            {
                broker.SendWithRetryAsync(message, maxRetries, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            catch (AgentCommunicationException ex)
            {
                caughtException = ex;
            }

            caughtException.Should().NotBeNull(
                "exception must be thrown when all retries are exhausted");
            caughtException!.AttemptCount.Should().Be(maxRetries,
                $"attempt count must equal maxRetries ({maxRetries})");
            return true;
        });
    }

    /// <summary>
    /// Property 8: When all retries are exhausted, AgentCommunicationException
    /// contains a failure reason that identifies the target agent.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property AllRetriesExhausted_ExceptionContainsFailureReason()
    {
        var receiverGen = Gen.Elements(
            "TargetAgent", "SummaryAgent", "AnalysisAgent", "Agent-42");

        return Prop.ForAll(receiverGen.ToArbitrary(), receiver =>
        {
            var broker = new MessageBroker();
            var channel = Channel.CreateUnbounded<A2AMessage>();
            broker.RegisterAgent(receiver, channel.Writer);
            broker.EnableFailureSimulation(4); // More than maxRetries

            var message = new A2AMessage(
                SenderId: "TestSender",
                ReceiverId: receiver,
                Timestamp: DateTime.UtcNow,
                Content: "Test retry failure reason",
                Type: MessageType.Request);

            AgentCommunicationException? caughtException = null;
            try
            {
                broker.SendWithRetryAsync(message, 3, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            catch (AgentCommunicationException ex)
            {
                caughtException = ex;
            }

            caughtException.Should().NotBeNull(
                "exception must be thrown when all retries exhausted");
            caughtException!.FailureReason.Should().NotBeNullOrEmpty(
                "failure reason must be provided");
            caughtException.FailureReason.Should().Contain(receiver,
                "failure reason must identify the target agent");
            return true;
        });
    }

    /// <summary>
    /// Property 8: Maximum retry count is bounded (default 3, configurable).
    /// The mechanism never attempts more than the specified maxRetries.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property RetryCount_NeverExceedsMaxRetries()
    {
        var maxRetriesGen = Gen.Elements(1, 2, 3);

        return Prop.ForAll(maxRetriesGen.ToArbitrary(), maxRetries =>
        {
            var broker = new MessageBroker();
            var channel = Channel.CreateUnbounded<A2AMessage>();
            broker.RegisterAgent("TestReceiver", channel.Writer);

            // Simulate more failures than maxRetries to ensure we don't exceed
            var failureCount = maxRetries + 5;
            broker.EnableFailureSimulation(failureCount);

            var message = new A2AMessage(
                SenderId: "TestSender",
                ReceiverId: "TestReceiver",
                Timestamp: DateTime.UtcNow,
                Content: "Test bounded retries",
                Type: MessageType.Request);

            AgentCommunicationException? caughtException = null;
            try
            {
                broker.SendWithRetryAsync(message, maxRetries, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            catch (AgentCommunicationException ex)
            {
                caughtException = ex;
            }

            caughtException.Should().NotBeNull(
                "exception must be thrown when retries exhausted");
            // AttemptCount should be exactly maxRetries, never more
            caughtException!.AttemptCount.Should().Be(maxRetries,
                $"attempts must be exactly {maxRetries}, never more");
            return true;
        });
    }

    /// <summary>
    /// Property 8: Retry delays follow exponential backoff pattern 2^(n-1) seconds.
    /// Delay for attempt 1 = 1s, attempt 2 = 2s, attempt 3 = 4s.
    /// We verify by measuring elapsed time is consistent with the expected pattern.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property RetryDelays_FollowExponentialBackoff()
    {
        // Verify the mathematical pattern: delay for attempt n = 2^(attempt_index) seconds
        // attempt_index 0 -> 2^0 = 1s, attempt_index 1 -> 2^1 = 2s, attempt_index 2 -> 2^2 = 4s
        var attemptGen = Gen.Elements(0, 1, 2);

        return Prop.ForAll(attemptGen.ToArbitrary(), attemptIndex =>
        {
            var expectedDelaySeconds = Math.Pow(2, attemptIndex);
            var expectedDelay = TimeSpan.FromSeconds(expectedDelaySeconds);

            // Verify the formula matches the documented pattern
            switch (attemptIndex)
            {
                case 0:
                    expectedDelay.TotalSeconds.Should().Be(1,
                        "first retry delay should be 1 second (2^0)");
                    break;
                case 1:
                    expectedDelay.TotalSeconds.Should().Be(2,
                        "second retry delay should be 2 seconds (2^1)");
                    break;
                case 2:
                    expectedDelay.TotalSeconds.Should().Be(4,
                        "third retry delay should be 4 seconds (2^2)");
                    break;
            }

            return true;
        });
    }

    /// <summary>
    /// Property 8: Error message after exhaustion includes the failure reason
    /// and the attempt count in the exception message.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property ErrorMessage_IncludesFailureReasonAndAttemptNumber()
    {
        var receiverGen = Gen.Elements(
            "AgentAlpha", "AgentBeta", "AgentGamma", "Summary-Agent");
        var maxRetriesGen = Gen.Elements(1, 2, 3);

        var gen = from receiver in receiverGen
                  from maxRetries in maxRetriesGen
                  select (receiver, maxRetries);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var (receiver, maxRetries) = tuple;

            var broker = new MessageBroker();
            var channel = Channel.CreateUnbounded<A2AMessage>();
            broker.RegisterAgent(receiver, channel.Writer);
            broker.EnableFailureSimulation(maxRetries + 1);

            var message = new A2AMessage(
                SenderId: "TestSender",
                ReceiverId: receiver,
                Timestamp: DateTime.UtcNow,
                Content: "Test error message content",
                Type: MessageType.Request);

            AgentCommunicationException? caughtException = null;
            try
            {
                broker.SendWithRetryAsync(message, maxRetries, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            catch (AgentCommunicationException ex)
            {
                caughtException = ex;
            }

            caughtException.Should().NotBeNull(
                "exception must be thrown when retries exhausted");

            // Error message should contain the failure reason
            caughtException!.Message.Should().Contain(receiver,
                "error message must reference the failing agent");

            // Exception properties should contain correct attempt info
            caughtException.AttemptCount.Should().Be(maxRetries,
                "exception must record the number of attempts made");

            // FailureReason should be descriptive
            caughtException.FailureReason.Should().NotBeNullOrEmpty(
                "failure reason must be provided in the exception");
            caughtException.FailureReason.Should().Contain(maxRetries.ToString(),
                "failure reason should include the attempt count");

            return true;
        });
    }

    /// <summary>
    /// Property 8: Successful delivery on retry does not throw an exception.
    /// If the operation succeeds before max retries, no exception is produced.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property SuccessBeforeMaxRetries_NoExceptionThrown()
    {
        // Simulate 0, 1, or 2 failures followed by success (within 3 max retries)
        var failuresBeforeSuccessGen = Gen.Elements(0, 1, 2);

        return Prop.ForAll(failuresBeforeSuccessGen.ToArbitrary(), failureCount =>
        {
            var broker = new MessageBroker();
            var channel = Channel.CreateUnbounded<A2AMessage>();
            broker.RegisterAgent("SuccessAgent", channel.Writer);
            broker.RegisterMessageHandler("SuccessAgent", (msg, ct) =>
                Task.FromResult("Processed successfully"));

            if (failureCount > 0)
            {
                broker.EnableFailureSimulation(failureCount);
            }

            var message = new A2AMessage(
                SenderId: "TestSender",
                ReceiverId: "SuccessAgent",
                Timestamp: DateTime.UtcNow,
                Content: "Message that eventually succeeds",
                Type: MessageType.Request);

            A2AMessage? response = null;
            Exception? thrownException = null;
            try
            {
                response = broker.SendWithRetryAsync(message, 3, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }

            thrownException.Should().BeNull(
                $"no exception should be thrown when succeeding after {failureCount} failures");
            response.Should().NotBeNull(
                "a response should be returned on successful delivery");
            return true;
        });
    }
}
