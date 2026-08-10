// Program.cs — Entry point untuk MCP Advanced Server
// Menggunakan ASP.NET Core dengan Streamable HTTP Transport untuk MCP.
// Mode stateful (Stateless = false) diperlukan untuk fitur yang membutuhkan session:
// - Resource subscriptions dan change notifications
// - Server-to-client requests (Sampling, Elicitation)
// - Progress tracking pada long-running operations
//
// Konfigurasi ini mendaftarkan semua komponen MCP Server:
// - Tools: operasi CRUD dan advanced operations (dari assembly)
// - Resources: direct resources dan template resources untuk knowledge base
// - Prompts: template prompt yang reusable untuk pencarian dan ringkasan
// - Filters: logging dan timing filter untuk cross-cutting concerns
// - Pagination: cursor-based pagination untuk listing resources
// - Completions: auto-completion untuk prompt arguments dan resource template variables
//
// KEAMANAN — Host Name Validation:
// DNS Rebinding Attack adalah serangan di mana penyerang memanipulasi resolusi DNS
// sehingga browser korban mengirim request ke server lokal (localhost) menggunakan
// domain milik penyerang. Tanpa validasi Host header, server MCP yang berjalan di
// localhost bisa diakses oleh halaman web berbahaya melalui teknik ini.
//
// Untuk MCP server berbasis HTTP, host validation sangat penting karena:
// 1. Server sering berjalan di localhost untuk development/lokal
// 2. MCP server mengekspos tools yang bisa memodifikasi data atau menjalankan operasi
// 3. Tanpa perlindungan, halaman web berbahaya bisa memanggil MCP tools via DNS rebinding
//
// Konfigurasi AllowedHosts di appsettings.json mencegah serangan ini dengan:
// - Memvalidasi HTTP Host header pada setiap incoming request
// - Menolak request yang Host header-nya tidak sesuai dengan daftar host yang diizinkan
// - Memastikan hanya request dari hostname yang diharapkan yang bisa mencapai MCP endpoints

using McpAdvanced.Server.Completions;
using McpAdvanced.Server.Filters;
using McpAdvanced.Server.Models;
using McpAdvanced.Server.Pagination;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Registrasi KnowledgeBaseStore sebagai singleton — shared state untuk semua MCP sessions.
// Menggunakan singleton agar data konsisten di antara semua request dan session MCP.
builder.Services.AddSingleton<KnowledgeBaseStore>();

// Konfigurasi MCP Server dengan HTTP transport dan semua fitur lanjutan
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        // Mode stateful untuk mendukung subscriptions dan server-to-client requests.
        // Stateless = false berarti server mempertahankan session state menggunakan
        // header Mcp-Session-Id, yang diperlukan untuk:
        // - Resource subscriptions (client menerima notifikasi perubahan)
        // - Sampling requests (server meminta LLM completion dari client)
        // - Elicitation requests (server meminta input tambahan dari user)
        options.Stateless = false;
    })
    // Registrasi tools dari assembly menggunakan reflection-based discovery.
    // Semua class dengan atribut [McpServerToolType] akan ditemukan secara otomatis,
    // termasuk: ArticleTools, SearchTools, dan AdminTools.
    .WithToolsFromAssembly()
    // Registrasi resources dari assembly — menemukan class dengan [McpServerResourceType].
    // KnowledgeBaseResources menyediakan direct resources (URI tetap) dan
    // template resources (URI dinamis dengan parameter) untuk knowledge base.
    .WithResourcesFromAssembly()
    // Registrasi prompts dari assembly — menemukan class dengan [McpServerPromptType].
    // KnowledgeBasePrompts menyediakan template prompt: search-knowledge-base,
    // summarize-article, dan compare-articles.
    .WithPromptsFromAssembly()
    // Registrasi handler filters untuk cross-cutting concerns.
    // Filter membungkus setiap tool invocation dengan logic tambahan:
    // - LoggingFilter: mencatat nama tool, jumlah parameter, dan status penyelesaian
    // - TimingFilter: mengukur waktu eksekusi setiap tool
    // Urutan: Logging(pre) → Timing(pre) → Tool Handler → Timing(post) → Logging(post)
    .WithRequestFilters(filters =>
    {
        filters.AddKnowledgeBaseFilters();
    })
    // Registrasi custom handler untuk pagination pada listing resources.
    // Handler ini menggantikan default listing behavior dengan cursor-based pagination,
    // membatasi jumlah item per halaman dan menyediakan cursor untuk navigasi.
    .WithListResourcesHandler(ResourcePaginationHandler.HandleListResourcesAsync)
    // Registrasi handler untuk auto-completion pada prompt arguments dan resource template variables.
    // Memungkinkan client mendapatkan saran saat mengisi parameter (contoh: articleId, categoryName).
    .WithCompleteHandler(KnowledgeBaseCompletions.HandleCompleteAsync);

var app = builder.Build();

// Host Filtering Middleware — mencegah DNS rebinding attacks.
// Middleware ini memvalidasi HTTP Host header pada setiap request yang masuk.
// Jika Host header tidak cocok dengan daftar AllowedHosts di appsettings.json,
// request akan ditolak dengan HTTP 400 Bad Request sebelum mencapai MCP handler.
//
// Mengapa ini penting untuk MCP server berbasis HTTP:
// - MCP server mengekspos tools yang dapat memodifikasi data (CRUD operations)
// - Tanpa validasi host, penyerang bisa menggunakan DNS rebinding untuk
//   memanggil MCP tools dari halaman web berbahaya di browser korban
// - AllowedHosts: "localhost" memastikan hanya request langsung ke localhost
//   yang diterima, bukan request melalui domain penyerang yang di-resolve ke 127.0.0.1
app.UseHostFiltering();

// Health check endpoint — berguna untuk Docker deployment dan monitoring.
// Endpoint ini tidak memerlukan MCP session dan bisa digunakan untuk liveness probe.
app.MapGet("/", () => "McpAdvanced.Server is running");

// Map MCP endpoints ke HTTP routes (default: /mcp).
// Semua komunikasi MCP protocol (initialize, tools/call, resources/read, dll.)
// akan ditangani melalui endpoint ini.
app.MapMcp();

// Jalankan server pada port 5100.
// URL ini yang akan digunakan oleh McpAdvanced.Client untuk terhubung via HTTP transport.
app.Run("http://localhost:5100");
