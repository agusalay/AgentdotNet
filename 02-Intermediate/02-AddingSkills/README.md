# Module 4: Adding Skills

## Overview

Module ini membangun di atas fondasi *function tools* dari Module 3 dan memperkenalkan konsep **skills** - mekanisme untuk mengemas tools terkait menjadi unit yang reusable dan modular.

Yang didemonstrasikan dalam module ini:

- **Skill packaging** - mengelompokkan beberapa tools terkait secara fungsional menjadi satu unit kohesif dengan nama dan deskripsi yang jelas
- **Reusable capabilities** - membuat kapabilitas yang dapat digunakan ulang tanpa duplikasi kode
- **Skill registration** - mendaftarkan skill ke agent dengan konfirmasi output yang mencantumkan nama skill dan jumlah tools
- **Skill sharing across agents** - mendaftarkan satu skill yang sama ke multiple agents yang berbeda

Setelah menyelesaikan module ini, Anda akan memahami bagaimana skills mengorganisir tools menjadi kapabilitas modular, perbedaan antara *flat-tools architecture* dan *skill-based architecture*, serta bagaimana skill sharing memungkinkan reusability lintas agent.

---

## Prerequisites

| Tool / Resource | Keterangan |
|-----------------|------------|
| .NET 9.0 SDK | Download di [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0) |
| Azure Subscription | Diperlukan untuk mengakses Azure OpenAI resources |
| Azure CLI (2.60+) | Untuk autentikasi via `az login` |
| Azure OpenAI Resource | Resource dengan minimal satu model yang sudah di-deploy (contoh: `gpt-4o-mini`) |

### ⚠️ Prerequisite: Module 3 - Adding Tools

Module ini **mengharuskan** Anda telah menyelesaikan [Module 3: Adding Tools](../01-AddingTools/README.md). Konsep yang harus sudah dikuasai:

- Cara mendefinisikan *function tools* menggunakan `AIFunctionFactory.Create()` dengan `[Description]` attribute
- Cara mendaftarkan tools ke agent melalui `ChatClientAgentOptions.Tools`
- Pemahaman tentang *tool invocation cycle* (user request → LLM memilih tool → eksekusi → return result → LLM melanjutkan)
- Tool execution logging - mencatat nama tool dan parameter yang dikirim
- Penanganan error saat tool execution gagal
- Konsep *Model Context Protocol* (MCP) untuk tools eksternal

Jika Anda belum menyelesaikan Module 3, kembali ke module tersebut terlebih dahulu.

---

## Konsep yang Dipelajari

- Apa itu *skill* dan perbedaannya dengan individual tools - skill sebagai abstraksi di atas tools
- *Skill definition* - mendefinisikan kumpulan tools dalam satu static class berdasarkan domain fungsional
- *Tool grouping* - mekanisme pengelompokan multiple tools ke satu skill identifier
- *Batch registration* - mendaftarkan semua tools dari satu skill secara bersamaan ke agent
- *Skill sharing* - menggunakan satu skill yang sama di multiple agents tanpa duplikasi
- Perbandingan *flat-tools architecture* vs *skill-based architecture* - kapan menggunakan masing-masing
- Design patterns: *functional cohesion*, *single responsibility* pada skill level, dan strategi penamaan skill

> 💡 Baca file `THEORY.md` terlebih dahulu untuk pemahaman konseptual yang lebih mendalam tentang arsitektur skill system, design patterns untuk skill composition, dan evolusi dari individual tools ke packaged skills.

---

## Langkah-Langkah Implementasi

Berikut ringkasan alur yang dilakukan oleh aplikasi console:

1. **Load konfigurasi** - Membaca `appsettings.json` untuk mendapatkan endpoint dan nama model deployment
2. **Setup Dependency Injection** - Mendaftarkan `IConfiguration` dan services ke DI container
3. **Buat koneksi** - Membuat instance `AzureOpenAIClient` menggunakan `DefaultAzureCredential`
4. **Definisikan custom skill** - Membuat static class `ResearchSkill` yang mengemas minimal 2 tools terkait (contoh: `WebSearch` + `Summarize`)
5. **Daftarkan skill ke agent** - Mendaftarkan semua tools dari skill menggunakan `AIFunctionFactory.Create()` secara batch, dengan konfirmasi output ke console
6. **Demonstrasi flat vs skill-based** - Membandingkan agent dengan individual tools versus agent dengan packaged skills, menunjukkan perbedaan struktur registrasi
7. **Kirim prompt ke agent** - Mengirim prompt yang memicu agent untuk menggunakan tools dari skill
8. **Log skill activation** - Mencatat nama skill yang diaktifkan, tools yang dieksekusi, dan urutan eksekusinya
9. **Demonstrasi skill sharing** - Mendaftarkan skill yang sama ke 2 agent berbeda dan membuktikan kedua agent dapat menggunakannya secara independen
10. **Handle error** - Menangani kegagalan registrasi skill (nama duplikat atau tool tidak valid) dengan pesan error yang informatif

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
dotnet run --project 02-Intermediate/02-AddingSkills/AddingSkills.csproj
```

---

## Expected Output

Ketika aplikasi berjalan dengan sukses, Anda akan melihat output seperti berikut di console:

```
══════════════════════════════════════════════════════════════
  Adding Skills - Mengemas Tools Menjadi Reusable Skills
══════════════════════════════════════════════════════════════

[INFO] Koneksi ke Azure OpenAI berhasil.

── Demo 1: Skill Registration ─────────────────────────────────
Mendaftarkan custom skill ke agent dengan konfirmasi output.

