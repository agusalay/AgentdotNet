// McpFilters.cs — Handler Filters untuk MCP Server
// Handler filter adalah middleware-like pattern yang membungkus (wrap) tool handler,
// memungkinkan cross-cutting concerns seperti logging dan pengukuran waktu eksekusi.
// Filter dieksekusi dalam urutan pipeline: filter pertama yang didaftarkan menjadi 
// lapisan terluar (pre-logic pertama dijalankan, post-logic terakhir dijalankan).

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpAdvanced.Server.Filters;

/// <summary>
/// Kelas statis yang menyediakan handler filters untuk MCP Server.
/// Setiap filter mengikuti pola: pre-processing → panggil next → post-processing.
/// </summary>
public static class McpFilters
{
    /// <summary>
    /// Logging filter — mencatat setiap tool invocation termasuk nama tool,
    /// jumlah parameter, dan status penyelesaian.
    /// Filter ini berguna untuk audit trail dan debugging.
    /// </summary>
    public static readonly McpRequestFilter<CallToolRequestParams, CallToolResult> LoggingFilter = (next) =>
    {
        return async (context, ct) =>
        {
            // Ambil logger dari service provider yang tersedia di context
            var logger = context.Services?.GetService<ILoggerFactory>()
                ?.CreateLogger("McpAdvanced.Filters.Logging");

            var toolName = context.Params?.Name ?? "unknown";
            var paramCount = context.Params?.Arguments?.Count ?? 0;

            // Pre-processing: catat informasi pemanggilan tool
            logger?.LogInformation(
                "[Filter:Logging] Tool dipanggil: {ToolName} dengan {ParamCount} parameter",
                toolName, paramCount);

            try
            {
                // Panggil handler berikutnya dalam pipeline (atau tool handler itu sendiri)
                var result = await next(context, ct);

                // Post-processing: catat hasil sukses
                var isError = result.IsError == true;
                if (isError)
                {
                    logger?.LogWarning(
                        "[Filter:Logging] Tool selesai dengan error: {ToolName}",
                        toolName);
                }
                else
                {
                    logger?.LogInformation(
                        "[Filter:Logging] Tool selesai sukses: {ToolName}",
                        toolName);
                }

                return result;
            }
            catch (Exception ex)
            {
                // Catat exception yang terjadi selama eksekusi tool
                logger?.LogError(ex,
                    "[Filter:Logging] Tool gagal dengan exception: {ToolName}",
                    toolName);
                throw;
            }
        };
    };

    /// <summary>
    /// Timing filter — mengukur dan mencatat waktu eksekusi setiap tool invocation.
    /// Menggunakan Stopwatch untuk pengukuran waktu yang presisi.
    /// Filter ini berguna untuk performance monitoring dan identifikasi bottleneck.
    /// </summary>
    public static readonly McpRequestFilter<CallToolRequestParams, CallToolResult> TimingFilter = (next) =>
    {
        return async (context, ct) =>
        {
            // Ambil logger dari service provider
            var logger = context.Services?.GetService<ILoggerFactory>()
                ?.CreateLogger("McpAdvanced.Filters.Timing");

            var toolName = context.Params?.Name ?? "unknown";

            // Pre-processing: mulai pengukuran waktu
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Panggil handler berikutnya dalam pipeline
                var result = await next(context, ct);

                // Post-processing: hentikan stopwatch dan catat durasi
                stopwatch.Stop();
                logger?.LogInformation(
                    "[Filter:Timing] Tool {ToolName} selesai dalam {ElapsedMs}ms",
                    toolName, stopwatch.ElapsedMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                // Tetap catat waktu meskipun terjadi error
                stopwatch.Stop();
                logger?.LogWarning(
                    "[Filter:Timing] Tool {ToolName} gagal setelah {ElapsedMs}ms: {Error}",
                    toolName, stopwatch.ElapsedMilliseconds, ex.Message);
                throw;
            }
        };
    };

    /// <summary>
    /// Extension method untuk mendaftarkan semua filter dalam urutan yang benar.
    /// Urutan pendaftaran menentukan urutan eksekusi pipeline:
    /// - LoggingFilter didaftarkan pertama → menjadi lapisan terluar
    /// - TimingFilter didaftarkan kedua → berjalan di dalam LoggingFilter
    /// 
    /// Alur eksekusi: Logging-pre → Timing-pre → Tool Handler → Timing-post → Logging-post
    /// Ini memastikan logging mencakup total waktu termasuk overhead timing filter.
    /// </summary>
    public static IMcpRequestFilterBuilder AddKnowledgeBaseFilters(this IMcpRequestFilterBuilder builder)
    {
        // Filter pertama yang didaftarkan menjadi lapisan terluar dalam pipeline.
        // LoggingFilter di luar berarti akan mencatat seluruh lifecycle termasuk waktu.
        builder.AddCallToolFilter(LoggingFilter);

        // TimingFilter di dalam berarti mengukur waktu eksekusi tool yang lebih akurat
        // (tidak termasuk overhead logging filter di luar).
        builder.AddCallToolFilter(TimingFilter);

        return builder;
    }
}
