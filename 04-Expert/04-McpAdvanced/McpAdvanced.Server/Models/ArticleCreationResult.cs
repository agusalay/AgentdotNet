namespace McpAdvanced.Server.Models;

/// <summary>
/// Output schema untuk tool CreateArticle dengan Structured Content.
/// Record ini mendefinisikan JSON Schema 2020-12 yang harus diikuti output tool.
/// Diaktifkan melalui [McpServerTool(UseStructuredContent = true)] pada tool handler.
/// 
/// Sesuai requirement 5.1 dan 5.2: tool output mengikuti schema yang telah ditentukan
/// dengan properties, types, required fields, dan descriptions.
/// </summary>
public record ArticleCreationResult
{
    // Id artikel yang baru dibuat — diisi dari store setelah penyimpanan berhasil
    public required string ArticleId { get; init; }

    // Judul artikel — harus sama dengan input title untuk konfirmasi
    public required string Title { get; init; }

    // Kategori di mana artikel disimpan
    public required string CategoryId { get; init; }

    // Waktu pembuatan artikel — diambil dari Article.CreatedAt
    public required DateTime CreatedAt { get; init; }

    // Status operasi: "created" jika berhasil, "error" jika gagal
    // Client dapat menggunakan field ini untuk menentukan langkah selanjutnya
    public required string Status { get; init; }

    // Pesan error opsional — hanya diisi jika Status == "error"
    // Memberikan informasi diagnostik ke client tentang penyebab kegagalan
    public string? ErrorMessage { get; init; }
}
