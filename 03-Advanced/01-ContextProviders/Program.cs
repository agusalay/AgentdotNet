// =============================================================================
// Context Providers - Modul Pembelajaran Keenam (Advanced Level)
// Demonstrasi context provider pattern untuk menyediakan memory dan
// dynamic context ke agent. Mencakup: conversation history (sliding window),
// file-based knowledge base, dan token-aware truncation.
// =============================================================================

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.AI;
using ContextProviders.Providers;

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
// Fungsi utama aplikasi - mendemonstrasikan context provider pattern
// =============================================================================
static async Task RunApplicationAsync(IConfiguration configuration, CancellationToken cancellationToken)
{
    var endpoint = configuration["AzureOpenAI:Endpoint"]!;
    var deploymentName = configuration["AzureOpenAI:DeploymentName"]!;

    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║     Context Providers - Microsoft Agent Framework            ║");
    Console.WriteLine("║     Demonstrasi memory dan dynamic context untuk agent       ║");
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

    Console.WriteLine("[INFO] Koneksi berhasil. Memulai demonstrasi context providers...");
    Console.WriteLine();

    // --- Menyiapkan context providers ---
    // ConversationHistoryProvider: menyimpan 10 turn terakhir dengan token truncation
    var historyProvider = new ConversationHistoryProvider();

    // FileContextProvider: membaca knowledge base dari file JSON
    var knowledgeBasePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "knowledge-base.json");
    var fileProvider = new FileContextProvider(knowledgeBasePath);

    // === DEMO 1: Agent TANPA context provider ===
    await DemoTanpaContextProvider(chatClient, cancellationToken);

    // === DEMO 2: Agent DENGAN ConversationHistoryProvider ===
    await DemoDenganHistoryProvider(chatClient, historyProvider, cancellationToken);

    // === DEMO 3: FileContextProvider dengan knowledge base ===
    await DemoDenganFileProvider(chatClient, fileProvider, cancellationToken);

    // === DEMO 4: Token truncation ketika history terlalu panjang ===
    await DemoTokenTruncation(chatClient, cancellationToken);
}

// =============================================================================
// DEMO 1: Agent tanpa context provider - tidak bisa mengingat percakapan sebelumnya
// Menunjukkan bahwa tanpa memory, agent tidak dapat mereferensi info sebelumnya
// =============================================================================
static async Task DemoTanpaContextProvider(IChatClient chatClient, CancellationToken cancellationToken)
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ DEMO 1: Agent TANPA Context Provider                         │");
    Console.WriteLine("│ Agent tidak memiliki memory - tidak bisa recall info lama     │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();
    Console.WriteLine("  Skenario: Mengirim dua pesan berurutan tanpa context.");
    Console.WriteLine("  Agent tidak akan bisa mengingat pesan pertama saat menjawab pesan kedua.");
    Console.WriteLine();

    // Membuat agent sederhana tanpa context provider
    var agent = chatClient.AsAIAgent(
        instructions: "Kamu adalah asisten AI. Jawab dengan singkat dalam bahasa Indonesia.",
        name: "AgentTanpaMemory",
        description: "Agent tanpa context provider");

    // Pesan pertama: memberikan informasi (nama pengguna)
    var pesan1 = "Halo, nama saya Budi dan saya bekerja sebagai data scientist di Jakarta.";
    Console.WriteLine($"  [User] Pesan 1: \"{pesan1}\"");
    var response1 = await agent.RunAsync(pesan1, cancellationToken: cancellationToken);
    Console.WriteLine($"  [Agent] Response 1: {TruncateDisplay(response1?.ToString() ?? "(kosong)", 200)}");
    Console.WriteLine();

    // Pesan kedua: menanyakan kembali informasi dari pesan pertama
    // Tanpa context provider, agent tidak tahu siapa yang bertanya
    var pesan2 = "Siapa nama saya dan di mana saya bekerja?";
    Console.WriteLine($"  [User] Pesan 2: \"{pesan2}\"");
    var response2 = await agent.RunAsync(pesan2, cancellationToken: cancellationToken);
    Console.WriteLine($"  [Agent] Response 2: {TruncateDisplay(response2?.ToString() ?? "(kosong)", 200)}");
    Console.WriteLine();

    // Penjelasan hasil
    Console.WriteLine("  📝 Hasil: Agent TIDAK bisa mengingat nama dan pekerjaan karena");
    Console.WriteLine("     tidak ada context provider yang menyimpan riwayat percakapan.");
    Console.WriteLine();
    Console.WriteLine(new string('─', 66));
    Console.WriteLine();
}

