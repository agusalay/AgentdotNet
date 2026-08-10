// SamplingHandler.cs — Handler untuk Sampling request dari server
// Sampling memungkinkan server meminta LLM completion dari client.
// Handler ini mencoba menggunakan OpenAI API jika API key tersedia,
// dan menyediakan fallback cerdas berbasis keyword jika LLM tidak dikonfigurasi.

#pragma warning disable MCP9005 // Sampling deprecated di protocol 2026-07-28

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace McpAdvanced.Client.Handlers;

/// <summary>
/// Handler yang memproses sampling (LLM completion) request dari server.
/// Server meminta client melakukan inferensi LLM dan mengembalikan hasilnya.
/// 
/// Strategi:
/// 1. Cek apakah OPENAI_API_KEY tersedia di environment variables
/// 2. Jika tersedia — kirim request ke OpenAI Chat Completions API
/// 3. Jika tidak tersedia — gunakan fallback cerdas berbasis keyword matching
///    yang menganalisis prompt untuk menentukan respons yang relevan
/// </summary>
public static class SamplingHandler
{
    // Nama environment variable untuk API key OpenAI
    private const string OpenAiApiKeyEnvVar = "OPENAI_API_KEY";

    // Model default yang digunakan jika tidak ada hint dari server
    private const string DefaultModel = "gpt-4o-mini";

    /// <summary>
    /// Menangani permintaan sampling dari server.
    /// Dipanggil oleh MCP SDK ketika server mengirim CreateMessageRequest ke client.
    /// </summary>
    /// <param name="request">Parameter request dari server berisi messages, model hints, max tokens</param>
    /// <param name="progress">Progress reporter untuk melaporkan kemajuan ke server</param>
    /// <param name="cancellationToken">Token pembatalan operasi</param>
    /// <returns>Hasil completion berisi model, role, dan content text</returns>
    public static async ValueTask<CreateMessageResult> HandleAsync(
        CreateMessageRequestParams? request,
        IProgress<ProgressNotificationValue> progress,
        CancellationToken cancellationToken)
    {
        // Tampilkan informasi sampling request yang masuk ke console
        Console.WriteLine();
        Console.WriteLine("  ┌─── 🤖 SAMPLING REQUEST DITERIMA ───");
        Console.WriteLine($"  │ Waktu       : {DateTime.Now:HH:mm:ss.fff}");

        // Ekstrak model hint dari request jika tersedia
        var modelHint = request?.ModelPreferences?.Hints?.FirstOrDefault()?.Name;
        Console.WriteLine($"  │ Model hint  : {modelHint ?? "(tidak ditentukan)"}");
        Console.WriteLine($"  │ Max tokens  : {request?.MaxTokens ?? 0}");

        // Tampilkan ringkasan pesan yang dikirim server
        if (request?.Messages is { Count: > 0 } messages)
        {
            Console.WriteLine($"  │ Jumlah pesan: {messages.Count}");
            foreach (var msg in messages)
            {
                var text = ExtractTextFromMessage(msg);
                var preview = text.Length > 100 ? text[..100] + "..." : text;
                Console.WriteLine($"  │ [{msg.Role}]: {preview}");
            }
        }

        // Tampilkan system prompt jika ada
        if (!string.IsNullOrEmpty(request?.SystemPrompt))
        {
            var sysPreview = request.SystemPrompt.Length > 80
                ? request.SystemPrompt[..80] + "..."
                : request.SystemPrompt;
            Console.WriteLine($"  │ System     : {sysPreview}");
        }

        Console.WriteLine("  │");

        // Cek apakah API key tersedia untuk menggunakan LLM
        var apiKey = Environment.GetEnvironmentVariable(OpenAiApiKeyEnvVar);
        var hasApiKey = !string.IsNullOrWhiteSpace(apiKey);

        if (hasApiKey)
        {
            // API key tersedia — coba gunakan OpenAI untuk completion
            Console.WriteLine("  │ 🔑 API Key terdeteksi — menggunakan OpenAI...");
            try
            {
                var result = await CallOpenAiAsync(request!, apiKey!, modelHint, cancellationToken);
                Console.WriteLine($"  │ ✅ Respons LLM diterima (model: {result.Model})");
                Console.WriteLine($"  └───────────────────────────────────────────────");
                Console.WriteLine();
                return result;
            }
            catch (Exception ex)
            {
                // Panggilan ke OpenAI gagal — fallback ke keyword matching
                Console.WriteLine($"  │ ⚠️  OpenAI gagal: {ex.Message}");
                Console.WriteLine("  │ 🔄 Menggunakan fallback keyword matching...");
            }
        }
        else
        {
            // Tidak ada API key — langsung gunakan fallback
            Console.WriteLine("  │ ℹ️  Tidak ada OPENAI_API_KEY — menggunakan fallback cerdas");
        }

        // Fallback: analisis prompt dan berikan respons berbasis keyword
        var fallbackResult = GenerateSmartFallbackResponse(request);
        Console.WriteLine($"  │ ✅ Fallback response dihasilkan (model: {fallbackResult.Model})");
        Console.WriteLine($"  └───────────────────────────────────────────────");
        Console.WriteLine();

        return fallbackResult;
    }

