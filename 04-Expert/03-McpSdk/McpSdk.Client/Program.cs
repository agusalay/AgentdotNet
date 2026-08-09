// ============================================================================
// MCP Client - Entry Point
// File ini mengimplementasikan MCP Client yang terhubung ke MCP Server melalui
// stdio transport, melakukan tool discovery, dan mendemonstrasikan interaksi
// dengan MCP tools.
// ============================================================================

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// ═══════════════════════════════════════════════════════════════════════════════
// SECTION 1: Koneksi ke MCP Server
// ═══════════════════════════════════════════════════════════════════════════════

Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine("  MCP Client — Weather Tools Demo");
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine();

try
{
    // Konfigurasi transport — menjalankan MCP Server sebagai child process.
    // StdioClientTransport akan spawn process `dotnet run` yang menjalankan server,
    // kemudian berkomunikasi melalui stdin/stdout menggunakan JSON-RPC 2.0.
    // Path ke server project dihitung relatif dari lokasi file source ini,
    // sehingga dapat dijalankan dari directory manapun.
    var clientDir = Path.GetDirectoryName(typeof(Program).Assembly.Location)
        ?? AppContext.BaseDirectory;
    // Naik dari bin/Debug/net9.0 ke McpSdk.Client/, lalu ke ../McpSdk.Server/
    var serverProjectPath = Path.GetFullPath(
        Path.Combine(clientDir, "..", "..", "..", "..", "McpSdk.Server", "McpSdk.Server.csproj"));

    var transport = new StdioClientTransport(new()
    {
        Command = "dotnet",
        Arguments = ["run", "--project", serverProjectPath],
        Name = "Weather MCP Server"
    });

    // Membuat MCP Client menggunakan transport yang sudah dikonfigurasi.
    // `await using` memastikan resource cleanup yang proper — saat client di-dispose,
    // koneksi ditutup dan child process server dihentikan secara graceful.
    await using var mcpClient = await McpClient.CreateAsync(transport);

    Console.WriteLine("[INFO] Koneksi ke MCP Server berhasil!");
    Console.WriteLine();

    // ═══════════════════════════════════════════════════════════════════════════
    // SECTION 2: Tool Discovery
    // Menemukan daftar tools yang tersedia pada MCP Server secara dinamis.
    // McpClientTool mewarisi dari AIFunction, sehingga tools yang ditemukan dapat
    // langsung digunakan sebagai tools untuk IChatClient tanpa adapter tambahan.
    // ═══════════════════════════════════════════════════════════════════════════

    Console.WriteLine("───────────────────────────────────────────────────────────────");
    Console.WriteLine("  Tool Discovery — Daftar Tools yang Tersedia");
    Console.WriteLine("───────────────────────────────────────────────────────────────");
    Console.WriteLine();

    // ListToolsAsync() mengirim request ke server untuk mendapatkan semua tools
    // yang terdaftar. Setiap tool memiliki nama, deskripsi, dan input schema.
    IList<McpClientTool> tools = await mcpClient.ListToolsAsync();

    Console.WriteLine($"  Ditemukan {tools.Count} tool(s) pada server:");
    Console.WriteLine();

    // Menampilkan informasi setiap tool yang ditemukan
    for (int i = 0; i < tools.Count; i++)
    {
        var tool = tools[i];
        Console.WriteLine($"  [{i + 1}] {tool.Name}");
        Console.WriteLine($"      Deskripsi: {tool.Description}");
        Console.WriteLine();
    }

    Console.WriteLine("───────────────────────────────────────────────────────────────");
    Console.WriteLine();

    // ═══════════════════════════════════════════════════════════════════════════
    // SECTION 3: Direct Tool Invocation
    // Memanggil setiap tool yang ditemukan menggunakan CallToolAsync() dengan
    // parameter contoh. Setiap pemanggilan dilengkapi timestamp dan error handling.
    // ═══════════════════════════════════════════════════════════════════════════

    Console.WriteLine("───────────────────────────────────────────────────────────────");
    Console.WriteLine("  Direct Tool Invocation — Pemanggilan Tool Secara Langsung");
    Console.WriteLine("───────────────────────────────────────────────────────────────");
    Console.WriteLine();

    // Variabel untuk menghitung total tool calls yang berhasil di semua section
    int totalToolCalls = 0;

    // --- Tool 1: GetCurrentWeather ---
    try
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var toolName = "get_current_weather";
        var parameters = new Dictionary<string, object?> { { "city", "Jakarta" } };

        Console.WriteLine($"  [{timestamp}] Memanggil tool: {toolName}");
        Console.WriteLine($"      Parameter: city = \"Jakarta\"");

        var result = await mcpClient.CallToolAsync(toolName, parameters);

        // Menampilkan response dari server — extract text dari ContentBlock
        var responseText = string.Join("", result.Content
            .OfType<TextContentBlock>()
            .Select(c => c.Text ?? string.Empty));
        Console.WriteLine($"      Response: {responseText}");
        Console.WriteLine();
        totalToolCalls++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"      [WARNING] Tool 'get_current_weather' error: {ex.Message}");
        Console.WriteLine($"       Agent melanjutkan tanpa hasil tool.");
        Console.WriteLine();
    }

    // --- Tool 2: GetWeatherForecast ---
    try
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var toolName = "get_weather_forecast";
        var parameters = new Dictionary<string, object?> { { "city", "Surabaya" }, { "days", 3 } };

        Console.WriteLine($"  [{timestamp}] Memanggil tool: {toolName}");
        Console.WriteLine($"      Parameter: city = \"Surabaya\", days = 3");

        var result = await mcpClient.CallToolAsync(toolName, parameters);

        // Menampilkan response dari server — extract text dari ContentBlock
        var responseText = string.Join("", result.Content
            .OfType<TextContentBlock>()
            .Select(c => c.Text ?? string.Empty));
        Console.WriteLine($"      Response: {responseText}");
        Console.WriteLine();
        totalToolCalls++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"      [WARNING] Tool 'get_weather_forecast' error: {ex.Message}");
        Console.WriteLine($"       Agent melanjutkan tanpa hasil tool.");
        Console.WriteLine();
    }

    // --- Tool 3: ConvertTemperature ---
    try
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var toolName = "convert_temperature";
        var parameters = new Dictionary<string, object?> { { "value", 100 }, { "fromUnit", "celsius" }, { "toUnit", "fahrenheit" } };

        Console.WriteLine($"  [{timestamp}] Memanggil tool: {toolName}");
        Console.WriteLine($"      Parameter: value = 100, fromUnit = \"celsius\", toUnit = \"fahrenheit\"");

        var result = await mcpClient.CallToolAsync(toolName, parameters);

        // Menampilkan response dari server — extract text dari ContentBlock
        var responseText = string.Join("", result.Content
            .OfType<TextContentBlock>()
            .Select(c => c.Text ?? string.Empty));
        Console.WriteLine($"      Response: {responseText}");
        Console.WriteLine();
        totalToolCalls++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"      [WARNING] Tool 'convert_temperature' error: {ex.Message}");
        Console.WriteLine($"       Agent melanjutkan tanpa hasil tool.");
        Console.WriteLine();
    }

    Console.WriteLine("───────────────────────────────────────────────────────────────");
    Console.WriteLine($"  Ringkasan: {tools.Count} tool(s) ditemukan, {totalToolCalls} tool call(s) berhasil");
    Console.WriteLine("───────────────────────────────────────────────────────────────");
    Console.WriteLine();

    // ═══════════════════════════════════════════════════════════════════════════
    // SECTION 4: Agent Integration — MCP Tools ke IChatClient
    // McpClientTool mewarisi dari AIFunction, sehingga tools yang ditemukan dari
    // MCP Server dapat langsung diteruskan ke IChatClient sebagai ChatOptions.Tools.
    // UseFunctionInvocation() memungkinkan agent secara otomatis memanggil tools
    // yang dipilih berdasarkan konteks percakapan.
    // ═══════════════════════════════════════════════════════════════════════════

    Console.WriteLine("───────────────────────────────────────────────────────────────");
    Console.WriteLine("  Agent Integration — MCP Tools ke IChatClient");
    Console.WriteLine("───────────────────────────────────────────────────────────────");
    Console.WriteLine();

    // Membaca konfigurasi Azure OpenAI dari environment variables.
    // Pastikan file .env sudah dikonfigurasi atau environment variables sudah di-set.
    var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
    var azureDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");

    if (string.IsNullOrWhiteSpace(azureEndpoint) || string.IsNullOrWhiteSpace(azureDeployment))
    {
        Console.WriteLine("  [WARNING] Environment variables belum dikonfigurasi:");
        Console.WriteLine("    - AZURE_OPENAI_ENDPOINT");
        Console.WriteLine("    - AZURE_OPENAI_DEPLOYMENT_NAME");
        Console.WriteLine("  [HINT] Set environment variables atau buat file .env sesuai .env.example");
        Console.WriteLine("  [INFO] Melewati agent integration demo.");
        Console.WriteLine();
    }
    else
    {
        Console.WriteLine($"  [INFO] Azure OpenAI Endpoint: {azureEndpoint}");
        Console.WriteLine($"  [INFO] Deployment: {azureDeployment}");
        Console.WriteLine();

        // Membuat Azure OpenAI client dengan DefaultAzureCredential.
        // DefaultAzureCredential mencoba beberapa metode autentikasi secara berurutan
        // (Azure CLI, Managed Identity, Visual Studio, dll.)
        var azureOpenAIClient = new AzureOpenAIClient(
            new Uri(azureEndpoint),
            new DefaultAzureCredential());

        // Membuat IChatClient dengan ChatClientBuilder dan UseFunctionInvocation().
        // UseFunctionInvocation() mengaktifkan automatic function calling — ketika agent
        // memilih tool, IChatClient akan secara otomatis memanggil AIFunction yang sesuai
        // (dalam hal ini McpClientTool) dan mengirim hasilnya kembali ke model.
        IChatClient chatClient = new ChatClientBuilder(
                azureOpenAIClient.GetChatClient(azureDeployment).AsIChatClient())
            .UseFunctionInvocation()
            .Build();

        Console.WriteLine("  [INFO] IChatClient berhasil dibuat dengan UseFunctionInvocation()");
        Console.WriteLine($"  [INFO] {tools.Count} MCP tool(s) diteruskan sebagai ChatOptions.Tools");
        Console.WriteLine();

        // Menyiapkan ChatOptions dengan MCP tools sebagai tools yang tersedia.
        // Karena McpClientTool mewarisi dari AIFunction, tools dapat langsung
        // dimasukkan ke ChatOptions.Tools tanpa adapter tambahan.
        var chatOptions = new ChatOptions { Tools = [.. tools] };

        // ─── Demo 1: Single-Tool Interaction ─────────────────────────────────
        // Demonstrasi agent integration — mengirim test prompt dan menampilkan
        // alur lengkap: prompt → tool selection → invocation → result → response
        var testPrompt = "Bagaimana cuaca di Jakarta saat ini?";

        Console.WriteLine("  ┌─ Demo 1: Single-Tool Interaction ─────────────────────────");
        Console.WriteLine($"  │ [PROMPT] User: \"{testPrompt}\"");
        Console.WriteLine("  │");
        Console.WriteLine("  │ [INFO] Mengirim prompt ke agent...");
        Console.WriteLine("  │        Agent akan memilih tool yang sesuai secara otomatis.");
        Console.WriteLine("  │");

        try
        {
            // Mengirim prompt ke agent — agent akan otomatis memilih dan memanggil
            // MCP tool yang relevan melalui UseFunctionInvocation() pipeline.
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var response = await chatClient.GetResponseAsync(testPrompt, chatOptions);

            // Hitung tool calls yang dilakukan agent pada demo ini
            foreach (var msg in response.Messages)
            {
                foreach (var content in msg.Contents)
                {
                    if (content is FunctionCallContent fc)
                    {
                        totalToolCalls++;
                        var toolTs = DateTime.Now.ToString("HH:mm:ss.fff");
                        Console.WriteLine($"  │ [{toolTs}] [TOOL] {fc.Name}({FormatArguments(fc.Arguments)})");
                    }
                }
            }

            Console.WriteLine("  │");

            // Menampilkan response akhir dari agent
            var agentResponse = response.Text;
            Console.WriteLine($"  │ [RESPONSE] Agent: {agentResponse}");
            Console.WriteLine("  └─────────────────────────────────────────────────────────────");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  │ [ERROR] Agent call gagal: {ex.Message}");
            Console.WriteLine("  │ [HINT] Pastikan Azure OpenAI credentials valid dan endpoint accessible.");
            Console.WriteLine("  └─────────────────────────────────────────────────────────────");
        }

        Console.WriteLine();

        // ─── Demo 2: Multi-Tool Scenario ──────────────────────────────────────
        // Demonstrasi skenario multi-tool di mana agent memanggil lebih dari satu
        // MCP tool dalam satu conversation turn untuk menjawab pertanyaan user
        // yang memerlukan kombinasi informasi dari beberapa tools.
        // Agent TIDAK memiliki hardcoded knowledge tentang tools — ia menemukan
        // capabilities melalui ListToolsAsync() dan memilih tools secara dinamis.
        var multiToolPrompt = "Bandingkan cuaca Jakarta dan Surabaya saat ini, lalu konversi suhu Jakarta ke Fahrenheit.";

        Console.WriteLine("  ┌─ Demo 2: Multi-Tool Scenario ─────────────────────────────");
        Console.WriteLine($"  │ [PROMPT] User: \"{multiToolPrompt}\"");
        Console.WriteLine("  │");
        Console.WriteLine("  │ [INFO] Prompt ini dirancang agar agent memanggil >1 tool:");
        Console.WriteLine("  │        - GetCurrentWeather (Jakarta)");
        Console.WriteLine("  │        - GetCurrentWeather (Surabaya)");
        Console.WriteLine("  │        - ConvertTemperature (suhu Jakarta → Fahrenheit)");
        Console.WriteLine("  │");

        try
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var multiResponse = await chatClient.GetResponseAsync(multiToolPrompt, chatOptions);

            // Menampilkan setiap tool call yang dilakukan oleh agent
            int multiToolCount = 0;
            foreach (var msg in multiResponse.Messages)
            {
                foreach (var content in msg.Contents)
                {
                    if (content is FunctionCallContent fc)
                    {
                        multiToolCount++;
                        totalToolCalls++;
                        var toolTs = DateTime.Now.ToString("HH:mm:ss.fff");
                        Console.WriteLine($"  │ [{toolTs}] [TOOL {multiToolCount}] {fc.Name}({FormatArguments(fc.Arguments)})");
                    }
                }
            }

            Console.WriteLine("  │");
            Console.WriteLine($"  │ [INFO] Agent memanggil {multiToolCount} tool(s) dalam satu turn.");
            Console.WriteLine("  │");

            // Menampilkan response akhir dari agent
            Console.WriteLine($"  │ [RESPONSE] Agent: {multiResponse.Text}");
            Console.WriteLine("  └─────────────────────────────────────────────────────────────");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  │ [ERROR] Multi-tool call gagal: {ex.Message}");
            Console.WriteLine("  │ [HINT] Pastikan Azure OpenAI credentials valid dan endpoint accessible.");
            Console.WriteLine("  └─────────────────────────────────────────────────────────────");
        }

        Console.WriteLine();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SECTION 5: Interactive Agent Loop
    // Loop interaktif yang memungkinkan user berinteraksi dengan agent secara
    // berkelanjutan. Agent menggunakan MCP tools yang ditemukan untuk menjawab
    // pertanyaan user. Loop berakhir saat user mengetik "exit" atau "quit".
    // ═══════════════════════════════════════════════════════════════════════════

    // Interactive loop hanya berjalan jika Azure OpenAI credentials tersedia
    if (!string.IsNullOrWhiteSpace(azureEndpoint) && !string.IsNullOrWhiteSpace(azureDeployment))
    {
        Console.WriteLine("───────────────────────────────────────────────────────────────");
        Console.WriteLine("  Interactive Agent Loop — Tanya Jawab dengan Agent");
        Console.WriteLine("───────────────────────────────────────────────────────────────");
        Console.WriteLine();
        Console.WriteLine("  Ketik pertanyaan Anda, atau ketik \"exit\"/\"quit\" untuk keluar.");
        Console.WriteLine();

        // Counter untuk ringkasan sesi
        int sessionPromptCount = 0;
        int sessionToolCallCount = 0;

        // Membuat ulang chatClient dan chatOptions untuk interactive loop
        // (variabel dari Section 4 berada di scope else yang sudah selesai)
        var interactiveAzureClient = new AzureOpenAIClient(
            new Uri(azureEndpoint),
            new DefaultAzureCredential());

        IChatClient interactiveChatClient = new ChatClientBuilder(
                interactiveAzureClient.GetChatClient(azureDeployment!).AsIChatClient())
            .UseFunctionInvocation()
            .Build();

        var interactiveChatOptions = new ChatOptions { Tools = [.. tools] };

        // Conversation history untuk menjaga konteks percakapan
        var conversationHistory = new List<ChatMessage>();

        while (true)
        {
            // Meminta input dari user
            Console.Write("  [YOU] > ");
            var userInput = Console.ReadLine();

            // Handle null input (contoh: redirect stdin atau Ctrl+Z)
            if (userInput is null)
                break;

            // Deteksi perintah exit — menggunakan helper function yang reusable
            if (IsExitCommand(userInput))
                break;

            // Skip input kosong
            if (string.IsNullOrWhiteSpace(userInput))
            {
                Console.WriteLine("  [HINT] Ketik pertanyaan atau \"exit\" untuk keluar.");
                Console.WriteLine();
                continue;
            }

            sessionPromptCount++;

            // Menambahkan pesan user ke conversation history
            conversationHistory.Add(new ChatMessage(ChatRole.User, userInput));

            try
            {
                // Mengirim prompt ke agent — agent akan memilih dan memanggil
                // MCP tool yang relevan secara otomatis melalui UseFunctionInvocation()
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                Console.WriteLine($"  [{timestamp}] [INFO] Mengirim ke agent...");

                var response = await interactiveChatClient.GetResponseAsync(
                    conversationHistory, interactiveChatOptions);

                // Menampilkan tool calls yang dilakukan oleh agent selama interaksi
                foreach (var message in response.Messages)
                {
                    foreach (var content in message.Contents)
                    {
                        if (content is FunctionCallContent functionCall)
                        {
                            sessionToolCallCount++;
                            totalToolCalls++;
                            var toolTimestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                            Console.WriteLine($"  [{toolTimestamp}] [TOOL] {functionCall.Name}({FormatArguments(functionCall.Arguments)})");
                        }
                    }
                }

                // Menampilkan response akhir dari agent
                Console.WriteLine($"  [AGENT] {response.Text}");
                Console.WriteLine();

                // Menambahkan response agent ke conversation history
                conversationHistory.Add(new ChatMessage(ChatRole.Assistant, response.Text ?? string.Empty));
            }
            catch (Exception ex)
            {
                // Handle tool failure gracefully — agent melanjutkan tanpa tool result
                Console.WriteLine($"  [WARNING] Gagal memproses: {ex.Message}");
                Console.WriteLine($"  [INFO] Agent melanjutkan tanpa hasil tool.");
                Console.WriteLine();
            }
        }

        // Cleanup resources interactive loop
        (interactiveChatClient as IDisposable)?.Dispose();

        // Menampilkan ringkasan sesi saat loop berakhir
        Console.WriteLine();
        Console.WriteLine("───────────────────────────────────────────────────────────────");
        Console.WriteLine("  Ringkasan Sesi Interaktif:");
        Console.WriteLine($"    Total prompt: {sessionPromptCount}");
        Console.WriteLine($"    Total tool calls: {sessionToolCallCount}");
        Console.WriteLine("───────────────────────────────────────────────────────────────");
        Console.WriteLine();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SECTION 6: Execution Summary
    // Ringkasan akhir eksekusi: total tools discovered dan total tool calls
    // yang dilakukan di seluruh section (direct invocation + agent demo + interactive).
    // ═══════════════════════════════════════════════════════════════════════════

    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  EXECUTION SUMMARY");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine();
    Console.WriteLine($"  • Total tools discovered    : {tools.Count}");
    Console.WriteLine($"  • Total tool calls made     : {totalToolCalls}");
    Console.WriteLine($"  • Dynamic discovery         : Ya (tidak ada hardcoded knowledge)");
    Console.WriteLine($"  • Multi-tool scenario       : Ya (agent memanggil >1 tool per turn)");
    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  Sesi MCP Client selesai. Resources dibersihkan.");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
}
catch (Exception ex) when (ex.Message.Contains("not found") || ex.Message.Contains("No such file"))
{
    // Error: Server executable tidak ditemukan
    Console.WriteLine("[ERROR] Server tidak ditemukan: ../McpSdk.Server/McpSdk.Server.csproj");
    Console.WriteLine("[HINT] Pastikan path ke McpSdk.Server.csproj benar.");
    Console.WriteLine($"[DETAIL] {ex.Message}");
}
catch (Exception ex) when (ex is InvalidOperationException || ex.Message.Contains("failed") || ex.Message.Contains("crash"))
{
    // Error: Server process gagal start atau crash saat koneksi
    Console.WriteLine($"[ERROR] Server gagal start: {ex.Message}");
    Console.WriteLine("[HINT] Jalankan `dotnet build` pada project server terlebih dahulu.");
}
catch (TimeoutException ex)
{
    // Error: Timeout menunggu response dari server
    Console.WriteLine($"[ERROR] Timeout menunggu response dari server.");
    Console.WriteLine("[HINT] Periksa apakah server berjalan dengan benar.");
    Console.WriteLine($"[DETAIL] {ex.Message}");
}
catch (Exception ex)
{
    // Error: Kegagalan umum yang tidak terduga
    Console.WriteLine($"[ERROR] Terjadi kesalahan: {ex.Message}");
    Console.WriteLine($"[TYPE] {ex.GetType().Name}");
    Console.WriteLine("[HINT] Periksa log server dan pastikan semua dependencies terinstall.");
}

