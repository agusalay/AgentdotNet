using System.Collections.Concurrent;

namespace McpAdvanced.Server.Models;

/// <summary>
/// In-memory data store untuk Knowledge Base Management System.
/// Menggunakan ConcurrentDictionary untuk thread-safety karena MCP server
/// menangani multiple concurrent requests melalui HTTP transport.
/// 
/// Desain ini dipilih untuk fokus pada konsep MCP, bukan database integration.
/// Pada production, store ini dapat diganti dengan database atau layanan eksternal.
/// </summary>
public class KnowledgeBaseStore
{
    // ConcurrentDictionary dipilih karena MCP server bersifat multi-threaded —
    // multiple client dapat mengakses resources secara bersamaan melalui HTTP transport
    public ConcurrentDictionary<string, Article> Articles { get; }
    public ConcurrentDictionary<string, Category> Categories { get; }

    /// <summary>
    /// Konstruktor menginisialisasi store dengan sample data.
    /// Pre-seeded data diperlukan agar demonstrasi MCP Resources, Prompts, dan Tools
    /// dapat langsung berjalan tanpa setup tambahan.
    /// </summary>
    public KnowledgeBaseStore()
    {
        Categories = new ConcurrentDictionary<string, Category>(SeedCategories()
            .ToDictionary(c => c.Id));

        Articles = new ConcurrentDictionary<string, Article>(SeedArticles()
            .ToDictionary(a => a.Id));

        // Hitung jumlah artikel per kategori setelah seeding
        RecalculateArticleCounts();
    }

    #region CRUD Operations

    /// <summary>
    /// Mengambil artikel berdasarkan Id.
    /// Digunakan oleh MCP Resource handler untuk URI seperti kb://articles/{articleId}
    /// </summary>
    public Article? GetArticle(string id)
    {
        // TryGetValue thread-safe — aman dipanggil dari multiple MCP request
        Articles.TryGetValue(id, out var article);
        return article;
    }

