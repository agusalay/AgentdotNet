// AdminTools.cs — MCP Tools untuk operasi administrasi Knowledge Base
// File ini mendemonstrasikan tiga fitur MCP lanjutan:
// 1. MRTR (Multi Round-Trip Request) — meminta konfirmasi user sebelum operasi destruktif
// 2. Sampling — meminta LLM classification dari client untuk auto-kategorisasi
// 3. Elicitation — meminta input tambahan dari user melalui form terstruktur
//
// Ketiga fitur ini menunjukkan pola "server-to-client request" dimana server
// menginisiasi komunikasi ke client untuk mendapatkan informasi tambahan.

using System.ComponentModel;
using System.Text.Json;
using McpAdvanced.Server.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpAdvanced.Server.Tools;

/// <summary>
/// Kelas yang berisi administration tools untuk Knowledge Base.
/// Mendemonstrasikan server-to-client communication patterns:
/// - MRTR via InputRequiredException untuk konfirmasi penghapusan
/// - Sampling via McpServer.SampleAsync() untuk AI-based kategorisasi
/// - Elicitation via McpServer.ElicitAsync() untuk input format dari user
/// 
/// Setiap tool memeriksa client capabilities sebelum menggunakan fitur lanjutan,
/// dan menyediakan fallback behavior jika client tidak mendukung fitur tersebut.
/// </summary>
[McpServerToolType]
public class AdminTools
{
    /// <summary>
    /// Tool untuk menghapus artikel dengan konfirmasi user menggunakan MRTR pattern.
    /// 
    /// Flow MRTR:
    /// 1. Client memanggil tool pertama kali tanpa InputResponses
    /// 2. Server melempar InputRequiredException yang berisi elicitation request untuk konfirmasi
    /// 3. Client menerima InputRequiredResult, menampilkan konfirmasi ke user
    /// 4. Client memanggil ulang tool dengan InputResponses berisi jawaban user
    /// 5. Server membaca InputResponses dan melakukan penghapusan jika dikonfirmasi
    /// 
    /// Pattern ini memastikan operasi destruktif tidak dilakukan tanpa persetujuan user.
    /// </summary>
    /// <param name="articleId">Id artikel yang akan dihapus</param>
    /// <param name="server">MCP Server instance untuk logging</param>
    /// <param name="context">Request context yang berisi InputResponses dari client pada retry</param>
    /// <param name="store">Knowledge Base store (di-inject melalui DI)</param>
    /// <returns>Status penghapusan artikel</returns>
    [McpServerTool, Description("Deletes an article with user confirmation using MRTR pattern")]
    public static string DeleteArticleWithConfirmation(
        [Description("The ID of the article to delete")] string articleId,
        McpServer server,
        RequestContext<CallToolRequestParams> context,
        KnowledgeBaseStore store)
    {
        // Buat logger untuk mengirim log ke MCP client
        var loggerProvider = server.AsClientLoggerProvider();
        var logger = loggerProvider.CreateLogger("AdminTools.DeleteArticleWithConfirmation");

        logger.LogInformation("Permintaan hapus artikel: {ArticleId}", articleId);

        // Validasi artikel ada di store sebelum meminta konfirmasi
        var article = store.GetArticle(articleId);
        if (article is null)
        {
            logger.LogWarning("Artikel tidak ditemukan: {ArticleId}", articleId);
            return $"Error: Article '{articleId}' not found.";
        }

        // Periksa apakah ini adalah retry call dengan InputResponses dari client
        // InputResponses berisi jawaban user terhadap permintaan konfirmasi sebelumnya
        if (context.Params?.InputResponses is { } inputResponses
            && inputResponses.TryGetValue("confirm_delete", out var response))
        {
            // Parse respons user — periksa apakah user mengkonfirmasi penghapusan
            var confirmed = response.RawValue.ValueKind == JsonValueKind.True;

            if (confirmed)
            {
                // User mengkonfirmasi — lakukan penghapusan
                var deleted = store.DeleteArticle(articleId);
                if (deleted)
                {
                    logger.LogInformation("Artikel berhasil dihapus: {ArticleId} ({Title})",
                        articleId, article.Title);
                    return $"Article '{article.Title}' (ID: {articleId}) has been successfully deleted.";
                }

                logger.LogError("Gagal menghapus artikel: {ArticleId}", articleId);
                return $"Error: Failed to delete article '{articleId}'.";
            }

            // User menolak penghapusan
            logger.LogInformation("Penghapusan dibatalkan oleh user untuk artikel: {ArticleId}", articleId);
            return $"Deletion cancelled. Article '{article.Title}' was not deleted.";
        }

        // Ini adalah panggilan pertama — lempar InputRequiredException untuk meminta konfirmasi
        // Server mengirim elicitation request dengan schema boolean untuk konfirmasi
        logger.LogInformation("Meminta konfirmasi penghapusan untuk artikel: {Title} ({ArticleId})",
            article.Title, articleId);

        // Buat elicitation params untuk konfirmasi penghapusan
        var elicitParams = new ElicitRequestParams
        {
            Message = $"Are you sure you want to delete article '{article.Title}' (ID: {articleId})? This action cannot be undone.",
            RequestedSchema = new ElicitRequestParams.RequestSchema
            {
                Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                {
                    ["confirm_delete"] = new ElicitRequestParams.BooleanSchema
                    {
                        Title = "Confirm Deletion",
                        Description = $"Set to true to confirm deletion of article '{article.Title}'"
                    }
                },
                Required = ["confirm_delete"]
            }
        };

        // Serialize ElicitRequestParams ke JsonElement dan assign ke InputRequest.Params
        // ElicitationParams adalah computed property yang di-deserialize dari Params
        var paramsJson = JsonSerializer.Serialize(elicitParams);
        var paramsElement = JsonSerializer.Deserialize<JsonElement>(paramsJson);

        var inputRequests = new Dictionary<string, InputRequest>
        {
            ["confirm_delete"] = new InputRequest
            {
                Method = RequestMethods.ElicitationCreate,
                Params = paramsElement
            }
        };

        // Simpan state untuk digunakan pada retry — berisi articleId yang akan dihapus
        var requestState = JsonSerializer.Serialize(new { articleId, title = article.Title });

        // Lempar exception — SDK akan mengkonversi ini menjadi InputRequiredResult
        // dan mengembalikannya ke client sebagai respons tool call
        throw new InputRequiredException(inputRequests, requestState);
    }

