# Module 6: Context Providers

## Overview

Module ini membangun di atas fondasi *middleware pipeline* dari Module 5 dan memperkenalkan konsep **context providers** - mekanisme untuk menyediakan *memory* dan *dynamic context* kepada agent sehingga agent dapat mempertahankan state dan mengakses informasi relevan dari percakapan sebelumnya.

Yang didemonstrasikan dalam module ini:

- **AIContextProvider** - base class untuk menyuntikkan konteks secara otomatis sebelum agent invocation dan menyimpan konteks setelah agent merespons
- **Sliding window** - menyimpan N conversation turns terakhir (default: 10 turns) sebagai short-term memory agent
- **Token truncation** - memotong conversation history ketika total token melebihi batas (4000 tokens) dengan mempertahankan pesan terbaru
- **File-based context** - membaca data dari file lokal (JSON) dan menyediakannya sebagai konteks tambahan ke agent
- **Recall capability** - kemampuan agent untuk mereferensikan informasi dari percakapan sebelumnya secara akurat

Setelah menyelesaikan module ini, Anda akan memahami bagaimana context management bekerja dalam AI agent, cara mengimplementasikan conversation history yang token-aware, cara menyediakan external knowledge melalui custom context provider, serta bagaimana kualitas dan kuantitas context mempengaruhi response quality dari agent.

---

## Prerequisites

| Tool / Resource | Keterangan |
|-----------------|------------|
| .NET 9.0 SDK | Download di [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0) |
| Azure Subscription | Diperlukan untuk mengakses Azure OpenAI resources |
| Azure CLI (2.60+) | Untuk autentikasi via `az login` |
| Azure OpenAI Resource | Resource dengan minimal satu model yang sudah di-deploy (contoh: `gpt-4o-mini`) |

### ⚠️ Prerequisite: Module 5 - Adding Middleware

Module ini **mengharuskan** Anda telah menyelesaikan Module 5 (Adding Middleware).

Lihat: [Module 5: Adding Middleware](../../02-Intermediate/03-AddingMiddleware/README.md)

Konsep yang harus sudah dikuasai dari Module 5:
- Pemahaman tentang *middleware pipeline* dan bagaimana request/response mengalir melalui chain of middleware
- Cara middleware mencegat dan memodifikasi perilaku agent sebelum dan sesudah agent processing
- Konsep *pipeline execution order* - urutan registrasi menentukan urutan eksekusi
- Pemahaman tentang *short-circuit pattern* - menghentikan pipeline tanpa meneruskan ke agent
- Cara implementasi cross-cutting concerns (logging, guardrails) tanpa mengubah logika inti agent

Context providers menggunakan konsep serupa dengan middleware - keduanya beroperasi "di sekitar" agent core. Namun context providers berfokus khusus pada **data yang disediakan ke agent** (memory, knowledge) sedangkan middleware berfokus pada **perilaku pipeline** (logging, validation, blocking).

Jika Anda belum menyelesaikan Module 5, kembali ke module tersebut terlebih dahulu.

---

## Konsep yang Dipelajari

- Apa itu *AIContextProvider* - base class pada Microsoft Agent Framework yang menyediakan mekanisme `ProvideAIContextAsync()` untuk menyuntikkan konteks sebelum agent invocation dan `StoreAIContextAsync()` untuk menyimpan konteks setelah agent merespons
- *Sliding window* - strategi menyimpan hanya N conversation turns terakhir (10 turns) sebagai short-term memory, menghapus turn terlama secara FIFO saat batas terlampaui
- *Token truncation* - strategi memotong conversation history ketika estimasi total token melebihi 4000, mempertahankan pesan terbaru dan menghapus pesan terlama untuk memastikan context tetap dalam batas token limit LLM
- *File-based context provider* - custom context provider yang membaca data dari file lokal (JSON knowledge base) dan menyuntikkannya sebagai konteks tambahan ke agent, memungkinkan agent mengakses domain-specific knowledge
- *Recall capability* - kemampuan agent untuk mengingat dan mereferensikan informasi yang disebutkan user pada turn sebelumnya, membuktikan bahwa conversation history disediakan dengan benar
- *Context lifecycle* - alur lengkap dari provide context → agent processing → store context yang terjadi pada setiap agent invocation
- *ProviderSessionState<T>* - typed session state yang memungkinkan context provider menyimpan data terstruktur antar invokasi

