// =============================================================================
// ResearchExecutor - Executor untuk tahap riset dalam content creation pipeline
// Mengumpulkan informasi dan data sebagai bahan untuk tahap drafting
// =============================================================================

using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;

namespace Workflows.Executors;

/// <summary>
/// ResearchExecutor bertanggung jawab untuk melakukan riset dan pengumpulan informasi.
/// Merupakan node pertama dalam pipeline content creation (research → draft → review).
/// </summary>
public class ResearchExecutor : IWorkflowExecutor
{
    // Identitas unik executor dalam workflow graph
    public string ExecutorId => "ResearchExecutor";

    // Deskripsi peran executor untuk logging
    public string Description => "Melakukan riset dan pengumpulan informasi sebagai bahan konten";

    // Instance AIAgent yang melakukan riset menggunakan LLM
    private readonly AIAgent _agent;

    // Counter untuk simulasi kegagalan (demonstrasi retry)
    private int _failureCountdown;

    /// <summary>
    /// Membuat ResearchExecutor dengan koneksi ke LLM untuk riset.
    /// </summary>
    /// <param name="chatClient">Koneksi ke LLM melalui IChatClient</param>
    public ResearchExecutor(IChatClient chatClient)
    {
        // Membuat agent dengan instruksi riset spesifik
        _agent = chatClient.AsAIAgent(
            instructions: "Kamu adalah Research Specialist. Tugasmu: " +
                "1) Mengumpulkan informasi dan fakta relevan tentang topik yang diberikan. " +
                "2) Menyusun poin-poin kunci yang akan menjadi bahan penulisan. " +
                "3) Memberikan data dan referensi yang mendukung. " +
                "Berikan hasil riset dalam format terstruktur (poin-poin) dalam bahasa Indonesia. " +
                "Batasi output maksimal 5 poin kunci.",
            name: "ResearchAgent",
            description: "Agent spesialis riset dan pengumpulan informasi");
    }

    /// <summary>
    /// Mengatur simulasi kegagalan untuk demonstrasi mekanisme retry.
    /// </summary>
    /// <param name="failureCount">Jumlah kegagalan yang akan disimulasikan</param>
    public void SimulateFailures(int failureCount)
    {
        // Menyimpan jumlah kegagalan yang akan terjadi sebelum berhasil
        _failureCountdown = failureCount;
    }

    /// <summary>
    /// Menjalankan riset berdasarkan topik yang diberikan sebagai input.
    /// </summary>
    /// <param name="input">Topik atau pertanyaan riset yang perlu diteliti</param>
    /// <param name="cancellationToken">Token pembatalan operasi</param>
    /// <returns>Hasil riset dalam format terstruktur</returns>
    public async Task<ExecutorResult> ExecuteAsync(
        string input, CancellationToken cancellationToken = default)
    {
        // Simulasi kegagalan jika countdown masih aktif (untuk demo retry)
        if (_failureCountdown > 0)
        {
            _failureCountdown--;
            return new ExecutorResult
            {
                IsSuccess = false,
                ErrorMessage = $"Simulasi: koneksi ke sumber data gagal (sisa simulasi: {_failureCountdown})"
            };
        }

        try
        {
            // Menjalankan agent riset dengan topik sebagai input
            var prompt = $"Lakukan riset tentang topik berikut: {input}";
            var response = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);
            var result = response?.ToString() ?? string.Empty;

            // Memvalidasi bahwa riset menghasilkan output
            if (string.IsNullOrWhiteSpace(result))
            {
                return new ExecutorResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Riset tidak menghasilkan output yang valid"
                };
            }

            // Mengembalikan hasil riset yang berhasil
            return new ExecutorResult
            {
                IsSuccess = true,
                Output = result,
                Metadata = new Dictionary<string, string>
                {
                    ["topic"] = input,
                    ["source"] = "LLM Research"
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Mengembalikan error jika terjadi kegagalan saat riset
            return new ExecutorResult
            {
                IsSuccess = false,
                ErrorMessage = $"Gagal melakukan riset: {ex.Message}"
            };
        }
    }
}