    /// <summary>
    /// Tool untuk mengkategorisasi artikel secara otomatis menggunakan Sampling.
    /// 
    /// Sampling memungkinkan server meminta LLM completion dari client.
    /// Server mengirim CreateMessageRequestParams ke client, client meneruskan ke LLM,
    /// dan mengembalikan hasilnya ke server.
    /// 
    /// Jika client tidak mendukung Sampling capability, tool ini menggunakan
    /// fallback berupa kategorisasi berbasis keyword sederhana.
    /// </summary>
    /// <param name="articleId">Id artikel yang akan dikategorisasi</param>
    /// <param name="server">MCP Server instance — digunakan untuk SampleAsync dan logging</param>
    /// <param name="store">Knowledge Base store (di-inject melalui DI)</param>
    /// <param name="cancellationToken">Token pembatalan operasi</param>
    /// <returns>Hasil kategorisasi artikel</returns>
    [McpServerTool, Description("Auto-categorizes an article using AI via sampling")]
    public static async Task<string> AutoCategorizeArticle(
        [Description("The ID of the article to categorize")] string articleId,
        McpServer server,
        KnowledgeBaseStore store,
        CancellationToken cancellationToken)
    {
        // Buat logger untuk mengirim log ke MCP client
        var loggerProvider = server.AsClientLoggerProvider();
        var logger = loggerProvider.CreateLogger("AdminTools.AutoCategorizeArticle");

        logger.LogInformation("Memulai auto-kategorisasi untuk artikel: {ArticleId}", articleId);

        // Validasi artikel ada di store
        var article = store.GetArticle(articleId);
        if (article is null)
        {
            logger.LogWarning("Artikel tidak ditemukan: {ArticleId}", articleId);
            return $"Error: Article '{articleId}' not found.";
        }

        // Ambil daftar kategori yang tersedia untuk referensi LLM
        var categories = store.GetCategories().Select(c => c.Name).ToList();
        var categoryList = string.Join(", ", categories);

        // Periksa apakah client mendukung Sampling capability
        if (server.ClientCapabilities?.Sampling is not null)
        {
            // Client mendukung Sampling — gunakan LLM untuk kategorisasi
            logger.LogInformation("Client mendukung Sampling, meminta LLM classification");

            try
            {
                // Buat request sampling ke client dengan prompt untuk klasifikasi
                var samplingRequest = new CreateMessageRequestParams
                {
                    Messages =
                    [
                        new SamplingMessage
                        {
                            Role = Role.User,
                            Content =
                            [
                                new TextContentBlock
                                {
                                    Text = $"""
                                        Classify the following article into exactly one of these categories: {categoryList}

                                        Article Title: {article.Title}
                                        Article Content (first 500 chars): {article.Content[..Math.Min(500, article.Content.Length)]}
                                        Current Tags: {string.Join(", ", article.Tags)}

                                        Respond with ONLY the category name, nothing else.
                                        """
                                }
                            ]
                        }
                    ],
                    MaxTokens = 50,
                    SystemPrompt = "You are a content classifier. Respond only with the category name."
                };

                // Kirim sampling request ke client — client akan meneruskan ke LLM
                var result = await server.SampleAsync(samplingRequest, cancellationToken);

                // Ekstrak kategori dari respons LLM
                var suggestedCategory = ExtractTextFromContent(result.Content).Trim().ToLowerInvariant();

                // Validasi kategori yang disarankan LLM ada di store
                if (categories.Contains(suggestedCategory, StringComparer.OrdinalIgnoreCase))
                {
                    // Update kategori artikel
                    var oldCategory = article.CategoryId;
                    article.CategoryId = suggestedCategory;
                    article.UpdatedAt = DateTime.UtcNow;

                    logger.LogInformation(
                        "Artikel berhasil dikategorisasi oleh AI: {ArticleId} dari '{OldCategory}' ke '{NewCategory}'",
                        articleId, oldCategory, suggestedCategory);

                    return $"Article '{article.Title}' has been re-categorized from '{oldCategory}' to '{suggestedCategory}' (AI-suggested).";
                }

                // Kategori dari LLM tidak valid — gunakan fallback
                logger.LogWarning(
                    "LLM menyarankan kategori tidak valid: '{Suggested}'. Menggunakan fallback.",
                    suggestedCategory);

                return FallbackCategorize(article, categories, store, logger);
            }
            catch (Exception ex)
            {
                // Sampling gagal — gunakan fallback
                logger.LogWarning("Sampling gagal: {Error}. Menggunakan fallback kategorisasi.", ex.Message);
                return FallbackCategorize(article, categories, store, logger);
            }
        }

        // Client tidak mendukung Sampling — gunakan fallback keyword-based
        logger.LogInformation("Client tidak mendukung Sampling capability, menggunakan fallback");
        return FallbackCategorize(article, categories, store, logger);
    }