    /// <summary>
    /// Mengambil semua artikel dalam kategori tertentu berdasarkan nama kategori.
    /// Digunakan oleh resource template kb://categories/{categoryName}/articles
    /// </summary>
    public IEnumerable<Article> GetArticlesByCategory(string categoryName)
    {
        // Cari kategori berdasarkan nama (case-insensitive) untuk fleksibilitas URI
        var category = Categories.Values
            .FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));

        if (category is null)
            return [];

        // Filter artikel berdasarkan CategoryId yang cocok
        return Articles.Values
            .Where(a => a.CategoryId == category.Id)
            .OrderByDescending(a => a.CreatedAt);
    }

    /// <summary>
    /// Pencarian artikel berdasarkan query teks.
    /// Mencari di judul, konten, dan tags — digunakan oleh search prompt dan tools.
    /// </summary>
    public IEnumerable<Article> SearchArticles(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var lowerQuery = query.ToLowerInvariant();

        // Pencarian sederhana di title, content, dan tags
        // Untuk production, gunakan full-text search engine
        return Articles.Values.Where(a =>
            a.Title.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
            a.Content.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
            a.Tags.Any(t => t.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Membuat artikel baru dan menyimpannya ke store.
    /// Digunakan oleh CreateArticle tool — mengembalikan Article yang dibuat
    /// untuk dikonversi ke ArticleCreationResult (Structured Content).
    /// </summary>
    public Article CreateArticle(string title, string content, string categoryId)
    {
        // Generate Id unik berbasis timestamp — sederhana untuk demonstrasi
        var id = $"art-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..8]}";

        var article = new Article
        {
            Id = id,
            Title = title,
            Content = content,
            CategoryId = categoryId,
            Author = "system",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Tags = [],
            MimeType = "text/markdown"
        };

        // TryAdd thread-safe — mencegah duplikasi pada concurrent requests
        Articles.TryAdd(id, article);

        // Perbarui jumlah artikel di kategori terkait
        UpdateCategoryCount(categoryId, increment: true);

        return article;
    }

    /// <summary>
    /// Memperbarui konten artikel yang sudah ada.
    /// Setelah update, MCP server harus mengirim resource change notification
    /// ke client yang telah subscribe ke resource artikel ini.
    /// </summary>
    public bool UpdateArticle(string id, string content)
    {
        if (!Articles.TryGetValue(id, out var article))
            return false;

        // Update content dan timestamp — perubahan ini memicu resource notification
        article.Content = content;
        article.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Menghapus artikel dari store.
    /// Pada implementasi MRTR, penghapusan memerlukan konfirmasi user terlebih dahulu.
    /// </summary>
    public bool DeleteArticle(string id)
    {
        if (!Articles.TryRemove(id, out var removed))
            return false;

        // Perbarui jumlah artikel di kategori terkait
        UpdateCategoryCount(removed.CategoryId, increment: false);
        return true;
    }

    /// <summary>
    /// Mengambil semua kategori.
    /// Digunakan oleh direct resource kb://categories dan completion suggestions.
    /// </summary>
    public IEnumerable<Category> GetCategories()
    {
        return Categories.Values.OrderBy(c => c.Name);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Memperbarui ArticleCount pada kategori saat artikel ditambah atau dihapus.
    /// </summary>
    private void UpdateCategoryCount(string categoryId, bool increment)
    {
        if (Categories.TryGetValue(categoryId, out var category))
        {
            category.ArticleCount += increment ? 1 : -1;
        }
    }

    /// <summary>
    /// Menghitung ulang jumlah artikel per kategori berdasarkan data aktual.
    /// Dipanggil setelah seeding untuk memastikan konsistensi.
    /// </summary>
    private void RecalculateArticleCounts()
    {
        foreach (var category in Categories.Values)
        {
            category.ArticleCount = Articles.Values.Count(a => a.CategoryId == category.Id);
        }
    }

    #endregion

    #region Seed Data

    /// <summary>
    /// Data seed untuk kategori — minimal 4 kategori yang mencakup berbagai topik.
    /// Kategori ini digunakan di seluruh demonstrasi: resources, prompts, completions, pagination.
    /// </summary>
    private static List<Category> SeedCategories() =>
    [
        new Category
        {
            Id = "getting-started",
            Name = "getting-started",
            Description = "Panduan memulai penggunaan Knowledge Base dan MCP"
        },
        new Category
        {
            Id = "tutorials",
            Name = "tutorials",
            Description = "Tutorial langkah demi langkah untuk fitur-fitur utama"
        },
        new Category
        {
            Id = "best-practices",
            Name = "best-practices",
            Description = "Praktik terbaik dalam pengembangan dan arsitektur"
        },
        new Category
        {
            Id = "api-reference",
            Name = "api-reference",
            Description = "Referensi API lengkap dengan contoh penggunaan"
        }
    ];

    /// <summary>
    /// Data seed untuk artikel — minimal 6 artikel tersebar di berbagai kategori.
    /// Artikel dengan Id "introduction" dan "getting-started" wajib ada karena
    /// digunakan sebagai direct resource URI (kb://articles/introduction, kb://articles/getting-started).
    /// </summary>
    private static List<Article> SeedArticles() =>
    [
        // Artikel ini diakses melalui direct resource: kb://articles/introduction
        new Article
        {
            Id = "introduction",
            Title = "Introduction to Knowledge Base",
            Content = """
                # Introduction to Knowledge Base

                Selamat datang di Knowledge Base Management System. Sistem ini mendemonstrasikan 
                konsep-konsep MCP (Model Context Protocol) lanjutan termasuk Resources, Prompts, 
                Tools, dan fitur advanced lainnya.

                ## Apa itu Knowledge Base?

                Knowledge Base adalah repositori terpusat untuk menyimpan, mengelola, dan 
                mengakses dokumen pengetahuan. Dalam konteks MCP, setiap artikel diekspos 
                sebagai Resource yang dapat dibaca oleh client.

                ## Fitur Utama

                - **Resources**: Akses artikel melalui URI (direct dan template)
                - **Prompts**: Template pencarian dan ringkasan artikel
                - **Tools**: Operasi CRUD dan administrasi
                - **Subscriptions**: Notifikasi perubahan real-time
                """,
            CategoryId = "getting-started",
            Author = "admin",
            CreatedAt = new DateTime(2024, 1, 15, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 15, 8, 0, 0, DateTimeKind.Utc),
            Tags = ["mcp", "introduction", "overview"],
            MimeType = "text/markdown"
        },
        // Artikel ini diakses melalui direct resource: kb://articles/getting-started
        new Article
        {
            Id = "getting-started",
            Title = "Getting Started with MCP Advanced",
            Content = """
                # Getting Started with MCP Advanced

                Panduan ini membantu Anda memulai penggunaan MCP Advanced Server 
                dan Client dalam beberapa langkah sederhana.

                ## Prerequisites

                - .NET SDK 9.0 atau lebih baru
                - NuGet package ModelContextProtocol v2.x
                - Editor teks atau IDE (VS Code / Visual Studio)

                ## Langkah 1: Jalankan Server

                ```bash
                cd McpAdvanced.Server
                dotnet run
                ```

                Server akan berjalan di `http://localhost:5100`.

                ## Langkah 2: Jalankan Client

                ```bash
                cd McpAdvanced.Client
                dotnet run
                ```

                Client akan terhubung ke server melalui HTTP transport.

                ## Langkah 3: Eksplorasi Fitur

                Gunakan menu interaktif untuk mencoba Resources, Prompts, dan Tools.
                """,
            CategoryId = "getting-started",
            Author = "admin",
            CreatedAt = new DateTime(2024, 1, 16, 10, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 16, 10, 0, 0, DateTimeKind.Utc),
            Tags = ["mcp", "setup", "quickstart"],
            MimeType = "text/markdown"
        },
        new Article
        {
            Id = "resource-tutorial",
            Title = "Tutorial: Implementing MCP Resources",
            Content = """
                # Tutorial: Implementing MCP Resources

                Pelajari cara mengimplementasikan MCP Resources untuk mengekspos 
                data read-only dari server ke client.

                ## Konsep Dasar

                MCP Resources memungkinkan server mengekspos data melalui URI. 
                Ada dua jenis resource:

                1. **Direct Resources** — URI tetap (contoh: `kb://articles/introduction`)
                2. **Template Resources** — URI dengan parameter (contoh: `kb://articles/{id}`)

                ## Implementasi Direct Resource

                ```csharp
                [McpServerResource(UriTemplate = "kb://articles/intro", Name = "Intro")]
                public static TextResourceContents GetIntro(KnowledgeBaseStore store)
                {
                    var article = store.GetArticle("introduction");
                    return new TextResourceContents(article.Content, "kb://articles/intro", "text/markdown");
                }
                ```

                ## Resource Templates

                Template menggunakan RFC 6570 URI Templates untuk parameter dinamis.
                """,
            CategoryId = "tutorials",
            Author = "developer",
            CreatedAt = new DateTime(2024, 2, 1, 14, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 2, 5, 9, 30, 0, DateTimeKind.Utc),
            Tags = ["mcp", "resources", "tutorial", "uri-template"],
            MimeType = "text/markdown"
        },
        new Article
        {
            Id = "prompt-tutorial",
            Title = "Tutorial: Creating MCP Prompt Templates",
            Content = """
                # Tutorial: Creating MCP Prompt Templates

                Prompt templates memungkinkan server menyediakan template prompt 
                yang reusable untuk berbagai operasi knowledge base.

                ## Mengapa Prompt Templates?

                - **Reusability**: Satu template untuk banyak skenario
                - **Parameterization**: Isi dinamis berdasarkan context
                - **Discovery**: Client dapat menemukan prompts yang tersedia

                ## Contoh: Search Prompt

                ```csharp
                [McpServerPrompt(Name = "search-knowledge-base")]
                public static ChatMessage[] SearchKnowledgeBase(string query, KnowledgeBaseStore store)
                {
                    var results = store.SearchArticles(query);
                    // Build prompt content dengan hasil pencarian
                }
                ```

                ## Parameter Types

                Setiap parameter memiliki nama, deskripsi, dan status required/optional.
                """,
            CategoryId = "tutorials",
            Author = "developer",
            CreatedAt = new DateTime(2024, 2, 10, 11, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 2, 10, 11, 0, 0, DateTimeKind.Utc),
            Tags = ["mcp", "prompts", "tutorial", "templates"],
            MimeType = "text/markdown"
        },
        new Article
        {
            Id = "error-handling-practices",
            Title = "Best Practices: Error Handling in MCP",
            Content = """
                # Best Practices: Error Handling in MCP

                Panduan penanganan error yang robust untuk MCP server dan client.

                ## Prinsip Utama

                1. **Graceful Degradation** — Jangan crash, berikan fallback
                2. **Descriptive Errors** — Pesan error harus informatif
                3. **Proper Status Codes** — Gunakan McpException dengan pesan yang jelas

                ## Server-Side Patterns

                ```csharp
                // Resource tidak ditemukan
                if (article is null)
                    throw new McpException($"Resource not found: {uri}");

                // Tool execution failure
                return new CallToolResult { IsError = true, Content = [errorMessage] };
                ```

                ## Client-Side Patterns

                - Tangkap HttpRequestException untuk connection errors
                - Tangkap TimeoutException untuk timeout scenarios
                - Selalu tampilkan pesan yang user-friendly
                """,
            CategoryId = "best-practices",
            Author = "architect",
            CreatedAt = new DateTime(2024, 3, 5, 16, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 3, 10, 8, 0, 0, DateTimeKind.Utc),
            Tags = ["mcp", "error-handling", "best-practices", "resilience"],
            MimeType = "text/markdown"
        },
        new Article
        {
            Id = "security-best-practices",
            Title = "Best Practices: MCP Security Patterns",
            Content = """
                # Best Practices: MCP Security Patterns

                Keamanan adalah aspek kritis dalam deployment MCP server.

                ## Host Name Validation

                Cegah DNS rebinding attacks dengan memvalidasi Host header:

                ```json
                { "AllowedHosts": "localhost;myserver.example.com" }
                ```

                ## Environment Variable Isolation

                Jangan teruskan semua env vars ke child processes:

                ```csharp
                // AMAN: hanya variabel yang diperlukan
                InheritEnvironmentVariables = false
                ```

                ## Principle of Least Privilege

                - Ekspos hanya capabilities yang diperlukan
                - Periksa client capabilities sebelum menggunakan fitur
                - Batasi akses file ke Root URIs yang dideklarasikan client

                ## Transport Security

                - Gunakan HTTPS untuk production
                - Implementasikan rate limiting
                - Validasi semua input dari client
                """,
            CategoryId = "best-practices",
            Author = "security-team",
            CreatedAt = new DateTime(2024, 3, 15, 9, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 3, 15, 9, 0, 0, DateTimeKind.Utc),
            Tags = ["mcp", "security", "best-practices", "host-validation"],
            MimeType = "text/markdown"
        },
        new Article
        {
            Id = "tools-api-reference",
            Title = "API Reference: MCP Tools",
            Content = """
                # API Reference: MCP Tools

                Dokumentasi lengkap untuk semua tools yang tersedia di MCP Advanced Server.

                ## CreateArticle

                Membuat artikel baru dalam knowledge base.

                **Parameters:**
                - `title` (string, required): Judul artikel
                - `content` (string, required): Konten artikel dalam markdown
                - `categoryId` (string, required): Id kategori target

                **Returns:** ArticleCreationResult (Structured Content)

                ## UpdateArticle

                Memperbarui konten artikel yang sudah ada.

                **Parameters:**
                - `articleId` (string, required): Id artikel yang akan diperbarui
                - `content` (string, required): Konten baru

                **Returns:** Status update (success/failure)

                ## DeleteArticle

                Menghapus artikel (memerlukan konfirmasi via MRTR).

                **Parameters:**
                - `articleId` (string, required): Id artikel yang akan dihapus

                **Returns:** Status penghapusan
                """,
            CategoryId = "api-reference",
            Author = "docs-team",
            CreatedAt = new DateTime(2024, 4, 1, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 4, 5, 14, 0, 0, DateTimeKind.Utc),
            Tags = ["mcp", "api", "tools", "reference", "crud"],
            MimeType = "text/markdown"
        }
    ];

    #endregion
}
