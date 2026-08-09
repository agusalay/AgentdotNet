// =============================================================================
// Agent-to-Agent Communication - Modul Pembelajaran Kedelapan (Expert Level)
// Demonstrasi komunikasi inter-agent menggunakan pola A2A protocol
// Setiap agent beroperasi sebagai unit independen dengan identity unik
// dan berkomunikasi melalui message broker dengan retry mechanism
// =============================================================================

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.AI;
using AgentToAgentCommunication.Agents;

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

    // Validasi konfigurasi yang diperlukan tersedia
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
// Fungsi utama aplikasi - mendemonstrasikan komunikasi A2A antar agent
// =============================================================================
static async Task RunApplicationAsync(IConfiguration configuration, CancellationToken cancellationToken)
{
    var endpoint = configuration["AzureOpenAI:Endpoint"]!;
    var deploymentName = configuration["AzureOpenAI:DeploymentName"]!;

    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║  Agent-to-Agent Communication - Microsoft Agent Framework    ║");
    Console.WriteLine("║  Demonstrasi komunikasi inter-agent dengan A2A protocol      ║");
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

    Console.WriteLine("[INFO] Koneksi berhasil dibuat.");
    Console.WriteLine();

    // === BAGIAN 1: Setup infrastruktur A2A dan inisialisasi agent ===
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 1: Setup Infrastruktur A2A                            │");
    Console.WriteLine("│ Membuat message broker dan mendaftarkan agent dengan         │");
    Console.WriteLine("│ identity unik. Setiap agent memiliki inbox tersendiri.       │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Membuat message broker sebagai komponen routing pusat
    Console.WriteLine("[INFO] Membuat MessageBroker untuk routing pesan antar agent...");
    var broker = new MessageBroker();
    Console.WriteLine("[INFO] MessageBroker berhasil dibuat.");
    Console.WriteLine();

    // Membuat agent dengan identity unik yang terdaftar di broker
    Console.WriteLine("[INFO] Membuat dan mendaftarkan agent ke broker...");
    var analysisAgent = new AnalysisAgent(chatClient, broker);
    var summaryAgent = new SummaryAgent(chatClient, broker);
    Console.WriteLine();

    // Mendaftarkan message handler untuk setiap agent
    // Handler ini dipanggil oleh broker ketika pesan diterima
    broker.RegisterMessageHandler(AnalysisAgent.AgentId,
        (msg, ct) => analysisAgent.ProcessMessageAsync(msg, ct));
    broker.RegisterMessageHandler(SummaryAgent.AgentId,
        (msg, ct) => summaryAgent.ProcessMessageAsync(msg, ct));

    Console.WriteLine($"[INFO] Total agent terdaftar: 2");
    Console.WriteLine($"       - {AnalysisAgent.AgentId} (handler aktif)");
    Console.WriteLine($"       - {SummaryAgent.AgentId} (handler aktif)");
    Console.WriteLine();

    // === BAGIAN 2: Demo 1 - Round-trip message passing ===
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 2: Demo 1 - Round-Trip Message Passing                │");
    Console.WriteLine("│ AnalysisAgent mengirim request ke SummaryAgent dan menerima  │");
    Console.WriteLine("│ response. Demonstrasi pola request-response dasar A2A.       │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    Console.WriteLine("[INFO] Skenario: AnalysisAgent mengirim permintaan ringkasan ke SummaryAgent.");
    Console.WriteLine();

    // AnalysisAgent mengirim pesan ke SummaryAgent dan menunggu respons
    var roundTripContent = "Tolong rangkum konsep berikut: Agent-to-Agent (A2A) protocol " +
        "memungkinkan agent berkomunikasi secara independen tanpa tight coupling. " +
        "Setiap agent memiliki identity unik dan antrian pesan tersendiri.";

    Console.WriteLine($"  [KIRIM] {AnalysisAgent.AgentId} → {SummaryAgent.AgentId}");
    Console.WriteLine($"          Konten: \"{Truncate(roundTripContent, 100)}\"");
    Console.WriteLine();

    var roundTripResponse = await analysisAgent.SendMessageAsync(
        SummaryAgent.AgentId, roundTripContent, cancellationToken);

    Console.WriteLine();
    Console.WriteLine($"  [TERIMA] Respons diterima oleh {AnalysisAgent.AgentId}:");
    Console.WriteLine($"           Dari: {roundTripResponse.SenderId}");
    Console.WriteLine($"           Waktu: {roundTripResponse.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
    Console.WriteLine($"           Konten: \"{Truncate(roundTripResponse.Content, 200)}\"");
    Console.WriteLine();
    Console.WriteLine("[INFO] Demo 1 selesai - round-trip message passing berhasil.");
    Console.WriteLine();

    // === BAGIAN 3: Demo 2 - Kolaborasi antar agent ===
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 3: Demo 2 - Kolaborasi Antar Agent                    │");
    Console.WriteLine("│ AnalysisAgent melakukan analisis, mengirim hasil ke           │");
    Console.WriteLine("│ SummaryAgent untuk dirangkum, menghasilkan combined output.   │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    Console.WriteLine("[INFO] Skenario: Kolaborasi multi-step antara dua agent.");
    Console.WriteLine("       Step 1: AnalysisAgent melakukan analisis tentang cloud computing.");
    Console.WriteLine("       Step 2: Hasil analisis dikirim ke SummaryAgent untuk dirangkum.");
    Console.WriteLine("       Step 3: Combined result ditampilkan sebagai output akhir.");
    Console.WriteLine();

    // Step 1: AnalysisAgent melakukan analisis secara mandiri
    Console.WriteLine("  ─── Step 1: AnalysisAgent melakukan analisis ──────────────────");
    Console.WriteLine();

    var analysisInput = new A2AMessage(
        SenderId: "Orchestrator",
        ReceiverId: AnalysisAgent.AgentId,
        Timestamp: DateTime.UtcNow,
        Content: "Analisis keuntungan dan tantangan adopsi cloud computing untuk " +
                 "perusahaan menengah di Indonesia. Berikan minimal 3 poin untuk masing-masing.",
        Type: MessageType.Request);

    var analysisResult = await analysisAgent.ProcessMessageAsync(analysisInput, cancellationToken);
    Console.WriteLine($"  [HASIL ANALISIS] {AnalysisAgent.AgentId} selesai memproses.");
    Console.WriteLine($"  Konten: \"{Truncate(analysisResult, 300)}\"");
    Console.WriteLine();

    // Step 2: Kirim hasil analisis ke SummaryAgent untuk dirangkum
    Console.WriteLine("  ─── Step 2: Mengirim hasil ke SummaryAgent untuk dirangkum ────");
    Console.WriteLine();

    var collaborationContent = $"Rangkum hasil analisis berikut menjadi 3-5 kalimat padat: {analysisResult}";
    var summaryResponse = await analysisAgent.SendMessageAsync(
        SummaryAgent.AgentId, collaborationContent, cancellationToken);

    Console.WriteLine();

    // Step 3: Menampilkan combined result
    Console.WriteLine("  ─── Step 3: Combined Result ──────────────────────────────────");
    Console.WriteLine();
    Console.WriteLine("  ╔═══════════════════════════════════════════════════════════╗");
    Console.WriteLine("  ║ COMBINED OUTPUT - Kolaborasi AnalysisAgent + SummaryAgent ║");
    Console.WriteLine("  ╠═══════════════════════════════════════════════════════════╣");
    Console.WriteLine($"  ║ Analisis oleh: {AnalysisAgent.AgentId,-42}║");
    Console.WriteLine($"  ║ Ringkasan oleh: {SummaryAgent.AgentId,-41}║");
    Console.WriteLine("  ╚═══════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine($"  Ringkasan Akhir:");
    Console.WriteLine($"  {FormatResponse(Truncate(summaryResponse.Content, 500))}");
    Console.WriteLine();
    Console.WriteLine("[INFO] Demo 2 selesai - kolaborasi antar agent berhasil.");
    Console.WriteLine("[INFO] Task selesai: analisis → pengiriman A2A → perangkuman → combined output.");
    Console.WriteLine();

    // === BAGIAN 4: Demo 3 - Retry mechanism dengan exponential backoff ===
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 4: Demo 3 - Retry Mechanism (Exponential Backoff)      │");
    Console.WriteLine("│ Simulasi kegagalan komunikasi untuk mendemonstrasikan         │");
    Console.WriteLine("│ retry dengan delay: 1s, 2s, 4s (maks 3 percobaan).           │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    Console.WriteLine("[INFO] Skenario: Simulasi timeout pada komunikasi A2A.");
    Console.WriteLine("[INFO] Retry policy: exponential backoff (1s, 2s, 4s), maks 3 percobaan.");
    Console.WriteLine();

    // Mengaktifkan simulasi kegagalan pada broker
    // 2 kegagalan = akan berhasil pada percobaan ke-3
    Console.WriteLine("  ─── Skenario A: Retry berhasil (gagal 2x, berhasil pada percobaan ke-3) ───");
    Console.WriteLine();

    broker.EnableFailureSimulation(failureCount: 2);

    var retryContent = "Pesan test retry: tolong rangkum konsep exponential backoff.";
    Console.WriteLine($"  [KIRIM] {AnalysisAgent.AgentId} → {SummaryAgent.AgentId}");
    Console.WriteLine($"          Konten: \"{retryContent}\"");
    Console.WriteLine();

    try
    {
        var retryResponse = await analysisAgent.SendMessageAsync(
            SummaryAgent.AgentId, retryContent, cancellationToken);

        Console.WriteLine();
        Console.WriteLine($"  [BERHASIL] Pesan terkirim setelah retry!");
        Console.WriteLine($"             Respons: \"{Truncate(retryResponse.Content, 200)}\"");
    }
    catch (AgentCommunicationException ex)
    {
        // Seharusnya tidak terjadi karena hanya 2 kegagalan dari 3 percobaan
        Console.WriteLine($"  [ERROR] Tidak terduga: {ex.Message}");
    }

    Console.WriteLine();

    // Skenario B: Semua retry habis (3 kegagalan = semua percobaan gagal)
    Console.WriteLine("  ─── Skenario B: Semua retry habis (gagal 3x) ─────────────────");
    Console.WriteLine();

    broker.EnableFailureSimulation(failureCount: 3);

    var failContent = "Pesan test: semua retry akan gagal.";
    Console.WriteLine($"  [KIRIM] {SummaryAgent.AgentId} → {AnalysisAgent.AgentId}");
    Console.WriteLine($"          Konten: \"{failContent}\"");
    Console.WriteLine();

    try
    {
        await summaryAgent.SendMessageAsync(
            AnalysisAgent.AgentId, failContent, cancellationToken);
    }
    catch (AgentCommunicationException ex)
    {
        // Menampilkan error setelah semua retry habis
        Console.WriteLine();
        Console.WriteLine($"  [GAGAL]  Komunikasi gagal setelah {ex.AttemptCount} percobaan.");
        Console.WriteLine($"           Alasan: {ex.FailureReason}");
        Console.WriteLine($"           Exception: {ex.Message}");
    }

    // Menonaktifkan simulasi kegagalan
    broker.DisableFailureSimulation();
    Console.WriteLine();

    // === Ringkasan ===
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("[INFO] Demonstrasi Agent-to-Agent Communication selesai.");
    Console.WriteLine("[INFO] Konsep yang dipelajari:");
    Console.WriteLine("       1. Dua agent dengan identity unik berkomunikasi via A2A protocol");
    Console.WriteLine("       2. Round-trip message passing (request-response pattern)");
    Console.WriteLine("       3. Kolaborasi: sub-task delegation dan result combination");
    Console.WriteLine("       4. Message logging (sender, receiver, timestamp, content ≤500 chars)");
    Console.WriteLine("       5. Retry mechanism dengan exponential backoff (1s, 2s, 4s)");
    Console.WriteLine("       6. Error handling saat semua retry habis");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");

    // Menampilkan ringkasan log pesan
    var messageLog = broker.GetMessageLog();
    Console.WriteLine();
    Console.WriteLine($"[INFO] Total pesan yang tercatat: {messageLog.Count}");
}

// =============================================================================
// Fungsi helper: memformat response agar tampil rapi di console
// =============================================================================
static string FormatResponse(string response)
{
    // Mengganti newline agar tampilan console tetap rapi dengan indentasi
    return response.Replace("\n", "\n  ").Trim();
}

// =============================================================================
// Fungsi helper: memotong string panjang untuk tampilan log yang ringkas
// =============================================================================
static string Truncate(string text, int maxLength)
{
    // Memotong teks yang terlalu panjang dan menambahkan ellipsis
    if (string.IsNullOrEmpty(text)) return string.Empty;
    return text.Length <= maxLength ? text : text[..maxLength] + "...";
}