    /// <summary>
    /// Tool untuk mengekspor artikel dengan format yang dipilih user melalui Elicitation.
    /// 
    /// Elicitation memungkinkan server meminta input tambahan dari user melalui form.
    /// Server mengirim ElicitRequestParams dengan schema form, client menampilkan form ke user,
    /// dan mengembalikan hasilnya ke server.
    /// 
    /// Jika client tidak mendukung Elicitation capability, tool menggunakan format default (JSON).
    /// </summary>
    /// <param name="categoryId">Id kategori yang artikelnya akan diekspor</param>
    /// <param name="server">MCP Server instance — digunakan untuk ElicitAsync dan logging</param>
    /// <param name="store">Knowledge Base store (di-inject melalui DI)</param>
    /// <param name="cancellationToken">Token pembatalan operasi</param>
    /// <returns>Artikel yang diekspor dalam format yang dipilih user</returns>
    [McpServerTool, Description("Exports articles with user-specified format via elicitation")]
    public static async Task<string> ExportArticles(
        [Description("The category ID to export articles from")] string categoryId,
        McpServer server,
        KnowledgeBaseStore store,
        CancellationToken cancellationToken)
    {
        // Buat logger untuk mengirim log ke MCP client
        var loggerProvider = server.AsClientLoggerProvider();
        var logger = loggerProvider.CreateLogger("AdminTools.ExportArticles");

        logger.LogInformation("Permintaan ekspor artikel untuk kategori: {CategoryId}", categoryId);

        // Validasi kategori ada di store
        if (!store.Categories.ContainsKey(categoryId))
        {
            var availableCategories = store.GetCategories().Select(c => c.Name);
            logger.LogWarning("Kategori tidak ditemukan: {CategoryId}", categoryId);
            return $"Error: Category '{categoryId}' not found. Available categories: {string.Join(", ", availableCategories)}";
        }

        // Ambil artikel dalam kategori
        var articles = store.GetArticlesByCategory(categoryId).ToList();
        if (articles.Count == 0)
        {
            return $"No articles found in category '{categoryId}'.";
        }

        // Tentukan format ekspor — gunakan Elicitation jika didukung
        string exportFormat;

        // Periksa apakah client mendukung Elicitation capability
        if (server.ClientCapabilities?.Elicitation is not null)
        {
            // Client mendukung Elicitation — tanyakan format ke user
            logger.LogInformation("Client mendukung Elicitation, meminta format dari user");

            try
            {
                // Buat elicitation request dengan schema enum untuk pilihan format
                var elicitRequest = new ElicitRequestParams
                {
                    Message = $"Choose the export format for {articles.Count} articles in category '{categoryId}':",
                    RequestedSchema = new ElicitRequestParams.RequestSchema
                    {
                        Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                        {
                            ["format"] = new ElicitRequestParams.UntitledSingleSelectEnumSchema
                            {
                                Title = "Export Format",
                                Description = "Select the format for article export",
                                Enum = ["json", "markdown", "csv"],
                                Default = "json"
                            }
                        },
                        Required = ["format"]
                    }
                };

                // Kirim elicitation request ke client — client menampilkan form ke user
                var result = await server.ElicitAsync(elicitRequest, cancellationToken);

                // Periksa apakah user menerima atau menolak elicitation
                if (result.IsAccepted && result.Content is { } content
                    && content.TryGetValue("format", out var formatValue))
                {
                    exportFormat = formatValue.GetString() ?? "json";
                    logger.LogInformation("User memilih format ekspor: {Format}", exportFormat);
                }
                else
                {
                    // User menolak elicitation — gunakan default
                    logger.LogInformation("User menolak elicitation, menggunakan format default JSON");
                    exportFormat = "json";
                }
            }
            catch (Exception ex)
            {
                // Elicitation gagal — gunakan format default
                logger.LogWarning("Elicitation gagal: {Error}. Menggunakan format default JSON.", ex.Message);
                exportFormat = "json";
            }
        }
        else
        {
            // Client tidak mendukung Elicitation — gunakan format default
            logger.LogInformation("Client tidak mendukung Elicitation capability, menggunakan format default JSON");
            exportFormat = "json";
        }

        // Lakukan ekspor sesuai format yang dipilih
        return FormatExport(articles, categoryId, exportFormat, logger);
    }

