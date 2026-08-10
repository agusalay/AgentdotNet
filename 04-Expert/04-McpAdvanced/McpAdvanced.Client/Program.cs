// Program.cs - Entry point untuk MCP Advanced Client
// Client terhubung ke McpAdvanced.Server via Streamable HTTP Transport
// Mendemonstrasikan: capabilities negotiation, resources, prompts, tools,
// progress tracking, cancellation, sampling, elicitation, dan MRTR

#pragma warning disable MCP9005 // Suppress deprecation warnings untuk Sampling, Roots, Logging

using McpAdvanced.Client.Handlers;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

Console.OutputEncoding = System.Text.Encoding.UTF8;
PrintHeader();

// Membuat koneksi ke MCP server dengan penanganan error yang informatif
McpClient? client = null;
try
{
    client = await ConnectToServerAsync();
}
catch (HttpRequestException ex)
{
    // Koneksi gagal — tampilkan pesan troubleshooting
    PrintConnectionError(ex);
    return;
}
catch (TimeoutException ex)
{
    PrintTimeoutError(ex);
    return;
}
catch (Exception ex)
{
    PrintGenericError(ex);
    return;
}

// Koneksi berhasil — tampilkan informasi server dan capabilities
await using (client)
{
    DisplayServerInfo(client);
    DisplayNegotiatedCapabilities(client);
    await RunInteractiveMenuAsync(client);
}

Console.WriteLine("\n✅ Client selesai. Terima kasih!");
return;

// =============================================================================
// Fungsi Koneksi ke Server
// =============================================================================

/// <summary>
/// Membuat koneksi ke MCP server menggunakan HttpClientTransport.
/// Mengonfigurasi client capabilities: Sampling, Roots, Elicitation.
/// SECURITY: InheritEnvironmentVariables = false — tidak digunakan di HTTP transport,
/// tetapi prinsip least privilege diterapkan via explicit env var mapping.
/// </summary>
async Task<McpClient> ConnectToServerAsync()
{
    Console.WriteLine("🔌 Menghubungkan ke server di http://localhost:5100/mcp ...");
    Console.WriteLine();

    // Konfigurasi transport HTTP — koneksi ke server yang sudah berjalan
    // SECURITY: Hanya variabel lingkungan yang diperlukan yang diteruskan secara eksplisit
    var transportOptions = new HttpClientTransportOptions
    {
        Endpoint = new Uri("http://localhost:5100/mcp"),
        Name = "McpAdvanced.Client"
    };
    var transport = new HttpClientTransport(transportOptions);

    // Konfigurasi client options dengan capabilities yang didukung
    // Client mendeklarasikan: Sampling, Roots, Elicitation
    // Server akan menyesuaikan behavior berdasarkan capabilities ini
    var clientOptions = new McpClientOptions
    {
        ClientInfo = new Implementation { Name = "McpAdvanced.Client", Version = "1.0.0" },
        Capabilities = new ClientCapabilities
        {
            Sampling = new SamplingCapability(),
            Roots = new RootsCapability { ListChanged = true },
            Elicitation = new ElicitationCapability()
        },
        Handlers = new McpClientHandlers
        {
            // Handler Sampling — server meminta LLM completion dari client
            SamplingHandler = SamplingHandler.HandleAsync,
            // Handler Elicitation — server meminta informasi tambahan dari user
            ElicitationHandler = ElicitationHandler.HandleAsync,
            // Handler Roots — client menyediakan filesystem roots ke server
            RootsHandler = (_, _) => ValueTask.FromResult(new ListRootsResult
            {
                Roots = [new Root { Uri = "file:///workspace", Name = "Workspace" }]
            })
        }
    };

    // Membuat koneksi dan melakukan handshake (capabilities negotiation)
    return await McpClient.CreateAsync(transport, clientOptions);
}

// =============================================================================
// Fungsi Tampilan Informasi Server
// =============================================================================

void DisplayServerInfo(McpClient mcpClient)
{
    Console.WriteLine("════════════════════════════════════════════════════════════════");
    Console.WriteLine("  📡 TERHUBUNG KE SERVER");
    Console.WriteLine("════════════════════════════════════════════════════════════════");

    try
    {
        var serverInfo = mcpClient.ServerInfo;
        Console.WriteLine($"  Server     : {serverInfo.Name} v{serverInfo.Version}");
    }
    catch (InvalidOperationException)
    {
        Console.WriteLine("  Server     : (informasi tidak tersedia)");
    }

    var instructions = mcpClient.ServerInstructions;
    if (!string.IsNullOrEmpty(instructions))
        Console.WriteLine($"  Instruksi  : {instructions}");

    Console.WriteLine($"  Protocol   : {mcpClient.NegotiatedProtocolVersion ?? "default"}");
    Console.WriteLine();
}

