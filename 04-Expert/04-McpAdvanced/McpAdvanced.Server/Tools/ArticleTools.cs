using System.ComponentModel;
using McpAdvanced.Server.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpAdvanced.Server.Tools;

/// <summary>
/// Tool class untuk operasi artikel dalam Knowledge Base.
/// Mendemonstrasikan dua fitur MCP lanjutan:
/// 1. Structured Content — output mengikuti JSON Schema 2020-12 yang telah didefinisikan
/// 2. Progress Tracking — pelaporan progress untuk operasi long-running
/// 
/// Kedua tool juga mendemonstrasikan server-side logging ke client melalui MCP protocol.
/// </summary>
[McpServerToolType]
public class ArticleTools
{
    /// <summary>
    /// Tool untuk membuat artikel baru dalam Knowledge Base.
    /// Menggunakan UseStructuredContent = true sehingga output di-serialize sebagai
    /// JSON Schema 2020-12 structured content, bukan plain text.
    /// 
    /// Client menerima output terstruktur yang dapat divalidasi terhadap schema,
    /// memungkinkan tooling otomatis dan integrasi yang lebih robust.
    /// </summary>
    /// <param name="title">Judul artikel yang akan dibuat</param>
    /// <param name="content">Konten artikel dalam format markdown</param>
    /// <param name="categoryId">Id kategori tempat artikel disimpan</param>
    /// <param name="store">In-memory data store — di-inject oleh DI container</param>
    /// <param name="server">Instance MCP Server — digunakan untuk logging ke client</param>
    /// <returns>ArticleCreationResult yang di-serialize sebagai structured content</returns>
    [McpServerTool(UseStructuredContent = true), Description("Creates a new article in the knowledge base")]
    public static ArticleCreationResult CreateArticle(
        [Description("The title of the article")] string title,
        [Description("The content of the article in markdown format")] string content,
        [Description("The category ID where the article will be stored")] string categoryId,
        KnowledgeBaseStore store,
        McpServer server)
    {
        // Buat logger untuk mengirim log messages ke connected MCP client
        var loggerProvider = server.AsClientLoggerProvider();
        var logger = loggerProvider.CreateLogger("ArticleTools.CreateArticle");

        // Log level debug — informasi detail untuk debugging
        logger.LogDebug("Memulai pembuatan artikel dengan judul: {Title}, kategori: {CategoryId}", title, categoryId);

        // Validasi input — pastikan parameter tidak kosong
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
        {
            // Log level error — operasi gagal karena input tidak valid
            logger.LogError("Pembuatan artikel gagal: judul atau konten kosong");

            return new ArticleCreationResult
            {
                ArticleId = string.Empty,
                Title = title ?? string.Empty,
                CategoryId = categoryId ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                Status = "error",
                ErrorMessage = "Title and content must not be empty"
            };
        }

        // Validasi kategori — pastikan categoryId ada di store
        if (!store.Categories.ContainsKey(categoryId))
        {
            // Log level warning — kategori tidak ditemukan, operasi gagal secara graceful
            logger.LogWarning("Kategori tidak ditemukan: {CategoryId}. Artikel tidak dibuat.", categoryId);

            return new ArticleCreationResult
            {
                ArticleId = string.Empty,
                Title = title,
                CategoryId = categoryId,
                CreatedAt = DateTime.UtcNow,
                Status = "error",
                ErrorMessage = $"Category '{categoryId}' not found"
            };
        }

        // Buat artikel melalui store — operasi thread-safe menggunakan ConcurrentDictionary
        var article = store.CreateArticle(title, content, categoryId);

        // Log level info — operasi berhasil, informasikan client
        logger.LogInformation("Artikel berhasil dibuat: Id={ArticleId}, Judul={Title}, Kategori={CategoryId}",
            article.Id, article.Title, article.CategoryId);

        // Kembalikan hasil sebagai structured content — SDK akan serialize sesuai JSON Schema
        return new ArticleCreationResult
        {
            ArticleId = article.Id,
            Title = article.Title,
            CategoryId = article.CategoryId,
            CreatedAt = article.CreatedAt,
            Status = "created"
        };
    }

