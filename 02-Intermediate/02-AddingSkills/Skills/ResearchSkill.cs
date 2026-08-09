// =============================================================================
// ResearchSkill.cs - Definisi skill yang mengemas beberapa tools terkait riset
// Skill adalah abstraksi di atas tools: mengelompokkan tools yang secara
// fungsional terkait menjadi satu unit reusable yang dapat didaftarkan ke
// berbagai agent secara modular.
// =============================================================================

using System.ComponentModel;

namespace AddingSkills.Skills;

/// <summary>
/// ResearchSkill - skill yang mengemas tools terkait riset dan analisis.
/// Berisi WebSearch untuk mencari informasi dan Summarize untuk merangkum teks.
/// Skill ini mendemonstrasikan konsep packaging tools menjadi unit kohesif.
/// </summary>
public static class ResearchSkill
{
    // Nama skill untuk identifikasi saat registrasi
    public const string SkillName = "ResearchSkill";

    // Deskripsi skill yang menjelaskan fungsi keseluruhan
    public const string SkillDescription =
        "Skill riset yang menyediakan kemampuan pencarian web dan perangkuman teks. " +
        "Digunakan ketika agent perlu mencari informasi atau merangkum konten panjang.";

    // Daftar nama tools dalam skill ini (untuk logging dan discovery)
    public static readonly string[] ToolNames = ["WebSearch", "Summarize", "ExtractKeywords"];

