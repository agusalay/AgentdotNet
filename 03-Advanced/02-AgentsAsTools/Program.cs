// =============================================================================
// Agents as Tools - Modul Pembelajaran Ketujuh (Advanced Level)
// Demonstrasi penggunaan agent sebagai tool untuk agent lain (composition pattern)
// Parent agent mendelegasikan tugas ke child agents berdasarkan konteks
// =============================================================================

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.AI;
using AgentsAsTools.Agents;

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
// Fungsi utama aplikasi - mendemonstrasikan agent-as-tool composition pattern
// =============================================================================
static async Task RunApplicationAsync(IConfiguration configuration, CancellationToken cancellationToken)
{
    var endpoint = configuration["AzureOpenAI:Endpoint"]!;
    var deploymentName = configuration["AzureOpenAI:DeploymentName"]!;

    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║     Agents as Tools - Microsoft Agent Framework              ║");
    Console.WriteLine("║     Demonstrasi agent composition dan delegation pattern     ║");
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

    // === BAGIAN 1: Membuat child agents (ResearchAgent dan WritingAgent) ===
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 1: Inisialisasi Child Agents                          │");
    Console.WriteLine("│ Setiap child agent memiliki spesialisasi unik yang akan      │");
    Console.WriteLine("│ dimanfaatkan oleh parent agent melalui delegation.           │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Membuat ResearchAgent - spesialis riset dan pencarian informasi
    Console.WriteLine("[INFO] Membuat child agents...");
    var researchAgent = ResearchAgentFactory.Create(chatClient);
    var writingAgent = WritingAgentFactory.Create(chatClient);
    Console.WriteLine();
    Console.WriteLine($"[INFO] Total child agents: 2 ({ResearchAgentFactory.AgentName}, {WritingAgentFactory.AgentName})");
    Console.WriteLine();

    // === BAGIAN 2: Mendaftarkan child agents sebagai tools untuk parent agent ===
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 2: Registrasi Child Agents sebagai Tools              │");
    Console.WriteLine("│ Child agent dibungkus menggunakan AIFunctionFactory.Create()  │");
    Console.WriteLine("│ sehingga parent agent dapat memanggilnya seperti tool biasa.  │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Mendaftarkan ResearchAgent sebagai tool melalui AIFunctionFactory.Create()
    // Parent agent akan memanggil tool ini ketika membutuhkan riset
    var researchTool = AIFunctionFactory.Create(
        async (string query) =>
        {
            // Mencatat alur komunikasi: parent → child → input
            Console.WriteLine($"  [DELEGASI] OrchestratorAgent → {ResearchAgentFactory.AgentName}");
            Console.WriteLine($"             Input: \"{Truncate(query, 100)}\"");

            // Menjalankan child agent dan mendapatkan hasil
            var result = await ResearchAgentFactory.RunAsync(researchAgent, query, cancellationToken);

            // Mencatat output yang dikembalikan dari child agent
            Console.WriteLine($"  [HASIL]    {ResearchAgentFactory.AgentName} → OrchestratorAgent");
            Console.WriteLine($"             Output: \"{Truncate(result, 150)}\"");

            return result;
        },
        "research",
        "Melakukan riset dan pencarian informasi mendalam tentang suatu topik. " +
        "Gunakan tool ini ketika task memerlukan pengumpulan data, analisis informasi, " +
        "atau pencarian fakta tentang suatu subjek.");

    // Mendaftarkan WritingAgent sebagai tool melalui AIFunctionFactory.Create()
    // Parent agent akan memanggil tool ini ketika membutuhkan penulisan konten
    var writingTool = AIFunctionFactory.Create(
        async (string content) =>
        {
            // Mencatat alur komunikasi: parent → child → input
            Console.WriteLine($"  [DELEGASI] OrchestratorAgent → {WritingAgentFactory.AgentName}");
            Console.WriteLine($"             Input: \"{Truncate(content, 100)}\"");

            // Menjalankan child agent dan mendapatkan hasil
            var result = await WritingAgentFactory.RunAsync(writingAgent, content, cancellationToken);

            // Mencatat output yang dikembalikan dari child agent
            Console.WriteLine($"  [HASIL]    {WritingAgentFactory.AgentName} → OrchestratorAgent");
            Console.WriteLine($"             Output: \"{Truncate(result, 150)}\"");

            return result;
        },
        "write",
        "Menulis, menyunting, atau memformat konten berdasarkan instruksi atau data. " +
        "Gunakan tool ini ketika task memerlukan pembuatan artikel, ringkasan, " +
        "email, laporan, atau konten tertulis lainnya.");

    // Daftar tools yang tersedia untuk parent agent
    var childTools = new List<AITool> { researchTool, writingTool };

    Console.WriteLine("[INFO] Child agents berhasil didaftarkan sebagai tools:");
    Console.WriteLine($"       - Tool 'research' ← {ResearchAgentFactory.AgentName}");
    Console.WriteLine($"       - Tool 'write' ← {WritingAgentFactory.AgentName}");
    Console.WriteLine();
    Console.WriteLine("[INFO] Registrasi selesai.");
    Console.WriteLine($"       Parent: OrchestratorAgent");
    Console.WriteLine($"       Child yang terdaftar: {ResearchAgentFactory.AgentName}, {WritingAgentFactory.AgentName}");
    Console.WriteLine();

    // === BAGIAN 3: Membuat parent agent (orchestrator) dengan child tools ===
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 3: Demonstrasi Parent-Child Delegation                │");
    Console.WriteLine("│ Parent agent memilih child agent berdasarkan konteks task.   │");
    Console.WriteLine("│ Alur: User → Parent → Child (tool) → Parent → User          │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Membuat parent agent (orchestrator) yang menggunakan child agents sebagai tools
    var orchestratorAgent = chatClient.AsAIAgent(
        instructions: "Kamu adalah OrchestratorAgent, seorang koordinator yang mendelegasikan tugas ke agent spesialis. " +
                      "Kamu memiliki dua tools: " +
                      "1) 'research' - untuk riset dan pencarian informasi (delegasi ke ResearchAgent). " +
                      "2) 'write' - untuk penulisan dan pembuatan konten (delegasi ke WritingAgent). " +
                      "Analisis setiap task dari user, tentukan child agent mana yang paling cocok, " +
                      "lalu delegasikan tugas tersebut. Sampaikan hasil dari child agent ke user " +
                      "dengan format yang rapi. Jawab dalam bahasa Indonesia.",
        name: "OrchestratorAgent",
        description: "Parent agent yang mengorkestrasikan child agents",
        tools: childTools);

    Console.WriteLine("[INFO] OrchestratorAgent dibuat dengan 2 child tools terdaftar.");
    Console.WriteLine();

    // --- Demo 1: Task riset → parent mendelegasikan ke ResearchAgent ---
    Console.WriteLine("  ─── Demo 1: Task Riset (delegasi ke ResearchAgent) ───────────");
    Console.WriteLine();

    var researchTask = "Lakukan riset tentang manfaat energi terbarukan untuk Indonesia. " +
                       "Berikan minimal 3 poin utama.";
    Console.WriteLine($"  User: \"{researchTask}\"");
    Console.WriteLine();
    Console.WriteLine("  [INFO] Parent agent menganalisis task...");
    Console.WriteLine("  [INFO] Konteks: task memerlukan riset → memilih ResearchAgent");
    Console.WriteLine();

    await InvokeOrchestratorAsync(orchestratorAgent, researchTask, cancellationToken);
    Console.WriteLine();

    // --- Demo 2: Task penulisan → parent mendelegasikan ke WritingAgent ---
    Console.WriteLine("  ─── Demo 2: Task Penulisan (delegasi ke WritingAgent) ────────");
    Console.WriteLine();

    var writingTask = "Tulis paragraf pembuka artikel tentang transformasi digital " +
                      "di sektor pendidikan Indonesia dengan gaya formal.";
    Console.WriteLine($"  User: \"{writingTask}\"");
    Console.WriteLine();
    Console.WriteLine("  [INFO] Parent agent menganalisis task...");
    Console.WriteLine("  [INFO] Konteks: task memerlukan penulisan → memilih WritingAgent");
    Console.WriteLine();

    await InvokeOrchestratorAsync(orchestratorAgent, writingTask, cancellationToken);
    Console.WriteLine();

    // === BAGIAN 4: Error handling dengan fallback ke agent alternatif ===
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 4: Error Handling dan Fallback Strategy               │");
    Console.WriteLine("│ Jika child agent gagal, parent mencoba agent alternatif.     │");
    Console.WriteLine("│ Demonstrasi ketahanan sistem melalui fallback mechanism.     │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Demo 3: Simulasi error pada child agent dan fallback ke agent lain
    await DemonstrasikanErrorHandlingDenganFallback(chatClient, cancellationToken);
    Console.WriteLine();

    // --- Ringkasan pembelajaran ---
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("[INFO] Demonstrasi Agents as Tools selesai.");
    Console.WriteLine("[INFO] Konsep yang dipelajari:");
    Console.WriteLine("       1. Membuat child agents dengan spesialisasi unik");
    Console.WriteLine("       2. Mendaftarkan agent sebagai tool via AIFunctionFactory.Create()");
    Console.WriteLine("       3. Parent agent memilih child berdasarkan konteks task");
    Console.WriteLine("       4. Logging alur komunikasi parent ↔ child");
    Console.WriteLine("       5. Error handling dengan fallback ke agent alternatif");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
}

// =============================================================================
// Demonstrasi error handling: simulasi kegagalan child agent dan fallback
// Parent agent mendeteksi kegagalan dan mendelegasikan ke agent alternatif
// =============================================================================
static async Task DemonstrasikanErrorHandlingDenganFallback(
    IChatClient chatClient,
    CancellationToken cancellationToken)
{
    Console.WriteLine("  ─── Demo 3: Error Handling dengan Fallback ────────────────────");
    Console.WriteLine();

    // Membuat child agents: satu yang akan "gagal" (simulasi) dan satu sebagai fallback
    var writingAgent = WritingAgentFactory.Create(chatClient);
    var researchAgent = ResearchAgentFactory.Create(chatClient);
    Console.WriteLine();

    // Variabel untuk melacak apakah simulasi error sudah terjadi
    bool simulateFailure = true;

    // Tool "write" yang mensimulasikan kegagalan pada panggilan pertama
    var failingWriteTool = AIFunctionFactory.Create(
        async (string content) =>
        {
            Console.WriteLine($"  [DELEGASI] OrchestratorAgent → {WritingAgentFactory.AgentName}");
            Console.WriteLine($"             Input: \"{Truncate(content, 100)}\"");

            if (simulateFailure)
            {
                // Simulasi kegagalan child agent (misalnya timeout atau error internal)
                simulateFailure = false; // Hanya gagal sekali
                Console.WriteLine($"  [ERROR]    {WritingAgentFactory.AgentName} mengalami kegagalan!");
                Console.WriteLine("             Penyebab: Simulasi timeout pada proses penulisan.");
                throw new InvalidOperationException(
                    $"{WritingAgentFactory.AgentName} gagal memproses: simulasi timeout");
            }

            // Jika tidak gagal, jalankan normal
            var result = await WritingAgentFactory.RunAsync(writingAgent, content, cancellationToken);
            Console.WriteLine($"  [HASIL]    {WritingAgentFactory.AgentName} → OrchestratorAgent");
            Console.WriteLine($"             Output: \"{Truncate(result, 150)}\"");
            return result;
        },
        "write",
        "Menulis konten berdasarkan instruksi. Gunakan untuk pembuatan artikel atau konten tertulis.");

    // Tool "research" sebagai fallback - selalu berhasil
    var fallbackResearchTool = AIFunctionFactory.Create(
        async (string query) =>
        {
            Console.WriteLine($"  [FALLBACK] OrchestratorAgent → {ResearchAgentFactory.AgentName} (alternatif)");
            Console.WriteLine($"             Input: \"{Truncate(query, 100)}\"");

            var result = await ResearchAgentFactory.RunAsync(researchAgent, query, cancellationToken);

            Console.WriteLine($"  [HASIL]    {ResearchAgentFactory.AgentName} → OrchestratorAgent");
            Console.WriteLine($"             Output: \"{Truncate(result, 150)}\"");
            return result;
        },
        "research",
        "Melakukan riset informasi. Dapat digunakan sebagai alternatif jika penulisan gagal.");

    // Membuat orchestrator dengan kedua tools
    var orchestratorWithFallback = chatClient.AsAIAgent(
        instructions: "Kamu adalah OrchestratorAgent. Kamu memiliki tools 'write' dan 'research'. " +
                      "Jika tool 'write' gagal (melempar error), segera gunakan tool 'research' sebagai " +
                      "alternatif untuk mengumpulkan informasi terkait, lalu sampaikan hasilnya ke user. " +
                      "Selalu informasikan user jika terjadi fallback ke agent alternatif. " +
                      "Jawab dalam bahasa Indonesia.",
        name: "OrchestratorAgent",
        description: "Parent agent dengan fallback strategy",
        tools: [failingWriteTool, fallbackResearchTool]);

    // Task yang akan memicu WritingAgent (yang akan gagal) kemudian fallback ke ResearchAgent
    var fallbackTask = "Tulis ringkasan singkat tentang perkembangan AI di Indonesia tahun 2024.";
    Console.WriteLine($"  User: \"{fallbackTask}\"");
    Console.WriteLine();
    Console.WriteLine("  [INFO] Task dikirim ke WritingAgent (akan disimulasikan gagal)...");
    Console.WriteLine("  [INFO] Jika gagal, parent akan fallback ke ResearchAgent...");
    Console.WriteLine();

    try
    {
        // Menjalankan orchestrator - WritingAgent akan gagal, lalu fallback ke ResearchAgent
        var response = await orchestratorWithFallback.RunAsync(fallbackTask, cancellationToken: cancellationToken);
        var responseText = response?.ToString();

        if (!string.IsNullOrWhiteSpace(responseText))
        {
            Console.WriteLine();
            Console.WriteLine($"  [FINAL] Hasil setelah fallback:");
            Console.WriteLine($"  Agent: {FormatResponse(responseText)}");
        }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        // Jika seluruh mekanisme gagal, tampilkan error informatif
        Console.WriteLine($"  [ERROR] Seluruh strategi gagal: {ex.GetType().Name}");
        Console.WriteLine($"  [CAUSE] {ex.Message}");
        Console.WriteLine("  [INFO] Dalam produksi, implementasikan retry atau circuit breaker pattern.");
    }
}

// =============================================================================
// Helper: Menjalankan orchestrator agent dan menampilkan hasilnya
// =============================================================================
static async Task InvokeOrchestratorAsync(AIAgent agent, string prompt, CancellationToken cancellationToken)
{
    try
    {
        // Menjalankan parent agent - delegation ke child terjadi otomatis via tool calls
        var response = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
        var responseText = response?.ToString();

        if (!string.IsNullOrWhiteSpace(responseText))
        {
            Console.WriteLine();
            Console.WriteLine($"  Agent: {FormatResponse(responseText)}");
        }
        else
        {
            Console.WriteLine("  [INFO] Agent mengembalikan response kosong.");
        }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        // Menangani kegagalan pada level orchestrator
        Console.WriteLine($"  [ERROR] Orchestrator gagal: {ex.GetType().Name}");
        Console.WriteLine($"  [CAUSE] {ex.Message}");
        Console.WriteLine("  [HINT] Periksa koneksi dan konfigurasi child agents.");
    }
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
