# Module 7: Agents as Tools

## Overview

Module ini membangun di atas fondasi *context providers* dari Module 6 dan memperkenalkan konsep **agent composition** - teknik menggunakan agent sebagai tool untuk agent lain, memungkinkan pembangunan sistem yang modular melalui pola delegasi parent-child.

Yang didemonstrasikan dalam module ini:

- **Agent composition** - mendaftarkan child agent sebagai tool untuk parent agent menggunakan `AIFunctionFactory`, sehingga parent agent dapat mendelegasikan task ke child agent yang memiliki spesialisasi tertentu
- **Parent-child delegation** - parent agent (orchestrator) memilih child agent yang tepat berdasarkan konteks task dan mendelegasikan pekerjaan secara otomatis
- **Specialized child agents** - membuat agent dengan expertise berbeda (ResearchAgent untuk riset, WritingAgent untuk penulisan) yang masing-masing memiliki instructions, nama, dan deskripsi unik
- **Communication flow logging** - menampilkan alur komunikasi lengkap di console: siapa yang memanggil siapa, input yang dikirim, dan output yang dikembalikan
- **Fallback strategy** - jika child agent mengalami error, parent agent mendelegasikan task ke child agent lain sebagai strategi alternatif

Setelah menyelesaikan module ini, Anda akan memahami kapan menggunakan agent composition versus single agent dengan banyak tools, cara mendesain child agents yang spesialis, mekanisme delegation melalui `AIFunctionFactory`, serta strategi error handling dengan fallback ke agent lain.

---

## Prerequisites

| Tool / Resource | Keterangan |
|-----------------|------------|
| .NET 9.0 SDK | Download di [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0) |
| Azure Subscription | Diperlukan untuk mengakses Azure OpenAI resources |
| Azure CLI (2.60+) | Untuk autentikasi via `az login` |
| Azure OpenAI Resource | Resource dengan minimal satu model yang sudah di-deploy (contoh: `gpt-4o-mini`) |

### ⚠️ Prerequisite: Module 6 - Context Providers

Module ini **mengharuskan** Anda telah menyelesaikan Module 6 (Context Providers).

Lihat: [Module 6: Context Providers](../01-ContextProviders/README.md)

Konsep yang harus sudah dikuasai dari Module 6:
- Pemahaman tentang *AIContextProvider* dan bagaimana context disuntikkan ke agent sebelum invocation
- Cara menyimpan dan mengelola state antar invokasi menggunakan context lifecycle (`ProvideAIContextAsync` → agent → `StoreAIContextAsync`)
- Konsep *token management* - memahami batasan token dan strategi truncation
- Pemahaman tentang cara agent mempertahankan memory dan mengakses dynamic context
- Pengalaman dengan multiple provider registration ke satu agent

Agent composition membangun di atas konsep context dan tools - parent agent perlu memahami kapabilitas child agents (mirip tools) dan child agents dapat memanfaatkan context providers untuk mempertahankan state saat memproses delegated tasks. Pemahaman tentang bagaimana agent menggunakan tools (dari Module 3) juga krusial, karena agent-as-tool merupakan evolusi natural dari konsep tool registration.

Jika Anda belum menyelesaikan Module 6, kembali ke module tersebut terlebih dahulu.

---

## Konsep yang Dipelajari

- **Parent-child agent relationship** - arsitektur di mana satu agent (parent/orchestrator) mendelegasikan tugas ke agent lain (child) yang memiliki spesialisasi tertentu, parent agent bertindak sebagai dispatcher yang memilih child agent berdasarkan konteks task
- **AIFunctionFactory untuk agent wrapping** - menggunakan `AIFunctionFactory.Create()` untuk membungkus child agent sebagai tool yang dapat dipanggil oleh parent agent, dengan nama dan deskripsi yang membantu LLM memilih agent yang tepat
- **Delegation pattern** - mekanisme di mana parent agent menerima request dari user, menganalisis konteks, memilih child agent yang sesuai, mengirim input ke child agent, dan menerima output untuk diteruskan ke user
- **Routing logic** - bagaimana parent agent memutuskan child agent mana yang akan dipanggil berdasarkan deskripsi tool (yang merupakan wrapper dari child agent) dan konteks request user
- **Fallback strategy** - strategi penanganan error di mana jika child agent utama gagal, parent agent mendelegasikan task ke child agent lain yang tersedia sebagai alternatif, dengan maksimal 1 kali percobaan ulang

> 💡 Baca file `THEORY.md` terlebih dahulu untuk pemahaman konseptual yang lebih mendalam tentang arsitektur agent-as-tool, design patterns untuk agent composition (specialization, routing, hierarchical), trade-offs antara composed agents vs single agent, dan evolusi dari tool concept ke agent composition.

