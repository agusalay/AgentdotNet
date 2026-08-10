namespace McpAdvanced.Server.Models;

/// <summary>
/// Representasi artikel dalam Knowledge Base.
/// Record type digunakan karena artikel bersifat immutable pada field identitas (Id, Author),
/// namun content dan metadata dapat diperbarui.
/// </summary>
public record Article
{
    // Id unik untuk artikel — digunakan sebagai key di ConcurrentDictionary
    // dan juga menjadi bagian dari URI resource MCP (contoh: kb://articles/{id})
    public required string Id { get; init; }

    // Judul artikel — dapat diperbarui melalui tool update
    public required string Title { get; set; }

    // Konten utama artikel — mendukung format markdown
    public required string Content { get; set; }

    // Referensi ke kategori — digunakan untuk pengelompokan dan filtering
    // Harus sesuai dengan Id pada Category record
    public required string CategoryId { get; set; }

    // Penulis artikel — immutable setelah pembuatan
    public required string Author { get; init; }

    // Waktu pembuatan dalam UTC — immutable setelah pembuatan
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    // Waktu terakhir diperbarui — diubah setiap kali content dimodifikasi
    // Digunakan untuk menentukan apakah resource subscription perlu mengirim notifikasi
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Daftar tag untuk pencarian dan kategorisasi tambahan
    // Digunakan oleh SearchArticles untuk memperluas hasil pencarian
    public List<string> Tags { get; set; } = [];

    // MIME type menentukan format konten yang dikembalikan oleh MCP Resource
    // Sesuai requirement 3.5: server harus mengembalikan format yang sesuai
    public string MimeType { get; init; } = "text/markdown";
}
