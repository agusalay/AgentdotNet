// =============================================================================
// IContextProvider - Interface dasar untuk context provider
// Mendefinisikan kontrak untuk menyediakan dan menyimpan konteks agent
// =============================================================================

namespace ContextProviders.Providers;

/// <summary>
/// Interface yang mendefinisikan kontrak untuk context provider.
/// Context provider bertanggung jawab menyediakan informasi tambahan ke agent
/// sebelum invocation dan menyimpan hasil setelah invocation.
/// </summary>
public interface IContextProvider
{
    /// <summary>
    /// Nama unik dari context provider untuk identifikasi dalam log.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Menyediakan konteks tambahan sebelum agent memproses request.
    /// Konteks ini akan digabungkan dengan prompt user sebelum dikirim ke LLM.
    /// </summary>
    /// <returns>String berisi konteks tambahan, atau empty jika tidak ada konteks</returns>
    Task<string> ProvideContextAsync();

    /// <summary>
    /// Menyimpan konteks setelah agent selesai memproses request.
    /// Digunakan untuk menyimpan conversation history atau data lain yang relevan.
    /// </summary>
    /// <param name="userMessage">Pesan dari user</param>
    /// <param name="assistantMessage">Response dari agent</param>
    Task StoreContextAsync(string userMessage, string assistantMessage);
}
