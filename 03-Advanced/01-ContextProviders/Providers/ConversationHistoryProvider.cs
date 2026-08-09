// =============================================================================
// ConversationHistoryProvider - Menyimpan riwayat percakapan dengan sliding window
// Mengimplementasikan strategi sliding window (10 turn terakhir)
// dengan token-aware truncation ketika melebihi batas 4000 token
// =============================================================================

namespace ContextProviders.Providers;

/// <summary>
/// Merepresentasikan satu turn percakapan (user + assistant).
/// Setiap turn menyimpan pesan user dan response dari agent.
/// </summary>
public record ConversationTurn(string UserMessage, string AssistantMessage, DateTime Timestamp);

/// <summary>
/// Context provider yang menyimpan riwayat percakapan menggunakan sliding window.
/// Menyimpan maksimal 10 turn terakhir dan menerapkan truncation
/// ketika total token melebihi batas 4000 token.
/// </summary>
public class ConversationHistoryProvider : IContextProvider
{
    // Jumlah maksimum turn yang disimpan dalam sliding window
    private const int MaxTurns = 10;

    // Batas maksimum token untuk konteks (mencegah context overflow)
    private const int MaxTokens = 4000;

    // Estimasi: rata-rata 4 karakter per token untuk teks bahasa Inggris/campuran
    private const int CharsPerToken = 4;

    // Penyimpanan internal riwayat percakapan
    private readonly List<ConversationTurn> _history = [];

    /// <summary>
    /// Nama provider untuk identifikasi di log output.
    /// </summary>
    public string Name => "ConversationHistoryProvider";

    /// <summary>
    /// Jumlah turn yang saat ini tersimpan dalam history.
    /// Berguna untuk monitoring dan debugging.
    /// </summary>
    public int TurnCount => _history.Count;

    /// <summary>
    /// Menyediakan riwayat percakapan sebagai konteks untuk agent.
    /// Menerapkan token-aware truncation jika total token melebihi batas.
    /// Turn terlama dihapus terlebih dahulu untuk menjaga relevansi.
    /// </summary>
    /// <returns>String berisi formatted conversation history</returns>
    public Task<string> ProvideContextAsync()
    {
        // Jika belum ada riwayat, kembalikan string kosong
        if (_history.Count == 0)
            return Task.FromResult(string.Empty);

        // Membangun konteks dari turn yang tersimpan dengan truncation
        var contextTurns = GetTruncatedHistory();

        // Memformat riwayat percakapan menjadi string yang mudah dipahami LLM
        var formattedHistory = FormatHistory(contextTurns);

        return Task.FromResult(formattedHistory);
    }

    /// <summary>
    /// Menyimpan turn percakapan baru ke dalam history.
    /// Menerapkan sliding window: jika sudah mencapai MaxTurns,
    /// turn paling lama dihapus sebelum menambahkan turn baru.
    /// </summary>
    /// <param name="userMessage">Pesan dari user pada turn ini</param>
    /// <param name="assistantMessage">Response dari agent pada turn ini</param>
    public Task StoreContextAsync(string userMessage, string assistantMessage)
    {
        // Sliding window: hapus turn terlama jika sudah penuh
        if (_history.Count >= MaxTurns)
        {
            // Menghapus turn paling awal (index 0) untuk menjaga ukuran window
            _history.RemoveAt(0);
        }

        // Menambahkan turn baru ke akhir list (paling terbaru)
        _history.Add(new ConversationTurn(userMessage, assistantMessage, DateTime.Now));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Menghapus seluruh riwayat percakapan.
    /// Digunakan saat memulai sesi baru atau untuk demonstrasi.
    /// </summary>
    public void ClearHistory()
    {
        _history.Clear();
    }

    /// <summary>
    /// Mengestimasi jumlah token dari sebuah teks.
    /// Menggunakan heuristik sederhana: ~4 karakter per token.
    /// Estimasi ini cukup akurat untuk campuran bahasa Inggris dan Indonesia.
    /// </summary>
    /// <param name="text">Teks yang akan dihitung tokennya</param>
    /// <returns>Estimasi jumlah token</returns>
    public static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Heuristik: rata-rata 4 karakter per token
        return (int)Math.Ceiling((double)text.Length / CharsPerToken);
    }

    /// <summary>
    /// Mendapatkan riwayat yang sudah di-truncate agar tidak melebihi MaxTokens.
    /// Turn terlama dihapus terlebih dahulu (prioritas pada recency).
    /// </summary>
    /// <returns>List of turns yang sudah di-truncate</returns>
    private List<ConversationTurn> GetTruncatedHistory()
    {
        // Mulai dari semua turn yang tersimpan
        var turns = new List<ConversationTurn>(_history);

        // Hitung total token dari semua turn
        var totalTokens = CalculateTotalTokens(turns);

        // Jika total token masih dalam batas, kembalikan semua turn
        if (totalTokens <= MaxTokens)
            return turns;

        // Truncation: hapus turn terlama satu per satu sampai di bawah batas
        while (turns.Count > 0 && totalTokens > MaxTokens)
        {
            // Menghitung token dari turn terlama yang akan dihapus
            var oldestTurn = turns[0];
            var oldestTokens = EstimateTokenCount(oldestTurn.UserMessage)
                             + EstimateTokenCount(oldestTurn.AssistantMessage);

            // Menghapus turn terlama
            turns.RemoveAt(0);
            totalTokens -= oldestTokens;
        }

        return turns;
    }

    /// <summary>
    /// Menghitung total estimasi token dari seluruh turn.
    /// </summary>
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

    /// <summary>
    /// Memformat riwayat percakapan menjadi string konteks yang dapat dipahami LLM.
    /// Format ini memudahkan agent untuk mereferensi percakapan sebelumnya.
    /// </summary>
    private static string FormatHistory(List<ConversationTurn> turns)
    {
        if (turns.Count == 0)
            return string.Empty;

        // Header untuk memberi konteks ke LLM bahwa ini adalah riwayat
        var lines = new List<string>
        {
            "[Riwayat Percakapan Sebelumnya]"
        };

        // Memformat setiap turn dengan label User/Assistant
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