> 💡 Baca file `THEORY.md` terlebih dahulu untuk pemahaman konseptual yang lebih mendalam tentang arsitektur context provider system, strategi context management (sliding window, summarization, RAG), implikasi token limit, dan hubungan context providers dengan agent behavior.

---

## Langkah-Langkah Implementasi

Berikut ringkasan alur yang dilakukan oleh aplikasi console:

1. **Load konfigurasi** - Membaca `appsettings.json` untuk mendapatkan endpoint dan nama model deployment
2. **Setup Dependency Injection** - Mendaftarkan `IConfiguration` dan services ke DI container
3. **Buat koneksi** - Membuat instance `AzureOpenAIClient` menggunakan `DefaultAzureCredential`
4. **Implementasi ConversationHistoryProvider** - Membuat context provider yang menyimpan 10 conversation turns terakhir menggunakan sliding window dan mendukung token truncation ketika total token melebihi 4000
5. **Implementasi FileContextProvider** - Membuat context provider yang membaca data dari file `Data/knowledge-base.json` dan menyediakannya sebagai konteks tambahan ke agent
6. **Registrasi context providers ke agent** - Mendaftarkan kedua context providers ke agent sehingga konteks disediakan secara otomatis pada setiap invocation
7. **Demonstrasi recall capability** - Menyebutkan informasi pada satu turn, lalu bertanya tentang informasi tersebut pada turn berikutnya untuk membuktikan agent mengingat conversation history
8. **Demonstrasi perbandingan dengan/tanpa context provider** - Mengirim pertanyaan follow-up yang mereferensikan informasi sebelumnya ke agent dengan context provider dan agent tanpa context provider, menampilkan kedua response secara berurutan
9. **Demonstrasi token truncation** - Menambahkan banyak conversation turns hingga total token melebihi 4000, lalu menunjukkan bahwa turn terlama dihapus untuk menjaga batas token
10. **Handle error** - Menangani kegagalan saat membaca file context, konfigurasi tidak valid, dan kegagalan koneksi dengan pesan yang informatif

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
dotnet run --project 03-Advanced/01-ContextProviders/ContextProviders.csproj
```

---

## Expected Output

Ketika aplikasi berjalan dengan sukses, Anda akan melihat output seperti berikut di console:

```
══════════════════════════════════════════════════════════════
  Context Providers - Memory dan Dynamic Context untuk Agent
══════════════════════════════════════════════════════════════

[INFO] Koneksi ke Azure OpenAI berhasil.
[INFO] Context providers terdaftar: ConversationHistoryProvider, FileContextProvider

── Demo 1: Recall Capability ──────────────────────────────────
Mendemonstrasikan kemampuan agent mengingat informasi dari
percakapan sebelumnya.

Turn 1 - Memberikan informasi ke agent:
> Nama saya Budi dan saya bekerja sebagai software engineer.

Agent: Halo Budi! Senang bertemu dengan Anda. Sebagai software
engineer, ada yang bisa saya bantu terkait pekerjaan Anda?

[CONTEXT] Conversation history: 1 turn (estimasi: 45 tokens)

Turn 2 - Menguji recall:
> Siapa nama saya dan apa pekerjaan saya?

Agent: Nama Anda adalah Budi dan Anda bekerja sebagai software
engineer.

[CONTEXT] Conversation history: 2 turns (estimasi: 112 tokens)

✓ Agent berhasil mengingat informasi dari turn sebelumnya.

── Demo 2: Perbandingan Dengan vs Tanpa Context Provider ──────
Mengirim pertanyaan follow-up ke dua agent - satu dengan
context provider, satu tanpa context provider.

Pertanyaan follow-up: "Apa pekerjaan saya?"

[DENGAN Context Provider]
Agent: Anda bekerja sebagai software engineer, seperti yang
Anda sebutkan sebelumnya.

[TANPA Context Provider]
Agent: Maaf, saya tidak memiliki informasi tentang pekerjaan
Anda. Bisa Anda beritahu saya?

✓ Terlihat perbedaan - agent tanpa context provider tidak
  memiliki memory dari percakapan sebelumnya.

── Demo 3: Token Truncation ───────────────────────────────────
Menambahkan banyak conversation turns untuk melampaui batas
4000 tokens, lalu menunjukkan truncation.

