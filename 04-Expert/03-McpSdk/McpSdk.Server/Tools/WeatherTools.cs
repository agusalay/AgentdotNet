// ============================================================================
// WeatherTools - MCP Server Tools untuk domain cuaca
// File ini berisi implementasi tools yang diekspos melalui MCP protocol.
// Setiap method yang ditandai [McpServerTool] akan terdaftar secara otomatis
// dan dapat dipanggil oleh MCP Client melalui CallToolAsync().
// ============================================================================

using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace McpSdk.Server.Tools;

/// <summary>
/// Class yang berisi weather-related tools untuk MCP Server.
/// Ditandai dengan [McpServerToolType] agar tools di dalamnya terdaftar otomatis
/// melalui WithToolsFromAssembly() pada konfigurasi server.
/// </summary>
[McpServerToolType]
public static class WeatherTools
{
    // Unit suhu yang didukung untuk konversi
    private static readonly string[] ValidUnits = ["celsius", "fahrenheit", "kelvin"];

    // Kondisi cuaca yang digunakan untuk simulasi deterministik
    private static readonly string[] WeatherConditions =
    [
        "Cerah", "Berawan", "Hujan Ringan", "Hujan Lebat",
        "Mendung", "Berkabut", "Gerimis", "Cerah Berawan"
    ];

    /// <summary>
    /// Mendapatkan data cuaca saat ini untuk kota tertentu.
    /// Data disimulasikan secara deterministik berdasarkan hash nama kota,
    /// sehingga pemanggilan berulang dengan kota yang sama menghasilkan data konsisten.
    /// </summary>
    /// <param name="city">Nama kota yang ingin dicek cuacanya</param>
    /// <returns>JSON string berisi WeatherData, atau pesan error jika parameter tidak valid</returns>
    [McpServerTool, Description("Mendapatkan cuaca saat ini untuk kota tertentu")]
    public static string GetCurrentWeather(
        [Description("Nama kota, contoh: Jakarta, Surabaya")] string city)
    {
        // Validasi parameter: city tidak boleh null atau whitespace
        if (string.IsNullOrWhiteSpace(city))
        {
            return "Parameter 'city' tidak boleh kosong. Berikan nama kota yang valid.";
        }

        // Menggunakan hash deterministik dari nama kota untuk menghasilkan
        // data cuaca yang konsisten pada setiap pemanggilan dengan kota yang sama.
        var hash = GetCityHash(city);

        // Simulasi suhu berdasarkan hash (range: 15.0 - 39.9 Celsius)
        var temperature = 15.0 + (hash % 250) / 10.0;

        // Simulasi kondisi cuaca berdasarkan hash
        var conditionIndex = hash % WeatherConditions.Length;
        var condition = WeatherConditions[conditionIndex];

        // Simulasi humidity berdasarkan hash (range: 40 - 99%)
        var humidity = 40 + (hash % 60);

        // Membuat objek WeatherData dengan data simulasi
        var weatherData = new WeatherData(
            City: city,
            TemperatureCelsius: temperature,
            Condition: condition,
            Humidity: humidity,
            Timestamp: DateTime.UtcNow);

        // Serialisasi ke JSON untuk dikembalikan ke MCP Client
        return JsonSerializer.Serialize(weatherData);
    }

    /// <summary>
    /// Mengkonversi nilai suhu antar unit (Celsius, Fahrenheit, Kelvin).
    /// Merupakan pure calculation tanpa side effects.
    /// </summary>
    /// <param name="value">Nilai suhu yang akan dikonversi</param>
    /// <param name="fromUnit">Unit asal (celsius, fahrenheit, atau kelvin)</param>
    /// <param name="toUnit">Unit tujuan (celsius, fahrenheit, atau kelvin)</param>
    /// <returns>JSON string berisi TemperatureConversion, atau pesan error jika parameter tidak valid</returns>
    [McpServerTool, Description("Mengkonversi suhu antar unit (Celsius, Fahrenheit, Kelvin)")]
    public static string ConvertTemperature(
        [Description("Nilai suhu yang akan dikonversi")] double value,
        [Description("Unit asal: celsius, fahrenheit, atau kelvin")] string fromUnit,
        [Description("Unit tujuan: celsius, fahrenheit, atau kelvin")] string toUnit)
    {
        // Validasi: nilai suhu harus finite (bukan NaN atau Infinity)
        if (!double.IsFinite(value))
        {
            return $"Nilai suhu harus berupa angka valid (finite). Diberikan: {value}";
        }

        // Validasi: fromUnit tidak boleh null/empty dan harus unit yang didukung
        if (string.IsNullOrWhiteSpace(fromUnit))
        {
            return "Unit '' tidak valid. Gunakan: celsius, fahrenheit, atau kelvin.";
        }

        // Normalisasi unit ke lowercase untuk case-insensitive comparison
        var from = fromUnit.Trim().ToLowerInvariant();

        // Validasi: fromUnit harus salah satu dari unit yang didukung
        if (!ValidUnits.Contains(from))
        {
            return $"Unit '{fromUnit}' tidak valid. Gunakan: celsius, fahrenheit, atau kelvin.";
        }

        // Validasi: toUnit tidak boleh null/empty dan harus unit yang didukung
        if (string.IsNullOrWhiteSpace(toUnit))
        {
            return "Unit '' tidak valid. Gunakan: celsius, fahrenheit, atau kelvin.";
        }

        var to = toUnit.Trim().ToLowerInvariant();

        if (!ValidUnits.Contains(to))
        {
            return $"Unit '{toUnit}' tidak valid. Gunakan: celsius, fahrenheit, atau kelvin.";
        }

        // Konversi suhu: pertama konversi ke Celsius sebagai intermediate,
        // lalu dari Celsius ke unit tujuan.
        var celsius = ConvertToCelsius(value, from);
        var result = ConvertFromCelsius(celsius, to);

        // Membuat objek TemperatureConversion dengan hasil kalkulasi
        var conversion = new TemperatureConversion(
            OriginalValue: value,
            FromUnit: from,
            ConvertedValue: result,
            ToUnit: to);

        // Serialisasi ke JSON untuk dikembalikan ke MCP Client
        return JsonSerializer.Serialize(conversion);
    }

