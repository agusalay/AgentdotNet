// =============================================================================
// Adding Skills - Modul Pembelajaran Keempat (Intermediate Level)
// Demonstrasi konsep skills: mengemas tools terkait menjadi unit reusable
// Skills memungkinkan modularitas, reusability, dan organisasi yang lebih baik
// =============================================================================

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.AI;
using AddingSkills.Skills;

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
// Fungsi utama aplikasi - mendemonstrasikan konsep skills pada agent
// =============================================================================
static async Task RunApplicationAsync(IConfiguration configuration, CancellationToken cancellationToken)
{
    var endpoint = configuration["AzureOpenAI:Endpoint"]!;
    var deploymentName = configuration["AzureOpenAI:DeploymentName"]!;

    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║     Adding Skills - Microsoft Agent Framework                ║");
    Console.WriteLine("║     Demonstrasi skill packaging dan reusability               ║");
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

    // === BAGIAN 1: Mendefinisikan dan mendaftarkan skill ===
    await DemonstrasikanSkillRegistration(chatClient, cancellationToken);

    // === BAGIAN 2: Perbandingan flat tools vs grouped skills ===
    DemonstrasikanFlatVsGroupedSkills();

    // === BAGIAN 3: Skill digunakan oleh multiple agents ===
    await DemonstrasikanSkillSharing(chatClient, cancellationToken);

    // === BAGIAN 4: Error handling pada registrasi skill ===
    DemonstrasikanSkillRegistrationErrors();
}

// =============================================================================
// Bagian 1: Mendefinisikan skill dan mendaftarkannya ke agent
// Skill = koleksi tools yang terkait secara fungsional, didaftarkan sebagai unit
// =============================================================================
static async Task DemonstrasikanSkillRegistration(IChatClient chatClient, CancellationToken cancellationToken)
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 1: Skill Registration - Packaging & Registrasi        │");
    Console.WriteLine("│ Skill mengemas 2+ tools terkait menjadi satu unit kohesif.   │");
    Console.WriteLine("│ Agent dapat menggunakan semua tools dalam skill sekaligus.    │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // --- Membuat tools dari ResearchSkill ---
    // AIFunctionFactory.Create() mengkonversi static method menjadi AI tool
    // Setiap method dalam ResearchSkill menjadi tool individu yang dikelompokkan
    Console.WriteLine("[INFO] Membuat tools dari ResearchSkill...");

    var webSearchTool = AIFunctionFactory.Create(ResearchSkill.WebSearch);
    var summarizeTool = AIFunctionFactory.Create(ResearchSkill.Summarize);
    var extractKeywordsTool = AIFunctionFactory.Create(ResearchSkill.ExtractKeywords);

    // Mengelompokkan tools menjadi satu skill unit
    var skillTools = new List<AITool> { webSearchTool, summarizeTool, extractKeywordsTool };

    // --- Menampilkan konfirmasi registrasi skill ---
    // Output mencantumkan nama skill dan jumlah tools yang terdaftar
    Console.WriteLine($"[INFO] Skill '{ResearchSkill.SkillName}' berhasil dibuat:");
    Console.WriteLine($"       Deskripsi: {ResearchSkill.SkillDescription}");
    Console.WriteLine($"       Jumlah tools: {skillTools.Count}");
    Console.WriteLine($"       Tools terdaftar:");
    foreach (var tool in skillTools)
    {
        // Menampilkan nama dan deskripsi setiap tool dalam skill
        Console.WriteLine($"         - {tool.Name}: {tool.Description}");
    }
    Console.WriteLine();

    // --- Mendaftarkan skill ke agent ---
    // Tools dari skill didaftarkan ke agent sebagai satu paket
    Console.WriteLine("[INFO] Mendaftarkan ResearchSkill ke agent...");

    var researchAgent = chatClient.AsAIAgent(
        instructions: "Kamu adalah asisten riset yang membantu mencari dan merangkum informasi. " +
                      "Gunakan tools WebSearch untuk mencari informasi dan Summarize untuk merangkum. " +
                      "Gunakan ExtractKeywords untuk menemukan kata kunci penting. " +
                      "Jawab dalam bahasa Indonesia dengan format yang jelas.",
        name: "ResearchAgent",
        description: "Agent riset dengan kemampuan pencarian dan perangkuman",
        tools: skillTools);

    Console.WriteLine($"[INFO] Agent 'ResearchAgent' berhasil dibuat dengan skill '{ResearchSkill.SkillName}'.");
    Console.WriteLine($"       Konfirmasi: {ResearchSkill.SkillName} → {skillTools.Count} tools terdaftar ✓");
    Console.WriteLine();

    // --- Demonstrasi penggunaan skill oleh agent ---
    Console.WriteLine("  ─── Demonstrasi: Agent menggunakan ResearchSkill ─────────────");
    Console.WriteLine();

    // Demonstrasi 1: WebSearch tool dipanggil
    var prompt1 = "Cari informasi tentang AI agents";
    Console.WriteLine($"  User: \"{prompt1}\"");
    Console.WriteLine();
    Console.WriteLine("  [SKILL ACTIVATION] Skill: ResearchSkill");
    Console.WriteLine("  [EXECUTION ORDER]  Tool ke-1 yang mungkin dipanggil oleh LLM:");
    await InvokeAgentAsync(researchAgent, prompt1, cancellationToken);
    Console.WriteLine();

    // Demonstrasi 2: Summarize tool dipanggil
    var prompt2 = "Rangkum teks berikut: Machine learning adalah cabang AI yang memungkinkan " +
                  "komputer belajar dari data tanpa diprogram secara eksplisit. Teknik utama meliputi " +
                  "supervised learning, unsupervised learning, dan reinforcement learning. Setiap teknik " +
                  "memiliki use case yang berbeda tergantung pada jenis data dan tujuan analisis.";
    Console.WriteLine($"  User: \"{prompt2[..60]}...\"");
    Console.WriteLine();
    Console.WriteLine("  [SKILL ACTIVATION] Skill: ResearchSkill");
    Console.WriteLine("  [EXECUTION ORDER]  Tool ke-2 yang mungkin dipanggil oleh LLM:");
    await InvokeAgentAsync(researchAgent, prompt2, cancellationToken);
    Console.WriteLine();
}