    #region Helper Methods

    /// <summary>
    /// Fallback kategorisasi berbasis keyword sederhana.
    /// Digunakan ketika client tidak mendukung Sampling atau ketika Sampling gagal.
    /// </summary>
    private static string FallbackCategorize(
        Article article,
        List<string> categories,
        KnowledgeBaseStore store,
        ILogger logger)
    {
        // Kategorisasi sederhana berdasarkan keyword matching
        var content = (article.Title + " " + article.Content).ToLowerInvariant();
        var tags = string.Join(" ", article.Tags).ToLowerInvariant();
        var combined = content + " " + tags;

        string suggestedCategory;

        if (combined.Contains("tutorial") || combined.Contains("step by step") || combined.Contains("how to"))
        {
            suggestedCategory = "tutorials";
        }
        else if (combined.Contains("api") || combined.Contains("reference") || combined.Contains("endpoint"))
        {
            suggestedCategory = "api-reference";
        }
        else if (combined.Contains("best practice") || combined.Contains("pattern") || combined.Contains("security"))
        {
            suggestedCategory = "best-practices";
        }
        else if (combined.Contains("getting started") || combined.Contains("introduction") || combined.Contains("setup"))
        {
            suggestedCategory = "getting-started";
        }
        else
        {
            // Default — tetap di kategori saat ini
            suggestedCategory = article.CategoryId;
        }

        // Update kategori jika berbeda
        if (suggestedCategory != article.CategoryId && categories.Contains(suggestedCategory))
        {
            var oldCategory = article.CategoryId;
            article.CategoryId = suggestedCategory;
            article.UpdatedAt = DateTime.UtcNow;

            logger.LogInformation(
                "Artikel dikategorisasi (fallback keyword): {ArticleId} dari '{Old}' ke '{New}'",
                article.Id, oldCategory, suggestedCategory);

            return $"Article '{article.Title}' has been re-categorized from '{oldCategory}' to '{suggestedCategory}' (keyword-based fallback, Sampling not available).";
        }

        logger.LogInformation("Artikel tetap di kategori saat ini: {CategoryId}", article.CategoryId);
        return $"Article '{article.Title}' remains in category '{article.CategoryId}' (keyword-based fallback, no better category found).";
    }

