// =============================================================================
// From LLMs to Agents - Modul Pembelajaran Kedua
// Demonstrasi perbedaan antara LLM call biasa dan Agent dengan instructions
// Membangun agent pertama dengan interactive loop dan session state
// =============================================================================

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.AI;

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
    Console.WriteLine($"[CAUSE] Endpoint tidak dapat dijangkau atau terjadi masalah jaringan.");
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
// Fungsi utama aplikasi - mendemonstrasikan transisi dari LLM ke Agent
// =============================================================================
static async Task RunApplicationAsync(IConfiguration configuration, CancellationToken cancellationToken)
{
    var endpoint = configuration["AzureOpenAI:Endpoint"]!;
    var deploymentName = configuration["AzureOpenAI:DeploymentName"]!;

    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║     From LLMs to Agents - Microsoft Agent Framework         ║");
    Console.WriteLine("║     Demonstrasi perbedaan LLM call vs Agent + Instructions  ║");
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

    // === BAGIAN 1: Perbandingan LLM call vs Agent call ===
    await DemonstrasikanLlmVsAgent(chatClient, cancellationToken);

    // === BAGIAN 2: Dua agent dengan instructions berbeda ===
    await DemonstrasikanDuaAgentBerbeda(chatClient, cancellationToken);

    // === BAGIAN 3: Interactive loop dengan AgentSession ===
    await JalankanInteractiveLoop(chatClient, cancellationToken);
}

// =============================================================================
// Bagian 1: Mendemonstrasikan perbedaan antara LLM call biasa vs Agent
// LLM call = tanpa instructions, Agent call = dengan instructions (persona)
// =============================================================================
static async Task DemonstrasikanLlmVsAgent(IChatClient chatClient, CancellationToken cancellationToken)
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 1: Perbandingan LLM Response vs Agent Response        │");
    Console.WriteLine("│ LLM call langsung tanpa instructions vs Agent dengan persona.│");
    Console.WriteLine("│ Agent memiliki instruksi yang membentuk perilaku responsnya.  │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Prompt yang sama dikirim ke LLM langsung dan ke Agent
    var prompt = "Apa manfaat utama menggunakan AI dalam pengembangan software?";
    Console.WriteLine($"  Prompt: \"{prompt}\"");
    Console.WriteLine();

    // --- LLM Response: panggilan langsung tanpa instructions ---
    Console.WriteLine("  ─── LLM Response (tanpa instructions) ───────────────────────");
    try
    {
        // Memanggil LLM secara langsung tanpa konteks atau persona apapun
        var llmResponse = await SendWithTimeoutAsync(
            chatClient, prompt, options: null, cancellationToken);

        if (llmResponse is not null)
        {
            Console.WriteLine($"  {FormatResponse(llmResponse)}");
        }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Console.WriteLine($"  [ERROR] LLM call gagal: {ex.Message}");
    }
    Console.WriteLine();

    // --- Agent Response: menggunakan .AsAIAgent() dengan custom instructions ---
    Console.WriteLine("  ─── Agent Response (dengan instructions) ─────────────────────");
    try
    {
        // Membuat agent dengan instruksi yang membentuk persona spesifik
        // AsAIAgent menerima parameter: name, instructions, description
        var agent = chatClient.AsAIAgent(
            name: "SeniorEngineerAgent",
            instructions: "Kamu adalah senior software engineer Indonesia yang berpengalaman 15 tahun. " +
                          "Jawab dengan gaya praktis, berikan contoh konkret, dan gunakan bahasa Indonesia. " +
                          "Fokus pada pengalaman nyata di industri, bukan teori abstrak.",
            description: "Agent dengan persona senior engineer");

        // Menjalankan agent untuk single-turn response
        var agentResponse = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
        var responseText = agentResponse?.ToString();

        if (!string.IsNullOrWhiteSpace(responseText))
        {
            Console.WriteLine($"  {FormatResponse(responseText)}");
        }
        else
        {
            Console.WriteLine("  [INFO] Agent mengembalikan response kosong.");
        }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Console.WriteLine($"  [ERROR] Agent call gagal: {ex.Message}");
    }

    Console.WriteLine();
    Console.WriteLine("  [INFO] Perhatikan bagaimana Agent dengan instructions memberikan");
    Console.WriteLine("         response yang lebih terarah dan sesuai persona.");
    Console.WriteLine();
}

