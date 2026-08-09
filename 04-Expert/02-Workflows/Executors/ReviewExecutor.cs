// =============================================================================
// ReviewExecutor - Executor untuk tahap review dalam content creation pipeline
// Mengevaluasi draft dan memberikan keputusan approve atau reject dengan feedback
// =============================================================================

using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;

namespace Workflows.Executors;

/// <summary>
/// ReviewExecutor bertanggung jawab untuk mengevaluasi draft konten.
/// Memberikan keputusan approve (konten diterima) atau reject (perlu revisi).
/// Merupakan node ketiga dalam pipeline content creation (research → draft → review).
/// </summary>
public class ReviewExecutor : IWorkflowExecutor
{
    // Identitas unik executor dalam workflow graph
    public string ExecutorId => "ReviewExecutor";

    // Deskripsi peran executor untuk logging
    public string Description => "Mengevaluasi draft dan memberikan keputusan approve/reject";

    // Instance AIAgent yang melakukan review menggunakan LLM
    private readonly AIAgent _agent;

    // Counter review untuk menentukan kapan harus approve
    // (simulasi: reject pada review pertama, approve pada review kedua)
    private int _reviewCount;

    // Threshold review sebelum auto-approve (untuk mencegah infinite loop)
    private readonly int _autoApproveAfter;

    /// <summary>
    /// Membuat ReviewExecutor dengan koneksi ke LLM untuk evaluasi konten.
    /// </summary>
    /// <param name="chatClient">Koneksi ke LLM melalui IChatClient</param>
    /// <param name="autoApproveAfter">Jumlah review sebelum auto-approve (default: 2)</param>
    public ReviewExecutor(IChatClient chatClient, int autoApproveAfter = 2)
    {
        // Membuat agent dengan instruksi review konten
        _agent = chatClient.AsAIAgent(
            instructions: "Kamu adalah Content Reviewer. Tugasmu: " +
                "1) Mengevaluasi kualitas draft konten yang diberikan. " +
                "2) Memberikan feedback yang konstruktif dan spesifik. " +
                "3) Menilai: kejelasan, struktur, akurasi, dan kelengkapan. " +
                "Berikan review dalam bahasa Indonesia. " +
                "Format feedback: sebutkan 1-2 hal yang perlu diperbaiki.",
            name: "ReviewAgent",
            description: "Agent spesialis review dan evaluasi kualitas konten");

        _autoApproveAfter = autoApproveAfter;
    }

    /// <summary>
    /// Mengevaluasi draft konten dan mengembalikan keputusan approve/reject.
    /// </summary>
    /// <param name="input">Draft konten yang perlu di-review</param>
    /// <param name="cancellationToken">Token pembatalan operasi</param>
    /// <returns>Hasil review dengan flag IsApproved untuk conditional routing</returns>
    public async Task<ExecutorResult> ExecuteAsync(
        string input, CancellationToken cancellationToken = default)
    {
        try
        {
            _reviewCount++;

            // Auto-approve setelah threshold untuk mencegah infinite loop
            // Dalam skenario nyata, approval bergantung pada kualitas konten
            if (_reviewCount >= _autoApproveAfter)
            {
                // Menjalankan review final sebelum approve
                var approvePrompt = $"Berikan ringkasan evaluasi final (sudah disetujui) " +
                    $"untuk draft berikut:\n\n{input}";
                var approveResponse = await _agent.RunAsync(
                    approvePrompt, cancellationToken: cancellationToken);
                var approveResult = approveResponse?.ToString() ?? "Draft disetujui.";

                return new ExecutorResult
                {
                    IsSuccess = true,
                    IsApproved = true,
                    Output = input, // Meneruskan draft yang sudah disetujui
                    Metadata = new Dictionary<string, string>
                    {
                        ["decision"] = "APPROVED",
                        ["review_number"] = _reviewCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["feedback"] = approveResult
                    }
                };
            }

            // Review pertama: memberikan feedback untuk perbaikan (reject)
            var reviewPrompt = $"Review draft berikut dan berikan feedback perbaikan " +
                $"(jangan approve dulu, berikan saran revisi):\n\n{input}";
            var response = await _agent.RunAsync(
                reviewPrompt, cancellationToken: cancellationToken);
            var result = response?.ToString() ?? string.Empty;

            // Memvalidasi output review
            if (string.IsNullOrWhiteSpace(result))
            {
                result = "Draft perlu diperbaiki: tambahkan detail dan contoh lebih spesifik.";
            }

            // Mengembalikan feedback (reject) untuk di-loop kembali ke DraftExecutor
            return new ExecutorResult
            {
                IsSuccess = true,
                IsApproved = false,
                Output = result, // Feedback yang dikirim kembali ke DraftExecutor
                Metadata = new Dictionary<string, string>
                {
                    ["decision"] = "REJECTED",
                    ["review_number"] = _reviewCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["feedback"] = result
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Mengembalikan error jika proses review gagal
            return new ExecutorResult
            {
                IsSuccess = false,
                ErrorMessage = $"Gagal melakukan review: {ex.Message}"
            };
        }
    }
}