// =============================================================================
// DEMO 2: Agent dengan ConversationHistoryProvider
// Mendemonstrasikan recall capability - agent bisa mereferensi info sebelumnya
// =============================================================================
static async Task DemoDenganHistoryProvider(
    IChatClient chatClient,
    ConversationHistoryProvider historyProvider,
    CancellationToken cancellationToken)
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ DEMO 2: Agent DENGAN ConversationHistoryProvider              │");
    Console.WriteLine("│ Agent memiliki memory - bisa recall informasi sebelumnya      │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();
    Console.WriteLine("  Skenario: Mengirim pesan dengan context provider aktif.");
    Console.WriteLine("  Agent akan mengingat pesan pertama saat menjawab pesan kedua.");
    Console.WriteLine();

    // Menghapus history agar demo dimulai dari keadaan bersih
    historyProvider.ClearHistory();

    // Pesan pertama: memberikan informasi yang sama
    var pesan1 = "Halo, nama saya Budi dan saya bekerja sebagai data scientist di Jakarta.";
    Console.WriteLine($"  [User] Pesan 1: \"{pesan1}\"");

    // Memanggil agent DENGAN konteks dari provider
    var response1 = await InvokeWithContext(chatClient, [historyProvider], pesan1, cancellationToken);
    Console.WriteLine($"  [Agent] Response 1: {TruncateDisplay(response1, 200)}");
    Console.WriteLine($"  [INFO] Riwayat tersimpan: {historyProvider.TurnCount} turn");
    Console.WriteLine();

    // Pesan kedua: follow-up yang mereferensikan informasi sebelumnya
    var pesan2 = "Siapa nama saya dan di mana saya bekerja?";
    Console.WriteLine($"  [User] Pesan 2: \"{pesan2}\"");

    // Dengan history provider, agent menerima konteks percakapan sebelumnya
    var response2 = await InvokeWithContext(chatClient, [historyProvider], pesan2, cancellationToken);
    Console.WriteLine($"  [Agent] Response 2: {TruncateDisplay(response2, 200)}");
    Console.WriteLine($"  [INFO] Riwayat tersimpan: {historyProvider.TurnCount} turn");
    Console.WriteLine();

    // Penjelasan hasil
    Console.WriteLine("  📝 Hasil: Agent BISA mengingat nama 'Budi' dan pekerjaan 'data scientist'");
    Console.WriteLine("     karena ConversationHistoryProvider menyediakan riwayat percakapan.");
    Console.WriteLine();
    Console.WriteLine(new string('─', 66));
    Console.WriteLine();
}

// =============================================================================
// DEMO 3: FileContextProvider - menyediakan knowledge base dari file JSON
// Agent dapat menjawab pertanyaan berdasarkan fakta dari file eksternal
// =============================================================================
static async Task DemoDenganFileProvider(
    IChatClient chatClient,
    FileContextProvider fileProvider,
    CancellationToken cancellationToken)
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ DEMO 3: FileContextProvider - Knowledge Base dari File        │");
    Console.WriteLine("│ Agent menjawab berdasarkan fakta dari Data/knowledge-base.json│");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();
    Console.WriteLine("  Skenario: Agent menjawab pertanyaan menggunakan knowledge base.");
    Console.WriteLine("  Konteks dari file JSON disediakan sebelum agent memproses request.");
    Console.WriteLine();

    // Pertanyaan yang jawabannya ada di knowledge base
    var pertanyaan = "Apa itu Sliding Window Strategy dalam context management? Jelaskan singkat.";
    Console.WriteLine($"  [User] \"{pertanyaan}\"");
    Console.WriteLine();

    // Memanggil agent dengan FileContextProvider
    Console.WriteLine("  [INFO] Memuat konteks dari knowledge base...");
    var response = await InvokeWithContext(chatClient, [fileProvider], pertanyaan, cancellationToken);
    Console.WriteLine($"  [Agent] {TruncateDisplay(response, 300)}");
    Console.WriteLine();
    Console.WriteLine($"  [INFO] FileContextProvider menyediakan {fileProvider.FactCount} fakta sebagai konteks.");
    Console.WriteLine();

    // Penjelasan hasil
    Console.WriteLine("  📝 Hasil: Agent menjawab berdasarkan knowledge base yang dimuat dari file.");
    Console.WriteLine("     Pola ini adalah versi sederhana dari RAG (Retrieval-Augmented Generation).");
    Console.WriteLine();
    Console.WriteLine(new string('─', 66));
    Console.WriteLine();
}

