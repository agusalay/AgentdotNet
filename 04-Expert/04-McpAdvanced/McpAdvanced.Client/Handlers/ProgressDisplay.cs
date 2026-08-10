// ProgressDisplay.cs - Utility untuk menampilkan progress bar di console
// Menerima ProgressNotificationValue dari server dan menampilkan visualisasi
// progress yang informatif dengan persentase dan pesan status

using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace McpAdvanced.Client.Handlers;

/// <summary>
/// Utility class untuk menampilkan progress notifikasi dari server
/// dalam format visual progress bar di console.
/// Mengimplementasikan IProgress&lt;ProgressNotificationValue&gt; untuk integrasi
/// langsung dengan McpClient.CallToolAsync.
/// </summary>
public sealed class ProgressDisplay : IProgress<ProgressNotificationValue>
{
    // Lebar default progress bar dalam karakter
    private const int DefaultBarWidth = 30;

    // Lebar progress bar yang digunakan instance ini
    private readonly int _barWidth;

    // Label opsional yang ditampilkan sebelum progress bar
    private readonly string _label;

    /// <summary>
    /// Membuat instance ProgressDisplay baru.
    /// </summary>
    /// <param name="label">Label yang ditampilkan di awal baris (default: "Progress")</param>
    /// <param name="barWidth">Lebar progress bar dalam jumlah karakter (default: 30)</param>
    public ProgressDisplay(string label = "Progress", int barWidth = DefaultBarWidth)
    {
        // Validasi barWidth — minimal 5 karakter agar progress bar terlihat
        _barWidth = barWidth < 5 ? 5 : barWidth;
        _label = label;
    }

    /// <summary>
    /// Menerima notifikasi progress dari server dan menampilkan progress bar.
    /// Dipanggil otomatis oleh MCP client saat menerima progress notification.
    /// </summary>
    /// <param name="value">Nilai progress dari server (bisa null jika terjadi error)</param>
    public void Report(ProgressNotificationValue value)
    {
        // Tangani nilai null — tampilkan indikator tanpa persentase
        if (value is null)
        {
            PrintIndeterminate("menunggu...");
            return;
        }

        // Hitung persentase berdasarkan progress dan total
        var percentage = CalculatePercentage(value.Progress, value.Total);

        // Bangun dan tampilkan progress bar
        var bar = BuildProgressBar(percentage);
        var message = value.Message ?? string.Empty;

        // Tampilkan progress bar dengan format: [████████░░░░░░] 40% - pesan
        Console.WriteLine($"  ⏳ {_label}: {bar} {percentage,3}% — {message}");
    }

    /// <summary>
    /// Menghitung persentase dari nilai progress dan total.
    /// Menangani edge cases: total null/nol, progress melebihi total, nilai negatif.
    /// </summary>
    /// <param name="progress">Nilai progress saat ini</param>
    /// <param name="total">Total nilai maksimum (bisa null jika tidak diketahui)</param>
    /// <returns>Persentase dalam rentang 0-100</returns>
    private static int CalculatePercentage(double progress, double? total)
    {
        // Jika total tidak tersedia atau nol, anggap progress sebagai persentase langsung
        if (total is null or 0)
        {
            // Clamp progress ke rentang 0-100 jika dipakai sebagai persentase langsung
            return (int)Math.Clamp(progress, 0, 100);
        }

        // Tangani nilai negatif — normalkan ke nol
        if (progress < 0) return 0;
        if (total < 0) return 0;

        // Hitung persentase dan clamp ke 0-100
        var rawPercentage = (progress / total.Value) * 100;
        return (int)Math.Clamp(rawPercentage, 0, 100);
    }

    /// <summary>
    /// Membangun string visual progress bar dari persentase.
    /// Contoh output: [████████████░░░░░░░░░░░░░░░░░░] untuk 40%
    /// </summary>
    /// <param name="percentage">Persentase (0-100)</param>
    /// <returns>String progress bar dengan bracket</returns>
    private string BuildProgressBar(int percentage)
    {
        // Hitung jumlah karakter terisi (filled) dan kosong (empty)
        var filledCount = (int)Math.Round((double)percentage / 100 * _barWidth);

        // Pastikan filledCount tidak melebihi lebar bar
        filledCount = Math.Clamp(filledCount, 0, _barWidth);
        var emptyCount = _barWidth - filledCount;

        // Bangun string bar dengan karakter Unicode block
        var filled = new string('█', filledCount);
        var empty = new string('░', emptyCount);

        return $"[{filled}{empty}]";
    }

    /// <summary>
    /// Menampilkan progress indeterminate (tanpa persentase pasti).
    /// Digunakan ketika total tidak diketahui atau value null.
    /// </summary>
    /// <param name="message">Pesan status yang ditampilkan</param>
    private void PrintIndeterminate(string message)
    {
        // Tampilkan animasi spinner sederhana tanpa persentase
        var spinner = new string('▓', _barWidth / 2);
        var padding = new string(' ', _barWidth - _barWidth / 2);
        Console.WriteLine($"  ⏳ {_label}: [{spinner}{padding}] --- — {message}");
    }

    // =========================================================================
    // Static Factory Methods — untuk kemudahan penggunaan tanpa instansiasi manual
    // =========================================================================

    /// <summary>
    /// Membuat instance IProgress&lt;ProgressNotificationValue&gt; dengan konfigurasi default.
    /// Cocok untuk penggunaan langsung di CallToolAsync.
    /// </summary>
    /// <returns>Instance IProgress yang siap digunakan</returns>
    public static ProgressDisplay Create() => new();

    /// <summary>
    /// Membuat instance dengan label kustom.
    /// </summary>
    /// <param name="label">Label yang ditampilkan sebelum progress bar</param>
    /// <returns>Instance IProgress yang siap digunakan</returns>
    public static ProgressDisplay Create(string label) => new(label);

    /// <summary>
    /// Membuat instance dengan label dan lebar bar kustom.
    /// </summary>
    /// <param name="label">Label yang ditampilkan sebelum progress bar</param>
    /// <param name="barWidth">Lebar progress bar dalam karakter</param>
    /// <returns>Instance IProgress yang siap digunakan</returns>
    public static ProgressDisplay Create(string label, int barWidth) => new(label, barWidth);
}
