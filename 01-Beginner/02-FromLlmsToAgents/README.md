# Module 2: From LLMs to Agents

## Overview

Module ini membangun di atas fondasi *LLM Fundamentals* dari Module 1 dan memperkenalkan konsep **agent** - entitas AI yang memiliki identity, instructions, dan kemampuan mempertahankan konteks percakapan.

Yang didemonstrasikan dalam module ini:

- **Membuat agent dengan instructions** menggunakan `.AsAIAgent()` untuk mendefinisikan persona dan perilaku spesifik
- **Perbandingan LLM vs Agent** - mengirim prompt yang sama ke raw LLM call dan agent ber-instructions untuk melihat perbedaan output
- **Interactive conversation loop** - implementasi loop interaktif dengan `AgentSession` yang mempertahankan state percakapan

Setelah menyelesaikan module ini, Anda akan memahami perbedaan fundamental antara LLM call biasa dan agent, serta mampu membangun agent interaktif pertama Anda.

---

## Prerequisites

| Tool / Resource | Keterangan |
|-----------------|------------|
| .NET 9.0 SDK | Download di [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0) |
| Azure Subscription | Diperlukan untuk mengakses Azure OpenAI resources |
| Azure CLI (2.60+) | Untuk autentikasi via `az login` |
| Azure OpenAI Resource | Resource dengan minimal satu model yang sudah di-deploy (contoh: `gpt-4o-mini`) |

### ⚠️ Prerequisite: Module 1 - LLM Fundamentals

Module ini **mengharuskan** Anda telah menyelesaikan [Module 1: LLM Fundamentals](../01-LlmFundamentals/README.md). Konsep yang harus sudah dikuasai:

- Cara membuat koneksi ke Azure OpenAI menggunakan `AzureOpenAIClient` dan `DefaultAzureCredential`
- Cara mengirim prompt ke model melalui `IChatClient`
- Pemahaman tentang parameter LLM (*temperature*, *max_tokens*) dan efeknya terhadap output
- Pola konfigurasi dengan `IConfiguration` dan dependency injection

Jika Anda belum menyelesaikan Module 1, kembali ke module tersebut terlebih dahulu.

---

## Konsep yang Dipelajari

- Apa itu *agent* dan perbedaan fundamental dengan *raw LLM call* (stateless vs stateful)
- Bagaimana *instructions* membentuk persona dan perilaku agent
- Cara membuat agent menggunakan `.AsAIAgent()` extension method
- Konsep *AgentSession* untuk manajemen state percakapan
- Interactive conversation loop dengan *graceful exit handling*
- Perbedaan output antara LLM tanpa instructions dan agent dengan instructions

> 💡 Baca file `THEORY.md` terlebih dahulu untuk pemahaman konseptual yang lebih mendalam tentang arsitektur agent dan perbedaannya dengan LLM call langsung.

---

## Langkah-Langkah Implementasi

Berikut ringkasan alur yang dilakukan oleh aplikasi console:

1. **Load konfigurasi** - Membaca `appsettings.json` untuk mendapatkan endpoint dan nama model deployment
2. **Setup Dependency Injection** - Mendaftarkan `IConfiguration` dan services ke DI container
3. **Buat koneksi** - Membuat instance `AzureOpenAIClient` menggunakan `DefaultAzureCredential`
4. **Demonstrasi LLM vs Agent** - Mengirim prompt yang sama ke raw LLM call (tanpa instructions) dan agent (dengan instructions), lalu menampilkan perbandingan output
5. **Buat agent dengan instructions** - Membuat `AIAgent` menggunakan `.AsAIAgent()` dengan custom instructions yang mendefinisikan persona
6. **Demonstrasi dua agent berbeda** - Mengirim prompt identik ke dua agent dengan instructions berbeda untuk menunjukkan bagaimana instructions mempengaruhi perilaku
7. **Buat AgentSession** - Menginisialisasi session untuk mempertahankan konteks percakapan
8. **Jalankan interactive loop** - Menampilkan prompt indicator `> `, menerima input user, mengirim ke agent, dan menampilkan response
9. **Handle exit** - Mendeteksi perintah "exit" atau "quit" (case-insensitive) untuk mengakhiri loop dengan pesan konfirmasi
10. **Handle error dalam loop** - Jika agent call gagal, tampilkan error dan lanjutkan loop tanpa crash

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
dotnet run --project 01-Beginner/02-FromLlmsToAgents/FromLlmsToAgents.csproj
```

---

## Expected Output

Ketika aplikasi berjalan dengan sukses, Anda akan melihat output seperti berikut di console:

```
══════════════════════════════════════════════════════════════
  From LLMs to Agents - Membangun Agent Pertama Anda
