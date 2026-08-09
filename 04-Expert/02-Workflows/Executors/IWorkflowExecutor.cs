// =============================================================================
// IWorkflowExecutor - Interface dasar untuk setiap langkah dalam workflow graph
// Setiap executor merepresentasikan satu node dalam directed graph workflow
// =============================================================================

namespace Workflows.Executors;

/// <summary>
/// Interface yang harus diimplementasikan setiap executor (node) dalam workflow.
/// Executor adalah unit kerja terkecil yang memproses input dan menghasilkan output.
/// </summary>
public interface IWorkflowExecutor
{
    /// <summary>
    /// Identitas unik executor dalam graph workflow.
    /// Digunakan sebagai referensi saat mendefinisikan edges antar node.
    /// </summary>
    string ExecutorId { get; }

    /// <summary>
    /// Deskripsi singkat peran executor untuk logging dan visualisasi.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Memproses input dan menghasilkan result untuk diteruskan ke executor berikutnya.
    /// </summary>
    /// <param name="input">Data input dari executor sebelumnya atau input awal workflow</param>
    /// <param name="cancellationToken">Token pembatalan untuk operasi async</param>
    /// <returns>Hasil eksekusi yang berisi data output dan status keberhasilan</returns>
    Task<ExecutorResult> ExecuteAsync(string input, CancellationToken cancellationToken = default);
}

/// <summary>
/// Record yang merepresentasikan hasil eksekusi sebuah executor.
/// Berisi output data, status keberhasilan, dan metadata tambahan.
/// </summary>
public record ExecutorResult
{
    /// <summary>
    /// Output data dari executor, akan diteruskan ke executor berikutnya.
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// Indikator apakah eksekusi berhasil atau gagal.
    /// </summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>
    /// Khusus untuk ReviewExecutor: indikator apakah konten disetujui.
    /// Digunakan dalam conditional routing (approve/reject loop).
    /// </summary>
    public bool IsApproved { get; init; }

    /// <summary>
    /// Pesan error jika eksekusi gagal.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Metadata tambahan untuk informasi step (opsional).
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}
