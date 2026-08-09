// =============================================================================
// Adding Tools - Modul Pembelajaran Ketiga (Intermediate Level)
// Demonstrasi penambahan function tools dan MCP integration ke agent
// Agent dapat melakukan aksi nyata di luar text generation menggunakan tools
// =============================================================================

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.AI;
using ModelContextProtocol.Client;
using AddingTools.Tools;

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
// Fungsi utama aplikasi - mendemonstrasikan penggunaan tools pada agent
// =============================================================================
static async Task RunApplicationAsync(IConfiguration configuration, CancellationToken cancellationToken)
{
    var endpoint = configuration["AzureOpenAI:Endpoint"]!;
    var deploymentName = configuration["AzureOpenAI:DeploymentName"]!;

    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║     Adding Tools - Microsoft Agent Framework                 ║");
    Console.WriteLine("║     Demonstrasi function tools dan MCP integration           ║");
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

    // === BAGIAN 1: Mendefinisikan dan mendaftarkan function tools ===
    await DemonstrasikanFunctionTools(chatClient, cancellationToken);

    // === BAGIAN 2: Demonstrasi MCP Server connection ===
    await DemonstrasikanMcpConnection(configuration, chatClient, cancellationToken);
}

// =============================================================================
// Bagian 1: Mendefinisikan function tools dan mendaftarkannya ke agent
// Mendemonstrasikan tool invocation cycle lengkap
// =============================================================================
static async Task DemonstrasikanFunctionTools(IChatClient chatClient, CancellationToken cancellationToken)
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 1: Function Tools - Definisi, Registrasi, dan Invokasi│");
    Console.WriteLine("│ Agent menggunakan tools untuk melakukan aksi di luar text    │");
    Console.WriteLine("│ generation. LLM memilih tool berdasarkan deskripsi & konteks.│");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // --- Membuat function tools menggunakan AIFunctionFactory ---
    // AIFunctionFactory.Create() mengkonversi static method menjadi AI tool
    // Atribut [Description] memberikan metadata agar LLM tahu kapan menggunakan tool
    Console.WriteLine("[INFO] Membuat function tools dari WeatherTool class...");

    // Membuat tool dari method GetCurrentWeather
    var getCurrentWeatherTool = AIFunctionFactory.Create(WeatherTool.GetCurrentWeather);

    // Membuat tool dari method GetWeatherForecast
    var getWeatherForecastTool = AIFunctionFactory.Create(WeatherTool.GetWeatherForecast);

    // Daftar semua tools yang tersedia untuk agent
    var tools = new List<AITool> { getCurrentWeatherTool, getWeatherForecastTool };

    Console.WriteLine($"[INFO] {tools.Count} tools berhasil dibuat:");
    foreach (var tool in tools)
    {
        // Menampilkan nama dan metadata setiap tool yang terdaftar
        Console.WriteLine($"       - {tool.Name}: {tool.Description}");
    }
    Console.WriteLine();

    // --- Membuat agent dengan tools terdaftar ---
    // Tools didaftarkan melalui parameter tools pada AsAIAgent()
    Console.WriteLine("[INFO] Membuat agent dengan tools terdaftar...");

    // Membuat tools dengan logging bawaan menggunakan wrapper delegates
    // Setiap tool dibungkus dengan delegate yang mencatat panggilan ke console
    var loggingGetWeather = AIFunctionFactory.Create(
        (string cityName) =>
        {
            // Mencatat nama tool dan parameter yang dipanggil oleh agent
            Console.WriteLine($"  [TOOL CALL] Tool: GetCurrentWeather");
            Console.WriteLine($"              Parameter: cityName = {cityName}");

            // Menjalankan logic tool yang sebenarnya
            var result = WeatherTool.GetCurrentWeather(cityName);

            // Mencatat hasil eksekusi tool
            Console.WriteLine($"  [TOOL RESULT] GetCurrentWeather: {result}");

            // Mengembalikan hasil ke agent untuk melanjutkan response generation
            return result;
        },
        "GetCurrentWeather",
        "Mendapatkan informasi cuaca saat ini untuk kota di Indonesia. " +
        "Gunakan tool ini ketika user bertanya tentang cuaca, suhu, atau kondisi atmosfer suatu kota.");

    var loggingGetForecast = AIFunctionFactory.Create(
        (string cityName) =>
        {
            // Mencatat nama tool dan parameter yang dipanggil oleh agent
            Console.WriteLine($"  [TOOL CALL] Tool: GetWeatherForecast");
            Console.WriteLine($"              Parameter: cityName = {cityName}");

            // Menjalankan logic tool yang sebenarnya
            var result = WeatherTool.GetWeatherForecast(cityName);

            // Mencatat hasil eksekusi tool
            Console.WriteLine($"  [TOOL RESULT] GetWeatherForecast: {result}");

            // Mengembalikan hasil ke agent untuk melanjutkan response generation
            return result;
        },
        "GetWeatherForecast",
        "Mendapatkan prakiraan cuaca 3 hari ke depan untuk kota di Indonesia. " +
        "Gunakan tool ini ketika user bertanya tentang prakiraan atau rencana cuaca beberapa hari ke depan.");

    // Daftar tools dengan logging yang siap didaftarkan ke agent
    var loggingTools = new List<AITool> { loggingGetWeather, loggingGetForecast };

    // AsAIAgent menerima parameter: instructions, name, description, tools
    // Tools diregistrasi langsung ke agent saat pembuatan
    var agent = chatClient.AsAIAgent(
        instructions: "Kamu adalah asisten cuaca Indonesia yang membantu memberikan informasi cuaca. " +
                      "Gunakan tools yang tersedia untuk mendapatkan data cuaca. " +
                      "Jawab dalam bahasa Indonesia dengan format yang mudah dibaca. " +
                      "Jika kota tidak tersedia, sampaikan kota-kota yang tersedia.",
        name: "WeatherAgent",
        description: "Agent dengan kemampuan mengakses data cuaca",
        tools: loggingTools);

    Console.WriteLine("[INFO] Agent 'WeatherAgent' berhasil dibuat dengan tools terdaftar.");
    Console.WriteLine();

    // --- Mengirim prompt yang memicu penggunaan tool ---
    // Demonstrasi 1: Prompt yang memicu GetCurrentWeather
    Console.WriteLine("  ─── Demonstrasi 1: Memicu Tool GetCurrentWeather ─────────────");
    var prompt1 = "Bagaimana cuaca di Jakarta hari ini?";
    Console.WriteLine($"  User: \"{prompt1}\"");
    Console.WriteLine();

    await InvokeAgentWithToolLogging(agent, prompt1, cancellationToken);
    Console.WriteLine();

    // Demonstrasi 2: Prompt yang memicu GetWeatherForecast
    Console.WriteLine("  ─── Demonstrasi 2: Memicu Tool GetWeatherForecast ────────────");
    var prompt2 = "Berikan prakiraan cuaca Bandung 3 hari ke depan.";
    Console.WriteLine($"  User: \"{prompt2}\"");
    Console.WriteLine();

    await InvokeAgentWithToolLogging(agent, prompt2, cancellationToken);
    Console.WriteLine();

    // Demonstrasi 3: Prompt yang memicu multiple tool calls
    Console.WriteLine("  ─── Demonstrasi 3: Prompt Multi-Tool ─────────────────────────");
    var prompt3 = "Bandingkan cuaca Jakarta dan Surabaya saat ini.";
    Console.WriteLine($"  User: \"{prompt3}\"");
    Console.WriteLine();

    await InvokeAgentWithToolLogging(agent, prompt3, cancellationToken);
    Console.WriteLine();

    // Demonstrasi 4: Error handling - kota tidak tersedia
    Console.WriteLine("  ─── Demonstrasi 4: Tool dengan Input Tidak Valid ─────────────");
    var prompt4 = "Bagaimana cuaca di KotaFiktif?";
    Console.WriteLine($"  User: \"{prompt4}\"");
    Console.WriteLine();

    await InvokeAgentWithToolLogging(agent, prompt4, cancellationToken);
    Console.WriteLine();
}