[SKILL] ResearchSkill terdaftar dengan 3 tools:
  • WebSearch
  • Summarize
  • ExtractKeywords

── Demo 2: Flat Tools vs Skill-Based ──────────────────────────
Membandingkan struktur registrasi antara flat tools dan
packaged skills.

[FLAT TOOLS] Agent dengan individual tools:
  Tools: [WebSearch, Summarize, ExtractKeywords, SendEmail,
          CreateNotification]
  Total: 5 tools (tanpa pengelompokan)

[SKILL-BASED] Agent dengan packaged skills:
  ResearchSkill (3 tools): WebSearch, Summarize, ExtractKeywords
  CommunicationSkill (2 tools): SendEmail, CreateNotification
  Total: 2 skills, 5 tools (terorganisir per domain)

── Demo 3: Skill Invocation ───────────────────────────────────
Mengirim prompt yang memicu agent menggunakan tools dari skill.

Prompt: "Cari informasi tentang Microsoft Agent Framework dan
         buat ringkasannya"

[SKILL ACTIVATED] ResearchSkill
  [1] WebSearch("Microsoft Agent Framework")
      → "Microsoft Agent Framework adalah SDK unified..."
  [2] Summarize("Microsoft Agent Framework adalah SDK unified...")
      → "Framework ini menyediakan..."

Agent Response:
Berdasarkan pencarian dan ringkasan, Microsoft Agent Framework
adalah SDK unified dari Microsoft yang menyediakan...

── Demo 4: Skill Sharing Across Agents ────────────────────────
Mendaftarkan ResearchSkill ke dua agent berbeda untuk
membuktikan reusability.

[SHARING] ResearchSkill → AnalystAgent ✓
[SHARING] ResearchSkill → WriterAgent ✓

AnalystAgent menggunakan ResearchSkill:
  [TOOL CALL] WebSearch("AI trends 2025")
  Agent Response: "Berdasarkan analisis data tren AI..."

WriterAgent menggunakan ResearchSkill:
  [TOOL CALL] Summarize("Artikel panjang tentang AI...")
  Agent Response: "Berikut ringkasan dalam gaya naratif..."

[INFO] Kedua agent menggunakan skill yang sama secara independen
       dengan perilaku berbeda sesuai instructions masing-masing.

══════════════════════════════════════════════════════════════
```

> ⚠️ Output aktual akan berbeda setiap kali dijalankan karena sifat generatif dari LLM. Nama skill, tools, dan response yang ditampilkan di log akan sesuai dengan yang dipanggil oleh agent.

---

## Troubleshooting

### ❌ Error: "Skill registration failed: duplicate tool name"

**Penyebab**: Dua tools dari skill yang berbeda memiliki nama yang sama, menyebabkan konflik saat registrasi.

**Solusi**:
- Pastikan setiap tool memiliki nama yang unik di seluruh agent, meskipun berada di skill berbeda
- Gunakan prefix domain pada nama tool jika diperlukan (contoh: `Research_WebSearch` vs `Data_WebSearch`)
- Periksa bahwa tidak ada skill yang didaftarkan dua kali ke agent yang sama

---

### ❌ Error: "Invalid tool definition in skill" atau tool tidak bisa dibuat

**Penyebab**: Method dalam skill class tidak memenuhi persyaratan `AIFunctionFactory.Create()` - misalnya signature tidak valid, return type tidak didukung, atau `[Description]` attribute hilang.

**Solusi**:
- Pastikan semua method dalam skill class adalah `public static`
- Verifikasi bahwa setiap method memiliki `[Description]` attribute
- Periksa bahwa parameter juga memiliki `[Description]` attribute
- Pastikan return type didukung oleh `AIFunctionFactory` (string, primitives, atau serializable objects)

---

### ❌ Error: "Skill not found" atau tools dari skill tidak tersedia untuk agent

**Penyebab**: Skill belum didaftarkan ke agent, atau tools dari skill tidak ditambahkan ke `ChatClientAgentOptions.Tools`.

**Solusi**:
- Pastikan semua tools dari skill didaftarkan menggunakan `AIFunctionFactory.Create()` untuk setiap method
- Verifikasi bahwa array tools sudah ditambahkan ke agent saat pembuatan
- Periksa output console untuk konfirmasi registrasi (nama skill + jumlah tools)

---

### ❌ Error: Skill sharing gagal - agent kedua tidak bisa menggunakan skill

**Penyebab**: Tools dibuat ulang (new instance) untuk setiap agent alih-alih menggunakan referensi yang sama, atau ada konflik konfigurasi antar agent.

**Solusi**:
- Pastikan array tools dari skill dibuat satu kali dan digunakan oleh kedua agent
- Verifikasi bahwa kedua agent tidak memiliki tools dengan nama yang sama dari sumber lain
- Periksa bahwa kedua agent berhasil di-create tanpa exception

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
- Pastikan menjalankan `dotnet run` dari folder `02-Intermediate/02-AddingSkills/`
- Periksa format JSON (pastikan tidak ada trailing comma atau syntax error)
- Verifikasi bahwa field `Endpoint` dan `DeploymentName` terisi

---

## Referensi

- [Building AI Agent Skills - Microsoft Agent Framework](https://learn.microsoft.com/en-us/microsoft/agents/concepts/skills)
- [AIFunctionFactory and Tool Organization](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.aifunctionfactory)
- [Designing Modular AI Agents](https://learn.microsoft.com/en-us/microsoft/agents/how-to/design-agents)
- [Build AI Agents with .NET](https://learn.microsoft.com/en-us/dotnet/ai/get-started/build-ai-agents)
- [Microsoft.Extensions.AI - Tool Support](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