    /// <summary>
    /// Memanggil OpenAI Chat Completions API untuk mendapatkan respons LLM.
    /// Menggunakan HttpClient secara langsung tanpa dependency tambahan
    /// agar project tetap ringan (hanya butuh ModelContextProtocol package).
    /// </summary>
    /// <param name="request">Parameter sampling dari server</param>
    /// <param name="apiKey">API key OpenAI</param>
    /// <param name="modelHint">Nama model yang disarankan server (opsional)</param>
    /// <param name="cancellationToken">Token pembatalan</param>
    /// <returns>CreateMessageResult dengan respons dari LLM</returns>
    private static async Task<CreateMessageResult> CallOpenAiAsync(
        CreateMessageRequestParams request,
        string apiKey,
        string? modelHint,
        CancellationToken cancellationToken)
    {
        // Tentukan model yang akan digunakan — prioritaskan hint dari server
        var model = modelHint ?? DefaultModel;

        // Bangun daftar messages untuk OpenAI API
        var openAiMessages = new List<object>();

        // Tambahkan system prompt jika ada
        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            openAiMessages.Add(new { role = "system", content = request.SystemPrompt });
        }

        // Konversi sampling messages ke format OpenAI
        if (request.Messages is { Count: > 0 })
        {
            foreach (var msg in request.Messages)
            {
                var text = ExtractTextFromMessage(msg);
                var role = msg.Role == Role.Assistant ? "assistant" : "user";
                openAiMessages.Add(new { role, content = text });
            }
        }

        // Buat request body untuk OpenAI
        var requestBody = new
        {
            model,
            messages = openAiMessages,
            max_tokens = request.MaxTokens > 0 ? request.MaxTokens : 150,
            temperature = 0.3 // Suhu rendah untuk klasifikasi yang konsisten
        };

        var json = JsonSerializer.Serialize(requestBody);

        // Kirim request ke OpenAI API
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(
            "https://api.openai.com/v1/chat/completions",
            httpContent,
            cancellationToken);

        // Pastikan respons sukses
        response.EnsureSuccessStatusCode();

        // Parse respons JSON dari OpenAI
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        // Ekstrak teks respons dari choices[0].message.content
        var responseText = root
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        // Ekstrak model yang sebenarnya digunakan
        var usedModel = root.GetProperty("model").GetString() ?? model;

