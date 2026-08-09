// =============================================================================
// MiddlewarePipeline - Eksekutor pipeline yang menjalankan middleware secara berurutan
// Mengimplementasikan chain-of-responsibility pattern dengan delegate chaining
// =============================================================================

namespace AddingMiddleware.Middleware;

/// <summary>
/// Pipeline eksekutor yang meng-chain middleware bersama dan menjalankannya secara berurutan.
/// Middleware terakhir dalam chain memanggil handler final (biasanya agent invocation).
/// Pipeline dibangun dengan pola delegate composition: setiap middleware
/// menerima 'next' yang merupakan middleware berikutnya yang dibungkus dalam delegate.
/// </summary>
public class MiddlewarePipeline
{
    // Daftar middleware yang terdaftar dalam pipeline, dieksekusi sesuai urutan
    private readonly List<IAgentMiddleware> _middlewares = [];

    /// <summary>
    /// Mendaftarkan middleware baru ke dalam pipeline.
    /// Middleware dieksekusi sesuai urutan pendaftaran (FIFO).
    /// </summary>
    /// <param name="middleware">Instance middleware yang akan didaftarkan</param>
    public void Use(IAgentMiddleware middleware)
    {
        _middlewares.Add(middleware);
    }

    /// <summary>
    /// Mendapatkan daftar middleware yang terdaftar (read-only).
    /// Digunakan untuk menampilkan status middleware saat runtime.
    /// </summary>
    public IReadOnlyList<IAgentMiddleware> Middlewares => _middlewares;

    /// <summary>
    /// Menjalankan pipeline lengkap dengan semua middleware yang aktif.
    /// Middleware dieksekusi dalam urutan pendaftaran. Jika middleware tidak aktif,
    /// middleware tersebut langsung meneruskan ke next tanpa menjalankan logicnya.
    /// Handler final dipanggil setelah semua middleware selesai dieksekusi.
    /// </summary>
    /// <param name="context">Konteks berisi input user</param>
    /// <param name="finalHandler">Handler terakhir (biasanya pemanggilan agent)</param>
    public async Task ExecuteAsync(MiddlewareContext context, Func<MiddlewareContext, Task> finalHandler)
    {
        // Membangun chain dari belakang ke depan (reverse order)
        // Middleware[0] membungkus Middleware[1] yang membungkus ... yang membungkus finalHandler
        // Hasilnya: urutan eksekusi sesuai urutan pendaftaran

        // Mulai dari handler terakhir (agent call)
        Func<MiddlewareContext, Task> pipeline = finalHandler;

        // Membungkus setiap middleware dari belakang ke depan
        // Ini menciptakan nested delegate chain
        for (int i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var next = pipeline; // Capture 'next' untuk closure

            // Setiap middleware menjadi wrapper untuk middleware berikutnya
            pipeline = (ctx) => middleware.InvokeAsync(ctx, next);
        }

        // Menjalankan pipeline dari middleware pertama
        await pipeline(context);
    }
}