void DisplayNegotiatedCapabilities(McpClient mcpClient)
{
    Console.WriteLine("  🤝 CAPABILITIES NEGOTIATION");
    Console.WriteLine("  ─────────────────────────────────────────────────────────────");

    var caps = mcpClient.ServerCapabilities;
    Console.WriteLine("  Server Capabilities:");
    Console.WriteLine($"    • Tools        : {(caps.Tools is not null ? "✓" : "✗")}");
    Console.WriteLine($"    • Resources    : {(caps.Resources is not null ? "✓" : "✗")}");
    Console.WriteLine($"    • Prompts      : {(caps.Prompts is not null ? "✓" : "✗")}");
    Console.WriteLine($"    • Logging      : {(caps.Logging is not null ? "✓" : "✗")}");
    Console.WriteLine($"    • Completions  : {(caps.Completions is not null ? "✓" : "✗")}");
    Console.WriteLine();
    Console.WriteLine("  Client Capabilities (declared):");
    Console.WriteLine("    • Sampling     : ✓");
    Console.WriteLine("    • Roots        : ✓ (ListChanged)");
    Console.WriteLine("    • Elicitation  : ✓");
    Console.WriteLine("════════════════════════════════════════════════════════════════");
    Console.WriteLine();
}

// =============================================================================
// Interactive Menu Loop
// =============================================================================

async Task RunInteractiveMenuAsync(McpClient mcpClient)
{
    var running = true;
    while (running)
    {
        PrintMenu();
        var choice = Console.ReadLine()?.Trim();
        Console.WriteLine();

        switch (choice)
        {
            case "1": await BrowseResourcesAsync(mcpClient); break;
            case "2": await UsePromptsAsync(mcpClient); break;
            case "3": await CallToolsAsync(mcpClient); break;
            case "4": await TestCancellationAsync(mcpClient); break;
            case "5": await TestSamplingAsync(mcpClient); break;
            case "6": await TestElicitationAsync(mcpClient); break;
            case "7": await TestMrtrAsync(mcpClient); break;
            case "0": running = false; break;
            default: Console.WriteLine("  ⚠️  Pilihan tidak valid. Coba lagi."); break;
        }
    }
}

// =============================================================================
// Menu Option 1: Browse Resources
// =============================================================================

async Task BrowseResourcesAsync(McpClient mcpClient)
{
    PrintSectionHeader("📚 BROWSE RESOURCES");
    try
    {
        var resources = await mcpClient.ListResourcesAsync();
        Console.WriteLine($"  Direct Resources ({resources.Count}):");
        for (var i = 0; i < resources.Count; i++)
            Console.WriteLine($"    [{i + 1}] {resources[i].Name} — {resources[i].Uri}");

        var templates = await mcpClient.ListResourceTemplatesAsync();
        Console.WriteLine($"\n  Resource Templates ({templates.Count}):");
        foreach (var tpl in templates)
            Console.WriteLine($"    • {tpl.Name} — {tpl.UriTemplate}");

        Console.Write("\n  Masukkan nomor resource untuk dibaca (atau Enter untuk skip): ");
        var input = Console.ReadLine()?.Trim();
        if (int.TryParse(input, out var idx) && idx >= 1 && idx <= resources.Count)
        {
            var selected = resources[idx - 1];
            Console.WriteLine($"\n  📖 Membaca: {selected.Uri}");
            var result = await mcpClient.ReadResourceAsync(selected.Uri);
            foreach (var content in result.Contents)
            {
                var text = (content as TextResourceContents)?.Text ?? "[binary content]";
                Console.WriteLine($"  ─── Content (MimeType: {content.MimeType}) ───");
                Console.WriteLine($"  {Truncate(text, 500)}");
            }
        }
    }
    catch (Exception ex) { Console.WriteLine($"  ❌ Error: {ex.Message}"); }
    Console.WriteLine();
}

// =============================================================================
// Menu Option 2: Use Prompts
// =============================================================================

