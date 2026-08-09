// =============================================================================
// LoggingMiddleware - Middleware untuk mencatat semua interaksi agent
// Mencatat timestamp, isi request user, dan isi response agent ke console
// =============================================================================

namespace AddingMiddleware.Middleware;

/// <summary>
/// Middleware yang mencatat log interaksi agent ke console.
/// Mencatat timestamp, request user, dan response agent pada setiap invokasi.
/// </summary>
public class LoggingMiddleware : IAgentMiddleware
{
    // Properti untuk mengontrol apakah middleware ini aktif atau tidak
    // Dapat di-toggle saat runtime tanpa restart aplikasi
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Nama middleware untuk ditampilkan di console saat toggle
    /// </summary>
    public string Name => "LoggingMiddleware";

    /// <summary>
    /// Menjalankan middleware logging.
    /// Mencatat timestamp dan isi request sebelum diteruskan ke middleware berikutnya,
    /// kemudian mencatat timestamp dan isi response setelah proses selesai.
    /// </summary>
    /// <param name="context">Konteks pipeline yang berisi input dan output</param>
    /// <param name="next">Delegate untuk memanggil middleware berikutnya dalam pipeline</param>
    public async Task InvokeAsync(MiddlewareContext context, Func<MiddlewareContext, Task> nextMiddleware)
    {
        // Jika middleware tidak aktif, langsung teruskan ke middleware berikutnya
        if (!IsEnabled)
        {
            await nextMiddleware(context);
            return;
        }

        // Mencatat waktu dan isi request user sebelum diproses
        var timestamp = DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        Console.WriteLine($"  [LOG {timestamp}] ➡️  Request: {context.Input}");

        // Meneruskan konteks ke middleware berikutnya dalam pipeline (delegation pattern)
        await nextMiddleware(context);

        // Mencatat waktu dan isi response agent setelah diproses
        var responseTimestamp = DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        Console.WriteLine($"  [LOG {responseTimestamp}] ⬅️  Response: {TruncateForLog(context.Output)}");
    }

    /// <summary>
    /// Memotong teks response agar log tidak terlalu panjang di console.
    /// Maksimal 200 karakter ditampilkan di log.
    /// </summary>
    private static string TruncateForLog(string text)
    {
        // Membatasi panjang output log agar tetap terbaca di console
        const int maxLength = 200;
        if (string.IsNullOrEmpty(text)) return "(kosong)";
        if (text.Length <= maxLength) return text.Replace("\n", " ");
        return text[..maxLength].Replace("\n", " ") + "...";
    }
}