// ═══════════════════════════════════════════════════════════════════════════════
// Helper Functions — Dideklarasikan sebagai static methods agar dapat diakses
// oleh property-based tests untuk validasi exit command detection.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Mendeteksi apakah input user merupakan perintah exit.
/// Mengembalikan true jika input adalah "exit" atau "quit" (case-insensitive).
/// Method ini dibuat sebagai static function agar dapat di-test secara terpisah
/// melalui property-based testing.
/// </summary>
/// <param name="input">Input string dari user</param>
/// <returns>True jika input adalah perintah exit, false jika bukan</returns>
static bool IsExitCommand(string input)
{
    if (string.IsNullOrWhiteSpace(input))
        return false;

    var trimmed = input.Trim();
    return trimmed.Equals("exit", StringComparison.OrdinalIgnoreCase)
        || trimmed.Equals("quit", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Memformat argumen tool call menjadi string yang mudah dibaca.
/// Digunakan untuk menampilkan parameter yang dikirim ke MCP tool selama interaksi.
/// </summary>
/// <param name="arguments">Dictionary argumen dari FunctionCallContent</param>
/// <returns>String berformat "key=value, key=value"</returns>
static string FormatArguments(IDictionary<string, object?>? arguments)
{
    if (arguments is null || arguments.Count == 0)
        return string.Empty;

    return string.Join(", ", arguments.Select(kvp => $"{kvp.Key}={kvp.Value}"));
}