[CONTEXT] Menambahkan 15 conversation turns...
[CONTEXT] Total token sebelum truncation: 4,235 tokens (15 turns)
[TRUNCATION] Menghapus 3 turn terlama untuk memenuhi batas.
[CONTEXT] Total token setelah truncation: 3,812 tokens (12 turns)

✓ Context di-truncate: turn terlama dihapus, turn terbaru
  dipertahankan.

── Demo 4: File-Based Context Provider ────────────────────────
Agent mengakses knowledge base dari file lokal.

> Apa saja fakta yang kamu ketahui tentang Microsoft Agent Framework?

Agent: Berdasarkan knowledge base yang tersedia, Microsoft Agent
Framework adalah SDK unified dari Microsoft untuk membangun AI
agents. Framework ini mendukung .NET dan Python, dan merupakan
pengganti dari Semantic Kernel dan AutoGen...

[CONTEXT] File context: 3 fakta dari knowledge-base.json

══════════════════════════════════════════════════════════════
```

> ⚠️ Output aktual akan berbeda setiap kali dijalankan karena sifat generatif dari LLM. Response content, estimasi token, dan detail context yang ditampilkan akan sesuai dengan kondisi saat runtime.

---

## Troubleshooting

### ❌ Error: Agent tidak mengingat informasi dari turn sebelumnya (recall gagal)

**Penyebab**: ConversationHistoryProvider tidak terdaftar dengan benar ke agent, atau `StoreAIContextAsync` tidak menyimpan turn terbaru.

**Solusi**:
- Pastikan ConversationHistoryProvider didaftarkan ke agent sebelum interaksi pertama dimulai
- Verifikasi bahwa `StoreAIContextAsync` dipanggil setelah setiap agent response untuk menyimpan turn baru
- Periksa output console untuk konfirmasi jumlah turns yang tersimpan setelah setiap interaksi
- Pastikan `ProvideAIContextAsync` menyertakan conversation history sebagai bagian dari context yang dikirim ke LLM

---

### ❌ Error: Token truncation tidak terjadi atau context melebihi batas

**Penyebab**: Estimasi token count tidak akurat, atau logika truncation tidak terpicu saat batas terlampaui.

**Solusi**:
- Verifikasi bahwa estimasi token menggunakan pendekatan yang konsisten (contoh: 1 token ≈ 4 karakter untuk bahasa Inggris, atau menggunakan tokenizer library)
- Pastikan pengecekan batas 4000 token dilakukan di `ProvideAIContextAsync` sebelum context dikirim ke agent
- Periksa apakah turn terlama benar-benar dihapus dari collection saat truncation terjadi
- Periksa log output untuk melihat jumlah token sebelum dan sesudah truncation

---

### ❌ Error: FileContextProvider gagal membaca knowledge-base.json

**Penyebab**: File `Data/knowledge-base.json` tidak ditemukan di directory yang benar atau format JSON tidak valid.

**Solusi**:
- Pastikan file `Data/knowledge-base.json` ada di dalam folder project `03-Advanced/01-ContextProviders/Data/`
- Periksa bahwa file JSON memiliki format yang valid (gunakan JSON validator online jika perlu)
- Pastikan menjalankan `dotnet run` dari folder `03-Advanced/01-ContextProviders/` atau gunakan path relatif yang benar
- Verifikasi bahwa file di-copy ke output directory (periksa `.csproj` untuk `<Content>` atau `<None>` item)

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
- Pastikan menjalankan `dotnet run` dari folder `03-Advanced/01-ContextProviders/`
- Periksa format JSON (pastikan tidak ada trailing comma atau syntax error)
- Verifikasi bahwa field `Endpoint` dan `DeploymentName` terisi

---

## Referensi

- [Context and Memory in AI Agents](https://learn.microsoft.com/en-us/microsoft/agents/concepts/context-providers)
- [Token Limits and Context Windows - Azure OpenAI](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/models)
- [Build AI Agents with .NET](https://learn.microsoft.com/en-us/dotnet/ai/get-started/build-ai-agents)
- [Microsoft.Extensions.AI - Overview](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
- [Retrieval-Augmented Generation (RAG) Pattern](https://learn.microsoft.com/en-us/azure/search/retrieval-augmented-generation-overview)