    /// <summary>
    /// Tool untuk memproses banyak artikel secara bulk dengan pelaporan progress.
    /// Mendemonstrasikan:
    /// - IProgress&lt;ProgressNotificationValue&gt; untuk mengirim progress ke client
    /// - CancellationToken untuk mendukung pembatalan operasi long-running
    /// - Server-side logging pada berbagai tahapan operasi
    /// 
    /// Progress dikirim untuk setiap artikel yang diproses, sehingga client
    /// dapat menampilkan progress bar atau persentase penyelesaian.
    /// </summary>
    /// <param name="articleIds">Array Id artikel yang akan diproses</param>
    /// <param name="operation">Jenis operasi: "validate", "archive", atau "reindex"</param>
    /// <param name="progress">Handler progress — SDK menyediakan secara otomatis jika client mengirim progressToken</param>
    /// <param name="cancellationToken">Token pembatalan — memungkinkan client menghentikan operasi</param>
    /// <param name="store">In-memory data store — di-inject oleh DI container</param>
    /// <param name="server">Instance MCP Server — digunakan untuk logging ke client</param>
    /// <returns>Ringkasan hasil pemrosesan bulk</returns>
    [McpServerTool, Description("Processes multiple articles in bulk with progress reporting")]
    public static async Task<string> BulkProcessArticles(
        [Description("Array of article IDs to process")] string[] articleIds,
        [Description("Operation to perform: validate, archive, or reindex")] string operation,
        IProgress<ProgressNotificationValue> progress,
        CancellationToken cancellationToken,
        KnowledgeBaseStore store,
        McpServer server)
    {
        // Buat logger untuk mengirim log ke client selama operasi bulk
        var loggerProvider = server.AsClientLoggerProvider();
        var logger = loggerProvider.CreateLogger("ArticleTools.BulkProcessArticles");

        // Log level info — awal operasi bulk
        logger.LogInformation("Memulai bulk processing: {Operation} pada {Count} artikel",
            operation, articleIds.Length);

        // Validasi operasi yang didukung
        var validOperations = new[] { "validate", "archive", "reindex" };
        if (!validOperations.Contains(operation, StringComparer.OrdinalIgnoreCase))
        {
            logger.LogError("Operasi tidak valid: {Operation}. Operasi yang didukung: {ValidOps}",
                operation, string.Join(", ", validOperations));
            return $"Error: Invalid operation '{operation}'. Supported operations: {string.Join(", ", validOperations)}";
        }

        var totalItems = articleIds.Length;
        var processedCount = 0;
        var successCount = 0;
        var failedCount = 0;
        var cancelledEarly = false;

        // Kirim progress awal — notifikasi pertama bahwa operasi dimulai
        progress?.Report(new ProgressNotificationValue
        {
            Progress = 0,
            Total = totalItems,
            Message = $"Starting {operation} operation on {totalItems} articles..."
        });

        for (var i = 0; i < totalItems; i++)
        {
            // Periksa cancellation sebelum memproses artikel berikutnya
            // Ini memungkinkan client menghentikan operasi secara graceful
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Operasi dibatalkan oleh client setelah memproses {Processed}/{Total} artikel",
                    processedCount, totalItems);
                cancelledEarly = true;
                break;
            }

            var articleId = articleIds[i];

            // Log level debug — detail per-artikel untuk troubleshooting
            logger.LogDebug("Memproses artikel {Index}/{Total}: {ArticleId}",
                i + 1, totalItems, articleId);

            // Simulasi pemrosesan — cek apakah artikel ada di store
            var article = store.GetArticle(articleId);
            if (article is null)
            {
                // Artikel tidak ditemukan — catat sebagai gagal
                logger.LogWarning("Artikel tidak ditemukan: {ArticleId}, melewati pemrosesan", articleId);
                failedCount++;
            }
            else
            {
                // Lakukan operasi sesuai jenis yang diminta
                await ProcessSingleArticle(article, operation, cancellationToken);
                successCount++;
            }

            processedCount++;

            // Kirim progress notification untuk setiap artikel yang diproses
            // Ini memenuhi requirement minimal 3 notifikasi progress
            progress?.Report(new ProgressNotificationValue
            {
                Progress = processedCount,
                Total = totalItems,
                Message = $"Processed {processedCount}/{totalItems}: {operation} on '{articleId}'"
            });
        }

        // Log level info — ringkasan akhir operasi
        var statusMessage = cancelledEarly ? "cancelled" : "completed";
        logger.LogInformation(
            "Bulk {Operation} {Status}: {Processed}/{Total} diproses, {Success} berhasil, {Failed} gagal",
            operation, statusMessage, processedCount, totalItems, successCount, failedCount);

        // Kembalikan ringkasan hasil sebagai plain text
        return cancelledEarly
            ? $"Bulk {operation} cancelled after processing {processedCount}/{totalItems} articles. " +
              $"Success: {successCount}, Failed: {failedCount}."
            : $"Bulk {operation} completed. Processed: {processedCount}/{totalItems}, " +
              $"Success: {successCount}, Failed: {failedCount}.";
    }

    /// <summary>
    /// Helper method untuk memproses satu artikel berdasarkan jenis operasi.
    /// Simulasi delay untuk mendemonstrasikan progress tracking pada operasi long-running.
    /// </summary>
    private static async Task ProcessSingleArticle(Article article, string operation, CancellationToken cancellationToken)
    {
        // Simulasi waktu pemrosesan — operasi real akan melakukan I/O atau komputasi
        switch (operation.ToLowerInvariant())
        {
            case "validate":
                // Validasi: periksa struktur dan konten artikel
                await Task.Delay(100, cancellationToken);
                break;

            case "archive":
                // Archive: tandai artikel sebagai archived (simulasi)
                await Task.Delay(150, cancellationToken);
                break;

            case "reindex":
                // Reindex: perbarui indeks pencarian untuk artikel
                await Task.Delay(200, cancellationToken);
                break;
        }
    }
}
