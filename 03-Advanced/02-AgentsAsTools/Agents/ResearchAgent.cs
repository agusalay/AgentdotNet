// =============================================================================
// ResearchAgent - Agent spesialis untuk riset dan pencarian informasi
// Agent ini akan didaftarkan sebagai tool untuk parent agent (orchestrator)
// =============================================================================

using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;

namespace AgentsAsTools.Agents;

/// <summary>
/// Factory class untuk membuat ResearchAgent.
/// ResearchAgent bertugas melakukan riset dan mengumpulkan informasi
/// berdasarkan query yang diberikan oleh parent agent.
/// </summary>
public static class ResearchAgentFactory
{
    // Nama unik untuk identifikasi agent dalam sistem
    public const string AgentName = "ResearchAgent";

    // Deskripsi kemampuan agent untuk ditampilkan saat inisialisasi
    public const string AgentDescription = "Spesialis riset dan pencarian informasi";

    // Instruksi yang mendefinisikan persona dan perilaku agent
    private const string Instructions =
        "Kamu adalah ResearchAgent, seorang spesialis riset yang ahli dalam mengumpulkan " +
        "dan menyintesis informasi. Tugasmu adalah: " +
        "1) Menganalisis query riset yang diberikan. " +
        "2) Memberikan informasi faktual dan terstruktur. " +
        "3) Menyertakan sumber atau referensi jika memungkinkan. " +
        "4) Merangkum temuan dalam format yang mudah dipahami. " +
        "Berikan jawaban yang informatif, akurat, dan terstruktur dalam bahasa Indonesia.";

    /// <summary>
    /// Membuat instance AIAgent untuk ResearchAgent.
    /// Agent ini akan digunakan sebagai child agent dalam komposisi agent-as-tool.
    /// </summary>
    /// <param name="chatClient">IChatClient yang terhubung ke LLM</param>
    /// <returns>AIAgent yang dikonfigurasi sebagai research specialist</returns>
    public static AIAgent Create(IChatClient chatClient)
    {
        // Membuat agent dengan instruksi spesifik untuk riset
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
    /// Menjalankan ResearchAgent dengan input tertentu dan mengembalikan hasil riset.
    /// Method ini digunakan saat agent dipanggil sebagai tool oleh parent agent.
    /// </summary>
    /// <param name="agent">Instance AIAgent ResearchAgent</param>
    /// <param name="query">Query riset yang akan diproses</param>
    /// <param name="cancellationToken">Token untuk pembatalan operasi</param>
    /// <returns>Hasil riset dalam format string</returns>
    public static async Task<string> RunAsync(AIAgent agent, string query, CancellationToken cancellationToken = default)
    {
        try
        {
            // Menjalankan agent dengan query riset
            var response = await agent.RunAsync(query, cancellationToken: cancellationToken);
            var result = response?.ToString() ?? string.Empty;

            // Mengembalikan hasil riset atau pesan default jika kosong
            return !string.IsNullOrWhiteSpace(result)
                ? result
                : "[ResearchAgent] Tidak dapat menemukan informasi yang relevan untuk query tersebut.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Mengembalikan pesan error yang informatif jika agent gagal
            throw new InvalidOperationException(
                $"[ResearchAgent] Gagal memproses riset: {ex.Message}", ex);
        }
    }
}
