// =============================================================================
// WeatherTool.cs - Definisi function tools untuk demonstrasi tool use
// Berisi static methods yang akan dikonversi menjadi AI tools menggunakan
// AIFunctionFactory.Create() sehingga agent dapat memanggil fungsi-fungsi ini
// =============================================================================

using System.ComponentModel;

namespace AddingTools.Tools;

/// <summary>
/// Kelas berisi tool-tool terkait cuaca yang dapat digunakan oleh agent.
/// Setiap method memiliki atribut [Description] agar LLM memahami kapan
/// dan bagaimana menggunakan tool tersebut.
/// </summary>
public static class WeatherTool
{
    // Data cuaca simulasi untuk beberapa kota
    // Dalam produksi, ini akan memanggil API cuaca yang sebenarnya
    private static readonly Dictionary<string, WeatherData> SimulatedWeather = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Jakarta"] = new("Jakarta", 32, 75, "Cerah berawan", "Angin sepoi-sepoi dari timur"),
        ["Bandung"] = new("Bandung", 24, 80, "Berawan", "Angin tenang"),
        ["Surabaya"] = new("Surabaya", 34, 70, "Cerah", "Angin kencang dari utara"),
        ["Yogyakarta"] = new("Yogyakarta", 30, 78, "Hujan ringan", "Angin sedang dari barat"),
        ["Bali"] = new("Bali", 29, 82, "Cerah", "Angin laut sepoi-sepoi"),
        ["Medan"] = new("Medan", 31, 85, "Hujan deras", "Angin kencang dari barat daya"),
        ["Makassar"] = new("Makassar", 33, 72, "Cerah berawan", "Angin sedang dari selatan"),
    };

    // Data prakiraan cuaca simulasi (3 hari ke depan)
    private static readonly Dictionary<string, string[]> SimulatedForecast = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Jakarta"] = ["Hari 1: Cerah 33°C", "Hari 2: Berawan 31°C", "Hari 3: Hujan ringan 29°C"],
        ["Bandung"] = ["Hari 1: Berawan 23°C", "Hari 2: Hujan 21°C", "Hari 3: Cerah 25°C"],
        ["Surabaya"] = ["Hari 1: Cerah 35°C", "Hari 2: Cerah 34°C", "Hari 3: Berawan 32°C"],
        ["Yogyakarta"] = ["Hari 1: Hujan 28°C", "Hari 2: Berawan 29°C", "Hari 3: Cerah 31°C"],
        ["Bali"] = ["Hari 1: Cerah 30°C", "Hari 2: Cerah 29°C", "Hari 3: Berawan 28°C"],
        ["Medan"] = ["Hari 1: Hujan 30°C", "Hari 2: Hujan deras 28°C", "Hari 3: Berawan 31°C"],
        ["Makassar"] = ["Hari 1: Cerah 34°C", "Hari 2: Cerah berawan 33°C", "Hari 3: Cerah 35°C"],
    };

    /// <summary>
    /// Mendapatkan informasi cuaca saat ini untuk kota tertentu.
    /// LLM akan memanggil tool ini ketika user bertanya tentang cuaca terkini.
    /// </summary>
    /// <param name="cityName">Nama kota yang ingin diketahui cuacanya (contoh: Jakarta, Bandung, Surabaya)</param>
    /// <returns>Informasi cuaca dalam format teks yang mudah dibaca</returns>
    [Description("Mendapatkan informasi cuaca saat ini untuk kota di Indonesia. " +
                 "Gunakan tool ini ketika user bertanya tentang cuaca, suhu, atau kondisi atmosfer suatu kota. " +
                 "Parameter: nama kota di Indonesia.")]
    public static string GetCurrentWeather(string cityName)
    {
        // Mencari data cuaca berdasarkan nama kota (case-insensitive)
        if (SimulatedWeather.TryGetValue(cityName.Trim(), out var weather))
        {
            // Mengembalikan informasi cuaca lengkap dalam format yang mudah dibaca
            return $"Cuaca di {weather.City}: {weather.Condition}, " +
                   $"Suhu: {weather.TemperatureCelsius}°C, " +
                   $"Kelembaban: {weather.HumidityPercent}%, " +
                   $"Angin: {weather.WindDescription}";
        }

        // Kota tidak ditemukan dalam database simulasi
        return $"Data cuaca untuk kota '{cityName}' tidak tersedia. " +
               $"Kota yang tersedia: {string.Join(", ", SimulatedWeather.Keys)}";
    }

    /// <summary>
    /// Mendapatkan prakiraan cuaca 3 hari ke depan untuk kota tertentu.
    /// LLM akan memanggil tool ini ketika user bertanya tentang prakiraan cuaca.
    /// </summary>
    /// <param name="cityName">Nama kota yang ingin diketahui prakiraan cuacanya</param>
    /// <returns>Prakiraan cuaca 3 hari dalam format teks</returns>
    [Description("Mendapatkan prakiraan cuaca 3 hari ke depan untuk kota di Indonesia. " +
                 "Gunakan tool ini ketika user bertanya tentang prakiraan, perkiraan, atau rencana cuaca beberapa hari ke depan. " +
                 "Parameter: nama kota di Indonesia.")]
    public static string GetWeatherForecast(string cityName)
    {
        // Mencari data prakiraan berdasarkan nama kota
        if (SimulatedForecast.TryGetValue(cityName.Trim(), out var forecast))
        {
            // Menggabungkan prakiraan 3 hari menjadi satu string
            return $"Prakiraan cuaca di {cityName.Trim()} (3 hari ke depan):\n" +
                   string.Join("\n", forecast);
        }

        // Kota tidak ditemukan dalam database prakiraan
        return $"Data prakiraan untuk kota '{cityName}' tidak tersedia. " +
               $"Kota yang tersedia: {string.Join(", ", SimulatedForecast.Keys)}";
    }

    // Record internal untuk menyimpan data cuaca simulasi
    private sealed record WeatherData(
        string City,
        int TemperatureCelsius,
        int HumidityPercent,
        string Condition,
        string WindDescription);
}