    /// <summary>
    /// Mengekstrak teks dari IList ContentBlock pada CreateMessageResult.
    /// </summary>
    private static string ExtractTextFromContent(IList<ContentBlock>? content)
    {
        if (content is null || content.Count == 0)
            return string.Empty;

        // Cari TextContentBlock dalam content list
        foreach (var block in content)
        {
            if (block is TextContentBlock textBlock)
            {
                return textBlock.Text ?? string.Empty;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Memformat hasil ekspor sesuai format yang dipilih user.
    /// </summary>
    private static string FormatExport(
        List<Article> articles,
        string categoryId,
        string format,
        ILogger logger)
    {
        logger.LogInformation("Mengekspor {Count} artikel dalam format: {Format}", articles.Count, format);

        return format.ToLowerInvariant() switch
        {
            "json" => FormatAsJson(articles, categoryId),
            "markdown" => FormatAsMarkdown(articles, categoryId),
            "csv" => FormatAsCsv(articles, categoryId),
            _ => FormatAsJson(articles, categoryId) // Default ke JSON jika format tidak dikenal
        };
    }

    /// <summary>
    /// Format ekspor JSON — terstruktur dan mudah di-parse secara programmatik.
    /// </summary>
    private static string FormatAsJson(List<Article> articles, string categoryId)
    {
        var export = new
        {
            Category = categoryId,
            ExportedAt = DateTime.UtcNow,
            Count = articles.Count,
            Articles = articles.Select(a => new
            {
                a.Id,
                a.Title,
                a.Author,
                a.CreatedAt,
                a.UpdatedAt,
                a.Tags,
                ContentPreview = a.Content.Length > 200
                    ? a.Content[..200] + "..."
                    : a.Content
            })
        };

        return JsonSerializer.Serialize(export, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Format ekspor Markdown — readable dan cocok untuk dokumentasi.
    /// </summary>
    private static string FormatAsMarkdown(List<Article> articles, string categoryId)
    {
        var output = $"# Export: Category '{categoryId}'\n\n";
        output += $"**Exported at:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n";
        output += $"**Total articles:** {articles.Count}\n\n---\n\n";

        foreach (var article in articles)
        {
            output += $"## {article.Title}\n\n";
            output += $"- **ID:** {article.Id}\n";
            output += $"- **Author:** {article.Author}\n";
            output += $"- **Created:** {article.CreatedAt:yyyy-MM-dd}\n";
            output += $"- **Tags:** {string.Join(", ", article.Tags)}\n\n";
            output += $"{article.Content}\n\n---\n\n";
        }

        return output.TrimEnd();
    }

    /// <summary>
    /// Format ekspor CSV — compact dan mudah diimpor ke spreadsheet.
    /// </summary>
    private static string FormatAsCsv(List<Article> articles, string categoryId)
    {
        var output = "Id,Title,Author,CategoryId,CreatedAt,UpdatedAt,Tags\n";

        foreach (var article in articles)
        {
            // Escape fields yang mungkin mengandung koma atau newline
            var title = EscapeCsvField(article.Title);
            var author = EscapeCsvField(article.Author);
            var tags = EscapeCsvField(string.Join("; ", article.Tags));

            output += $"{article.Id},{title},{author},{article.CategoryId}," +
                      $"{article.CreatedAt:yyyy-MM-dd HH:mm:ss},{article.UpdatedAt:yyyy-MM-dd HH:mm:ss},{tags}\n";
        }

        return output.TrimEnd();
    }

    /// <summary>
    /// Helper untuk escape field CSV yang mengandung karakter khusus.
    /// </summary>
    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    #endregion
}
