// =============================================================================
// GuardrailMiddleware - Middleware untuk validasi input sebelum dikirim ke agent
// Mengimplementasikan short-circuit pattern: request yang melanggar aturan
// tidak diteruskan ke agent sama sekali
// =============================================================================

namespace AddingMiddleware.Middleware;

/// <summary>
/// Middleware guardrail yang memvalidasi input user sebelum diteruskan ke agent.
/// Jika input melanggar aturan validasi, request di-block (short-circuit)
/// tanpa pernah mencapai agent.
/// </summary>
public class GuardrailMiddleware : IAgentMiddleware
{
    // Batas maksimal karakter input yang diperbolehkan
    private const int MaxInputLength = 500;

    // Properti untuk mengontrol apakah middleware ini aktif atau tidak
    // Dapat di-toggle saat runtime tanpa restart aplikasi
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Nama middleware untuk ditampilkan di console saat toggle
    /// </summary>
    public string Name => "GuardrailMiddleware";

    /// <summary>
    /// Menjalankan validasi guardrail pada input user.
    /// Jika input melebihi 500 karakter, request langsung di-block (short-circuit).
    /// Agent tidak akan menerima request yang melanggar aturan.
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

        // Validasi: periksa apakah panjang input melebihi batas maksimal
        if (context.Input.Length > MaxInputLength)
        {
            // SHORT-CIRCUIT: Set output langsung tanpa memanggil nextMiddleware()
            // Agent tidak pernah menerima request ini
            context.Output = $"[BLOCKED] Input melebihi {MaxInputLength} karakter. " +
                             $"Input Anda: {context.Input.Length} karakter. " +
                             $"Harap kurangi panjang input.";

            Console.WriteLine($"  [GUARDRAIL] ⛔ Request DIBLOKIR - Input melebihi {MaxInputLength} karakter " +
                              $"(panjang: {context.Input.Length}). Agent TIDAK menerima request ini.");
            return; // Tidak memanggil nextMiddleware() - pipeline berhenti di sini
        }

        // Input valid - teruskan ke middleware berikutnya dalam pipeline
        Console.WriteLine($"  [GUARDRAIL] ✅ Input valid ({context.Input.Length}/{MaxInputLength} karakter) - diteruskan ke pipeline");
        await nextMiddleware(context);
    }
}
