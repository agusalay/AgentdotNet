// SearchTools.cs — MCP Tools untuk pencarian Knowledge Base
// File ini mengimplementasikan tools pencarian yang memungkinkan client
// mencari artikel berdasarkan query teks atau kategori.
//
// Fitur MCP yang didemonstrasikan:
// - [McpServerToolType] dan [McpServerTool] untuk registrasi tool otomatis
// - McpServer parameter injection untuk mengirim log messages ke client
// - Server-side logging melalui MCP protocol (notifications/message)
//   yang memungkinkan client melihat aktivitas internal server saat pencarian

using System.ComponentModel;
using System.Text.Json;
using McpAdvanced.Server.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpAdvanced.Server.Tools;

/// <summary>
/// Kelas yang berisi search-related tools untuk Knowledge Base.
/// Ditandai dengan [McpServerToolType] agar tools di dalamnya terdaftar otomatis
/// melalui WithToolsFromAssembly() pada konfigurasi server.
///
/// Setiap tool menerima McpServer sebagai parameter —
/// ini memungkinkan pengiriman log messages ke connected client
/// selama operasi pencarian berlangsung (Requirement 5.3, 5.4).
/// </summary>
[McpServerToolType]
public class SearchTools
{
    // Opsi JSON yang di-cache untuk konsistensi format output
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Tool: Mencari artikel dalam Knowledge Base berdasarkan query teks.
    /// Pencarian dilakukan di judul, konten, dan tags artikel.
    ///
    /// Tool ini mendemonstrasikan MCP logging — setiap operasi pencarian
    /// mengirimkan log message ke client sehingga client dapat melihat
    /// aktivitas pencarian yang sedang berlangsung di server.
    /// </summary>
    /// <param name="query">Kata kunci pencarian untuk mencari artikel</param>
    /// <param name="store">Knowledge Base store (di-inject melalui DI)</param>
    /// <param name="server">MCP Server instance untuk mengirim log ke client</param>
    /// <returns>Hasil pencarian dalam format teks yang terstruktur</returns>
    [McpServerTool, Description("Mencari artikel dalam Knowledge Base berdasarkan kata kunci")]
    public static string SearchArticles(
        [Description("Kata kunci pencarian (dicari di judul, konten, dan tags)")] string query,
        KnowledgeBaseStore store,
        McpServer server)
    {
        // Buat logger yang mengirim log messages ke MCP client
        // AsClientLoggerProvider() mengembalikan ILoggerProvider yang merutekan
        // log melalui MCP protocol (notifications/message) ke connected client
        var loggerProvider = server.AsClientLoggerProvider();
        var logger = loggerProvider.CreateLogger("SearchTools");

        // Log info: mulai pencarian — client dapat melihat pesan ini
        logger.LogInformation("Searching for: {Query}", query);

        // Validasi parameter input
        if (string.IsNullOrWhiteSpace(query))
        {
            logger.LogWarning("Search query is empty, returning no results");
            return "Parameter 'query' tidak boleh kosong. Berikan kata kunci pencarian yang valid.";
        }

        // Lakukan pencarian di knowledge base store
        var results = store.SearchArticles(query).ToList();

        // Log info: jumlah hasil yang ditemukan
        logger.LogInformation("Found {Count} results for query: {Query}", results.Count, query);

        // Jika tidak ada hasil, kembalikan pesan informatif
        if (results.Count == 0)
        {
            return $"Tidak ada artikel yang ditemukan untuk pencarian: \"{query}\"";
        }

        // Format hasil pencarian sebagai teks terstruktur
        // Setiap artikel menampilkan ID, judul, kategori, dan cuplikan konten
        var output = $"Hasil pencarian untuk \"{query}\" ({results.Count} artikel ditemukan):\n\n";

        for (var i = 0; i < results.Count; i++)
        {
            var article = results[i];
            // Ambil cuplikan dari konten (maksimal 150 karakter)
            var snippet = article.Content.Length > 150
                ? article.Content[..150].TrimEnd() + "..."
                : article.Content;

            output += $"[{i + 1}] {article.Title}\n";
            output += $"    ID: {article.Id}\n";
            output += $"    Kategori: {article.CategoryId}\n";
            output += $"    Tags: {string.Join(", ", article.Tags)}\n";
            output += $"    Cuplikan: {snippet}\n\n";
        }

        return output.TrimEnd();
    }

    /// <summary>
    /// Tool: Mengambil semua artikel dalam kategori tertentu.
    /// Berguna untuk browsing artikel berdasarkan topik/kategori.
    ///
    /// Tool ini juga mendemonstrasikan MCP logging dengan level info —
    /// mengirimkan pesan log ke client tentang kategori yang sedang dicari
    /// dan jumlah artikel yang ditemukan.
    /// </summary>
    /// <param name="categoryName">Nama kategori (case-insensitive)</param>
    /// <param name="store">Knowledge Base store (di-inject melalui DI)</param>
    /// <param name="server">MCP Server instance untuk mengirim log ke client</param>
    /// <returns>Daftar artikel dalam kategori tersebut (format JSON)</returns>
    [McpServerTool, Description("Mengambil daftar artikel berdasarkan nama kategori")]
    public static string GetArticlesByCategory(
        [Description("Nama kategori, contoh: tutorials, best-practices, getting-started, api-reference")] string categoryName,
        KnowledgeBaseStore store,
        McpServer server)
    {
        // Buat logger untuk mengirim log ke MCP client
        var loggerProvider = server.AsClientLoggerProvider();
        var logger = loggerProvider.CreateLogger("SearchTools");

        // Log info: pencarian berdasarkan kategori dimulai
        logger.LogInformation("Searching articles by category: {Category}", categoryName);

        // Validasi parameter input
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            logger.LogWarning("Category name is empty, returning no results");
            return "Parameter 'categoryName' tidak boleh kosong. Berikan nama kategori yang valid.";
        }

        // Ambil artikel berdasarkan nama kategori
        var articles = store.GetArticlesByCategory(categoryName).ToList();

        // Log info: jumlah artikel yang ditemukan dalam kategori
        logger.LogInformation("Found {Count} articles in category: {Category}", articles.Count, categoryName);

        // Jika tidak ada artikel, kembalikan pesan informatif
        if (articles.Count == 0)
        {
            // Tampilkan daftar kategori yang tersedia sebagai bantuan
            var availableCategories = store.GetCategories().Select(c => c.Name);
            return $"Tidak ada artikel dalam kategori \"{categoryName}\".\n" +
                   $"Kategori yang tersedia: {string.Join(", ", availableCategories)}";
        }

        // Format hasil sebagai JSON terstruktur untuk kemudahan parsing oleh client
        var result = new
        {
            Category = categoryName,
            Count = articles.Count,
            Articles = articles.Select(a => new
            {
                a.Id,
                a.Title,
                a.Author,
                a.CreatedAt,
                Tags = a.Tags
            })
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
