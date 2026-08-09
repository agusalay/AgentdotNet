// ============================================================================
// Interactive Loop Helpers
// Class ini mengekspos helper functions dari interactive loop agar dapat diakses
// oleh property-based tests. Method IsExitCommand digunakan untuk mendeteksi
// perintah exit dari user secara case-insensitive.
// ============================================================================

namespace McpSdk.Client;

/// <summary>
/// Helper class yang mengekspos fungsi-fungsi dari interactive loop
/// untuk keperluan testing (termasuk property-based testing).
/// </summary>
public static class InteractiveLoopHelpers
{
    /// <summary>
    /// Mendeteksi apakah input user merupakan perintah exit.
    /// Mengembalikan true jika input adalah "exit" atau "quit" (case-insensitive).
    /// Method ini diekspos sebagai public static agar dapat di-test melalui
    /// property-based testing (FsCheck).
    /// </summary>
    /// <param name="input">Input string dari user</param>
    /// <returns>True jika input adalah perintah exit, false jika bukan</returns>
    public static bool IsExitCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        return trimmed.Equals("exit", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("quit", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Memformat argumen tool call menjadi string yang mudah dibaca.
    /// Digunakan untuk menampilkan parameter yang dikirim ke MCP tool selama interaksi.
    /// </summary>
    /// <param name="arguments">Dictionary argumen dari FunctionCallContent</param>
    /// <returns>String berformat "key=value, key=value"</returns>
    public static string FormatArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
            return string.Empty;

        return string.Join(", ", arguments.Select(kvp => $"{kvp.Key}={kvp.Value}"));
    }
}