// =============================================================================
// DEMO 4: Token truncation - mendemonstrasikan pemotongan history yang panjang
// Ketika total token melebihi 4000, turn terlama dihapus otomatis
// =============================================================================
static async Task DemoTokenTruncation(IChatClient chatClient, CancellationToken cancellationToken)
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ DEMO 4: Token-Aware Truncation                               │");
    Console.WriteLine("│ Pemotongan otomatis ketika history melebihi 4000 token        │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();
    Console.WriteLine("  Skenario: Mengisi history dengan banyak percakapan panjang");
    Console.WriteLine("  hingga melebihi batas 4000 token, lalu melihat truncation beraksi.");
    Console.WriteLine();

    // Membuat history provider baru untuk demo ini
    var truncationProvider = new ConversationHistoryProvider();

    // Mengisi history dengan percakapan panjang agar melebihi 4000 token
    // Setiap turn ~800 karakter ≈ 200 token. 10 turn = ~2000 token belum cukup.
    // Kita buat percakapan yang lebih panjang untuk memaksa truncation.
    Console.WriteLine("  [INFO] Mengisi riwayat percakapan dengan data panjang...");
    Console.WriteLine();

    // Simulasi 10 turn percakapan panjang (masing-masing ~2000 karakter = ~500 token)
    for (int i = 1; i <= 10; i++)
    {
        // Membuat pesan user yang cukup panjang
        var userMsg = $"Pertanyaan turn ke-{i}: " + string.Join(" ",
            Enumerable.Repeat($"Ini adalah konteks panjang untuk percakapan turn {i} yang berisi banyak informasi detail tentang topik yang dibahas.", 5));

        // Membuat response assistant yang juga panjang
        var assistantMsg = $"Jawaban turn ke-{i}: " + string.Join(" ",
            Enumerable.Repeat($"Ini adalah respons komprehensif dari agent untuk turn {i} dengan penjelasan mendalam yang mencakup berbagai aspek dari pertanyaan yang diajukan.", 5));

        await truncationProvider.StoreContextAsync(userMsg, assistantMsg);
    }

    Console.WriteLine($"  [INFO] Turn tersimpan: {truncationProvider.TurnCount}");
    Console.WriteLine($"  [INFO] Batas token: 4000 token (estimasi ~4 char/token)");
    Console.WriteLine();

    // Mendapatkan konteks yang sudah di-truncate
    var context = await truncationProvider.ProvideContextAsync();
    var tokenCount = ConversationHistoryProvider.EstimateTokenCount(context);
    Console.WriteLine($"  [INFO] Token setelah truncation: ~{tokenCount} token");
    Console.WriteLine($"  [INFO] Status: {(tokenCount <= 4000 ? "✅ Di bawah batas 4000 token" : "⚠️ Melebihi batas")}");
    Console.WriteLine();

    // Menghitung berapa turn yang tersisa setelah truncation
    // Hitung jumlah "User:" yang muncul di context
    var turnsInContext = context.Split("User: ").Length - 1;
    Console.WriteLine($"  [INFO] Turn dalam konteks setelah truncation: {turnsInContext} dari 10 total");
    Console.WriteLine("  [INFO] Turn terlama dihapus untuk menjaga total token di bawah 4000.");
    Console.WriteLine();

    // Demo bahwa agent masih bisa menggunakan konteks yang di-truncate
    var pertanyaan = "Berapa nomor turn terakhir yang kamu ingat dari percakapan kita?";
    Console.WriteLine($"  [User] \"{pertanyaan}\"");
    var response = await InvokeWithContext(chatClient, [truncationProvider], pertanyaan, cancellationToken);
    Console.WriteLine($"  [Agent] {TruncateDisplay(response, 300)}");
    Console.WriteLine();

    // Penjelasan hasil
    Console.WriteLine("  📝 Hasil: Token truncation memastikan konteks tidak melebihi batas.");
    Console.WriteLine("     Turn terlama dihapus prioritas, menjaga informasi terbaru tetap tersedia.");
    Console.WriteLine("     Strategi ini mencegah error context overflow pada LLM.");
    Console.WriteLine();
}

// =============================================================================
// Fungsi helper: memanggil agent dengan konteks dari context providers
// Mengumpulkan konteks dari semua provider, lalu menyertakannya dalam prompt
// =============================================================================
static async Task<string> InvokeWithContext(
    IChatClient chatClient,
    IContextProvider[] providers,
    string userMessage,
    CancellationToken cancellationToken)
{
    // Langkah 1: Kumpulkan konteks dari semua provider (ProvideContextAsync)
    var contextParts = new List<string>();
    foreach (var provider in providers)
    {
        var context = await provider.ProvideContextAsync();
        if (!string.IsNullOrEmpty(context))
        {
            contextParts.Add(context);
        }
    }

    // Langkah 2: Gabungkan konteks dengan instruksi agent
    var systemInstruction = "Kamu adalah asisten AI yang membantu. Jawab dengan singkat dan jelas dalam bahasa Indonesia.";
    if (contextParts.Count > 0)
    {
        // Menyertakan konteks dari provider dalam system message
        var combinedContext = string.Join("\n", contextParts);
        systemInstruction += "\n\nBerikut adalah konteks tambahan yang tersedia untukmu:\n" + combinedContext;
    }

    // Langkah 3: Membuat agent dengan konteks yang sudah digabung
    var agent = chatClient.AsAIAgent(
        instructions: systemInstruction,
        name: "ContextAwareAgent",
        description: "Agent dengan context providers");

    // Langkah 4: Memanggil agent dengan pesan user
    var result = await agent.RunAsync(userMessage, cancellationToken: cancellationToken);
    var responseText = result?.ToString() ?? "(response kosong)";

    // Langkah 5: Simpan turn ke semua provider (StoreContextAsync)
    foreach (var provider in providers)
    {
        await provider.StoreContextAsync(userMessage, responseText);
    }

    return responseText;
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