// =============================================================================
// Bagian 2: Demonstrasi koneksi ke MCP Server
// MCP (Model Context Protocol) memungkinkan agent mengakses tools eksternal
// =============================================================================
static async Task DemonstrasikanMcpConnection(
    IConfiguration configuration,
    IChatClient chatClient,
    CancellationToken cancellationToken)
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 2: MCP Server Connection                              │");
    Console.WriteLine("│ Model Context Protocol memungkinkan agent menggunakan tools   │");
    Console.WriteLine("│ eksternal yang disediakan oleh MCP server terpisah.           │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Membaca konfigurasi MCP server dari appsettings.json
    var mcpCommand = configuration["McpServer:Command"] ?? "npx";
    var mcpArguments = configuration["McpServer:Arguments"] ?? "-y @modelcontextprotocol/server-everything";

    Console.WriteLine("[INFO] Konfigurasi MCP Server:");
    Console.WriteLine($"       Command: {mcpCommand}");
    Console.WriteLine($"       Arguments: {mcpArguments}");
    Console.WriteLine();

    // --- Mencoba koneksi ke MCP Server ---
    // MCP Client menggunakan stdio transport untuk berkomunikasi dengan server
    Console.WriteLine("[INFO] Mencoba koneksi ke MCP server...");

    try
    {
        // Membuat transport stdio - menjalankan proses MCP server dan berkomunikasi via stdin/stdout
        // StdioClientTransport mengelola lifecycle proses server
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "demo-mcp-server",
            Command = mcpCommand,
            Arguments = mcpArguments.Split(' ')
        });

        // Membuat MCP client dan melakukan handshake dengan server
        // CreateAsync menginisialisasi koneksi dan mendapatkan server capabilities
        await using var mcpClient = await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ClientInfo = new() { Name = "AddingTools-Demo", Version = "1.0.0" }
            },
            cancellationToken: cancellationToken);

        Console.WriteLine("[INFO] Koneksi ke MCP server berhasil!");
        Console.WriteLine();

        // Mendapatkan daftar tools yang tersedia dari MCP server
        // ListToolsAsync mengembalikan metadata semua tools yang didaftarkan server
        var toolsResult = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
        var mcpTools = toolsResult.ToList();

        Console.WriteLine($"[INFO] MCP server menyediakan {mcpTools.Count} tools:");
        foreach (var tool in mcpTools.Take(5)) // Tampilkan maksimal 5 tools
        {
            Console.WriteLine($"       - {tool.Name}: {tool.Description}");
        }
        if (mcpTools.Count > 5)
        {
            Console.WriteLine($"       ... dan {mcpTools.Count - 5} tools lainnya");
        }
        Console.WriteLine();

        // --- Membuat agent dengan kombinasi local tools dan MCP tools ---
        var localTools = new List<AITool>
        {
            AIFunctionFactory.Create(WeatherTool.GetCurrentWeather),
            AIFunctionFactory.Create(WeatherTool.GetWeatherForecast)
        };

        // Menggabungkan local tools dengan MCP tools untuk agent
        // McpClientTool mengimplementasikan AITool sehingga kompatibel langsung
        var combinedTools = new List<AITool>();
        combinedTools.AddRange(localTools);
        combinedTools.AddRange(mcpTools);

        // Membuat agent baru dengan semua tools (lokal + MCP)
        var mcpAgent = chatClient.AsAIAgent(
            instructions: "Kamu adalah asisten AI yang memiliki akses ke tools lokal dan MCP tools. " +
                          "Gunakan tools yang paling sesuai untuk menjawab pertanyaan user. " +
                          "Jawab dalam bahasa Indonesia.",
            name: "McpEnabledAgent",
            description: "Agent dengan local tools dan MCP tools",
            tools: combinedTools);

        Console.WriteLine("[INFO] Agent 'McpEnabledAgent' dibuat dengan local + MCP tools.");
        Console.WriteLine();

        // Demonstrasi invokasi MCP tool
        Console.WriteLine("  ─── Demonstrasi: Invokasi MCP Tool ──────────────────────────");
        var mcpPrompt = "Gunakan tool echo untuk mengirim pesan 'Hello from MCP!'";
        Console.WriteLine($"  User: \"{mcpPrompt}\"");
        Console.WriteLine();

        await InvokeAgentWithToolLogging(mcpAgent, mcpPrompt, cancellationToken);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        // Error handling untuk kegagalan koneksi MCP
        // Ini diharapkan jika MCP server tidak tersedia di environment saat ini
        Console.WriteLine($"[ERROR] Koneksi MCP gagal: {ex.GetType().Name}");
        Console.WriteLine($"[CAUSE] {ex.Message}");
        Console.WriteLine("[HINT] Pastikan Node.js terinstall dan jalankan: npm install -g @modelcontextprotocol/server-everything");
        Console.WriteLine("[INFO] Demonstrasi MCP dilewati. Function tools lokal tetap berfungsi tanpa MCP.");
    }

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("[INFO] Demonstrasi Adding Tools selesai.");
    Console.WriteLine("[INFO] Konsep yang dipelajari:");
    Console.WriteLine("       1. Membuat function tools dengan AIFunctionFactory.Create()");
    Console.WriteLine("       2. Mendaftarkan tools ke agent via parameter tools");
    Console.WriteLine("       3. Tool invocation logging (nama + parameter)");
    Console.WriteLine("       4. Error handling untuk tool execution failures");
    Console.WriteLine("       5. Koneksi ke MCP server untuk external tools");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
}

// =============================================================================
// Helper: Menjalankan agent dan mencatat semua tool calls ke console
// Mencatat nama tool, parameter, dan hasil eksekusi
// =============================================================================
static async Task InvokeAgentWithToolLogging(AIAgent agent, string prompt, CancellationToken cancellationToken)
{
    try
    {
        // Menjalankan agent - tool calls terjadi secara otomatis di dalam RunAsync
        // Agent framework menangani siklus: LLM memilih tool → eksekusi → kembalikan hasil → LLM lanjutkan
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
        // Menangani kegagalan tool execution
        // Menampilkan nama error dan alasan kegagalan sesuai format [ERROR]/[CAUSE]/[HINT]
        Console.WriteLine($"  [ERROR] Tool execution gagal: {ex.GetType().Name}");
        Console.WriteLine($"  [CAUSE] {ex.Message}");
        Console.WriteLine("  [HINT] Periksa parameter tool dan ketersediaan service.");
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