// =============================================================================
// Bagian 2: Mendemonstrasikan dua agent dengan instructions berbeda
// Prompt yang sama menghasilkan response berbeda tergantung instructions
// =============================================================================
static async Task DemonstrasikanDuaAgentBerbeda(IChatClient chatClient, CancellationToken cancellationToken)
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 2: Dua Agent dengan Instructions Berbeda              │");
    Console.WriteLine("│ Prompt yang sama, persona berbeda → hasil yang berbeda.       │");
    Console.WriteLine("│ Ini menunjukkan kekuatan instructions dalam membentuk agent.  │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Prompt yang sama dikirim ke kedua agent
    var prompt = "Jelaskan konsep 'clean code' dan mengapa itu penting.";
    Console.WriteLine($"  Prompt: \"{prompt}\"");
    Console.WriteLine();

    // --- Agent A: Guru pemrograman yang sabar ---
    Console.WriteLine("  ─── Agent A: Guru Pemrograman ───────────────────────────────");
    try
    {
        // Agent pertama memiliki persona guru yang sabar dan penuh analogi
        var agentGuru = chatClient.AsAIAgent(
            name: "GuruPemrograman",
            instructions: "Kamu adalah guru pemrograman yang sabar dan suka menggunakan analogi sehari-hari. " +
                          "Jelaskan konsep teknis dengan cara yang mudah dipahami pemula. " +
                          "Gunakan bahasa Indonesia dan berikan minimal satu analogi non-teknis.",
            description: "Agent dengan persona guru pemrograman");

        // Menjalankan agent guru untuk mendapatkan response
        var responseGuru = await agentGuru.RunAsync(prompt, cancellationToken: cancellationToken);
        var textGuru = responseGuru?.ToString();

        if (!string.IsNullOrWhiteSpace(textGuru))
        {
            Console.WriteLine($"  {FormatResponse(textGuru)}");
        }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Console.WriteLine($"  [ERROR] Agent Guru gagal: {ex.Message}");
    }
    Console.WriteLine();

    // --- Agent B: Code reviewer yang tegas ---
    Console.WriteLine("  ─── Agent B: Code Reviewer ──────────────────────────────────");
    try
    {
        // Agent kedua memiliki persona code reviewer yang langsung ke poin
        var agentReviewer = chatClient.AsAIAgent(
            name: "CodeReviewer",
            instructions: "Kamu adalah code reviewer senior yang tegas dan to-the-point. " +
                          "Berikan jawaban dalam format bullet points singkat. " +
                          "Gunakan bahasa Indonesia. Fokus pada dampak praktis di codebase nyata. " +
                          "Sertakan contoh kode buruk vs kode baik jika relevan.",
            description: "Agent dengan persona code reviewer senior");

        // Menjalankan agent reviewer untuk mendapatkan response
        var responseReviewer = await agentReviewer.RunAsync(prompt, cancellationToken: cancellationToken);
        var textReviewer = responseReviewer?.ToString();

        if (!string.IsNullOrWhiteSpace(textReviewer))
        {
            Console.WriteLine($"  {FormatResponse(textReviewer)}");
        }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Console.WriteLine($"  [ERROR] Agent Reviewer gagal: {ex.Message}");
    }

    Console.WriteLine();
    Console.WriteLine("  [INFO] Kedua agent menerima prompt yang sama, tetapi instructions");
    Console.WriteLine("         yang berbeda menghasilkan gaya dan konten response yang berbeda.");
    Console.WriteLine();
}

