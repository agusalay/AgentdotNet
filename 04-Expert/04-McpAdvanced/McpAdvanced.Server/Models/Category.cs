namespace McpAdvanced.Server.Models;

/// <summary>
/// Representasi kategori untuk mengelompokkan artikel.
/// Digunakan dalam resource template (kb://categories/{categoryName}/articles)
/// dan sebagai referensi dari field Article.CategoryId.
/// </summary>
public record Category
{
    // Id unik kategori — digunakan sebagai key di ConcurrentDictionary
    // dan sebagai nilai CategoryId pada Article
    public required string Id { get; init; }

    // Nama kategori yang ditampilkan — digunakan untuk completion suggestions
    // dan sebagai parameter pada resource template URI
    public required string Name { get; init; }

    // Deskripsi singkat tentang kategori
    public string Description { get; init; } = string.Empty;

    // Jumlah artikel dalam kategori ini — diperbarui saat artikel ditambah/dihapus
    // Berguna untuk pagination dan informasi overview
    public int ArticleCount { get; set; }
}