---

## Langkah-Langkah Implementasi

Berikut ringkasan alur yang dilakukan oleh aplikasi console:

1. **Load konfigurasi** - Membaca `appsettings.json` untuk mendapatkan endpoint dan nama model deployment
2. **Setup Dependency Injection** - Mendaftarkan `IConfiguration` dan services ke DI container
3. **Buat koneksi** - Membuat instance `AzureOpenAIClient` menggunakan `DefaultAzureCredential`
4. **Buat child agents** - Membuat ResearchAgent (spesialis riset) dan WritingAgent (spesialis penulisan) menggunakan `.AsAIAgent()` dengan instructions, nama, dan deskripsi unik masing-masing
5. **Bungkus child agents sebagai tools** - Menggunakan `AIFunctionFactory.Create()` untuk mendaftarkan setiap child agent sebagai tool yang dapat dipanggil oleh parent agent
6. **Buat parent agent (orchestrator)** - Membuat parent agent dengan instructions orchestration dan mendaftarkan child agent tools kepadanya
7. **Demonstrasi delegasi task riset** - Mengirim request yang memerlukan riset, parent agent memilih ResearchAgent, menampilkan alur komunikasi di console
8. **Demonstrasi delegasi task penulisan** - Mengirim request yang memerlukan penulisan, parent agent memilih WritingAgent, menampilkan alur komunikasi di console
9. **Demonstrasi routing decision** - Mengirim minimal dua task berbeda secara berurutan, menampilkan alasan pemilihan child agent oleh parent agent sebelum delegasi
10. **Demonstrasi fallback strategy** - Mensimulasikan error pada child agent utama, parent agent mendelegasikan ke child agent alternatif, menampilkan strategi alternatif yang diambil

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
dotnet run --project 03-Advanced/02-AgentsAsTools/AgentsAsTools.csproj
```

---

## Expected Output

Ketika aplikasi berjalan dengan sukses, Anda akan melihat output seperti berikut di console:

```
══════════════════════════════════════════════════════════════
  Agents as Tools - Agent Composition dan Delegation
══════════════════════════════════════════════════════════════

[INFO] Koneksi ke Azure OpenAI berhasil.

── Inisialisasi Child Agents ──────────────────────────────────
  [INIT] Agent 'ResearchAgent' berhasil dibuat.
         Deskripsi: Spesialis riset dan pencarian informasi
  [INIT] Agent 'WritingAgent' berhasil dibuat.
         Deskripsi: Spesialis penulisan dan penyuntingan konten

[INFO] Parent agent (Orchestrator) berhasil dibuat.
[INFO] Child agents terdaftar sebagai tools: ResearchAgent, WritingAgent

── Demo 1: Delegasi Task ke ResearchAgent ─────────────────────
Mendemonstrasikan parent agent mendelegasikan task riset
ke ResearchAgent.

User: Cari informasi tentang keuntungan menggunakan microservices
      architecture.

[DELEGATION] Parent Agent → ResearchAgent
  Input: "Cari informasi tentang keuntungan menggunakan
          microservices architecture"

  [ResearchAgent] Memproses riset...

[RESULT] ResearchAgent → Parent Agent
  Output: Berikut hasil riset tentang keuntungan microservices
          architecture:
          1. Skalabilitas independen - setiap service dapat
             di-scale secara terpisah sesuai kebutuhan
          2. Deploy independen - perubahan pada satu service
             tidak memerlukan deploy ulang seluruh sistem
          3. Technology diversity - setiap service dapat
             menggunakan stack yang paling sesuai...

Parent Agent: Berdasarkan hasil riset dari tim riset saya,
berikut keuntungan utama microservices architecture...

── Demo 2: Delegasi Task ke WritingAgent ──────────────────────
Mendemonstrasikan parent agent mendelegasikan task penulisan
ke WritingAgent.

User: Buatkan ringkasan eksekutif tentang AI agents untuk
      presentasi ke manajemen.

[DELEGATION] Parent Agent → WritingAgent
  Input: "Buatkan ringkasan eksekutif tentang AI agents untuk
          presentasi ke manajemen"

  [WritingAgent] Memproses penulisan...

[RESULT] WritingAgent → Parent Agent
  Output: # Ringkasan Eksekutif: AI Agents
          AI agents merupakan evolusi dari Large Language Models
          yang mampu melakukan aksi secara otonom...

Parent Agent: Berikut ringkasan eksekutif yang telah disiapkan
oleh tim penulisan saya...

