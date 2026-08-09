// =============================================================================
// WritingAgent - Agent spesialis untuk penulisan dan penyuntingan konten
// Agent ini akan didaftarkan sebagai tool untuk parent agent (orchestrator)
// =============================================================================

using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;

namespace AgentsAsTools.Agents;

/// <summary>
/// Factory class untuk membuat WritingAgent.
/// WritingAgent bertugas menulis, menyunting, dan memformat konten
/// berdasarkan instruksi yang diberikan oleh parent agent.
/// </summary>
public static class WritingAgentFactory
{
    // Nama unik untuk identifikasi agent dalam sistem
    public const string AgentName = "WritingAgent";

    // Deskripsi kemampuan agent untuk ditampilkan saat inisialisasi
    public const string AgentDescription = "Spesialis penulisan dan penyuntingan konten";

    // Instruksi yang mendefinisikan persona dan perilaku agent
    private const string Instructions =
        "Kamu adalah WritingAgent, seorang spesialis penulisan yang ahli dalam membuat " +
        "konten berkualitas tinggi. Tugasmu adalah: " +
        "1) Menulis konten berdasarkan instruksi atau data yang diberikan. " +
        "2) Menyunting dan memperbaiki tata bahasa serta struktur tulisan. " +
        "3) Memformat konten agar mudah dibaca dan menarik. " +
        "4) Menyesuaikan gaya penulisan sesuai konteks (formal, informal, teknis). " +
        "Berikan hasil tulisan yang berkualitas, terstruktur, dan sesuai konteks dalam bahasa Indonesia.";

    /// <summary>
    /// Membuat instance AIAgent untuk WritingAgent.
    /// Agent ini akan digunakan sebagai child agent dalam komposisi agent-as-tool.
    /// </summary>
    /// <param name="chatClient">IChatClient yang terhubung ke LLM</param>
    /// <returns>AIAgent yang dikonfigurasi sebagai writing specialist</returns>
    public static AIAgent Create(IChatClient chatClient)
    {
        // Membuat agent dengan instruksi spesifik untuk penulisan
        // AsAIAgent() adalah extension method dari Microsoft Agent Framework
        var agent = chatClient.AsAIAgent(
            instructions: Instructions,
            name: AgentName,
            description: AgentDescription);

        // Menampilkan pesan inisialisasi dengan nama agent
        Console.WriteLine($"  [INIT] Agent '{AgentName}' berhasil dibuat.");
        Console.WriteLine($"         Deskripsi: {AgentDescription}");

        return agent;
    }

    /// <summary>
    /// Menjalankan WritingAgent dengan input tertentu dan mengembalikan hasil penulisan.
    /// Method ini digunakan saat agent dipanggil sebagai tool oleh parent agent.
    /// </summary>
    /// <param name="agent">Instance AIAgent WritingAgent</param>
    /// <param name="content">Instruksi penulisan atau konten yang akan diproses</param>
    /// <param name="cancellationToken">Token untuk pembatalan operasi</param>
    /// <returns>Hasil penulisan dalam format string</returns>
    public static async Task<string> RunAsync(AIAgent agent, string content, CancellationToken cancellationToken = default)
    {
        try
        {
            // Menjalankan agent dengan instruksi penulisan
            var response = await agent.RunAsync(content, cancellationToken: cancellationToken);
            var result = response?.ToString() ?? string.Empty;

            // Mengembalikan hasil penulisan atau pesan default jika kosong
            return !string.IsNullOrWhiteSpace(result)
                ? result
                : "[WritingAgent] Tidak dapat menghasilkan konten untuk instruksi tersebut.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Mengembalikan pesan error yang informatif jika agent gagal
            throw new InvalidOperationException(
                $"[WritingAgent] Gagal memproses penulisan: {ex.Message}", ex);
        }
    }
}
