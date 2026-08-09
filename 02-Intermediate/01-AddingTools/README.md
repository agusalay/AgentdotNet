# Module 3: Adding Tools

## Overview

Module ini membangun di atas fondasi *agent creation* dari Module 2 dan memperkenalkan konsep **function tools** - kemampuan agent untuk melakukan aksi nyata di luar *text generation*.

Yang didemonstrasikan dalam module ini:

- **Mendefinisikan function tools** menggunakan `AIFunctionFactory.Create()` dengan metadata deskriptif agar LLM dapat menemukan dan memilih tool yang tepat
- **Mendaftarkan tools ke agent** melalui `ChatClientAgentOptions.Tools` sehingga agent mengetahui tools apa saja yang tersedia
- **Tool execution logging** - mencatat nama tool yang dipanggil beserta parameter yang dikirim ke console untuk transparansi
- **MCP integration** - menghubungkan agent ke *Model Context Protocol* server untuk mengakses external tools

Setelah menyelesaikan module ini, Anda akan memahami bagaimana agent berinteraksi dengan tools, mekanisme *function calling* oleh LLM, dan cara mengintegrasikan tools eksternal via MCP.

---

## Prerequisites

| Tool / Resource | Keterangan |
|-----------------|------------|
| .NET 9.0 SDK | Download di [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0) |
| Azure Subscription | Diperlukan untuk mengakses Azure OpenAI resources |
| Azure CLI (2.60+) | Untuk autentikasi via `az login` |
| Azure OpenAI Resource | Resource dengan minimal satu model yang sudah di-deploy (contoh: `gpt-4o-mini`) |

### ⚠️ Prerequisite: Module 2 - From LLMs to Agents

Module ini **mengharuskan** Anda telah menyelesaikan [Module 2: From LLMs to Agents](../../01-Beginner/02-FromLlmsToAgents/README.md). Konsep yang harus sudah dikuasai:

- Cara membuat agent menggunakan `.AsAIAgent()` dengan custom *instructions*
- Pemahaman tentang `AIAgent` dan bagaimana *instructions* membentuk perilaku agent
- Konsep *AgentSession* untuk manajemen state percakapan
- Interactive conversation loop dengan *graceful exit handling*
- Pola konfigurasi dengan `IConfiguration` dan dependency injection

Jika Anda belum menyelesaikan Module 2, kembali ke module tersebut terlebih dahulu.

---

## Konsep yang Dipelajari

- Apa itu *function tools* dan bagaimana LLM melakukan *function calling*
- Mekanisme *tool invocation cycle*: user request → LLM memilih tool → eksekusi tool → return result → LLM melanjutkan response
- Cara membuat tools menggunakan `AIFunctionFactory.Create()` dengan `[Description]` attribute
- Cara mendaftarkan tools ke agent via *tool registration*
- Konsep *Model Context Protocol* (MCP) untuk integrasi tools eksternal
- Tool execution logging - mencatat aktivitas tool untuk debugging dan transparansi
- Best practices dalam mendesain tools: naming, deskripsi yang jelas, dan error handling

> 💡 Baca file `THEORY.md` terlebih dahulu untuk pemahaman konseptual yang lebih mendalam tentang arsitektur tool system, perbedaan function tools vs MCP tools, dan strategi tool design.

---

## Langkah-Langkah Implementasi

Berikut ringkasan alur yang dilakukan oleh aplikasi console:

1. **Load konfigurasi** - Membaca `appsettings.json` untuk mendapatkan endpoint dan nama model deployment
2. **Setup Dependency Injection** - Mendaftarkan `IConfiguration` dan services ke DI container
3. **Buat koneksi** - Membuat instance `AzureOpenAIClient` menggunakan `DefaultAzureCredential`
4. **Definisikan function tools** - Membuat minimal 2 function tools dengan `AIFunctionFactory.Create()`, masing-masing memiliki nama, deskripsi, dan parameter schema
5. **Daftarkan tools ke agent** - Mendaftarkan semua tools melalui `ChatClientAgentOptions.Tools` saat membuat agent
6. **Setup MCP client** - Menghubungkan ke MCP server untuk mengakses external tools
7. **Kirim prompt ke agent** - Mengirim prompt yang memicu agent untuk memanggil tool
8. **Log tool invocation** - Mencatat nama tool dan parameter yang dikirim ke console saat agent memutuskan untuk memanggil tool
9. **Eksekusi tool** - Menjalankan tool dan mengembalikan hasil ke agent untuk melanjutkan response generation
10. **Demonstrasi MCP tool** - Mengirim prompt yang memicu agent untuk menggunakan external tool dari MCP server
11. **Handle error** - Menangani kegagalan tool execution dengan menampilkan nama tool dan alasan kegagalan, lalu mengembalikan informasi error ke agent

---

## Cara Menjalankan

### 1. Konfigurasi

Pastikan file `appsettings.json` sudah berisi endpoint dan deployment name yang valid:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://<your-resource>.openai.azure.com/",
    "DeploymentName": "gpt-4o-mini"
  }
}
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Build Project

```bash
dotnet build
```

### 4. Jalankan Aplikasi

```bash
dotnet run
```

Atau dari root directory:

```bash
dotnet run --project 02-Intermediate/01-AddingTools/AddingTools.csproj
```

---

## Expected Output

Ketika aplikasi berjalan dengan sukses, Anda akan melihat output seperti berikut di console:

```
══════════════════════════════════════════════════════════════
  Adding Tools - Menambahkan Function Tools ke Agent
══════════════════════════════════════════════════════════════

[INFO] Koneksi ke Azure OpenAI berhasil.
[INFO] Tools terdaftar: GetWeather, Calculator

── Demo 1: Function Tool Invocation ───────────────────────────
Mengirim prompt yang memicu agent untuk memanggil function tool.

Prompt: "Bagaimana cuaca di Jakarta hari ini?"

[TOOL CALL] GetWeather
  Parameters: { "city": "Jakarta" }
[TOOL RESULT] GetWeather → "Cerah, 32°C, kelembaban 75%"

Agent Response:
Cuaca di Jakarta hari ini cerah dengan suhu 32°C dan
kelembaban udara sekitar 75%. Cocok untuk aktivitas luar
ruangan!

── Demo 2: Multiple Tool Calls ────────────────────────────────
Mengirim prompt yang membutuhkan lebih dari satu tool call.

Prompt: "Hitung 15% dari 250000, lalu cek cuaca di Bandung"

[TOOL CALL] Calculator
  Parameters: { "expression": "0.15 * 250000" }
[TOOL RESULT] Calculator → "37500"

[TOOL CALL] GetWeather
  Parameters: { "city": "Bandung" }
[TOOL RESULT] GetWeather → "Berawan, 24°C, kelembaban 80%"

Agent Response:
15% dari 250.000 adalah 37.500. Untuk cuaca di Bandung saat
ini berawan dengan suhu 24°C dan kelembaban 80%.

── Demo 3: MCP External Tool ──────────────────────────────────
Mendemonstrasikan integrasi dengan MCP server untuk mengakses
external tool.

[INFO] Terhubung ke MCP server: localhost:3000
[INFO] External tools tersedia: SearchWeb

Prompt: "Cari informasi terbaru tentang Microsoft Agent Framework"

[TOOL CALL] SearchWeb (MCP)
  Parameters: { "query": "Microsoft Agent Framework 2025" }
[TOOL RESULT] SearchWeb → "Microsoft Agent Framework adalah..."

Agent Response:
Berdasarkan pencarian, Microsoft Agent Framework adalah SDK
unified dari Microsoft untuk membangun AI agents...

══════════════════════════════════════════════════════════════
```

> ⚠️ Output aktual akan berbeda setiap kali dijalankan karena sifat generatif dari LLM. Nama tool dan parameter yang ditampilkan di log akan sesuai dengan tool yang dipanggil oleh agent.

---

## Troubleshooting

### ❌ Error: "Tool not found" atau "No tools registered"

**Penyebab**: Tools belum didaftarkan dengan benar ke agent, atau nama tool tidak cocok.

**Solusi**:
- Pastikan tools dibuat menggunakan `AIFunctionFactory.Create()` dengan nama dan deskripsi yang valid
- Verifikasi bahwa tools sudah didaftarkan ke `ChatClientAgentOptions.Tools` sebelum agent dibuat
- Periksa apakah `[Description]` attribute sudah ditambahkan ke method yang dijadikan tool

---

### ❌ Error: "MCP connection failed" atau "Cannot connect to MCP server"

**Penyebab**: MCP server tidak berjalan, URL salah, atau port tidak tersedia.

**Solusi**:
- Pastikan MCP server sudah berjalan sebelum menjalankan aplikasi
- Periksa URL dan port MCP server di konfigurasi
- Verifikasi bahwa firewall tidak memblokir koneksi ke port MCP server
- Coba jalankan MCP server secara terpisah dan test konektivitasnya

---

### ❌ Error: "Tool execution failed" atau tool mengembalikan error

**Penyebab**: Tool function melempar exception saat dieksekusi, parameter tidak valid, atau external service tidak tersedia.

**Solusi**:
- Periksa log console untuk melihat nama tool dan parameter yang dikirim
- Pastikan parameter yang dikirim agent sesuai dengan schema yang didefinisikan di tool
- Jika menggunakan external service (API), pastikan service tersebut aktif dan credentials valid
- Aplikasi akan mengembalikan informasi error ke agent agar percakapan dapat dilanjutkan tanpa crash

---

### ❌ Error: "Authentication failed" atau "DefaultAzureCredential failed"

**Penyebab**: Azure CLI belum login atau token sudah expired.

**Solusi**:
```bash
az login
# Jika menggunakan tenant tertentu:
az login --tenant <tenant-id>
```

---

### ❌ Error: "appsettings.json not found" atau "Configuration invalid"

**Penyebab**: File `appsettings.json` tidak ada di directory saat ini atau format JSON tidak valid.

**Solusi**:
- Pastikan menjalankan `dotnet run` dari folder `02-Intermediate/01-AddingTools/`
- Periksa format JSON (pastikan tidak ada trailing comma atau syntax error)
- Verifikasi bahwa field `Endpoint` dan `DeploymentName` terisi

---

## Referensi

- [Microsoft Agent Framework - Tools and Function Calling](https://learn.microsoft.com/en-us/microsoft/agents/)
- [Function Calling with Azure OpenAI](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/function-calling)
- [Model Context Protocol (MCP) Specification](https://modelcontextprotocol.io/)
- [Build AI Agents with .NET](https://learn.microsoft.com/en-us/dotnet/ai/get-started/build-ai-agents)
- [Microsoft.Extensions.AI - Tool Support](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