async Task UsePromptsAsync(McpClient mcpClient)
{
    PrintSectionHeader("💬 USE PROMPTS");
    try
    {
        var prompts = await mcpClient.ListPromptsAsync();
        Console.WriteLine($"  Available Prompts ({prompts.Count}):");
        for (var i = 0; i < prompts.Count; i++)
        {
            var p = prompts[i];
            var promptArgs = p.ProtocolPrompt.Arguments;
            var argStr = promptArgs is { Count: > 0 }
                ? string.Join(", ", promptArgs.Select(a => $"{a.Name}{(a.Required == true ? "*" : "")}"))
                : "";
            Console.WriteLine($"    [{i + 1}] {p.Name}({argStr})");
            if (!string.IsNullOrEmpty(p.Description))
                Console.WriteLine($"        {p.Description}");
        }

        Console.Write("\n  Masukkan nomor prompt (atau Enter untuk skip): ");
        var input = Console.ReadLine()?.Trim();
        if (int.TryParse(input, out var idx) && idx >= 1 && idx <= prompts.Count)
        {
            var selected = prompts[idx - 1];
            var arguments = new Dictionary<string, object?>();
            var promptArgs = selected.ProtocolPrompt.Arguments;

            if (promptArgs is { Count: > 0 })
            {
                Console.WriteLine($"\n  Parameter untuk '{selected.Name}':");
                foreach (var arg in promptArgs)
                {
                    Console.Write($"    {arg.Name}{(arg.Required == true ? " (wajib)" : " (opsional)")}: ");
                    var val = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(val))
                        arguments[arg.Name] = val;
                }
            }

            Console.WriteLine($"\n  📝 Memanggil prompt '{selected.Name}'...");
            var result = await mcpClient.GetPromptAsync(selected.Name, arguments);
            Console.WriteLine("  ─── Prompt Result ───");
            foreach (var msg in result.Messages)
            {
                var text = GetContentText(msg.Content);
                Console.WriteLine($"  [{msg.Role}]: {Truncate(text, 300)}");
            }
        }
    }
    catch (Exception ex) { Console.WriteLine($"  ❌ Error: {ex.Message}"); }
    Console.WriteLine();
}

// =============================================================================
// Menu Option 3: Call Tools
// =============================================================================

async Task CallToolsAsync(McpClient mcpClient)
{
    PrintSectionHeader("🔧 CALL TOOLS");
    try
    {
        var tools = await mcpClient.ListToolsAsync();
        Console.WriteLine($"  Available Tools ({tools.Count}):");
        for (var i = 0; i < tools.Count; i++)
        {
            var desc = Truncate(tools[i].Description ?? "", 60);
            Console.WriteLine($"    [{i + 1}] {tools[i].Name} — {desc}");
        }

        Console.Write("\n  Masukkan nomor tool (atau Enter untuk skip): ");
        var input = Console.ReadLine()?.Trim();
        if (int.TryParse(input, out var idx) && idx >= 1 && idx <= tools.Count)
        {
            var selected = tools[idx - 1];
            Console.WriteLine($"\n  🔧 Tool: '{selected.Name}'");

            // Mengumpulkan argumen dari user
            var arguments = new Dictionary<string, object?>();
            Console.WriteLine("  Masukkan argumen (format: nama=nilai, Enter kosong untuk selesai):");
            while (true)
            {
                Console.Write("    > ");
                var line = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(line)) break;
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                    arguments[parts[0].Trim()] = parts[1].Trim();
                else
                    Console.WriteLine("    Format: nama=nilai");
            }

            // Progress handler — menampilkan progress dari server
            var progress = new Progress<ProgressNotificationValue>(p =>
            {
                Console.WriteLine($"  ⏳ Progress: {p.Message ?? "processing..."}");
            });

            var result = await mcpClient.CallToolAsync(selected.Name, arguments, progress);
            Console.WriteLine($"\n  ─── Tool Result (IsError: {result.IsError}) ───");
            foreach (var content in result.Content)
                Console.WriteLine($"  {GetBlockText(content)}");
        }
    }
    catch (Exception ex) { Console.WriteLine($"  ❌ Error: {ex.Message}"); }
    Console.WriteLine();
}

// =============================================================================
// Menu Option 4: Test Cancellation
// =============================================================================

