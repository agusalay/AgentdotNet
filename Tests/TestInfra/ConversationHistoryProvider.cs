namespace Tests.TestInfra;

public record ConversationTurn(string UserMessage, string AssistantMessage, DateTime Timestamp);

public class ConversationHistoryProvider : IContextProvider
{
    private const int MaxTurns = 10;
    private const int MaxTokens = 4000;
    private const int CharsPerToken = 4;

    private readonly List<ConversationTurn> _history = [];

    public string Name => "ConversationHistoryProvider";

    public int TurnCount => _history.Count;

    public Task<string> ProvideContextAsync()
    {
        if (_history.Count == 0)
            return Task.FromResult(string.Empty);

        var contextTurns = GetTruncatedHistory();
        var formattedHistory = FormatHistory(contextTurns);

        return Task.FromResult(formattedHistory);
    }

    public Task StoreContextAsync(string userMessage, string assistantMessage)
    {
        if (_history.Count >= MaxTurns)
        {
            _history.RemoveAt(0);
        }

        _history.Add(new ConversationTurn(userMessage, assistantMessage, DateTime.Now));

        return Task.CompletedTask;
    }

    public void ClearHistory()
    {
        _history.Clear();
    }

    public static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return (int)Math.Ceiling((double)text.Length / CharsPerToken);
    }

    private List<ConversationTurn> GetTruncatedHistory()
    {
        var turns = new List<ConversationTurn>(_history);
        var totalTokens = CalculateTotalTokens(turns);

        if (totalTokens <= MaxTokens)
            return turns;

        while (turns.Count > 0 && totalTokens > MaxTokens)
        {
            var oldestTurn = turns[0];
            var oldestTokens = EstimateTokenCount(oldestTurn.UserMessage)
                             + EstimateTokenCount(oldestTurn.AssistantMessage);

            turns.RemoveAt(0);
            totalTokens -= oldestTokens;
        }

        return turns;
    }

    private static int CalculateTotalTokens(List<ConversationTurn> turns)
    {
        var total = 0;
        foreach (var turn in turns)
        {
            total += EstimateTokenCount(turn.UserMessage);
            total += EstimateTokenCount(turn.AssistantMessage);
        }
        return total;
    }

    private static string FormatHistory(List<ConversationTurn> turns)
    {
        if (turns.Count == 0)
            return string.Empty;

        var lines = new List<string>
        {
            "[Riwayat Percakapan Sebelumnya]"
        };

        foreach (var turn in turns)
        {
            lines.Add($"User: {turn.UserMessage}");
            lines.Add($"Assistant: {turn.AssistantMessage}");
        }

        lines.Add("[Akhir Riwayat]");
        lines.Add("");

        return string.Join("\n", lines);
    }
}
