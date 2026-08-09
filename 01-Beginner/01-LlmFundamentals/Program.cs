// =============================================================================
// LLM Fundamentals - Modul Pembelajaran Pertama
// Demonstrasi interaksi langsung dengan Large Language Model menggunakan IChatClient
// =============================================================================

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
// Fungsi utama aplikasi - mendemonstrasikan interaksi dengan LLM
// =============================================================================
static async Task RunApplicationAsync(IConfiguration configuration, CancellationToken cancellationToken)
{
    var endpoint = configuration["AzureOpenAI:Endpoint"]!;
    var deploymentName = configuration["AzureOpenAI:DeploymentName"]!;

    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║        LLM Fundamentals - Microsoft Agent Framework         ║");
    Console.WriteLine("║    Demonstrasi interaksi langsung dengan Large Language Model║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();

    // --- Membuat koneksi ke Azure OpenAI menggunakan DefaultAzureCredential ---
    // DefaultAzureCredential mencoba beberapa metode autentikasi secara berurutan
    // (Azure CLI, Managed Identity, Visual Studio, dll.)
    Console.WriteLine("[INFO] Membuat koneksi ke Azure OpenAI...");
    Console.WriteLine($"[INFO] Endpoint: {endpoint}");
    Console.WriteLine($"[INFO] Model Deployment: {deploymentName}");
    Console.WriteLine();

    var azureClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new DefaultAzureCredential());

    // Mendapatkan IChatClient - abstraksi universal untuk model calls
    IChatClient chatClient = azureClient.GetChatClient(deploymentName).AsIChatClient();

    Console.WriteLine("[INFO] Koneksi berhasil dibuat. Siap mengirim prompt ke model.");
    Console.WriteLine();

    // === DEMONSTRASI 1: Temperature Rendah (Deterministik) ===
    await DemonstrasikanTemperatureRendah(chatClient, cancellationToken);

    // === DEMONSTRASI 2: Temperature Tinggi (Kreatif) ===
    await DemonstrasikanTemperatureTinggi(chatClient, cancellationToken);

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("[INFO] Demonstrasi selesai. Silakan bandingkan kedua output di atas.");
    Console.WriteLine("[INFO] Temperature rendah menghasilkan output yang lebih konsisten dan faktual.");
    Console.WriteLine("[INFO] Temperature tinggi menghasilkan output yang lebih variatif dan kreatif.");
}

// =============================================================================
// Demonstrasi 1: Mengirim prompt dengan temperature rendah (≤0.3)
// Temperature rendah menghasilkan output yang lebih deterministik dan konsisten
// =============================================================================
static async Task DemonstrasikanTemperatureRendah(IChatClient chatClient, CancellationToken cancellationToken)
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ DEMONSTRASI 1: Temperature Rendah (0.2)                      │");
    Console.WriteLine("│ Temperature rendah membuat model lebih deterministik.        │");
    Console.WriteLine("│ Output cenderung konsisten dan faktual setiap kali dijalankan.│");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Prompt yang sama akan digunakan di kedua demonstrasi untuk perbandingan
    var prompt = "Jelaskan apa itu artificial intelligence dalam 2 kalimat.";

    Console.WriteLine($"  Prompt: \"{prompt}\"");
    Console.WriteLine($"  Temperature: 0.2 (rendah - deterministik)");
    Console.WriteLine();

    // Mengkonfigurasi ChatOptions dengan temperature rendah
    var options = new ChatOptions
    {
        Temperature = 0.2f,
        MaxOutputTokens = 200
    };

    // Mengirim prompt ke model dengan timeout 30 detik
    var response = await SendPromptWithTimeoutAsync(chatClient, prompt, options, cancellationToken);

    if (response is not null)
    {
        Console.WriteLine("  ┌─ Response (Temperature 0.2) ─────────────────────────────");
        Console.WriteLine($"  │ {FormatResponse(response)}");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────");
    }

    Console.WriteLine();
}

// =============================================================================
// Demonstrasi 2: Mengirim prompt dengan temperature tinggi (≥0.8)
// Temperature tinggi menghasilkan output yang lebih kreatif dan variatif
// =============================================================================
static async Task DemonstrasikanTemperatureTinggi(IChatClient chatClient, CancellationToken cancellationToken)
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ DEMONSTRASI 2: Temperature Tinggi (0.9)                      │");
    Console.WriteLine("│ Temperature tinggi membuat model lebih kreatif.              │");
    Console.WriteLine("│ Output cenderung variatif dan berbeda setiap kali dijalankan. │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Prompt yang sama untuk perbandingan yang adil
    var prompt = "Jelaskan apa itu artificial intelligence dalam 2 kalimat.";

    Console.WriteLine($"  Prompt: \"{prompt}\"");
    Console.WriteLine($"  Temperature: 0.9 (tinggi - kreatif)");
    Console.WriteLine();

    // Mengkonfigurasi ChatOptions dengan temperature tinggi
    var options = new ChatOptions
    {
        Temperature = 0.9f,
        MaxOutputTokens = 200
    };

    // Mengirim prompt ke model dengan timeout 30 detik
    var response = await SendPromptWithTimeoutAsync(chatClient, prompt, options, cancellationToken);

    if (response is not null)
    {
        Console.WriteLine("  ┌─ Response (Temperature 0.9) ─────────────────────────────");
        Console.WriteLine($"  │ {FormatResponse(response)}");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────");
    }

    Console.WriteLine();
}

// =============================================================================
// Fungsi helper untuk mengirim prompt dengan timeout 30 detik
// Menangani kasus response kosong dan timeout
// =============================================================================
static async Task<string?> SendPromptWithTimeoutAsync(
    IChatClient chatClient,
    string prompt,
    ChatOptions options,
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
            Console.WriteLine("  [CAUSE] Model mungkin tidak dapat memproses prompt atau token limit terlalu rendah.");
            Console.WriteLine("  [HINT] Coba tingkatkan MaxOutputTokens atau ubah prompt Anda.");
            return null;
        }

        return responseText;
    }
    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
    {
        // Timeout terjadi (bukan user cancellation)
        Console.WriteLine("  [ERROR] Timeout: Response tidak diterima dalam 30 detik.");
        Console.WriteLine("  [CAUSE] Model membutuhkan waktu terlalu lama untuk merespons.");
        Console.WriteLine("  [HINT] Periksa koneksi internet atau coba lagi nanti. Pertimbangkan mengurangi MaxOutputTokens.");
        return null;
    }
    // Jika cancellationToken yang di-cancel (user Ctrl+C), biarkan exception naik
}

// =============================================================================
// Fungsi helper untuk memformat response agar tampil rapi di console
// =============================================================================
static string FormatResponse(string response)
{
    // Mengganti newline dengan format yang rapi untuk tampilan console
    return response.Replace("\n", "\n  │ ").Trim();
}