async Task TestCancellationAsync(McpClient mcpClient)
{
    PrintSectionHeader("🚫 TEST CANCELLATION");
    try
    {
        Console.WriteLine("  Memulai BulkProcessArticles dan membatalkan setelah 2 detik...");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var progress = new Progress<ProgressNotificationValue>(p =>
        {
            Console.WriteLine($"  ⏳ [{DateTime.Now:HH:mm:ss.fff}] {p.Message ?? "processing..."}");
        });

        var arguments = new Dictionary<string, object?>
        {
            ["articleIds"] = "[\"art-1\",\"art-2\",\"art-3\",\"art-4\",\"art-5\"]",
            ["operation"] = "validate"
        };

        try
        {
            var result = await mcpClient.CallToolAsync(
                "BulkProcessArticles", arguments, progress, cancellationToken: cts.Token);
            Console.WriteLine($"  ✅ Hasil: {GetBlockText(result.Content.FirstOrDefault())}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("  🚫 Operasi berhasil dibatalkan!");
            Console.WriteLine("  Server menghentikan processing secara graceful.");
        }
    }
    catch (Exception ex) { Console.WriteLine($"  ❌ Error: {ex.Message}"); }
    Console.WriteLine();
}

// =============================================================================
// Menu Option 5: Test Sampling
// =============================================================================

async Task TestSamplingAsync(McpClient mcpClient)
{
    PrintSectionHeader("🤖 TEST SAMPLING");
    try
    {
        Console.WriteLine("  Memanggil AutoCategorizeArticle...");
        Console.WriteLine("  (Server akan meminta LLM completion melalui client)");
        Console.Write("  Masukkan articleId (default: art-1): ");
        var articleId = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(articleId)) articleId = "art-1";

        var arguments = new Dictionary<string, object?> { ["articleId"] = articleId };
        var result = await mcpClient.CallToolAsync("AutoCategorizeArticle", arguments);

        Console.WriteLine("\n  ─── Sampling Result ───");
        foreach (var content in result.Content)
            Console.WriteLine($"  {GetBlockText(content)}");
    }
    catch (Exception ex) { Console.WriteLine($"  ❌ Error: {ex.Message}"); }
    Console.WriteLine();
}

// =============================================================================
// Menu Option 6: Test Elicitation
// =============================================================================

async Task TestElicitationAsync(McpClient mcpClient)
{
    PrintSectionHeader("📋 TEST ELICITATION");
    try
    {
        Console.WriteLine("  Memanggil ExportArticles...");
        Console.WriteLine("  (Server akan meminta format export melalui client)");
        Console.Write("  Masukkan categoryId (default: cat-1): ");
        var categoryId = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(categoryId)) categoryId = "cat-1";

        var arguments = new Dictionary<string, object?> { ["categoryId"] = categoryId };
        var result = await mcpClient.CallToolAsync("ExportArticles", arguments);

        Console.WriteLine("\n  ─── Elicitation Result ───");
        foreach (var content in result.Content)
            Console.WriteLine($"  {GetBlockText(content)}");
    }
    catch (Exception ex) { Console.WriteLine($"  ❌ Error: {ex.Message}"); }
    Console.WriteLine();
}

// =============================================================================
// Menu Option 7: Test MRTR
// =============================================================================

async Task TestMrtrAsync(McpClient mcpClient)
{
    PrintSectionHeader("🔄 TEST MRTR (Multi Round-Trip Request)");
    try
    {
        Console.WriteLine("  Memanggil DeleteArticleWithConfirmation...");
        Console.WriteLine("  (Server akan meminta konfirmasi sebelum menghapus)");
        Console.Write("  Masukkan articleId (default: art-1): ");
        var articleId = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(articleId)) articleId = "art-1";

        var arguments = new Dictionary<string, object?> { ["articleId"] = articleId };
        var result = await mcpClient.CallToolAsync("DeleteArticleWithConfirmation", arguments);

        Console.WriteLine("\n  ─── MRTR Result ───");
        foreach (var content in result.Content)
            Console.WriteLine($"  {GetBlockText(content)}");
    }
    catch (Exception ex) { Console.WriteLine($"  ❌ Error: {ex.Message}"); }
    Console.WriteLine();
}

// =============================================================================
// UI Helper Functions
// =============================================================================

