namespace McpSdk.Server;

/// <summary>
/// Data model untuk cuaca saat ini yang dikembalikan oleh tool GetCurrentWeather.
/// Merepresentasikan kondisi cuaca di suatu kota pada waktu tertentu.
/// </summary>
public record WeatherData(
    string City,
    double TemperatureCelsius,
    string Condition,
    int Humidity,
    DateTime Timestamp);

/// <summary>
/// Data model untuk prakiraan cuaca satu hari.
/// Digunakan sebagai elemen array yang dikembalikan oleh tool GetWeatherForecast.
/// </summary>
public record ForecastDay(
    DateTime Date,
    double HighCelsius,
    double LowCelsius,
    string Condition);

/// <summary>
/// Data model untuk hasil konversi suhu antar unit.
/// Menyimpan nilai asli, unit asal, nilai hasil konversi, dan unit tujuan.
/// </summary>
public record TemperatureConversion(
    double OriginalValue,
    string FromUnit,
    double ConvertedValue,
    string ToUnit);