// =============================================================================
// Bagian 3: Interactive loop dengan AgentSession untuk conversation state
// User dapat berdialog dengan agent dan agent mengingat konteks percakapan
// =============================================================================
static async Task JalankanInteractiveLoop(IChatClient chatClient, CancellationToken cancellationToken)
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 3: Interactive Loop dengan AgentSession               │");
    Console.WriteLine("│ Agent mempertahankan conversation state sepanjang sesi.      │");
    Console.WriteLine("│ Ketik 'exit' atau 'quit' untuk mengakhiri sesi.              │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Membuat agent dengan instructions untuk interactive conversation
    var agent = chatClient.AsAIAgent(
        name: "LearningAssistant",
        instructions: "Kamu adalah asisten AI yang membantu developer belajar Microsoft Agent Framework. " +
                      "Jawab dalam bahasa Indonesia dengan gaya ramah dan informatif. " +
                      "Ingat konteks percakapan sebelumnya dalam sesi ini. " +
                      "Jika user bertanya tentang topik di luar programming, " +
                      "arahkan kembali ke topik agent framework dengan sopan.",
        description: "Asisten pembelajaran Microsoft Agent Framework");

    // Membuat AgentSession untuk menyimpan conversation state
    // Session memungkinkan agent mengingat konteks dari turn sebelumnya
    var session = await agent.CreateSessionAsync(cancellationToken: cancellationToken);

    Console.WriteLine("[INFO] Session dibuat. Agent siap menerima input.");
    Console.WriteLine("[INFO] Ketik 'exit' atau 'quit' untuk mengakhiri sesi.");
    Console.WriteLine();

    // Interactive loop - terus menerima input sampai user mengetik exit/quit
    while (!cancellationToken.IsCancellationRequested)
    {
        // Menampilkan prompt indicator untuk user input
        Console.Write("> ");
        var input = Console.ReadLine();

        // Menangani kasus input null (misalnya saat stream tertutup)
        if (input is null)
        {
            break;
        }

        // Memeriksa apakah user ingin keluar (case-insensitive)
        var trimmedInput = input.Trim();
        if (trimmedInput.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
            trimmedInput.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            // Mengakhiri loop dengan pesan konfirmasi
            Console.WriteLine();
            Console.WriteLine("[INFO] Sesi berakhir. Terima kasih telah belajar Microsoft Agent Framework!");
            break;
        }

        // Mengabaikan input kosong
        if (string.IsNullOrWhiteSpace(trimmedInput))
        {
            continue;
        }

        // Mengirim input ke agent melalui session untuk mempertahankan konteks
        try
        {
            // RunAsync dengan session memungkinkan agent mengingat percakapan sebelumnya
            var response = await agent.RunAsync(
                trimmedInput,
                session,
                cancellationToken: cancellationToken);

            var responseText = response?.ToString();

            if (!string.IsNullOrWhiteSpace(responseText))
            {
                Console.WriteLine();
                Console.WriteLine(responseText);
                Console.WriteLine();
            }
            else
            {
                // Menangani response kosong dari agent
                Console.WriteLine();
                Console.WriteLine("[INFO] Agent tidak memberikan response. Coba pertanyaan lain.");
                Console.WriteLine();
            }
        }
        catch (OperationCanceledException)
        {
            // Re-throw untuk ditangani oleh handler di atas
            throw;
        }
        catch (Exception ex)
        {
            // Menangani kegagalan agent call tanpa menghentikan loop
            // User dapat melanjutkan interaksi setelah error
            Console.WriteLine();
            Console.WriteLine($"[ERROR] Agent call gagal: {ex.Message}");
            Console.WriteLine($"[CAUSE] Terjadi masalah saat memproses permintaan Anda.");
            Console.WriteLine("[HINT] Coba kirim pertanyaan lain atau periksa koneksi internet.");
            Console.WriteLine();
        }
    }
}

// =============================================================================
// Fungsi helper: mengirim prompt ke LLM dengan timeout 30 detik
// =============================================================================
static async Task<string?> SendWithTimeoutAsync(
    IChatClient chatClient,
    string prompt,
    ChatOptions? options,
    CancellationToken cancellationToken)
{
    // Membuat linked token yang menggabungkan user cancellation dan timeout
    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

    try
    {
        // Mengirim prompt ke model melalui IChatClient
        var result = await chatClient.GetResponseAsync(prompt, options, timeoutCts.Token);

        // Memeriksa apakah response kosong
        var responseText = result.Text;
        if (string.IsNullOrWhiteSpace(responseText))
        {
            Console.WriteLine("  [ERROR] Response kosong: Model mengembalikan response tanpa konten.");
            Console.WriteLine("  [HINT] Coba ubah prompt atau periksa konfigurasi model.");
            return null;
        }

        return responseText;
    }
    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
    {
        // Timeout terjadi (bukan user cancellation)
        Console.WriteLine("  [ERROR] Timeout: Response tidak diterima dalam 30 detik.");
        Console.WriteLine("  [HINT] Periksa koneksi internet atau coba lagi nanti.");
        return null;
    }
}

// =============================================================================
// Fungsi helper: memformat response agar tampil rapi di console
// =============================================================================
static string FormatResponse(string response)
{
    // Mengganti newline agar tampilan console tetap rapi
    return response.Replace("\n", "\n  ").Trim();
}