        // Kembalikan hasil sebagai CreateMessageResult
        return new CreateMessageResult
        {
            Model = usedModel,
            Role = Role.Assistant,
            Content = [new TextContentBlock { Text = responseText }]
        };
    }

    /// <summary>
    /// Menghasilkan respons fallback cerdas berdasarkan analisis konten prompt.
    /// Digunakan ketika LLM tidak tersedia (tidak ada API key atau panggilan gagal).
    /// 
    /// Strategi fallback:
    /// - Deteksi apakah prompt meminta klasifikasi/kategorisasi
    /// - Jika ya, lakukan keyword matching pada konten artikel
    /// - Jika bukan klasifikasi, berikan respons informatif default
    /// </summary>
    /// <param name="request">Parameter sampling dari server</param>
    /// <returns>CreateMessageResult dengan respons fallback</returns>
    private static CreateMessageResult GenerateSmartFallbackResponse(CreateMessageRequestParams? request)
    {
        // Gabungkan semua teks dari request untuk analisis
        var allText = "";
        if (request?.Messages is { Count: > 0 })
        {
            allText = string.Join(" ", request.Messages.Select(ExtractTextFromMessage));
        }

        var systemPrompt = request?.SystemPrompt ?? "";
        var combinedContext = (systemPrompt + " " + allText).ToLowerInvariant();

        // Deteksi tipe permintaan dan berikan respons yang sesuai
        string responseText;

        if (IsClassificationRequest(combinedContext))
        {
            // Permintaan klasifikasi/kategorisasi — gunakan keyword matching
            responseText = PerformKeywordClassification(allText, combinedContext);
        }
        else if (IsSummarizationRequest(combinedContext))
        {
            // Permintaan ringkasan — berikan ringkasan sederhana
            responseText = PerformSimpleSummarization(allText);
        }
        else
        {
            // Tipe permintaan tidak dikenal — berikan respons default informatif
            responseText = "Unable to process this request without an LLM. " +
                          "Please configure OPENAI_API_KEY for full functionality.";
        }

        return new CreateMessageResult
        {
            Model = "fallback-keyword-matcher",
            Role = Role.Assistant,
            Content = [new TextContentBlock { Text = responseText }]
        };
    }

    /// <summary>
    /// Mendeteksi apakah prompt meminta klasifikasi atau kategorisasi.
    /// Memeriksa keberadaan kata kunci terkait klasifikasi dalam teks.
    /// </summary>
    private static bool IsClassificationRequest(string context)
    {
        // Kata kunci yang mengindikasikan permintaan klasifikasi
        var classificationKeywords = new[]
        {
            "classify", "categorize", "categorise", "category", "categories",
            "classify the following", "which category", "respond with only the category"
        };

        return classificationKeywords.Any(kw => context.Contains(kw));
    }

    /// <summary>
    /// Mendeteksi apakah prompt meminta ringkasan konten.
    /// </summary>
    private static bool IsSummarizationRequest(string context)
    {
        var summarizationKeywords = new[]
        {
            "summarize", "summarise", "summary", "brief overview",
            "key points", "main ideas", "tldr"
        };

        return summarizationKeywords.Any(kw => context.Contains(kw));
    }

    /// <summary>
    /// Melakukan klasifikasi berbasis keyword matching.
    /// Menganalisis konten artikel dan mencocokan dengan kategori yang disebutkan dalam prompt.
    /// </summary>
    /// <param name="messageText">Teks asli dari pesan (case-sensitive)</param>
    /// <param name="context">Teks gabungan dalam lowercase untuk pencarian</param>
    /// <returns>Nama kategori yang paling cocok</returns>
    private static string PerformKeywordClassification(string messageText, string context)
    {
        // Ekstrak daftar kategori dari prompt jika disebutkan
        // Pola umum: "categories: cat1, cat2, cat3" atau "one of these categories: ..."
        var categories = ExtractCategoriesFromPrompt(context);

        if (categories.Count == 0)
        {
            // Tidak dapat menemukan kategori dalam prompt — berikan default
            return "general";
        }

        // Hitung skor kecocokan untuk setiap kategori berdasarkan keyword
        var scores = new Dictionary<string, int>();
        foreach (var category in categories)
        {
            scores[category] = CalculateCategoryScore(category, context);
        }

        // Pilih kategori dengan skor tertinggi
        var bestCategory = scores.OrderByDescending(kvp => kvp.Value).First();

        // Jika skor tertinggi masih 0, pilih kategori pertama sebagai default
        return bestCategory.Value > 0 ? bestCategory.Key : categories[0];
    }

    /// <summary>
    /// Mengekstrak nama-nama kategori yang disebutkan dalam prompt.
    /// Mencari pola "categories: x, y, z" dalam teks.
    /// </summary>
    private static List<string> ExtractCategoriesFromPrompt(string context)
    {
        var categories = new List<string>();

        // Cari pola "categories:" diikuti daftar nama
        var markers = new[] { "categories:", "these categories:", "one of these:" };
        foreach (var marker in markers)
        {
            var idx = context.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) continue;

            // Ambil teks setelah marker sampai newline atau titik
            var afterMarker = context[(idx + marker.Length)..];
            var endIdx = afterMarker.IndexOfAny(['\n', '.']);
            var categoryPart = endIdx > 0 ? afterMarker[..endIdx] : afterMarker;

            // Split berdasarkan koma dan bersihkan
            var parts = categoryPart.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var cleaned = part.Trim().Trim('"', '\'');
                if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length < 50)
                {
                    categories.Add(cleaned);
                }
            }

            if (categories.Count > 0) break;
        }

        return categories;
    }

    /// <summary>
    /// Menghitung skor kecocokan kategori berdasarkan keyword yang ada dalam konteks.
    /// Semakin banyak keyword kategori yang ditemukan, semakin tinggi skornya.
    /// </summary>
    private static int CalculateCategoryScore(string category, string context)
    {
        var score = 0;

        // Peta keyword untuk setiap kategori yang umum dalam knowledge base
        var categoryKeywords = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["tutorials"] = ["tutorial", "step by step", "how to", "guide", "learn", "walkthrough", "hands-on"],
            ["api-reference"] = ["api", "reference", "endpoint", "method", "parameter", "request", "response", "http"],
            ["best-practices"] = ["best practice", "pattern", "security", "performance", "optimization", "convention"],
            ["getting-started"] = ["getting started", "introduction", "setup", "install", "quickstart", "beginner"],
            ["architecture"] = ["architecture", "design", "system", "component", "service", "microservice", "structure"],
            ["troubleshooting"] = ["error", "fix", "debug", "problem", "issue", "solution", "troubleshoot"],
            ["general"] = ["general", "overview", "misc", "other"]
        };

        // Cek apakah ada keyword yang cocok untuk kategori ini
        if (categoryKeywords.TryGetValue(category, out var keywords))
        {
            foreach (var keyword in keywords)
            {
                if (context.Contains(keyword))
                    score += 2; // Bobot tinggi untuk keyword yang cocok
            }
        }

        // Bonus jika nama kategori sendiri muncul dalam konten artikel (bukan hanya di daftar)
        // Hanya hitung jika kategori muncul di bagian konten artikel, bukan di daftar kategori
        var articleContentStart = context.IndexOf("article content", StringComparison.Ordinal);
        if (articleContentStart > 0)
        {
            var articleContent = context[articleContentStart..];
            if (articleContent.Contains(category))
                score += 3;
        }

        return score;
    }

    /// <summary>
    /// Melakukan ringkasan sederhana dari teks — mengambil kalimat pertama dan poin utama.
    /// </summary>
    private static string PerformSimpleSummarization(string text)
    {
        // Ambil beberapa kalimat pertama sebagai ringkasan sederhana
        var sentences = text.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var summary = string.Join(". ",
            sentences.Take(3).Select(s => s.Trim()).Where(s => s.Length > 10));

        if (string.IsNullOrWhiteSpace(summary))
            return "Content is too short to summarize.";

        return summary + ".";
    }

    /// <summary>
    /// Mengekstrak teks dari SamplingMessage.
    /// Iterasi melalui content blocks dan mengambil TextContentBlock.
    /// </summary>
    private static string ExtractTextFromMessage(SamplingMessage message)
    {
        if (message.Content is not { Count: > 0 })
            return string.Empty;

        var textParts = new List<string>();
        foreach (var block in message.Content)
        {
            if (block is TextContentBlock textBlock && !string.IsNullOrEmpty(textBlock.Text))
            {
                textParts.Add(textBlock.Text);
            }
        }

        return string.Join(" ", textParts);
    }
}

#pragma warning restore MCP9005
