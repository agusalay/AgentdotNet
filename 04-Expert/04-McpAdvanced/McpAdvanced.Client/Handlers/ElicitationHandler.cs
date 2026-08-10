// ElicitationHandler.cs — Handler untuk Elicitation request dari server
// Elicitation memungkinkan server meminta input tambahan dari user melalui client.
// Handler ini menampilkan pertanyaan/form dari server ke console,
// menampilkan opsi yang tersedia (enum, boolean), mengumpulkan jawaban user,
// dan mengembalikan respons ke server.

using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace McpAdvanced.Client.Handlers;

/// <summary>
/// Handler yang memproses elicitation request dari server.
/// Ketika server memanggil ElicitAsync(), request diteruskan ke handler ini.
/// Handler menampilkan pertanyaan ke user di console dan mengumpulkan jawaban.
/// 
/// Tipe field yang didukung:
/// - Boolean: menampilkan prompt Y/N
/// - Enum (single select): menampilkan opsi bernomor
/// - String/default: menampilkan input teks bebas
/// </summary>
public static class ElicitationHandler
{
    /// <summary>
    /// Menangani permintaan elicitation dari server.
    /// Dipanggil oleh MCP SDK ketika server mengirim ElicitRequest ke client.
    /// </summary>
    /// <param name="request">Parameter elicitation berisi message dan schema form</param>
    /// <param name="cancellationToken">Token pembatalan operasi</param>
    /// <returns>ElicitResult berisi action (accept/decline) dan content (jawaban user)</returns>
    public static ValueTask<ElicitResult> HandleAsync(
        ElicitRequestParams? request,
        CancellationToken cancellationToken)
    {
        // Tampilkan header visual untuk elicitation request
        Console.WriteLine();
        Console.WriteLine("  ┌─── 📋 ELICITATION REQUEST DITERIMA ───");
        Console.WriteLine($"  │ Waktu: {DateTime.Now:HH:mm:ss.fff}");
        Console.WriteLine("  │");

        // Jika request null atau kosong, tolak elicitation
        if (request is null)
        {
            Console.WriteLine("  │ ⚠️  Request kosong — menolak elicitation");
            Console.WriteLine("  └───────────────────────────────────────────────");
            Console.WriteLine();

            return ValueTask.FromResult(new ElicitResult { Action = "decline" });
        }

        // Tampilkan pesan/pertanyaan dari server
        if (!string.IsNullOrEmpty(request.Message))
        {
            Console.WriteLine($"  │ 💬 {request.Message}");
            Console.WriteLine("  │");
        }

        // Kumpulkan jawaban untuk setiap field dalam schema
        var content = new Dictionary<string, JsonElement>();

        // Periksa apakah ada schema yang perlu diisi user
        if (request.RequestedSchema?.Properties is { Count: > 0 } properties)
        {
            Console.WriteLine("  │ Silakan jawab pertanyaan berikut:");
            Console.WriteLine("  │ (ketik 'batal' untuk menolak seluruh form)");
            Console.WriteLine("  │");

            foreach (var (fieldName, schema) in properties)
            {
                // Cek apakah user membatalkan
                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("  │ 🚫 Dibatalkan oleh sistem");
                    Console.WriteLine("  └───────────────────────────────────────────────");
                    Console.WriteLine();
                    return ValueTask.FromResult(new ElicitResult { Action = "decline" });
                }

                // Proses field berdasarkan tipe schema
                var (success, value) = CollectFieldInput(fieldName, schema);

                if (!success)
                {
                    // User menolak/membatalkan — kirim decline
                    Console.WriteLine("  │");
                    Console.WriteLine("  │ ❌ Form dibatalkan oleh user");
                    Console.WriteLine("  └───────────────────────────────────────────────");
                    Console.WriteLine();

                    return ValueTask.FromResult(new ElicitResult { Action = "decline" });
                }

                // Simpan jawaban user ke dictionary content
                content[fieldName] = value;
            }
        }
        else
        {
            // Tidak ada schema — minta input teks sederhana
            Console.Write("  │ Jawaban Anda: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input) || input.Equals("batal", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  │ ❌ Ditolak oleh user");
                Console.WriteLine("  └───────────────────────────────────────────────");
                Console.WriteLine();
                return ValueTask.FromResult(new ElicitResult { Action = "decline" });
            }

