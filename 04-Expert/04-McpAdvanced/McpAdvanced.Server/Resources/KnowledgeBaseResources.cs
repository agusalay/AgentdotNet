// KnowledgeBaseResources.cs — MCP Resources untuk Knowledge Base
// Resources adalah data read-only yang diekspos oleh server melalui URI.
// Ada dua jenis resource:
// 1. Direct Resources — URI tetap (contoh: kb://articles/introduction)
//    Client dapat langsung membaca resource ini tanpa parameter.
// 2. Template Resources — URI dengan parameter mengikuti RFC 6570 (contoh: kb://articles/{articleId})
//    Client menyediakan parameter untuk mengakses resource secara dinamis.
//
// Resource mendukung subscriptions — client mendaftar untuk menerima notifikasi
// ketika resource berubah (misalnya setelah artikel diperbarui melalui tool).

using System.ComponentModel;
using System.Text.Json;
using McpAdvanced.Server.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpAdvanced.Server.Resources;

/// <summary>
/// Kelas yang mendefinisikan semua MCP Resources untuk Knowledge Base.
/// Menggunakan atribut [McpServerResourceType] agar ditemukan oleh MCP framework
/// saat registrasi melalui WithResources atau WithResourcesFromAssembly.
/// </summary>
[McpServerResourceType]
public class KnowledgeBaseResources
{
    // Opsi JSON yang di-cache untuk menghindari alokasi berulang pada setiap serialisasi.
    // Menggunakan camelCase agar konsisten dengan konvensi JSON umum.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    #region Direct Resources (URI Tetap)

    // Direct resource adalah resource dengan URI yang sudah ditentukan (fixed).
    // Client dapat menemukan resource ini melalui resources/list tanpa perlu parameter.
    // Cocok untuk konten yang selalu tersedia dan memiliki identitas tetap.

    /// <summary>
    /// Resource: Artikel pengenalan Knowledge Base.
    /// URI tetap yang selalu mengembalikan artikel "introduction" dari store.
    /// MIME type mengikuti format yang dideklarasikan pada artikel (text/markdown).
    /// </summary>
    [McpServerResource(
        UriTemplate = "kb://articles/introduction",
        Name = "Introduction Article")]
    [Description("Artikel pengenalan tentang Knowledge Base Management System")]
    public static ResourceContents GetIntroduction(KnowledgeBaseStore store)
    {
        // Ambil artikel introduction dari store
        var article = store.GetArticle("introduction");

        // Jika artikel tidak ditemukan, kembalikan pesan error sebagai konten
        if (article is null)
        {
            return new TextResourceContents
            {
                Text = "Artikel introduction tidak ditemukan.",
                Uri = "kb://articles/introduction",
                MimeType = "text/plain"
            };
        }

        // Kembalikan konten artikel dengan MIME type sesuai deklarasi artikel
        // Requirement 3.5: format yang dikembalikan harus sesuai dengan tipe artikel
        return new TextResourceContents
        {
            Text = article.Content,
            Uri = "kb://articles/introduction",
            MimeType = article.MimeType
        };
    }

    /// <summary>
    /// Resource: Panduan memulai penggunaan MCP Advanced.
    /// URI tetap untuk artikel "getting-started" yang berisi langkah-langkah awal.
    /// </summary>
    [McpServerResource(
        UriTemplate = "kb://articles/getting-started",
        Name = "Getting Started")]
    [Description("Panduan memulai penggunaan MCP Advanced Server dan Client")]
    public static ResourceContents GetGettingStarted(KnowledgeBaseStore store)
    {
        // Ambil artikel getting-started dari store
        var article = store.GetArticle("getting-started");

        if (article is null)
        {
            return new TextResourceContents
            {
                Text = "Artikel getting-started tidak ditemukan.",
                Uri = "kb://articles/getting-started",
                MimeType = "text/plain"
            };
        }

        // Kembalikan konten dengan MIME type sesuai artikel (text/markdown)
        return new TextResourceContents
        {
            Text = article.Content,
            Uri = "kb://articles/getting-started",
            MimeType = article.MimeType
        };
    }