══════════════════════════════════════════════════════════════

[INFO] Koneksi ke Azure OpenAI berhasil.

── Demo 1: LLM Call vs Agent Call ─────────────────────────────
Mengirim prompt yang sama ke raw LLM (tanpa instructions) dan
agent (dengan instructions) untuk melihat perbedaan output.

Prompt: "Apa itu artificial intelligence?"

LLM Response:
Artificial intelligence (AI) adalah bidang ilmu komputer yang
berfokus pada pembuatan sistem yang dapat melakukan tugas yang
biasanya memerlukan kecerdasan manusia...

Agent Response:
Halo! Sebagai asisten teknologi Anda, saya jelaskan dengan
sederhana: AI adalah kemampuan mesin untuk meniru cara berpikir
manusia. Bayangkan komputer yang bisa belajar sendiri...

── Demo 2: Dua Agent dengan Instructions Berbeda ──────────────
Mengirim prompt identik ke dua agent dengan persona berbeda.

Prompt: "Jelaskan cloud computing."

[Agent Formal]:
Cloud computing merupakan paradigma komputasi yang menyediakan
akses on-demand ke sumber daya komputasi bersama melalui
jaringan internet...

[Agent Casual]:
Cloud computing itu simpelnya kayak nyewa komputer di internet.
Jadi kamu nggak perlu beli server sendiri...

── Demo 3: Interactive Conversation Loop ──────────────────────
Memulai percakapan interaktif dengan agent. Ketik 'exit' atau
'quit' untuk mengakhiri sesi.

> Halo, siapa kamu?
Halo! Saya adalah asisten AI yang siap membantu Anda memahami
teknologi. Ada yang ingin Anda tanyakan?

> Apa yang kita bahas sebelumnya?
Sebelumnya Anda menyapa saya dan bertanya siapa saya. Saya
memperkenalkan diri sebagai asisten AI teknologi.

> exit

[INFO] Sesi berakhir. Terima kasih telah menggunakan agent!
══════════════════════════════════════════════════════════════
```

> ⚠️ Output aktual akan berbeda setiap kali dijalankan karena sifat generatif dari LLM.

---

## Troubleshooting

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
- Pastikan menjalankan `dotnet run` dari folder `01-Beginner/02-FromLlmsToAgents/`
- Periksa format JSON (pastikan tidak ada trailing comma atau syntax error)
- Verifikasi bahwa field `Endpoint` dan `DeploymentName` terisi

---

### ❌ Error: "Agent call failed" dalam interactive loop

**Penyebab**: Koneksi terputus, model overloaded, atau session state bermasalah selama percakapan.

**Solusi**:
- Aplikasi akan menampilkan error dan kembali ke prompt `>` - Anda dapat langsung melanjutkan percakapan
- Jika error berulang, periksa koneksi internet
- Coba restart aplikasi jika session state bermasalah
- Pastikan model deployment masih aktif di Azure Portal

---

### ❌ Error: "HTTP 404" atau "Resource not found"

**Penyebab**: Endpoint URL salah atau model deployment name tidak ditemukan.

**Solusi**:
- Periksa endpoint URL di `appsettings.json` (harus diakhiri dengan `/`)
- Pastikan `DeploymentName` sesuai dengan nama deployment di Azure AI Foundry (bukan nama model)
- Verifikasi resource melalui: `az cognitiveservices account list`

---

### ❌ Error: "Request timeout" atau response lambat

**Penyebab**: Koneksi lambat atau model overloaded.

**Solusi**:
- Periksa koneksi internet
- Coba jalankan ulang setelah beberapa saat
- Pastikan model deployment status adalah "Succeeded" di Azure Portal

---

## Referensi

- [Microsoft Agent Framework - Getting Started](https://learn.microsoft.com/en-us/microsoft/agents/)
- [Build AI Agents with .NET](https://learn.microsoft.com/en-us/dotnet/ai/get-started/build-ai-agents)
- [Azure OpenAI Service Documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [Azure.Identity - DefaultAzureCredential](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)