            // Simpan input sebagai field "response"
            content["response"] = JsonSerializer.SerializeToElement(input);
        }

        // Kirim respons "accept" dengan content yang berisi jawaban user
        Console.WriteLine("  │");
        Console.WriteLine("  │ ✅ Jawaban dikirim ke server");
        Console.WriteLine("  └───────────────────────────────────────────────");
        Console.WriteLine();

        var result = new ElicitResult
        {
            Action = "accept",
            Content = content
        };

        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Mengumpulkan input dari user untuk satu field berdasarkan tipe schema.
    /// Mendukung: BooleanSchema (Y/N), EnumSchema (pilihan nomor), dan teks bebas.
    /// </summary>
    /// <param name="fieldName">Nama field yang ditanyakan</param>
    /// <param name="schema">Definisi schema field (tipe, judul, deskripsi, opsi)</param>
    /// <returns>Tuple (berhasil, nilai JsonElement). Berhasil false jika user membatalkan.</returns>
    private static (bool Success, JsonElement Value) CollectFieldInput(
        string fieldName,
        ElicitRequestParams.PrimitiveSchemaDefinition schema)
    {
        // Tampilkan judul field
        var title = schema.Title ?? fieldName;
        var description = schema.Description;

        Console.WriteLine($"  │ 📝 {title}");
        if (!string.IsNullOrEmpty(description))
        {
            Console.WriteLine($"  │    {description}");
        }

        // Tentukan tipe input berdasarkan class schema
        return schema switch
        {
            // Field boolean — tampilkan prompt Y/N
            ElicitRequestParams.BooleanSchema => CollectBooleanInput(),

            // Field enum (single select) — tampilkan opsi bernomor
            ElicitRequestParams.UntitledSingleSelectEnumSchema enumSchema
                => CollectEnumInput(enumSchema),

            // Field string atau tipe lainnya — input teks bebas
            _ => CollectTextInput(schema)
        };
    }

    /// <summary>
    /// Mengumpulkan input boolean dari user dengan prompt Y/N.
    /// Mendukung: y, yes, ya → true; n, no, tidak → false.
    /// </summary>
    /// <returns>Tuple (berhasil, JsonElement boolean)</returns>
    private static (bool Success, JsonElement Value) CollectBooleanInput()
    {
        // Loop sampai user memberikan input yang valid
        while (true)
        {
            Console.Write("  │    [Y/N]: ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();

            // Cek apakah user membatalkan
            if (input == "batal" || input == "cancel")
                return (false, default);

            // Parse input boolean
            if (input is "y" or "yes" or "ya" or "true" or "1")
            {
                return (true, JsonSerializer.SerializeToElement(true));
            }

            if (input is "n" or "no" or "tidak" or "false" or "0")
            {
                return (true, JsonSerializer.SerializeToElement(false));
            }

            // Input tidak valid — minta ulang
            Console.WriteLine("  │    ⚠️  Masukkan Y (ya) atau N (tidak)");
        }
    }

    /// <summary>
    /// Mengumpulkan input enum (pilihan) dari user dengan menampilkan opsi bernomor.
    /// User memilih dengan memasukkan nomor opsi.
    /// </summary>
    /// <param name="enumSchema">Schema enum berisi daftar opsi dan default</param>
    /// <returns>Tuple (berhasil, JsonElement string dari opsi terpilih)</returns>
    private static (bool Success, JsonElement Value) CollectEnumInput(
        ElicitRequestParams.UntitledSingleSelectEnumSchema enumSchema)
    {
        var options = enumSchema.Enum;

        // Jika tidak ada opsi, fallback ke input teks
        if (options is null || options.Count == 0)
        {
            return CollectTextInput(enumSchema);
        }

        // Tampilkan opsi bernomor
        Console.WriteLine("  │    Pilihan:");
        for (var i = 0; i < options.Count; i++)
        {
            // Tandai opsi default dengan tanda bintang
            var isDefault = options[i] == enumSchema.Default;
            var marker = isDefault ? " ← default" : "";
            Console.WriteLine($"  │      [{i + 1}] {options[i]}{marker}");
        }

        // Loop sampai user memberikan input yang valid
        while (true)
        {
            var defaultHint = !string.IsNullOrEmpty(enumSchema.Default)
                ? $" (Enter untuk '{enumSchema.Default}')"
                : "";
            Console.Write($"  │    Pilih nomor{defaultHint}: ");

            var input = Console.ReadLine()?.Trim();

            // Cek apakah user membatalkan
            if (input?.Equals("batal", StringComparison.OrdinalIgnoreCase) == true ||
                input?.Equals("cancel", StringComparison.OrdinalIgnoreCase) == true)
            {
                return (false, default);
            }

            // Enter kosong — gunakan default jika tersedia
            if (string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(enumSchema.Default))
            {
                Console.WriteLine($"  │    → Menggunakan default: {enumSchema.Default}");
                return (true, JsonSerializer.SerializeToElement(enumSchema.Default));
            }

            // Cek apakah input adalah nomor yang valid
            if (int.TryParse(input, out var idx) && idx >= 1 && idx <= options.Count)
            {
                var selected = options[idx - 1];
                Console.WriteLine($"  │    → Dipilih: {selected}");
                return (true, JsonSerializer.SerializeToElement(selected));
            }

            // Cek apakah input adalah nama opsi langsung (case-insensitive)
            var directMatch = options.FirstOrDefault(o =>
                o.Equals(input, StringComparison.OrdinalIgnoreCase));
            if (directMatch is not null)
            {
                Console.WriteLine($"  │    → Dipilih: {directMatch}");
                return (true, JsonSerializer.SerializeToElement(directMatch));
            }

            // Input tidak valid — minta ulang
            Console.WriteLine($"  │    ⚠️  Masukkan nomor 1-{options.Count} atau nama opsi");
        }
    }

    /// <summary>
    /// Mengumpulkan input teks bebas dari user.
    /// Digunakan untuk field string atau tipe yang tidak dikenali.
    /// </summary>
    /// <param name="schema">Schema field (untuk mengambil default jika ada)</param>
    /// <returns>Tuple (berhasil, JsonElement string dari input user)</returns>
    private static (bool Success, JsonElement Value) CollectTextInput(
        ElicitRequestParams.PrimitiveSchemaDefinition schema)
    {
        Console.Write("  │    Masukkan nilai: ");
        var input = Console.ReadLine()?.Trim();

        // Cek apakah user membatalkan
        if (input?.Equals("batal", StringComparison.OrdinalIgnoreCase) == true ||
            input?.Equals("cancel", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (false, default);
        }

        // Jika kosong, gunakan string kosong (server harus validasi required fields)
        var value = input ?? "";
        return (true, JsonSerializer.SerializeToElement(value));
    }
}
