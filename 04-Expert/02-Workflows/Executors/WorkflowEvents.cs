// =============================================================================
// WorkflowEvents - Sistem event untuk memonitor eksekusi workflow secara real-time
// Mendukung ExecutorCompletedEvent untuk tracking progress setiap langkah
// =============================================================================

namespace Workflows.Executors;

/// <summary>
/// Base class untuk semua event yang terjadi selama eksekusi workflow.
/// Memungkinkan monitoring real-time terhadap progress workflow.
/// </summary>
public abstract record WorkflowEvent
{
    /// <summary>
    /// Waktu event terjadi dalam format UTC.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event yang dipancarkan ketika sebuah executor mulai dieksekusi.
/// Berguna untuk visualisasi node aktif dalam workflow graph.
/// </summary>
public record ExecutorStartedEvent : WorkflowEvent
{
    /// <summary>
    /// ID executor yang mulai dijalankan.
    /// </summary>
    public string ExecutorId { get; init; } = string.Empty;

    /// <summary>
    /// Nomor percobaan eksekusi (1 = pertama kali, >1 = retry).
    /// </summary>
    public int AttemptNumber { get; init; } = 1;
}

/// <summary>
/// Event yang dipancarkan ketika sebuah executor selesai dieksekusi.
/// Berisi status keberhasilan dan metadata hasil eksekusi.
/// </summary>
public record ExecutorCompletedEvent : WorkflowEvent
{
    /// <summary>
    /// ID executor yang selesai dijalankan.
    /// </summary>
    public string ExecutorId { get; init; } = string.Empty;

    /// <summary>
    /// Status apakah eksekusi berhasil atau gagal.
    /// </summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>
    /// Durasi eksekusi dalam milidetik.
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// Nomor percobaan saat berhasil (atau percobaan terakhir saat gagal).
    /// </summary>
    public int AttemptNumber { get; init; } = 1;

    /// <summary>
    /// Pesan error jika executor gagal.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Event yang dipancarkan ketika retry terjadi karena kegagalan executor.
/// Memungkinkan tracking berapa kali sebuah step di-retry.
/// </summary>
public record ExecutorRetryEvent : WorkflowEvent
{
    /// <summary>
    /// ID executor yang akan di-retry.
    /// </summary>
    public string ExecutorId { get; init; } = string.Empty;

    /// <summary>
    /// Nomor percobaan berikutnya yang akan dilakukan.
    /// </summary>
    public int NextAttemptNumber { get; init; }

    /// <summary>
    /// Alasan mengapa retry diperlukan.
    /// </summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Event yang dipancarkan ketika sebuah executor gagal permanen (semua retry habis).
/// Berisi informasi dampak pada downstream executor.
/// </summary>
public record ExecutorFailedPermanentlyEvent : WorkflowEvent
{
    /// <summary>
    /// ID executor yang gagal permanen.
    /// </summary>
    public string ExecutorId { get; init; } = string.Empty;

    /// <summary>
    /// Pesan error terakhir dari percobaan terakhir.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Jumlah total percobaan yang dilakukan sebelum dinyatakan gagal.
    /// </summary>
    public int TotalAttempts { get; init; }

    /// <summary>
    /// Daftar executor downstream yang terdampak oleh kegagalan ini.
    /// </summary>
    public List<string> AffectedDownstream { get; init; } = new();
}