// =============================================================================
// Bagian 2: Perbandingan Flat Tools vs Grouped Skills
// Menunjukkan perbedaan arsitektur antara mendaftarkan tools secara individual
// versus mengelompokkannya dalam skill
// =============================================================================
static void DemonstrasikanFlatVsGroupedSkills()
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 2: Flat Tools vs Grouped Skills - Perbandingan         │");
    Console.WriteLine("│ Memahami kapan menggunakan flat tools vs packaged skills.     │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // --- Pendekatan 1: Flat Tools (individual, tanpa pengelompokan) ---
    Console.WriteLine("  ┌─ PENDEKATAN A: Flat Tools (Individual Registration) ────────┐");
    Console.WriteLine("  │                                                              │");
    Console.WriteLine("  │  Agent                                                       │");
    Console.WriteLine("  │   ├── Tool: WebSearch        (tidak terkelompok)            │");
    Console.WriteLine("  │   ├── Tool: Summarize        (tidak terkelompok)            │");
    Console.WriteLine("  │   ├── Tool: ExtractKeywords  (tidak terkelompok)            │");
    Console.WriteLine("  │   ├── Tool: GetWeather       (tidak terkelompok)            │");
    Console.WriteLine("  │   └── Tool: Calculator       (tidak terkelompok)            │");
    Console.WriteLine("  │                                                              │");
    Console.WriteLine("  │  Karakteristik:                                              │");
    Console.WriteLine("  │  - Semua tools di level yang sama (flat structure)           │");
    Console.WriteLine("  │  - Sulit mengetahui mana tools yang terkait                 │");
    Console.WriteLine("  │  - Registrasi satu per satu                                  │");
    Console.WriteLine("  │  - Tidak ada konteks domain yang jelas                       │");
    Console.WriteLine("  └──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // --- Pendekatan 2: Grouped Skills (terorganisir dalam skill) ---
    Console.WriteLine("  ┌─ PENDEKATAN B: Grouped Skills (Skill-based Registration) ───┐");
    Console.WriteLine("  │                                                              │");
    Console.WriteLine("  │  Agent                                                       │");
    Console.WriteLine("  │   ├── Skill: ResearchSkill                                  │");
    Console.WriteLine("  │   │    ├── Tool: WebSearch                                  │");
    Console.WriteLine("  │   │    ├── Tool: Summarize                                  │");
    Console.WriteLine("  │   │    └── Tool: ExtractKeywords                            │");
    Console.WriteLine("  │   └── Skill: UtilitySkill                                   │");
    Console.WriteLine("  │        ├── Tool: GetWeather                                 │");
    Console.WriteLine("  │        └── Tool: Calculator                                 │");
    Console.WriteLine("  │                                                              │");
    Console.WriteLine("  │  Karakteristik:                                              │");
    Console.WriteLine("  │  - Tools dikelompokkan berdasarkan domain/fungsi            │");
    Console.WriteLine("  │  - Mudah di-reuse ke agent lain (satu skill = satu unit)    │");
    Console.WriteLine("  │  - Registrasi sebagai paket (skill name + tool count)       │");
    Console.WriteLine("  │  - Konteks domain jelas dari nama skill                      │");
    Console.WriteLine("  └──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // --- Perbandingan ringkas ---
    Console.WriteLine("  ┌─ RINGKASAN PERBANDINGAN ─────────────────────────────────────┐");
    Console.WriteLine("  │                                                              │");
    Console.WriteLine("  │  Aspek              │ Flat Tools    │ Grouped Skills         │");
    Console.WriteLine("  │  ───────────────────┼───────────────┼────────────────────────│");
    Console.WriteLine("  │  Organisasi         │ Datar         │ Hierarkis              │");
    Console.WriteLine("  │  Reusability        │ Per tool      │ Per skill (paket)      │");
    Console.WriteLine("  │  Discoverability    │ Rendah        │ Tinggi                 │");
    Console.WriteLine("  │  Maintainability    │ Sulit         │ Mudah                  │");
    Console.WriteLine("  │  Cocok untuk        │ ≤3 tools      │ >3 tools terkait       │");
    Console.WriteLine("  │                                                              │");
    Console.WriteLine("  └──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    Console.WriteLine("[INFO] Rekomendasi: Gunakan skill ketika Anda memiliki 2+ tools yang");
    Console.WriteLine("       secara domain terkait dan akan di-reuse di berbagai agent.");
    Console.WriteLine();
}

// =============================================================================
// Bagian 3: Demonstrasi skill yang sama didaftarkan ke multiple agents
// Skill bersifat reusable - satu definisi dapat digunakan oleh banyak agent
// =============================================================================
static async Task DemonstrasikanSkillSharing(IChatClient chatClient, CancellationToken cancellationToken)
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 3: Skill Sharing - Satu Skill, Banyak Agent           │");
    Console.WriteLine("│ Skill yang sama dapat didaftarkan ke beberapa agent berbeda.  │");
    Console.WriteLine("│ Setiap agent menggunakan skill secara independen.             │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // --- Membuat tools dari ResearchSkill (definisi yang sama) ---
    // Skill yang sama digunakan oleh kedua agent
    var webSearchTool = AIFunctionFactory.Create(ResearchSkill.WebSearch);
    var summarizeTool = AIFunctionFactory.Create(ResearchSkill.Summarize);
    var extractKeywordsTool = AIFunctionFactory.Create(ResearchSkill.ExtractKeywords);
    var sharedSkillTools = new List<AITool> { webSearchTool, summarizeTool, extractKeywordsTool };

    // --- Agent 1: Asisten Akademik (menggunakan ResearchSkill) ---
    Console.WriteLine("[INFO] Mendaftarkan ResearchSkill ke Agent 1: AkademikAgent...");
    var akademikAgent = chatClient.AsAIAgent(
        instructions: "Kamu adalah asisten akademik yang membantu mahasiswa melakukan riset. " +
                      "Gunakan tools pencarian untuk menemukan informasi ilmiah. " +
                      "Jawab dengan gaya akademis dan formal dalam bahasa Indonesia.",
        name: "AkademikAgent",
        description: "Agent akademik dengan kemampuan riset",
        tools: sharedSkillTools);

    Console.WriteLine($"       ✓ AkademikAgent + {ResearchSkill.SkillName} ({sharedSkillTools.Count} tools)");
    Console.WriteLine();

    // --- Agent 2: Asisten Jurnalis (menggunakan ResearchSkill yang sama) ---
    Console.WriteLine("[INFO] Mendaftarkan ResearchSkill ke Agent 2: JurnalisAgent...");
    var jurnalisAgent = chatClient.AsAIAgent(
        instructions: "Kamu adalah asisten jurnalis yang membantu menulis artikel berita. " +
                      "Gunakan tools pencarian untuk fact-checking dan perangkuman. " +
                      "Jawab dengan gaya jurnalistik yang ringkas dalam bahasa Indonesia.",
        name: "JurnalisAgent",
        description: "Agent jurnalis dengan kemampuan riset",
        tools: sharedSkillTools);

    Console.WriteLine($"       ✓ JurnalisAgent + {ResearchSkill.SkillName} ({sharedSkillTools.Count} tools)");
    Console.WriteLine();

    // --- Demonstrasi kedua agent menggunakan skill yang sama ---
    Console.WriteLine("[INFO] Kedua agent menggunakan ResearchSkill secara independen:");
    Console.WriteLine();

    // Agent 1 menggunakan skill
    var sharedPrompt = "Cari informasi tentang cloud computing";
    Console.WriteLine("  ─── Agent 1 (AkademikAgent) menggunakan ResearchSkill ────────");
    Console.WriteLine($"  User: \"{sharedPrompt}\"");
    Console.WriteLine("  [SKILL ACTIVATION] Skill: ResearchSkill → Agent: AkademikAgent");
    Console.WriteLine("  [EXECUTION ORDER]  Tools yang tersedia: WebSearch, Summarize, ExtractKeywords");
    Console.WriteLine();
    await InvokeAgentAsync(akademikAgent, sharedPrompt, cancellationToken);
    Console.WriteLine();

    // Agent 2 menggunakan skill yang sama
    Console.WriteLine("  ─── Agent 2 (JurnalisAgent) menggunakan ResearchSkill ────────");
    Console.WriteLine($"  User: \"{sharedPrompt}\"");
    Console.WriteLine("  [SKILL ACTIVATION] Skill: ResearchSkill → Agent: JurnalisAgent");
    Console.WriteLine("  [EXECUTION ORDER]  Tools yang tersedia: WebSearch, Summarize, ExtractKeywords");
    Console.WriteLine();
    await InvokeAgentAsync(jurnalisAgent, sharedPrompt, cancellationToken);
    Console.WriteLine();

    // --- Konfirmasi bahwa kedua agent beroperasi independen ---
    Console.WriteLine("[INFO] Kedua agent menggunakan skill yang sama (ResearchSkill) secara independen.");
    Console.WriteLine("       Masing-masing agent memproses hasil tools sesuai instructions-nya:");
    Console.WriteLine("       - AkademikAgent → gaya akademis & formal");
    Console.WriteLine("       - JurnalisAgent → gaya jurnalistik & ringkas");
    Console.WriteLine();
}

// =============================================================================
// Bagian 4: Error handling pada registrasi skill
// Demonstrasi penanganan kesalahan: nama duplikat dan tool tidak valid
// =============================================================================
static void DemonstrasikanSkillRegistrationErrors()
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 4: Skill Registration Errors - Penanganan Kesalahan   │");
    Console.WriteLine("│ Demonstrasi error handling saat registrasi skill gagal.       │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // --- Skenario 1: Registrasi skill dengan nama duplikat ---
    Console.WriteLine("  ─── Skenario 1: Nama Skill Duplikat ─────────────────────────");
    Console.WriteLine();

    // Simulasi registry untuk mendeteksi duplikat
    var registeredSkills = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // Registrasi pertama berhasil
    var skillName = ResearchSkill.SkillName;
    var toolCount = ResearchSkill.ToolNames.Length;

    if (!registeredSkills.ContainsKey(skillName))
    {
        registeredSkills[skillName] = toolCount;
        Console.WriteLine($"  [OK] Skill '{skillName}' berhasil didaftarkan ({toolCount} tools).");
    }

    // Registrasi kedua gagal karena nama duplikat
    Console.WriteLine();
    Console.WriteLine("  [INFO] Mencoba mendaftarkan skill dengan nama yang sama...");
    if (registeredSkills.ContainsKey(skillName))
    {
        // Menampilkan error yang mengindikasikan penyebab kegagalan
        Console.WriteLine($"  [ERROR] Registrasi skill gagal: nama '{skillName}' sudah terdaftar.");
        Console.WriteLine($"  [CAUSE] Skill dengan nama '{skillName}' sudah ada dalam registry.");
        Console.WriteLine($"  [HINT] Gunakan nama unik untuk setiap skill, atau hapus skill lama terlebih dahulu.");
    }
    Console.WriteLine();

    // --- Skenario 2: Registrasi skill dengan tool tidak valid ---
    Console.WriteLine("  ─── Skenario 2: Tool Tidak Valid dalam Skill ─────────────────");
    Console.WriteLine();

    // Simulasi pembuatan tool dari method yang tidak valid (null delegate)
    Console.WriteLine("  [INFO] Mencoba membuat skill dengan tool yang tidak valid...");
    try
    {
        // Mencoba membuat tool dari null delegate - ini akan melempar exception
        Func<string, string>? invalidFunc = null;
        _ = AIFunctionFactory.Create(invalidFunc!, "InvalidTool", "Tool yang tidak valid");

        // Baris ini tidak akan tercapai jika exception dilempar
        Console.WriteLine("  [OK] Tool berhasil dibuat (tidak seharusnya terjadi).");
    }
    catch (ArgumentNullException ex)
    {
        // Menangani error tool tidak valid dengan pesan informatif
        Console.WriteLine($"  [ERROR] Registrasi skill gagal: tool tidak valid.");
        Console.WriteLine($"  [CAUSE] {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine($"  [HINT] Pastikan semua method untuk tool memiliki implementasi yang valid.");
    }
    catch (Exception ex)
    {
        // Menangani error lain yang mungkin terjadi saat pembuatan tool
        Console.WriteLine($"  [ERROR] Registrasi skill gagal: error tak terduga.");
        Console.WriteLine($"  [CAUSE] {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine($"  [HINT] Periksa definisi method dan atribut [Description] pada tool.");
    }
    Console.WriteLine();

    // --- Skenario 3: Skill tanpa tools (skill kosong) ---
    Console.WriteLine("  ─── Skenario 3: Skill Kosong (Tanpa Tools) ───────────────────");
    Console.WriteLine();
    Console.WriteLine("  [INFO] Mencoba mendaftarkan skill tanpa tools...");

    var emptySkillTools = new List<AITool>();
    if (emptySkillTools.Count == 0)
    {
        // Skill harus memiliki minimal 1 tool untuk berguna
        Console.WriteLine($"  [ERROR] Registrasi skill gagal: skill tidak memiliki tools.");
        Console.WriteLine($"  [CAUSE] Skill harus mengemas minimal 1 tool agar berguna.");
        Console.WriteLine($"  [HINT] Tambahkan tools ke skill sebelum mendaftarkannya ke agent.");
    }
    Console.WriteLine();

    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("[INFO] Demonstrasi Adding Skills selesai.");
    Console.WriteLine("[INFO] Konsep yang dipelajari:");
    Console.WriteLine("       1. Membuat skill dengan multiple tools terkait");
    Console.WriteLine("       2. Mendaftarkan skill ke agent (nama + jumlah tools)");
    Console.WriteLine("       3. Perbandingan flat tools vs grouped skills");
    Console.WriteLine("       4. Skill sharing: satu skill untuk banyak agent");
    Console.WriteLine("       5. Error handling: duplikat nama, tool tidak valid, skill kosong");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
}

// =============================================================================
// Helper: Menjalankan agent dan menampilkan response di console
// Mencatat skill activation dan tool execution order
// =============================================================================
static async Task InvokeAgentAsync(AIAgent agent, string prompt, CancellationToken cancellationToken)
{
    try
    {
        // Menjalankan agent - tool calls terjadi secara otomatis di dalam RunAsync
        // Agent framework menangani siklus: LLM pilih tool → eksekusi → hasil → LLM lanjut
        var response = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
        var responseText = response?.ToString();

        if (!string.IsNullOrWhiteSpace(responseText))
        {
            Console.WriteLine($"  Agent: {FormatResponse(responseText)}");
        }
        else
        {
            Console.WriteLine("  [INFO] Agent mengembalikan response kosong.");
        }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        // Menangani kegagalan tool execution dalam skill
        Console.WriteLine($"  [ERROR] Skill execution gagal: {ex.GetType().Name}");
        Console.WriteLine($"  [CAUSE] {ex.Message}");
        Console.WriteLine("  [HINT] Periksa koneksi ke Azure OpenAI dan konfigurasi skill.");
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