── Demo 3: Routing Decision - Parent Memilih Child Agent ──────
Mendemonstrasikan bagaimana parent agent memilih child agent
yang tepat berdasarkan konteks task.

Task 1: "Apa tren terbaru di bidang AI?"
[ROUTING] Parent memilih: ResearchAgent
  Alasan: Task memerlukan pencarian dan analisis informasi

Task 2: "Tulis artikel blog tentang hasil riset tersebut."
[ROUTING] Parent memilih: WritingAgent
  Alasan: Task memerlukan pembuatan konten tulisan

── Demo 4: Fallback Strategy ──────────────────────────────────
Mendemonstrasikan penanganan error dengan fallback ke agent
lain.

User: Tuliskan laporan tentang perkembangan cloud computing.

[DELEGATION] Parent Agent → WritingAgent
  [ERROR] WritingAgent gagal: Timeout setelah 30 detik.

[FALLBACK] Parent Agent → ResearchAgent (strategi alternatif)
  Input: "Buat ringkasan informatif tentang perkembangan
          cloud computing"

[RESULT] ResearchAgent → Parent Agent
  Output: Perkembangan cloud computing dalam beberapa tahun
          terakhir menunjukkan tren signifikan...

Parent Agent: Meskipun tim penulisan sedang tidak tersedia,
saya berhasil mendapatkan informasi dari tim riset sebagai
alternatif...

✓ Fallback berhasil - task diselesaikan melalui agent
  alternatif.

══════════════════════════════════════════════════════════════
```

> ⚠️ Output aktual akan berbeda setiap kali dijalankan karena sifat generatif dari LLM. Response content dan detail komunikasi yang ditampilkan akan sesuai dengan kondisi saat runtime.

---

## Troubleshooting

### ❌ Error: Parent agent tidak mendelegasikan ke child agent (merespons sendiri)

**Penyebab**: Deskripsi tool (wrapper child agent) tidak cukup jelas untuk LLM, sehingga LLM tidak mengenali kapan harus memanggil tool tersebut.

**Solusi**:
- Periksa bahwa deskripsi tool yang membungkus child agent sudah jelas dan spesifik - LLM menggunakan deskripsi ini untuk memutuskan kapan memanggil tool
- Pastikan instructions parent agent menyebutkan bahwa ia harus mendelegasikan task ke child agents, bukan menjawab sendiri
- Verifikasi bahwa tools (child agent wrappers) sudah terdaftar dengan benar pada parent agent saat pembuatan

---

### ❌ Error: Child agent mengembalikan response kosong atau error

**Penyebab**: Instructions child agent tidak sesuai dengan input yang diberikan, atau terjadi kegagalan koneksi ke LLM saat child agent memproses request.

**Solusi**:
- Periksa log console untuk melihat input yang dikirim ke child agent - pastikan format input sesuai dengan yang diharapkan oleh child agent
- Verifikasi bahwa child agent dapat berjalan secara independen (test langsung tanpa parent agent)
- Pastikan koneksi ke Azure OpenAI stabil dan token belum expired (`az login`)
- Periksa apakah rate limiting terjadi karena multiple agent calls dalam waktu singkat

---

### ❌ Error: Fallback tidak terjadi saat child agent gagal

**Penyebab**: Error handling pada parent agent tidak mengimplementasikan logika fallback, atau exception dari child agent tidak ditangkap dengan benar.

**Solusi**:
- Pastikan wrapper function untuk child agent menangkap exception dan mengembalikan informasi error ke parent agent (bukan throw exception langsung)
- Verifikasi bahwa parent agent instructions menyebutkan strategi fallback - jika satu agent gagal, coba agent lain
- Periksa bahwa ada minimal 2 child agents yang terdaftar sehingga alternatif tersedia

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
- Pastikan menjalankan `dotnet run` dari folder `03-Advanced/02-AgentsAsTools/`
- Periksa format JSON (pastikan tidak ada trailing comma atau syntax error)
- Verifikasi bahwa field `Endpoint` dan `DeploymentName` terisi

---

## Referensi

- [Agent Composition and Delegation - Microsoft Agent Framework](https://learn.microsoft.com/en-us/microsoft/agents/concepts/agent-composition)
- [AIFunctionFactory - Creating Tools from Functions](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
- [Build AI Agents with .NET](https://learn.microsoft.com/en-us/dotnet/ai/get-started/build-ai-agents)
- [Multi-Agent Architecture Patterns](https://learn.microsoft.com/en-us/microsoft/agents/concepts/multi-agent)
- [Microsoft.Extensions.AI - Overview](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
