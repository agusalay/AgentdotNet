// ResourcePaginationHandler.cs — Implementasi cursor-based pagination untuk listing resources
// MCP Protocol mendukung pagination untuk operasi list (resources, prompts, tools)
// ketika koleksi terlalu besar untuk dikirim dalam satu response.
//
// Mekanisme pagination menggunakan cursor opaque — client tidak perlu tahu format internal cursor.
// Server mengembalikan NextCursor di response; client mengirimkan cursor tersebut di request berikutnya.
// Ketika NextCursor bernilai null, berarti sudah halaman terakhir.
//
// Implementasi ini menggunakan Base64 encoding dari posisi indeks sebagai format cursor.
// Format ini dipilih karena sederhana dan cukup untuk demonstrasi in-memory data store.

using System.Text;
using McpAdvanced.Server.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpAdvanced.Server.Pagination;

/// <summary>
/// Handler untuk pagination pada listing resources.
/// Menggunakan WithListResourcesHandler pattern dari MCP SDK untuk menggantikan
/// default listing behavior dengan implementasi yang mendukung cursor-based pagination.
/// </summary>
public static class ResourcePaginationHandler
{
    // Ukuran halaman default — diatur kecil (3) agar pagination mudah didemonstrasikan
    // dengan jumlah data seed yang terbatas (7 artikel).
    // Pada production, nilai ini bisa disesuaikan melalui konfigurasi.
    public const int DefaultPageSize = 3;

    /// <summary>
    /// Handler utama untuk resources/list request dengan dukungan pagination.
    /// Dipanggil oleh MCP framework ketika client mengirim resources/list request.
    /// 
    /// Flow:
    /// 1. Jika tidak ada cursor → kembalikan halaman pertama
    /// 2. Jika ada cursor → decode posisi, kembalikan halaman sesuai posisi
    /// 3. Sertakan NextCursor jika masih ada halaman berikutnya
    /// </summary>
    public static ValueTask<ListResourcesResult> HandleListResourcesAsync(
        RequestContext<ListResourcesRequestParams> context,
        CancellationToken cancellationToken)
    {
        // Ambil KnowledgeBaseStore dari DI container
        var store = context.Services!.GetRequiredService<KnowledgeBaseStore>();

        // Ambil semua artikel dan konversikan ke Resource objects
        // Diurutkan berdasarkan Id agar konsisten antara request (deterministic ordering)
        var allResources = store.Articles.Values
            .OrderBy(a => a.Id)
            .Select(ArticleToResource)
            .ToList();

        // Tentukan posisi awal berdasarkan cursor yang diterima
        int startIndex = 0;
        if (context.Params?.Cursor is { } cursor)
        {
            // Decode cursor dari Base64 untuk mendapatkan posisi indeks
            startIndex = DecodeCursor(cursor);

            // Jika cursor tidak valid (posisi di luar range), kembalikan dari awal
            // Sesuai error handling design: "Pagination invalid cursor → treat as first page"
            if (startIndex < 0 || startIndex >= allResources.Count)
            {
                startIndex = 0;
            }
        }

        // Ambil item untuk halaman saat ini
        var pageItems = allResources
            .Skip(startIndex)
            .Take(DefaultPageSize)
            .ToList();

        // Tentukan apakah masih ada halaman berikutnya
        var nextStartIndex = startIndex + DefaultPageSize;
        var hasMore = nextStartIndex < allResources.Count;

        // Buat result dengan NextCursor hanya jika ada halaman berikutnya
        var result = new ListResourcesResult
        {
            Resources = pageItems,
            // NextCursor null menandakan halaman terakhir
            NextCursor = hasMore ? EncodeCursor(nextStartIndex) : null
        };

        return ValueTask.FromResult(result);
    }

    #region Cursor Encoding/Decoding

    /// <summary>
    /// Encode posisi indeks menjadi cursor string menggunakan Base64.
    /// Format cursor bersifat opaque bagi client — client hanya meneruskan cursor
    /// yang diterima dari server tanpa perlu memahami isinya.
    /// </summary>
    /// <param name="position">Posisi indeks item pertama di halaman berikutnya</param>
    /// <returns>Cursor string dalam format Base64</returns>
    public static string EncodeCursor(int position)
    {
        // Konversi posisi ke string, lalu encode ke Base64
        // Menggunakan UTF8 encoding untuk konsistensi cross-platform
        var positionBytes = Encoding.UTF8.GetBytes(position.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToBase64String(positionBytes);
    }

    /// <summary>
    /// Decode cursor string dari Base64 kembali menjadi posisi indeks.
    /// Jika cursor tidak valid (bukan Base64, atau bukan angka), kembalikan 0
    /// sehingga server mengembalikan halaman pertama (graceful fallback).
    /// </summary>
    /// <param name="cursor">Cursor string dalam format Base64</param>
    /// <returns>Posisi indeks, atau 0 jika cursor tidak valid</returns>
    public static int DecodeCursor(string cursor)
    {
        try
        {
            // Decode dari Base64 ke bytes, lalu ke string posisi
            var positionBytes = Convert.FromBase64String(cursor);
            var positionStr = Encoding.UTF8.GetString(positionBytes);

            // Parse string menjadi integer
            if (int.TryParse(positionStr, out var position))
            {
                return position;
            }
        }
        catch (FormatException)
        {
            // Cursor bukan Base64 valid — fallback ke halaman pertama
        }

        // Cursor tidak valid — kembalikan 0 (halaman pertama)
        return 0;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Konversi objek Article menjadi Resource protocol object untuk MCP listing.
    /// Resource memiliki URI, nama, deskripsi, dan MIME type yang digunakan client
    /// untuk menampilkan daftar resources yang tersedia.
    /// </summary>
    private static Resource ArticleToResource(Article article)
    {
        return new Resource
        {
            // URI mengikuti format template resource: kb://articles/{articleId}
            Uri = $"kb://articles/{article.Id}",
            // Name harus unik — menggunakan Id artikel
            Name = article.Id,
            // Title untuk tampilan user-friendly
            Title = article.Title,
            // Deskripsi singkat untuk membantu client/LLM memahami konten
            Description = $"Artikel: {article.Title} (kategori: {article.CategoryId})",
            // MIME type sesuai deklarasi artikel
            MimeType = article.MimeType
        };
    }

    #endregion
}
