// =============================================================================
// Adding Middleware - Modul Pembelajaran Kelima (Intermediate Level)
// Demonstrasi middleware pattern untuk mencegat dan memodifikasi perilaku agent
// Middleware pipeline menjalankan cross-cutting concerns: logging & guardrails
// =============================================================================

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.AI;
using AddingMiddleware.Middleware;

// --- Konfigurasi CancellationToken untuk menangani Ctrl+C ---
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    // Mencegah terminasi langsung agar cleanup bisa berjalan
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\n[INFO] Menekan Ctrl+C. Membatalkan operasi...");
};

try
{
    // --- Memuat konfigurasi dari appsettings.json ---
    var configuration = BuildConfiguration();

    // --- Setup Dependency Injection ---
    var services = new ServiceCollection();
    services.AddSingleton<IConfiguration>(configuration);
    var serviceProvider = services.BuildServiceProvider();

    // --- Menjalankan aplikasi utama ---
    await RunApplicationAsync(configuration, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("\n[INFO] Operasi dibatalkan oleh user.");
}
catch (HttpRequestException ex)
{
    // Menangani kegagalan koneksi ke Azure OpenAI
    Console.WriteLine($"[ERROR] Koneksi gagal: {ex.Message}");
    Console.WriteLine("[CAUSE] Endpoint tidak dapat dijangkau atau terjadi masalah jaringan.");
    Console.WriteLine("[HINT] Periksa endpoint di appsettings.json dan pastikan koneksi internet Anda aktif.");
}
catch (InvalidOperationException ex)
{
    // Menangani konfigurasi yang tidak valid
    Console.WriteLine($"[ERROR] Konfigurasi tidak valid: {ex.Message}");
    Console.WriteLine("[CAUSE] File appsettings.json tidak lengkap atau format salah.");
    Console.WriteLine("[HINT] Periksa appsettings.json memiliki key AzureOpenAI:Endpoint dan AzureOpenAI:DeploymentName.");
}
catch (Exception ex) when (ex is not OutOfMemoryException)
{
    // Menangani error tak terduga
    Console.WriteLine($"[ERROR] Terjadi kesalahan: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine("[CAUSE] Error tidak terduga saat menjalankan aplikasi.");
    Console.WriteLine("[HINT] Periksa log di atas untuk detail lebih lanjut.");
}

// =============================================================================
// Fungsi untuk membangun konfigurasi dari appsettings.json
// =============================================================================
static IConfiguration BuildConfiguration()
{
    // Memeriksa keberadaan file appsettings.json
    var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
    if (!File.Exists(configPath))
    {
        throw new InvalidOperationException(
            "File appsettings.json tidak ditemukan. " +
            "Pastikan file tersebut ada di direktori project.");
    }

    // Membaca dan mem-parse konfigurasi
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .Build();

    // Validasi bahwa konfigurasi yang diperlukan tersedia
    var endpoint = configuration["AzureOpenAI:Endpoint"];
    var deploymentName = configuration["AzureOpenAI:DeploymentName"];

    if (string.IsNullOrWhiteSpace(endpoint))
    {
        throw new InvalidOperationException(
            "AzureOpenAI:Endpoint belum dikonfigurasi di appsettings.json.");
    }

    if (string.IsNullOrWhiteSpace(deploymentName))
    {
        throw new InvalidOperationException(
            "AzureOpenAI:DeploymentName belum dikonfigurasi di appsettings.json.");
    }

    return configuration;
}

// =============================================================================
// Fungsi utama aplikasi - mendemonstrasikan middleware pipeline pada agent
// =============================================================================
static async Task RunApplicationAsync(IConfiguration configuration, CancellationToken cancellationToken)
{
    var endpoint = configuration["AzureOpenAI:Endpoint"]!;
    var deploymentName = configuration["AzureOpenAI:DeploymentName"]!;

    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║     Adding Middleware - Microsoft Agent Framework            ║");
    Console.WriteLine("║     Demonstrasi middleware pipeline: logging & guardrails    ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();

    // --- Membuat koneksi ke Azure OpenAI ---
    Console.WriteLine("[INFO] Membuat koneksi ke Azure OpenAI...");
    Console.WriteLine($"[INFO] Endpoint: {endpoint}");
    Console.WriteLine($"[INFO] Model Deployment: {deploymentName}");
    Console.WriteLine();

    // Inisialisasi Azure OpenAI client dengan DefaultAzureCredential
    var azureClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new DefaultAzureCredential());

    // Mendapatkan IChatClient - abstraksi universal untuk model calls
    IChatClient chatClient = azureClient.GetChatClient(deploymentName).AsIChatClient();

    // Membuat agent yang akan dibungkus oleh middleware pipeline
    var agent = chatClient.AsAIAgent(
        instructions: "Kamu adalah asisten AI yang membantu menjawab pertanyaan user. " +
                      "Jawab dengan singkat, jelas, dan dalam bahasa Indonesia.",
        name: "MiddlewareAgent",
        description: "Agent dengan middleware pipeline untuk logging dan guardrails");

    Console.WriteLine("[INFO] Koneksi berhasil. Agent 'MiddlewareAgent' siap.");
    Console.WriteLine();

    // --- Menyiapkan middleware pipeline ---
    // Middleware dieksekusi sesuai urutan pendaftaran:
    // 1. LoggingMiddleware (mencatat request & response)
    // 2. GuardrailMiddleware (memvalidasi input)
    // Urutan penting: logging mencatat SEMUA request termasuk yang diblokir
    var loggingMiddleware = new LoggingMiddleware();
    var guardrailMiddleware = new GuardrailMiddleware();

    var pipeline = new MiddlewarePipeline();
    pipeline.Use(loggingMiddleware);   // Urutan 1: Log semua request masuk
    pipeline.Use(guardrailMiddleware); // Urutan 2: Validasi input sebelum ke agent

    Console.WriteLine("[INFO] Middleware pipeline dikonfigurasi:");
    Console.WriteLine("       Pipeline: Request → [1. Logging] → [2. Guardrail] → Agent → [2. Guardrail] → [1. Logging] → Response");
    Console.WriteLine();

    // === BAGIAN 1: Demo request normal yang melewati semua middleware ===
    await DemonstrasikanNormalRequest(pipeline, agent, cancellationToken);

    // === BAGIAN 2: Demo request yang diblokir oleh guardrail (short-circuit) ===
    await DemonstrasikanBlockedRequest(pipeline, agent, cancellationToken);

    // === BAGIAN 3: Interactive loop dengan runtime toggle ===
    await RunInteractiveLoop(pipeline, agent, loggingMiddleware, guardrailMiddleware, cancellationToken);
}

// =============================================================================
// Bagian 1: Demonstrasi request normal melewati seluruh pipeline
// Menunjukkan urutan eksekusi middleware: logging → guardrail → agent
// =============================================================================
static async Task DemonstrasikanNormalRequest(
    MiddlewarePipeline pipeline, AIAgent agent, CancellationToken cancellationToken)
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ DEMO 1: Request Normal - Melewati Seluruh Pipeline           │");
    Console.WriteLine("│ Urutan: Logging → Guardrail(✅) → Agent → Logging            │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Input pendek yang valid (di bawah 500 karakter)
    var normalInput = "Apa itu middleware pattern dalam software engineering?";
    Console.WriteLine($"  Input: \"{normalInput}\" ({normalInput.Length} karakter)");
    Console.WriteLine();
    Console.WriteLine("  --- Eksekusi Pipeline ---");

    // Menjalankan pipeline lengkap
    var context = new MiddlewareContext { Input = normalInput };
    await pipeline.ExecuteAsync(context, async (ctx) =>
    {
        // Handler final: memanggil agent untuk mendapatkan response
        Console.WriteLine("  [AGENT] 🤖 Memproses request...");
        var response = await agent.RunAsync(ctx.Input, cancellationToken: cancellationToken);
        ctx.Output = response?.ToString() ?? "(response kosong)";
    });

    Console.WriteLine();
    Console.WriteLine($"  Response: {TruncateDisplay(context.Output, 300)}");
    Console.WriteLine();
}

// =============================================================================
// Bagian 2: Demonstrasi request yang diblokir oleh guardrail middleware
// Short-circuit pattern: agent TIDAK menerima request
// =============================================================================
static async Task DemonstrasikanBlockedRequest(
    MiddlewarePipeline pipeline, AIAgent agent, CancellationToken cancellationToken)
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ DEMO 2: Request Diblokir - Short-Circuit oleh Guardrail      │");
    Console.WriteLine("│ Urutan: Logging → Guardrail(⛔) → STOP (agent tidak dipanggil)│");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Input sangat panjang yang melebihi 500 karakter (guardrail akan memblokir)
    var longInput = string.Join(" ", Enumerable.Repeat(
        "Ini adalah contoh input yang sangat panjang untuk mendemonstrasikan guardrail middleware", 8));
    Console.WriteLine($"  Input: \"{longInput[..80]}...\" ({longInput.Length} karakter)");
    Console.WriteLine();
    Console.WriteLine("  --- Eksekusi Pipeline ---");

    // Menjalankan pipeline - guardrail akan melakukan short-circuit
    var context = new MiddlewareContext { Input = longInput };
    await pipeline.ExecuteAsync(context, async (ctx) =>
    {
        // Handler ini TIDAK AKAN dipanggil karena guardrail mem-block request
        Console.WriteLine("  [AGENT] 🤖 Memproses request...");
        var response = await agent.RunAsync(ctx.Input, cancellationToken: cancellationToken);
        ctx.Output = response?.ToString() ?? "(response kosong)";
    });

    Console.WriteLine();
    Console.WriteLine($"  Response: {context.Output}");
    Console.WriteLine();
}

// =============================================================================
// Bagian 3: Interactive loop dengan perintah toggle middleware
// User dapat mengaktifkan/menonaktifkan middleware saat runtime
// =============================================================================
static async Task RunInteractiveLoop(
    MiddlewarePipeline pipeline,
    AIAgent agent,
    LoggingMiddleware loggingMiddleware,
    GuardrailMiddleware guardrailMiddleware,
    CancellationToken cancellationToken)
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ DEMO 3: Interactive Loop dengan Runtime Toggle               │");
    Console.WriteLine("│ Perintah khusus:                                             │");
    Console.WriteLine("│   /toggle logging   - Aktifkan/nonaktifkan logging middleware│");
    Console.WriteLine("│   /toggle guardrail - Aktifkan/nonaktifkan guardrail         │");
    Console.WriteLine("│   /status           - Tampilkan status semua middleware      │");
    Console.WriteLine("│   exit / quit       - Keluar dari aplikasi                   │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Menampilkan status awal middleware
    TampilkanStatusMiddleware(loggingMiddleware, guardrailMiddleware);

    // Interactive loop: menerima input dan memproses melalui pipeline
    while (!cancellationToken.IsCancellationRequested)
    {
        Console.Write("> ");
        var input = Console.ReadLine();

        // Menangani input null (stream tertutup)
        if (input is null)
            break;

        // Menghapus whitespace di awal/akhir input
        input = input.Trim();

        // Mengabaikan input kosong
        if (string.IsNullOrEmpty(input))
            continue;

        // Perintah keluar: exit atau quit (case-insensitive)
        if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[INFO] Sesi berakhir. Terima kasih!");
            break;
        }

        // Perintah toggle: mengubah status middleware saat runtime
        if (input.StartsWith("/toggle ", StringComparison.OrdinalIgnoreCase))
        {
            var middlewareName = input[8..].Trim().ToLowerInvariant();
            HandleToggleCommand(middlewareName, loggingMiddleware, guardrailMiddleware);
            continue;
        }

        // Perintah status: menampilkan status semua middleware
        if (input.Equals("/status", StringComparison.OrdinalIgnoreCase))
        {
            TampilkanStatusMiddleware(loggingMiddleware, guardrailMiddleware);
            continue;
        }

        // Memproses input melalui middleware pipeline
        Console.WriteLine();
        Console.WriteLine("  --- Eksekusi Pipeline ---");

        try
        {
            var context = new MiddlewareContext { Input = input };
            await pipeline.ExecuteAsync(context, async (ctx) =>
            {
                // Handler final: memanggil agent
                Console.WriteLine("  [AGENT] 🤖 Memproses request...");
                var response = await agent.RunAsync(ctx.Input, cancellationToken: cancellationToken);
                ctx.Output = response?.ToString() ?? "(response kosong)";
            });

            Console.WriteLine();
            Console.WriteLine($"  Response: {TruncateDisplay(context.Output, 500)}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Menangani error tanpa menghentikan loop
            Console.WriteLine($"  [ERROR] Gagal memproses request: {ex.Message}");
            Console.WriteLine("  [HINT] Coba lagi atau periksa koneksi.");
        }

        Console.WriteLine();
    }
}

// =============================================================================
// Handler perintah /toggle - mengaktifkan/menonaktifkan middleware saat runtime
// =============================================================================
static void HandleToggleCommand(
    string middlewareName,
    LoggingMiddleware loggingMiddleware,
    GuardrailMiddleware guardrailMiddleware)
{
    switch (middlewareName)
    {
        case "logging":
            // Toggle status logging middleware (aktif ↔ nonaktif)
            loggingMiddleware.IsEnabled = !loggingMiddleware.IsEnabled;
            var loggingStatus = loggingMiddleware.IsEnabled ? "AKTIF ✅" : "NONAKTIF ❌";
            Console.WriteLine($"[TOGGLE] LoggingMiddleware sekarang: {loggingStatus}");
            break;

        case "guardrail":
            // Toggle status guardrail middleware (aktif ↔ nonaktif)
            guardrailMiddleware.IsEnabled = !guardrailMiddleware.IsEnabled;
            var guardrailStatus = guardrailMiddleware.IsEnabled ? "AKTIF ✅" : "NONAKTIF ❌";
            Console.WriteLine($"[TOGGLE] GuardrailMiddleware sekarang: {guardrailStatus}");
            break;

        default:
            // Nama middleware tidak dikenali
            Console.WriteLine($"[ERROR] Middleware '{middlewareName}' tidak ditemukan.");
            Console.WriteLine("[HINT] Gunakan: /toggle logging atau /toggle guardrail");
            break;
    }
    Console.WriteLine();
}

// =============================================================================
// Menampilkan status aktif/nonaktif semua middleware yang terdaftar
// =============================================================================
static void TampilkanStatusMiddleware(
    LoggingMiddleware loggingMiddleware,
    GuardrailMiddleware guardrailMiddleware)
{
    Console.WriteLine("  ┌─── Status Middleware ───────────────────────┐");
    Console.WriteLine($"  │ 1. LoggingMiddleware:   {(loggingMiddleware.IsEnabled ? "AKTIF ✅" : "NONAKTIF ❌"),-12} │");
    Console.WriteLine($"  │ 2. GuardrailMiddleware: {(guardrailMiddleware.IsEnabled ? "AKTIF ✅" : "NONAKTIF ❌"),-12} │");
    Console.WriteLine("  └─────────────────────────────────────────────┘");
    Console.WriteLine();
}

// =============================================================================
// Fungsi helper: memotong teks yang terlalu panjang untuk tampilan console
// =============================================================================
static string TruncateDisplay(string text, int maxLength)
{
    if (string.IsNullOrEmpty(text)) return "(kosong)";
    // Mengganti newline agar tampilan tetap rapi di satu baris
    var cleaned = text.Replace("\n", " ").Replace("\r", "");
    if (cleaned.Length <= maxLength) return cleaned;
    return cleaned[..maxLength] + "...";
}