    // Data simulasi hasil pencarian web
    // Dalam produksi, ini akan memanggil search API yang sebenarnya
    private static readonly Dictionary<string, string> SimulatedSearchResults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AI agents"] = "AI agents adalah sistem software otonom yang menggunakan Large Language Model (LLM) " +
                        "untuk memproses instruksi, membuat keputusan, dan melakukan aksi. Berbeda dengan chatbot " +
                        "tradisional, agents dapat menggunakan tools, mempertahankan memory, dan beroperasi secara proaktif.",
        ["Microsoft Agent Framework"] = "Microsoft Agent Framework adalah SDK unified untuk membangun AI agents, " +
                        "dirilis April 2026 sebagai pengganti Semantic Kernel dan AutoGen. Framework ini menyediakan " +
                        "API konsisten untuk tools, skills, middleware, dan multi-agent orchestration.",
        ["machine learning"] = "Machine learning adalah cabang AI yang memungkinkan komputer belajar dari data " +
                        "tanpa diprogram secara eksplisit. Teknik utama meliputi supervised learning, unsupervised " +
                        "learning, dan reinforcement learning.",
        ["cloud computing"] = "Cloud computing adalah model penyediaan layanan komputasi (server, storage, database, " +
                        "networking) melalui internet. Provider utama: Azure, AWS, GCP. Model layanan: IaaS, PaaS, SaaS.",
        ["Indonesia"] = "Indonesia adalah negara kepulauan terbesar di dunia dengan lebih dari 17.000 pulau. " +
                        "Populasi sekitar 275 juta jiwa. Ibu kota baru: Nusantara (IKN) di Kalimantan Timur.",
    };

    /// <summary>
    /// Mencari informasi di web berdasarkan query yang diberikan.
    /// Tool ini dipanggil oleh agent ketika user membutuhkan informasi faktual.
    /// </summary>
    /// <param name="query">Kata kunci atau pertanyaan untuk dicari</param>
    /// <returns>Hasil pencarian dalam format teks</returns>
    [Description("Mencari informasi di web berdasarkan kata kunci atau pertanyaan. " +
                 "Gunakan tool ini ketika perlu mencari fakta, definisi, atau informasi terkini. " +
                 "Parameter: query pencarian dalam bahasa Indonesia atau Inggris.")]
    public static string WebSearch(string query)
    {
        // Mencatat eksekusi tool untuk keperluan logging
        Console.WriteLine($"    [SKILL TOOL] ResearchSkill.WebSearch dieksekusi");
        Console.WriteLine($"                 Query: \"{query}\"");

        // Mencari kecocokan dalam data simulasi (case-insensitive partial match)
        foreach (var kvp in SimulatedSearchResults)
        {
            if (query.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                var result = $"[Hasil Pencarian untuk '{query}']: {kvp.Value}";
                Console.WriteLine($"    [SKILL RESULT] Ditemukan hasil untuk query.");
                return result;
            }
        }

        // Query tidak ditemukan dalam database simulasi
        var notFound = $"[Hasil Pencarian untuk '{query}']: Tidak ditemukan hasil yang relevan. " +
                       $"Topik yang tersedia: {string.Join(", ", SimulatedSearchResults.Keys)}";
        Console.WriteLine($"    [SKILL RESULT] Tidak ada hasil yang cocok.");
        return notFound;
    }

    /// <summary>
    /// Merangkum teks panjang menjadi ringkasan singkat.
    /// Tool ini dipanggil oleh agent ketika perlu menyederhanakan informasi.
    /// </summary>
    /// <param name="text">Teks panjang yang ingin dirangkum</param>
    /// <returns>Ringkasan singkat dari teks input</returns>
    [Description("Merangkum teks panjang menjadi ringkasan singkat dan padat. " +
                 "Gunakan tool ini setelah mendapat informasi panjang yang perlu disederhanakan. " +
                 "Parameter: teks yang ingin dirangkum.")]
    public static string Summarize(string text)
    {
        // Mencatat eksekusi tool untuk keperluan logging
        Console.WriteLine($"    [SKILL TOOL] ResearchSkill.Summarize dieksekusi");
        Console.WriteLine($"                 Input panjang: {text.Length} karakter");

        // Simulasi perangkuman: mengambil kalimat pertama dan menghitung statistik
        // Dalam produksi, ini akan menggunakan model AI untuk summarization
        var sentences = text.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        string summary;
        if (sentences.Length <= 2)
        {
            // Teks sudah cukup pendek, kembalikan langsung
            summary = $"[Ringkasan]: {text.Trim()}";
        }
        else
        {
            // Ambil 2 kalimat pertama sebagai ringkasan simulasi
            var shortSummary = string.Join(". ", sentences.Take(2)) + ".";
            summary = $"[Ringkasan ({wordCount} kata → ringkas)]: {shortSummary}";
        }

        Console.WriteLine($"    [SKILL RESULT] Ringkasan dihasilkan ({summary.Length} karakter).");
        return summary;
    }

    /// <summary>
    /// Mengekstrak kata kunci utama dari sebuah teks.
    /// Tool ini membantu agent mengidentifikasi topik penting dalam konten.
    /// </summary>
    /// <param name="text">Teks yang ingin diekstrak kata kuncinya</param>
    /// <returns>Daftar kata kunci yang ditemukan</returns>
    [Description("Mengekstrak kata kunci utama dari sebuah teks. " +
                 "Gunakan tool ini untuk mengidentifikasi topik-topik penting dalam konten. " +
                 "Parameter: teks yang ingin diekstrak kata kuncinya.")]
    // Karakter pemisah untuk ekstraksi kata kunci
    private static readonly char[] SeparatorChars = [' ', ',', '.', '!', '?', ':', ';', '(', ')', '[', ']'];

    public static string ExtractKeywords(string text)
    {
        // Mencatat eksekusi tool untuk keperluan logging
        Console.WriteLine($"    [SKILL TOOL] ResearchSkill.ExtractKeywords dieksekusi");
        Console.WriteLine($"                 Input panjang: {text.Length} karakter");

        // Simulasi ekstraksi kata kunci: ambil kata-kata yang lebih panjang dari 5 karakter
        // dan unik (pendekatan sederhana untuk demonstrasi)
        var words = text.Split(SeparatorChars,
                              StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                       .Where(w => w.Length > 5)
                       .Select(w => w.ToLowerInvariant())
                       .Distinct()
                       .Take(7)
                       .ToArray();

        var result = $"[Kata Kunci]: {string.Join(", ", words)}";
        Console.WriteLine($"    [SKILL RESULT] {words.Length} kata kunci diekstrak.");
        return result;
    }
}