    /// <summary>
    /// Mendapatkan prakiraan cuaca beberapa hari ke depan untuk kota tertentu.
    /// Menggunakan async/await pattern untuk mendemonstrasikan tool asinkron.
    /// Data disimulasikan secara deterministik berdasarkan hash nama kota dan indeks hari.
    /// </summary>
    /// <param name="city">Nama kota yang ingin dicek prakiraan cuacanya</param>
    /// <param name="days">Jumlah hari prakiraan (1-7, default 3)</param>
    /// <returns>JSON string berisi ForecastDay[], atau pesan error jika parameter tidak valid</returns>
    [McpServerTool, Description("Mendapatkan prakiraan cuaca beberapa hari ke depan")]
    public static async Task<string> GetWeatherForecast(
        [Description("Nama kota")] string city,
        [Description("Jumlah hari (1-7)")] int days = 3)
    {
        // Validasi parameter: city tidak boleh null atau whitespace
        if (string.IsNullOrWhiteSpace(city))
        {
            return "Parameter 'city' tidak boleh kosong. Berikan nama kota yang valid.";
        }

        // Validasi parameter: days harus dalam range 1-7
        if (days < 1 || days > 7)
        {
            return $"Parameter 'days' harus antara 1 dan 7. Diberikan: {days}";
        }

        // Simulasi async API call (mendemonstrasikan async/await pattern)
        await Task.Delay(100);

        // Menggunakan hash deterministik dari nama kota sebagai basis data simulasi
        var cityHash = GetCityHash(city);

        // Membuat array prakiraan cuaca untuk setiap hari
        var forecast = new ForecastDay[days];
        for (int i = 0; i < days; i++)
        {
            // Hash unik per hari berdasarkan hash kota dan indeks hari
            var dayHash = cityHash + (i * 7);

            // Simulasi suhu tertinggi (range: 20.0 - 39.9 Celsius)
            var high = 20.0 + (dayHash % 200) / 10.0;

            // Simulasi suhu terendah (selalu lebih rendah dari suhu tertinggi)
            var low = high - 5.0 - (dayHash % 50) / 10.0;

            // Simulasi kondisi cuaca berdasarkan hash hari
            var conditionIndex = dayHash % WeatherConditions.Length;
            var condition = WeatherConditions[conditionIndex];

            forecast[i] = new ForecastDay(
                Date: DateTime.UtcNow.Date.AddDays(i + 1),
                HighCelsius: high,
                LowCelsius: low,
                Condition: condition);
        }

        // Serialisasi array ForecastDay ke JSON untuk dikembalikan ke MCP Client
        return JsonSerializer.Serialize(forecast);
    }

    /// <summary>
    /// Menghasilkan hash deterministik dari nama kota.
    /// Digunakan agar data cuaca simulasi konsisten untuk kota yang sama
    /// di setiap pemanggilan dan lintas platform.
    /// </summary>
    private static int GetCityHash(string city)
    {
        // Hash sederhana berbasis karakter untuk konsistensi lintas platform.
        // Tidak menggunakan string.GetHashCode() karena hasilnya bisa berbeda
        // antar runtime/platform.
        var normalized = city.Trim().ToLowerInvariant();
        int hash = 0;
        foreach (char c in normalized)
        {
            hash = (hash * 31) + c;
        }
        return Math.Abs(hash);
    }

    /// <summary>
    /// Mengkonversi nilai suhu dari unit tertentu ke Celsius.
    /// Digunakan sebagai langkah intermediate dalam konversi antar unit.
    /// </summary>
    private static double ConvertToCelsius(double value, string fromUnit) => fromUnit switch
    {
        "celsius" => value,
        "fahrenheit" => (value - 32) * 5.0 / 9.0,
        "kelvin" => value - 273.15,
        _ => value // Tidak akan tercapai karena sudah divalidasi
    };

    /// <summary>
    /// Mengkonversi nilai suhu dari Celsius ke unit tujuan.
    /// Digunakan sebagai langkah akhir dalam konversi antar unit.
    /// </summary>
    private static double ConvertFromCelsius(double celsius, string toUnit) => toUnit switch
    {
        "celsius" => celsius,
        "fahrenheit" => celsius * 9.0 / 5.0 + 32,
        "kelvin" => celsius + 273.15,
        _ => celsius // Tidak akan tercapai karena sudah divalidasi
    };
}
