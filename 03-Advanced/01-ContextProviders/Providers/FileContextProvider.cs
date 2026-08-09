// =============================================================================
// FileContextProvider - Menyediakan konteks dari file JSON knowledge base
// Membaca data fakta/pengetahuan dari file lokal dan menyediakannya ke agent
// sebagai konteks tambahan (pola sederhana dari RAG)
// =============================================================================

using System.Text.Json;

namespace ContextProviders.Providers;

/// <summary>
/// Merepresentasikan satu fakta dalam knowledge base.
/// Setiap fakta memiliki topik, konten penjelasan, dan sumber referensi.
/// </summary>
public record KnowledgeFact(string Topic, string Content, string Source);

/// <summary>
/// Context provider yang membaca knowledge base dari file JSON lokal.
/// Menyediakan fakta-fakta relevan sebagai konteks tambahan untuk agent.
/// Ini adalah implementasi sederhana dari pola RAG (Retrieval-Augmented Generation).
/// </summary>
public class FileContextProvider : IContextProvider
{
    // Path ke file knowledge base JSON
    private readonly string _filePath;

    // Cache untuk fakta-fakta yang sudah dimuat dari file
    private List<KnowledgeFact>? _facts;

    /// <summary>
    /// Nama provider untuk identifikasi di log output.
    /// </summary>
    public string Name => "FileContextProvider";

    /// <summary>
    /// Membuat instance FileContextProvider dengan path ke file knowledge base.
    /// </summary>
    /// <param name="filePath">Path absolut atau relatif ke file JSON knowledge base</param>
    public FileContextProvider(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// Jumlah fakta yang dimuat dari knowledge base.
    /// Bernilai 0 jika belum dimuat atau file tidak ditemukan.
    /// </summary>
    public int FactCount => _facts?.Count ?? 0;

    /// <summary>
    /// Menyediakan seluruh isi knowledge base sebagai konteks untuk agent.
    /// Data di-cache setelah pembacaan pertama untuk menghindari I/O berulang.
    /// </summary>
    /// <returns>String berisi formatted knowledge base content</returns>
    public async Task<string> ProvideContextAsync()
    {
        // Memuat fakta dari file jika belum di-cache
        if (_facts is null)
        {
            await LoadFactsAsync();
        }

        // Jika tidak ada fakta yang dimuat, kembalikan string kosong
        if (_facts is null || _facts.Count == 0)
            return string.Empty;

        // Memformat fakta menjadi konteks yang mudah dipahami LLM
        return FormatFacts(_facts);
    }

    /// <summary>
    /// FileContextProvider tidak menyimpan data baru setelah invocation.
    /// Knowledge base bersifat read-only dalam implementasi ini.
    /// </summary>
    public Task StoreContextAsync(string userMessage, string assistantMessage)
    {
        // Tidak ada aksi - knowledge base bersifat read-only
        return Task.CompletedTask;
    }

    /// <summary>
    /// Memuat fakta-fakta dari file JSON ke dalam cache internal.
    /// Menangani error jika file tidak ditemukan atau format tidak valid.
    /// </summary>
    private async Task LoadFactsAsync()
    {
        // Memeriksa keberadaan file sebelum membaca
        if (!File.Exists(_filePath))
        {
            Console.WriteLine($"  [WARN] Knowledge base tidak ditemukan: {_filePath}");
            _facts = [];
            return;
        }

        try
        {
            // Membaca dan mem-parse file JSON
            var jsonContent = await File.ReadAllTextAsync(_filePath);

            // Deserialize ke struktur yang sesuai
            var knowledgeBase = JsonSerializer.Deserialize<KnowledgeBase>(jsonContent, JsonOptions);

            // Mengkonversi ke list of KnowledgeFact
            _facts = knowledgeBase?.Facts?
                .Select(f => new KnowledgeFact(f.Topic, f.Content, f.Source))
                .ToList() ?? [];

            Console.WriteLine($"  [INFO] Knowledge base dimuat: {_facts.Count} fakta dari {_filePath}");
        }
        catch (JsonException ex)
        {
            // Menangani error parsing JSON
            Console.WriteLine($"  [ERROR] Gagal mem-parse knowledge base: {ex.Message}");
            _facts = [];
        }
    }

    /// <summary>
    /// Memformat daftar fakta menjadi string konteks untuk LLM.
    /// Setiap fakta ditampilkan dengan topik, konten, dan sumber.
    /// </summary>
    private static string FormatFacts(List<KnowledgeFact> facts)
    {
        var lines = new List<string>
        {
            "[Knowledge Base - Informasi Referensi]"
        };

        // Memformat setiap fakta dengan struktur yang jelas
        foreach (var fact in facts)
        {
            lines.Add($"- Topik: {fact.Topic}");
            lines.Add($"  Isi: {fact.Content}");
            lines.Add($"  Sumber: {fact.Source}");
            lines.Add("");
        }

        lines.Add("[Akhir Knowledge Base]");
        lines.Add("");

        return string.Join("\n", lines);
    }

    // Cached JsonSerializerOptions untuk menghindari pembuatan instance baru setiap kali
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Model internal untuk deserialisasi file JSON knowledge base.
    /// </summary>
    private sealed class KnowledgeBase
    {
        public List<FactEntry>? Facts { get; set; }
    }

    /// <summary>
    /// Model entry tunggal dalam knowledge base JSON.
    /// </summary>
    private sealed class FactEntry
    {
        public string Topic { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
    }
}
