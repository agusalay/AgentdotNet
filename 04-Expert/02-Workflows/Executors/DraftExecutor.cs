// =============================================================================
// DraftExecutor - Executor untuk tahap penulisan draft dalam content creation
// Menerima hasil riset dan menyusunnya menjadi draft konten yang terstruktur
// =============================================================================

using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;

namespace Workflows.Executors;

/// <summary>
/// DraftExecutor bertanggung jawab untuk menyusun draft konten berdasarkan hasil riset.
/// Merupakan node kedua dalam pipeline content creation (research → draft → review).
/// Dalam conditional loop, executor ini menerima feedback dari review untuk perbaikan.
/// </summary>
public class DraftExecutor : IWorkflowExecutor
{
    // Identitas unik executor dalam workflow graph
    public string ExecutorId => "DraftExecutor";

    // Deskripsi peran executor untuk logging
    public string Description => "Menyusun draft konten berdasarkan hasil riset atau feedback review";

    // Instance AIAgent yang menyusun draft menggunakan LLM
    private readonly AIAgent _agent;

    // Menghitung berapa kali executor ini dipanggil (untuk tracking revisi)
    private int _revisionCount;

    /// <summary>
    /// Membuat DraftExecutor dengan koneksi ke LLM untuk penulisan.
    /// </summary>
    /// <param name="chatClient">Koneksi ke LLM melalui IChatClient</param>
    public DraftExecutor(IChatClient chatClient)
    {
        // Membuat agent dengan instruksi penulisan konten
        _agent = chatClient.AsAIAgent(
            instructions: "Kamu adalah Content Writer. Tugasmu: " +
                "1) Menyusun draft konten yang jelas, informatif, dan terstruktur. " +
                "2) Menggunakan bahan riset yang diberikan sebagai dasar penulisan. " +
                "3) Jika menerima feedback revisi, perbaiki draft sesuai catatan reviewer. " +
                "4) Format: gunakan paragraf pendek, heading jika perlu, dan poin-poin. " +
                "Tulis dalam bahasa Indonesia yang baik dan benar. " +
                "Batasi output maksimal 3 paragraf.",
            name: "DraftAgent",
            description: "Agent spesialis penulisan dan penyusunan draft konten");
    }

    /// <summary>
    /// Mendapatkan jumlah revisi yang telah dilakukan.
    /// Berguna untuk mengetahui berapa kali draft di-loop ulang.
    /// </summary>
    public int RevisionCount => _revisionCount;

    /// <summary>
    /// Menyusun draft konten berdasarkan input (hasil riset atau feedback review).
    /// </summary>
    /// <param name="input">Hasil riset atau feedback revisi dari ReviewExecutor</param>
    /// <param name="cancellationToken">Token pembatalan operasi</param>
    /// <returns>Draft konten yang disusun</returns>
    public async Task<ExecutorResult> ExecuteAsync(
        string input, CancellationToken cancellationToken = default)
    {
        try
        {
            // Menentukan prompt berdasarkan apakah ini draft pertama atau revisi
            string prompt;
            if (_revisionCount == 0)
            {
                // Draft pertama: berdasarkan hasil riset
                prompt = $"Buatkan draft konten berdasarkan hasil riset berikut:\n\n{input}";
            }
            else
            {
                // Revisi: berdasarkan feedback dari reviewer
                prompt = $"Revisi draft berdasarkan feedback berikut (revisi ke-{_revisionCount}):\n\n{input}";
            }

            // Menjalankan agent penulis draft
            var response = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);
            var result = response?.ToString() ?? string.Empty;

            // Memvalidasi output draft
            if (string.IsNullOrWhiteSpace(result))
            {
                return new ExecutorResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Draft tidak dapat dihasilkan"
                };
            }

            // Menambah counter revisi untuk tracking
            _revisionCount++;

            // Mengembalikan draft yang berhasil
            return new ExecutorResult
            {
                IsSuccess = true,
                Output = result,
                Metadata = new Dictionary<string, string>
                {
                    ["revision"] = _revisionCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["type"] = _revisionCount == 1 ? "initial_draft" : "revision"
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Mengembalikan error jika penulisan draft gagal
            return new ExecutorResult
            {
                IsSuccess = false,
                ErrorMessage = $"Gagal menyusun draft: {ex.Message}"
            };
        }
    }
}
