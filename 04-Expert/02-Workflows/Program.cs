// =============================================================================
// Workflows - Modul Pembelajaran Kesembilan (Expert Level)
// Demonstrasi multi-agent orchestration menggunakan graph-based workflow
// Mendukung eksekusi sequential, parallel (fan-out/fan-in), dan conditional
// Pipeline: research → draft → review (approve/reject loop)
// =============================================================================

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.AI;
using Workflows.Executors;

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
// Fungsi utama aplikasi - mendemonstrasikan workflow graph orchestration
// =============================================================================
static async Task RunApplicationAsync(IConfiguration configuration, CancellationToken cancellationToken)
{
    var endpoint = configuration["AzureOpenAI:Endpoint"]!;
    var deploymentName = configuration["AzureOpenAI:DeploymentName"]!;

    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║  Workflows - Multi-Agent Graph Orchestration                  ║");
    Console.WriteLine("║  Demonstrasi WorkflowBuilder dengan sequential, parallel,     ║");
    Console.WriteLine("║  dan conditional execution paths                              ║");
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

    // === BAGIAN 1: Definisi Executor (Node dalam Workflow Graph) ===
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 1: Inisialisasi Executor                              │");
    Console.WriteLine("│ Mendefinisikan 3 executor sebagai node dalam workflow graph.  │");
    Console.WriteLine("│ Setiap executor memiliki peran spesifik dalam pipeline.       │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Membuat executor untuk setiap langkah dalam content creation pipeline
    var researchExecutor = new ResearchExecutor(chatClient);
    var draftExecutor = new DraftExecutor(chatClient);
    var reviewExecutor = new ReviewExecutor(chatClient, autoApproveAfter: 2);

    Console.WriteLine($"  [✓] {researchExecutor.ExecutorId}: {researchExecutor.Description}");
    Console.WriteLine($"  [✓] {draftExecutor.ExecutorId}: {draftExecutor.Description}");
    Console.WriteLine($"  [✓] {reviewExecutor.ExecutorId}: {reviewExecutor.Description}");
    Console.WriteLine();

    // === BAGIAN 2: Membangun Workflow Graph ===
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 2: Membangun Workflow Graph dengan WorkflowBuilder     │");
    Console.WriteLine("│ Mendefinisikan nodes, edges, dan conditions secara deklaratif │");
    Console.WriteLine("│ Graph: Research → Draft → Review → (Approve/Reject Loop)      │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Membangun graph workflow menggunakan WorkflowBuilder (fluent API)
    var builder = new WorkflowBuilder(researchExecutor);
    builder
        // Edge sequential: research → draft (output riset → input draft)
        .AddEdge(researchExecutor, draftExecutor)
        // Edge sequential: draft → review (output draft → input review)
        .AddEdge(draftExecutor, reviewExecutor)
        // Edge conditional: review → draft (reject loop - jika TIDAK disetujui)
        .AddEdge(reviewExecutor, draftExecutor,
            condition: result => !result.IsApproved)
        // Output condition: workflow selesai jika review MENYETUJUI
        .WithOutputFrom(reviewExecutor,
            condition: result => result.IsApproved)
        // Konfigurasi retry: maks 3 percobaan per executor
        .WithMaxRetries(3);

    // Membangun workflow dari definisi graph
    var workflow = builder.Build();

    Console.WriteLine("  Workflow Graph Definition:");
    Console.WriteLine("  ┌─────────────┐    ┌─────────────┐    ┌──────────────┐");
    Console.WriteLine("  │  Research   │───▶│    Draft    │───▶│   Review     │");
    Console.WriteLine("  │  Executor   │    │  Executor   │◀───│  Executor    │");
    Console.WriteLine("  └─────────────┘    └─────────────┘    └──────┬───────┘");
    Console.WriteLine("                                                │");
    Console.WriteLine("                          ┌─────────────────────┘");
    Console.WriteLine("                          │ if approved");
    Console.WriteLine("                          ▼");
    Console.WriteLine("                    ┌───────────┐");
    Console.WriteLine("                    │  OUTPUT   │");
    Console.WriteLine("                    └───────────┘");
    Console.WriteLine();
    Console.WriteLine("  Edges:");
    Console.WriteLine("    • ResearchExecutor → DraftExecutor (sequential, unconditional)");
    Console.WriteLine("    • DraftExecutor → ReviewExecutor (sequential, unconditional)");
    Console.WriteLine("    • ReviewExecutor → DraftExecutor (conditional: !IsApproved)");
    Console.WriteLine("    • ReviewExecutor → OUTPUT (conditional: IsApproved)");
    Console.WriteLine();
    Console.WriteLine("  Config: MaxRetries = 3 per executor");
    Console.WriteLine();

    // === BAGIAN 3: Eksekusi Pipeline Content Creation ===
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 3: Eksekusi End-to-End Content Creation Pipeline       │");
    Console.WriteLine("│ Input topik → Research → Draft → Review (reject → revise →   │");
    Console.WriteLine("│ review lagi → approve) → Output konten final                 │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Input topik untuk pipeline content creation
    var topic = "Manfaat dan tantangan adopsi AI dalam pendidikan di Indonesia";
    Console.WriteLine($"  [INPUT] Topik: \"{topic}\"");
    Console.WriteLine();
    Console.WriteLine("  === Real-Time Workflow Visualization ===");
    Console.WriteLine();

    // Menjalankan workflow dan memonitor event secara real-time
    var runResult = await workflow.RunAsync(topic, cancellationToken);

    // === Visualisasi Hasil Eksekusi ===
    Console.WriteLine();
    Console.WriteLine("  === Workflow Execution Summary ===");
    Console.WriteLine();

    // Menampilkan event yang terjadi selama eksekusi
    DisplayExecutionEvents(runResult.Events);

    // Menampilkan status setiap step
    Console.WriteLine();
    Console.WriteLine("  Step Status:");
    foreach (var (stepId, status) in runResult.StepStatuses)
    {
        var statusIcon = status switch
        {
            StepStatus.Completed => "✓",
            StepStatus.Failed => "✗",
            StepStatus.Skipped => "⊘",
            StepStatus.Running => "►",
            _ => "○"
        };
        Console.WriteLine($"    [{statusIcon}] {stepId}: {status}");
    }

    Console.WriteLine();
    Console.WriteLine($"  [RESULT] Workflow {(runResult.IsSuccess ? "BERHASIL" : "GAGAL")}");
    Console.WriteLine($"  [OUTPUT BY] {runResult.CompletedByNode}");

    if (runResult.IsSuccess)
    {
        Console.WriteLine();
        Console.WriteLine("  ╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("  ║ FINAL OUTPUT (Konten yang Disetujui)                     ║");
        Console.WriteLine("  ╠═══════════════════════════════════════════════════════════╣");
        Console.WriteLine($"  {FormatOutput(runResult.FinalOutput)}");
        Console.WriteLine("  ╚═══════════════════════════════════════════════════════════╝");
    }
    Console.WriteLine();

    // === BAGIAN 4: Demonstrasi Step Retry pada Simulated Failure ===
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 4: Demonstrasi Step Retry (Simulated Failure)          │");
    Console.WriteLine("│ ResearchExecutor akan gagal 2x pertama, berhasil pada ke-3.  │");
    Console.WriteLine("│ Retry policy: maks 3 percobaan per executor.                  │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Membuat executor baru dengan simulasi kegagalan
    var retryResearchExecutor = new ResearchExecutor(chatClient);
    retryResearchExecutor.SimulateFailures(2); // Gagal 2x, berhasil pada ke-3
    var retryDraftExecutor = new DraftExecutor(chatClient);
    var retryReviewExecutor = new ReviewExecutor(chatClient, autoApproveAfter: 1);

    // Membangun workflow baru untuk demo retry
    var retryBuilder = new WorkflowBuilder(retryResearchExecutor);
    retryBuilder
        .AddEdge(retryResearchExecutor, retryDraftExecutor)
        .AddEdge(retryDraftExecutor, retryReviewExecutor)
        .WithOutputFrom(retryReviewExecutor, condition: r => r.IsApproved)
        .WithMaxRetries(3);

    var retryWorkflow = retryBuilder.Build();

    Console.WriteLine("  [INFO] ResearchExecutor dikonfigurasi untuk gagal 2x (simulasi).");
    Console.WriteLine("  [INFO] Retry policy: maks 3 percobaan → akan berhasil pada percobaan ke-3.");
    Console.WriteLine();

    var retryTopic = "Perkembangan teknologi quantum computing";
    Console.WriteLine($"  [INPUT] Topik: \"{retryTopic}\"");
    Console.WriteLine();

    var retryResult = await retryWorkflow.RunAsync(retryTopic, cancellationToken);

    Console.WriteLine();
    Console.WriteLine("  === Retry Execution Summary ===");
    DisplayExecutionEvents(retryResult.Events);

    Console.WriteLine();
    Console.WriteLine($"  [RESULT] Workflow {(retryResult.IsSuccess ? "BERHASIL" : "GAGAL")} " +
        $"(setelah retry pada ResearchExecutor)");
    Console.WriteLine();

    // === BAGIAN 5: Demonstrasi Error Summary untuk Permanent Failure ===
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 5: Error Summary - Permanently Failed Steps            │");
    Console.WriteLine("│ ResearchExecutor gagal 3x (semua retry habis).               │");
    Console.WriteLine("│ Menampilkan step yang gagal dan dampak pada downstream.       │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Membuat executor yang akan gagal permanen (semua 3 retry habis)
    var failResearchExecutor = new ResearchExecutor(chatClient);
    failResearchExecutor.SimulateFailures(5); // Lebih dari maxRetries → gagal permanen
    var failDraftExecutor = new DraftExecutor(chatClient);
    var failReviewExecutor = new ReviewExecutor(chatClient, autoApproveAfter: 1);

    // Membangun workflow untuk demo permanent failure
    var failBuilder = new WorkflowBuilder(failResearchExecutor);
    failBuilder
        .AddEdge(failResearchExecutor, failDraftExecutor)
        .AddEdge(failDraftExecutor, failReviewExecutor)
        .WithOutputFrom(failReviewExecutor, condition: r => r.IsApproved)
        .WithMaxRetries(3);

    var failWorkflow = failBuilder.Build();

    Console.WriteLine("  [INFO] ResearchExecutor dikonfigurasi untuk SELALU gagal (5 kegagalan > 3 retry).");
    Console.WriteLine("  [INFO] Downstream (DraftExecutor, ReviewExecutor) akan di-skip.");
    Console.WriteLine();

    var failTopic = "Topik yang akan memicu kegagalan permanen";
    Console.WriteLine($"  [INPUT] Topik: \"{failTopic}\"");
    Console.WriteLine();

    var failResult = await failWorkflow.RunAsync(failTopic, cancellationToken);

    Console.WriteLine();
    Console.WriteLine("  === Error Summary ===");
    Console.WriteLine();

    // Menampilkan error summary untuk step yang gagal permanen
    DisplayErrorSummary(failResult);

    Console.WriteLine();

    // === BAGIAN 6: Demonstrasi Parallel Execution (Fan-out/Fan-in) ===
    Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│ BAGIAN 6: Demonstrasi Parallel Execution (Fan-out/Fan-in)     │");
    Console.WriteLine("│ Research → [Draft + Review (parallel)] → Final Output         │");
    Console.WriteLine("│ Dua executor berjalan bersamaan (fan-out), lalu hasilnya      │");
    Console.WriteLine("│ digabungkan (fan-in).                                         │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
    Console.WriteLine();

    // Membuat executor untuk demo parallel
    var parallelResearch = new ResearchExecutor(chatClient);
    var parallelDraft = new DraftExecutor(chatClient);
    var parallelReview = new ReviewExecutor(chatClient, autoApproveAfter: 1);

    // Membangun graph dengan fan-out: research → [draft, review] secara paralel
    var parallelBuilder = new WorkflowBuilder(parallelResearch);
    parallelBuilder
        // Fan-out: research mengirim output ke draft DAN review secara bersamaan
        .AddEdge(parallelResearch, parallelDraft)
        .AddEdge(parallelResearch, parallelReview)
        .WithMaxRetries(3);

    var parallelWorkflow = parallelBuilder.Build();

    Console.WriteLine("  Parallel Graph:");
    Console.WriteLine("                    ┌─────────────┐");
    Console.WriteLine("                 ┌─▶│    Draft    │");
    Console.WriteLine("  ┌──────────┐   │  └─────────────┘");
    Console.WriteLine("  │ Research │───┤");
    Console.WriteLine("  └──────────┘   │  ┌─────────────┐");
    Console.WriteLine("                 └─▶│   Review    │");
    Console.WriteLine("                    └─────────────┘");
    Console.WriteLine();

    var parallelTopic = "Cloud computing trends 2025";
    Console.WriteLine($"  [INPUT] Topik: \"{parallelTopic}\"");
    Console.WriteLine();

    var parallelResult = await parallelWorkflow.RunAsync(parallelTopic, cancellationToken);

    Console.WriteLine();
    Console.WriteLine("  === Parallel Execution Summary ===");
    DisplayExecutionEvents(parallelResult.Events);

    Console.WriteLine();
    Console.WriteLine($"  [RESULT] Parallel workflow selesai.");
    Console.WriteLine($"  [INFO] Draft dan Review dijalankan secara BERSAMAAN (fan-out).");
    Console.WriteLine();

    // === Ringkasan ===
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("[INFO] Demonstrasi Workflows selesai.");
    Console.WriteLine("[INFO] Konsep yang dipelajari:");
    Console.WriteLine("       1. WorkflowBuilder: definisi graph deklaratif (nodes + edges + conditions)");
    Console.WriteLine("       2. Sequential execution: Research → Draft → Review");
    Console.WriteLine("       3. Conditional routing: Review reject → loop back ke Draft");
    Console.WriteLine("       4. Parallel execution: fan-out (1 source → N targets bersamaan)");
    Console.WriteLine("       5. Step retry: maks 3 percobaan sebelum gagal permanen");
    Console.WriteLine("       6. Error summary: step gagal + dampak downstream");
    Console.WriteLine("       7. ExecutorCompletedEvent: monitoring real-time workflow progress");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
}

// =============================================================================
// Fungsi helper: menampilkan event eksekusi secara real-time
// =============================================================================
static void DisplayExecutionEvents(List<WorkflowEvent> events)
{
    foreach (var evt in events)
    {
        switch (evt)
        {
            // Menampilkan saat executor mulai berjalan (active node visualization)
            case ExecutorStartedEvent started:
                var attemptInfo = started.AttemptNumber > 1
                    ? $" (percobaan #{started.AttemptNumber})"
                    : "";
                Console.WriteLine(
                    $"    [►] {started.Timestamp:HH:mm:ss} | " +
                    $"{started.ExecutorId} MULAI{attemptInfo}");
                break;

            // Menampilkan saat executor selesai (ExecutorCompletedEvent monitoring)
            case ExecutorCompletedEvent completed:
                var icon = completed.IsSuccess ? "✓" : "✗";
                var duration = $"{completed.DurationMs}ms";
                Console.WriteLine(
                    $"    [{icon}] {completed.Timestamp:HH:mm:ss} | " +
                    $"{completed.ExecutorId} SELESAI ({duration})" +
                    $"{(completed.IsSuccess ? "" : $" - ERROR: {completed.ErrorMessage}")}");
                break;

            // Menampilkan saat retry terjadi
            case ExecutorRetryEvent retry:
                Console.WriteLine(
                    $"    [⟳] {retry.Timestamp:HH:mm:ss} | " +
                    $"{retry.ExecutorId} RETRY → percobaan #{retry.NextAttemptNumber} " +
                    $"(alasan: {retry.Reason})");
                break;

            // Menampilkan saat executor gagal permanen
            case ExecutorFailedPermanentlyEvent failed:
                Console.WriteLine(
                    $"    [✗✗] {failed.Timestamp:HH:mm:ss} | " +
                    $"{failed.ExecutorId} GAGAL PERMANEN " +
                    $"(setelah {failed.TotalAttempts} percobaan)");
                Console.WriteLine(
                    $"         Error: {failed.ErrorMessage}");
                if (failed.AffectedDownstream.Count > 0)
                {
                    Console.WriteLine(
                        $"         Downstream terdampak: {string.Join(", ", failed.AffectedDownstream)}");
                }
                break;
        }
    }
}

// =============================================================================
// Fungsi helper: menampilkan error summary untuk permanent failures
// =============================================================================
static void DisplayErrorSummary(WorkflowRunResult result)
{
    // Mengumpulkan informasi step yang gagal permanen
    var failedSteps = result.StepStatuses
        .Where(s => s.Value == StepStatus.Failed)
        .ToList();

    var skippedSteps = result.StepStatuses
        .Where(s => s.Value == StepStatus.Skipped)
        .ToList();

    if (failedSteps.Count == 0)
    {
        Console.WriteLine("  [INFO] Tidak ada step yang gagal permanen.");
        return;
    }

    Console.WriteLine("  ╔═══════════════════════════════════════════════════════════╗");
    Console.WriteLine("  ║ ERROR SUMMARY - Permanently Failed Steps                 ║");
    Console.WriteLine("  ╠═══════════════════════════════════════════════════════════╣");

    // Menampilkan detail step yang gagal
    foreach (var (stepId, _) in failedSteps)
    {
        var failEvent = result.Events
            .OfType<ExecutorFailedPermanentlyEvent>()
            .FirstOrDefault(e => e.ExecutorId == stepId);

        Console.WriteLine($"  ║ Step: {stepId,-49}║");
        Console.WriteLine($"  ║ Error: {(failEvent?.ErrorMessage ?? "Unknown"),-48}║");
        Console.WriteLine($"  ║ Percobaan: {failEvent?.TotalAttempts ?? 0} (semua gagal){new string(' ', 33)}║");

        if (failEvent?.AffectedDownstream.Count > 0)
        {
            Console.WriteLine($"  ║ Dampak: {string.Join(", ", failEvent.AffectedDownstream),-47}║");
        }
    }

    // Menampilkan step yang di-skip karena dependency gagal
    if (skippedSteps.Count > 0)
    {
        Console.WriteLine("  ╠═══════════════════════════════════════════════════════════╣");
        Console.WriteLine("  ║ SKIPPED STEPS (dependency gagal)                         ║");
        Console.WriteLine("  ╠═══════════════════════════════════════════════════════════╣");
        foreach (var (stepId, _) in skippedSteps)
        {
            Console.WriteLine($"  ║ ⊘ {stepId,-53}║");
        }
    }

    Console.WriteLine("  ╚═══════════════════════════════════════════════════════════╝");
}

// =============================================================================
// Fungsi helper: memformat output panjang agar tampil rapi di console
// =============================================================================
static string FormatOutput(string output)
{
    if (string.IsNullOrWhiteSpace(output))
        return "  (kosong)";

    // Membatasi output untuk tampilan console yang rapi
    var truncated = output.Length > 800 ? output[..800] + "..." : output;
    return truncated.Replace("\n", "\n  ").Trim();
}
