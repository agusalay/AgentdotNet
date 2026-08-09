// =============================================================================
// IAgentMiddleware - Abstraksi interface untuk middleware pipeline
// Mendefinisikan kontrak yang harus diimplementasikan setiap middleware
// =============================================================================

namespace AddingMiddleware.Middleware;

/// <summary>
/// Interface yang mendefinisikan kontrak middleware pada agent pipeline.
/// Setiap middleware menerima konteks dan delegate 'next' untuk memanggil
/// middleware berikutnya. Jika 'next' tidak dipanggil, pipeline di-short-circuit.
/// </summary>
public interface IAgentMiddleware
{
    /// <summary>
    /// Nama middleware - digunakan untuk identifikasi saat toggle dan logging
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Status aktif/nonaktif middleware - dapat diubah saat runtime
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Menjalankan logic middleware. Implementasi harus memanggil next(context)
    /// untuk meneruskan ke middleware berikutnya, atau tidak memanggilnya
    /// untuk melakukan short-circuit (menghentikan pipeline).
    /// </summary>
    /// <param name="context">Konteks yang berisi input user dan output response</param>
    /// <param name="nextMiddleware">Delegate ke middleware berikutnya dalam pipeline</param>
    Task InvokeAsync(MiddlewareContext context, Func<MiddlewareContext, Task> nextMiddleware);
}

/// <summary>
/// Konteks yang dibagikan antar middleware dalam pipeline.
/// Berisi input dari user dan output yang akan dikembalikan sebagai response.
/// Middleware dapat memodifikasi Input maupun Output sesuai kebutuhan.
/// </summary>
public class MiddlewareContext
{
    /// <summary>
    /// Input dari user yang akan dikirim ke agent.
    /// Middleware dapat membaca untuk validasi atau memodifikasi sebelum diteruskan.
    /// </summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// Output/response yang dihasilkan oleh agent atau middleware.
    /// Middleware dapat memodifikasi response atau meng-set langsung untuk short-circuit.
    /// </summary>
    public string Output { get; set; } = string.Empty;
}
