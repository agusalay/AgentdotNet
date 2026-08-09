using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Tests.TestInfra;

public class MessageBroker
{
    private readonly ConcurrentDictionary<string, ChannelWriter<A2AMessage>> _agents = new();
    private readonly ConcurrentQueue<A2AMessage> _messageLog = new();
    private readonly ConcurrentDictionary<string, Func<A2AMessage, CancellationToken, Task<string>>> _messageHandlers = new();
    private bool _simulateFailure;
    private int _remainingFailures;

    /// <summary>
    /// When true, skips Task.Delay in retry loops (for fast unit testing).
    /// </summary>
    public bool SkipDelays { get; set; } = true;

    public void RegisterAgent(string agentId, ChannelWriter<A2AMessage> writer)
    {
        _agents[agentId] = writer;
    }

    public void RegisterMessageHandler(
        string agentId, Func<A2AMessage, CancellationToken, Task<string>> handler)
    {
        _messageHandlers[agentId] = handler;
    }

    public void EnableFailureSimulation(int failureCount)
    {
        _simulateFailure = true;
        _remainingFailures = failureCount;
    }

    public void DisableFailureSimulation()
    {
        _simulateFailure = false;
        _remainingFailures = 0;
    }

    public async Task<A2AMessage> SendWithRetryAsync(
        A2AMessage message, int maxRetries = 3, CancellationToken cancellationToken = default)
    {
        _messageLog.Enqueue(message);

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (_simulateFailure && _remainingFailures > 0)
                {
                    Interlocked.Decrement(ref _remainingFailures);
                    throw new TimeoutException(
                        $"Simulasi timeout: agent '{message.ReceiverId}' tidak merespons dalam 5 detik.");
                }

                var response = await DeliverAndProcessAsync(message, cancellationToken);
                _messageLog.Enqueue(response);
                return response;
            }
            catch (TimeoutException)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));

                if (attempt < maxRetries - 1 && !SkipDelays)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        var failureReason = $"Agent '{message.ReceiverId}' tidak merespons setelah {maxRetries} percobaan";

        throw new AgentCommunicationException(
            message: $"Komunikasi A2A gagal: {failureReason}",
            attemptCount: maxRetries,
            failureReason: failureReason);
    }

    private async Task<A2AMessage> DeliverAndProcessAsync(
        A2AMessage message, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(message.ReceiverId, out var writer))
        {
            throw new TimeoutException(
                $"Agent '{message.ReceiverId}' tidak ditemukan atau tidak tersedia.");
        }

        if (!_messageHandlers.TryGetValue(message.ReceiverId, out var handler))
        {
            throw new TimeoutException(
                $"Handler untuk agent '{message.ReceiverId}' tidak terdaftar.");
        }

        await writer.WriteAsync(message, cancellationToken);
        var responseContent = await handler(message, cancellationToken);

        return new A2AMessage(
            SenderId: message.ReceiverId,
            ReceiverId: message.SenderId,
            Timestamp: DateTime.UtcNow,
            Content: responseContent,
            Type: MessageType.Response);
    }

    public IReadOnlyCollection<A2AMessage> GetMessageLog()
    {
        return _messageLog.ToArray();
    }
}