    /// <summary>
    /// Resource: Daftar semua kategori dalam Knowledge Base.
    /// Mengembalikan data terstruktur dalam format JSON (application/json).
    /// Ini mendemonstrasikan bahwa resource tidak hanya untuk teks —
    /// data terstruktur juga dapat diekspos sebagai resource.
    /// </summary>
    [McpServerResource(
        UriTemplate = "kb://categories",
        Name = "All Categories")]
    [Description("Daftar semua kategori artikel dalam Knowledge Base (format JSON)")]
    public static ResourceContents GetCategories(KnowledgeBaseStore store)
    {
        // Ambil semua kategori dari store
        var categories = store.GetCategories().Select(c => new
        {
            c.Id,
            c.Name,
            c.Description,
            c.ArticleCount
        });

        // Serialisasi ke JSON dengan format yang mudah dibaca
        var jsonContent = JsonSerializer.Serialize(categories, JsonOptions);

        // Requirement 3.5: data terstruktur menggunakan application/json
        return new TextResourceContents
        {
            Text = jsonContent,
            Uri = "kb://categories",
            MimeType = "application/json"
        };
    }

    #endregion

    #region Template Resources (URI Dinamis)

    // Template resource menggunakan URI Templates (RFC 6570) dengan variabel placeholder.
    // Client menyediakan nilai parameter untuk mengakses resource secara dinamis.
    // Cocok untuk koleksi data di mana setiap item diakses berdasarkan identifier.

    /// <summary>
    /// Resource Template: Mengambil artikel berdasarkan ID.
    /// Parameter {articleId} di URI template akan diisi oleh client.
    /// Contoh URI: kb://articles/resource-tutorial, kb://articles/error-handling-practices
    /// </summary>
    [McpServerResource(
        UriTemplate = "kb://articles/{articleId}",
        Name = "Article by ID")]
    [Description("Mengambil artikel berdasarkan ID unik artikel")]
    public static ResourceContents GetArticleById(string articleId, KnowledgeBaseStore store)
    {
        // Cari artikel berdasarkan ID yang diberikan melalui URI template
        var article = store.GetArticle(articleId);

        // Jika artikel tidak ditemukan, kembalikan pesan error yang informatif
        // Ini lebih baik daripada melempar exception karena resource tetap memberikan response
        if (article is null)
        {
            return new TextResourceContents
            {
                Text = $"Artikel dengan ID '{articleId}' tidak ditemukan dalam Knowledge Base.",
                Uri = $"kb://articles/{articleId}",
                MimeType = "text/plain"
            };
        }

        // Kembalikan konten artikel dengan MIME type sesuai deklarasi
        // Kebanyakan artikel menggunakan text/markdown, tapi MIME type mengikuti field artikel
        return new TextResourceContents
        {
            Text = article.Content,
            Uri = $"kb://articles/{articleId}",
            MimeType = article.MimeType
        };
    }

    /// <summary>
    /// Resource Template: Mengambil semua artikel dalam kategori tertentu.
    /// Parameter {categoryName} merujuk pada nama kategori (case-insensitive).
    /// Mengembalikan daftar artikel dalam format JSON untuk kemudahan parsing.
    /// Contoh URI: kb://categories/tutorials/articles, kb://categories/best-practices/articles
    /// </summary>
    [McpServerResource(
        UriTemplate = "kb://categories/{categoryName}/articles",
        Name = "Articles by Category")]
    [Description("Mengambil daftar artikel berdasarkan nama kategori")]
    public static ResourceContents GetArticlesByCategory(string categoryName, KnowledgeBaseStore store)
    {
        // Ambil artikel berdasarkan nama kategori (pencarian case-insensitive)
        var articles = store.GetArticlesByCategory(categoryName).Select(a => new
        {
            a.Id,
            a.Title,
            a.Author,
            a.CreatedAt,
            a.UpdatedAt,
            Tags = a.Tags,
            a.MimeType
        });

        var articleList = articles.ToList();

        // Jika tidak ada artikel dalam kategori, kembalikan array kosong
        // tetapi tetap dengan format JSON yang valid
        var result = new
        {
            Category = categoryName,
            Count = articleList.Count,
            Articles = articleList
        };

        var jsonContent = JsonSerializer.Serialize(result, JsonOptions);

        // Mengembalikan data terstruktur dalam format JSON
        return new TextResourceContents
        {
            Text = jsonContent,
            Uri = $"kb://categories/{categoryName}/articles",
            MimeType = "application/json"
        };
    }

    #endregion
}
