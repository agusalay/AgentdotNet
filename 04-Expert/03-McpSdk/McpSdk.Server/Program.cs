// ============================================================================
// MCP Server - Entry Point
// File ini mengkonfigurasi dan menjalankan MCP Server menggunakan Generic Host pattern.
// Server berkomunikasi melalui stdio transport (stdin/stdout) dengan MCP Client.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Membuat application builder menggunakan Generic Host pattern
// Generic Host menyediakan dependency injection, logging, dan lifecycle management
var builder = Host.CreateApplicationBuilder(args);

// Redirect logging ke stderr agar tidak mengganggu komunikasi MCP melalui stdout.
// MCP protocol menggunakan stdout untuk JSON-RPC messages, sehingga semua output diagnostik
// harus dialihkan ke stderr supaya tidak merusak format protocol.
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Mendaftarkan MCP Server ke dependency injection container.
// - AddMcpServer(): Mendaftarkan service inti MCP Server
// - WithStdioServerTransport(): Menggunakan stdin/stdout sebagai transport layer
// - WithToolsFromAssembly(): Mendaftarkan semua tools yang ditandai dengan atribut
//   [McpServerToolType] dan [McpServerTool] secara otomatis dari assembly ini
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Build dan jalankan host secara asynchronous.
// Server akan menunggu koneksi MCP Client melalui stdio tanpa menampilkan
// output ke stdout yang dapat mengganggu protocol communication.
await builder.Build().RunAsync();
