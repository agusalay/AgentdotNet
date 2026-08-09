namespace Tests.TestInfra;

public record A2AMessage(
    string SenderId,
    string ReceiverId,
    DateTime Timestamp,
    string Content,
    MessageType Type = MessageType.Request);

public enum MessageType
{
    Request,
    Response,
    Error
}

public class AgentCommunicationException : Exception
{
    public int AttemptCount { get; }
    public string FailureReason { get; }

    public AgentCommunicationException(
        string message,
        int attemptCount = 0,
        string failureReason = "",
        Exception? innerException = null)
        : base(message, innerException)
    {
        AttemptCount = attemptCount;
        FailureReason = failureReason;
    }
}