void PrintHeader()
{
    Console.WriteLine();
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║           MCP ADVANCED CLIENT — Knowledge Base              ║");
    Console.WriteLine("║      Demonstrating Advanced MCP Features via HTTP           ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
}

void PrintMenu()
{
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│  MENU UTAMA                                                  │");
    Console.WriteLine("├──────────────────────────────────────────────────────────────┤");
    Console.WriteLine("│  1. Browse Resources (list & read)                           │");
    Console.WriteLine("│  2. Use Prompts (list & call with params)                    │");
    Console.WriteLine("│  3. Call Tools (list & invoke)                               │");
    Console.WriteLine("│  4. Test Cancellation (BulkProcess + cancel)                 │");
    Console.WriteLine("│  5. Test Sampling (AutoCategorizeArticle)                    │");
    Console.WriteLine("│  6. Test Elicitation (ExportArticles)                        │");
    Console.WriteLine("│  7. Test MRTR (DeleteArticleWithConfirmation)                │");
    Console.WriteLine("│  0. Exit                                                     │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.Write("  Pilihan Anda: ");
}

void PrintSectionHeader(string title)
{
    Console.WriteLine($"  ┌─── {title} ───");
    Console.WriteLine();
}

// =============================================================================
// Content Helper Functions
// =============================================================================

/// <summary>
/// Mengekstrak teks dari ContentBlock (pattern matching untuk TextContentBlock).
/// </summary>
string GetBlockText(ContentBlock? block) => block switch
{
    TextContentBlock text => text.Text,
    _ => block?.Type ?? "[unknown content]"
};

/// <summary>
/// Mengekstrak teks dari ContentBlock yang digunakan dalam PromptMessage.
/// </summary>
string GetContentText(ContentBlock content) => content switch
{
    TextContentBlock text => text.Text,
    _ => $"[{content.Type} content]"
};

/// <summary>
/// Memotong string panjang untuk tampilan console.
/// </summary>
string Truncate(string text, int maxLength) =>
    text.Length <= maxLength ? text : text[..maxLength] + "...";

// =============================================================================
// Error Handling — Pesan yang informatif dengan troubleshooting hints
// =============================================================================

/// <summary>
/// Menampilkan error koneksi HTTP dengan saran troubleshooting.
/// SECURITY: Tidak menampilkan detail internal yang sensitif.
/// </summary>
void PrintConnectionError(HttpRequestException ex)
{
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║  ❌ GAGAL TERHUBUNG KE SERVER                               ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine($"  Error: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("  💡 TROUBLESHOOTING:");
    Console.WriteLine("  ─────────────────────────────────────────────────────────────");
    Console.WriteLine("  1. Pastikan server sudah berjalan:");
    Console.WriteLine("     cd McpAdvanced.Server && dotnet run");
    Console.WriteLine("  2. Periksa apakah port 5100 tidak digunakan proses lain:");
    Console.WriteLine("     netstat -an | findstr 5100");
    Console.WriteLine("  3. Pastikan firewall mengizinkan koneksi ke localhost:5100");
    Console.WriteLine("  4. Periksa apakah URL endpoint benar: http://localhost:5100/mcp");
    Console.WriteLine("  5. Jika menggunakan Docker, pastikan port sudah di-publish:");
    Console.WriteLine("     docker run -p 5100:5100 mcpadvanced-server");
    Console.WriteLine();
}

void PrintTimeoutError(TimeoutException ex)
{
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║  ⏱️  KONEKSI TIMEOUT                                        ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine($"  Error: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("  💡 TROUBLESHOOTING:");
    Console.WriteLine("  ─────────────────────────────────────────────────────────────");
    Console.WriteLine("  1. Server mungkin sedang overloaded atau cold-starting");
    Console.WriteLine("  2. Periksa network latency ke server");
    Console.WriteLine("  3. Coba jalankan ulang client setelah beberapa detik");
    Console.WriteLine("  4. Periksa apakah server merespons di browser:");
    Console.WriteLine("     http://localhost:5100/mcp");
    Console.WriteLine();
}

void PrintGenericError(Exception ex)
{
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║  ❌ ERROR TIDAK TERDUGA                                     ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine($"  Tipe : {ex.GetType().Name}");
    Console.WriteLine($"  Pesan: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("  💡 Pastikan McpAdvanced.Server berjalan dan dapat diakses.");
    Console.WriteLine("  Jika masalah berlanjut, periksa log server untuk detail.");
    Console.WriteLine();
}

#pragma warning restore MCP9005
