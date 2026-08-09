# Module 1: LLM Fundamentals

## Overview

Module ini merupakan titik awal dari learning path Microsoft Agent Framework. Di sini Anda akan mempelajari dasar-dasar *Large Language Model* (LLM) dan cara berinteraksi langsung dengan model melalui Azure OpenAI.

Yang didemonstrasikan dalam module ini:

- **Koneksi ke Azure OpenAI** menggunakan `AzureOpenAIClient` dengan `DefaultAzureCredential`
- **Mengirim prompt** ke model dan menerima response melalui `IChatClient`
- **Membandingkan efek parameter** - mengirim prompt yang sama dengan *temperature* rendah (≤0.3) dan tinggi (≥0.8) untuk melihat perbedaan output antara respons deterministik dan kreatif

Setelah menyelesaikan module ini, Anda akan memiliki fondasi yang kuat untuk memahami bagaimana LLM bekerja sebelum melangkah ke pembuatan agent di module berikutnya.

---

## Prerequisites

| Tool / Resource | Keterangan |
|-----------------|------------|
| .NET 9.0 SDK | Download di [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0) |
| Azure Subscription | Diperlukan untuk mengakses Azure OpenAI resources |
| Azure CLI (2.60+) | Untuk autentikasi via `az login` |
| Azure OpenAI Resource | Resource dengan minimal satu model yang sudah di-deploy (contoh: `gpt-4o-mini`) |

### Persiapan Azure

1. Pastikan Anda memiliki Azure OpenAI resource yang sudah aktif
2. Deploy model (misalnya `gpt-4o-mini`) di Azure AI Foundry
3. Catat endpoint URL resource Anda (format: `https://<nama-resource>.openai.azure.com/`)
4. Login ke Azure CLI:

```bash
az login
```

---

## Konsep yang Dipelajari

- Apa itu *Large Language Model* dan bagaimana model menghasilkan teks melalui *token prediction*
- Parameter-parameter kunci yang mempengaruhi output: *temperature*, *top-p*, *max_tokens*
- Cara menggunakan `IChatClient` sebagai abstraksi universal untuk berkomunikasi dengan model
- Perbedaan output antara konfigurasi parameter yang berbeda (*deterministic* vs *creative*)
- Pola *error handling* untuk koneksi yang gagal, response kosong, atau timeout

> 💡 Baca file `THEORY.md` terlebih dahulu untuk pemahaman konseptual yang lebih mendalam sebelum menjalankan aplikasi.

---

## Langkah-Langkah Implementasi

Berikut ringkasan alur yang dilakukan oleh aplikasi console:

1. **Load konfigurasi** - Membaca `appsettings.json` untuk mendapatkan endpoint dan nama model deployment
2. **Setup Dependency Injection** - Mendaftarkan `IConfiguration` dan services ke DI container
3. **Buat koneksi** - Membuat instance `AzureOpenAIClient` menggunakan `DefaultAzureCredential`
4. **Kirim prompt (temperature rendah)** - Mengirim prompt ke model dengan `temperature=0.2` untuk respons yang konsisten dan deterministik
5. **Kirim prompt (temperature tinggi)** - Mengirim prompt yang sama dengan `temperature=0.9` untuk respons yang lebih kreatif dan bervariasi
6. **Tampilkan perbandingan** - Menampilkan kedua respons secara berurutan agar learner dapat mengamati perbedaan

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
dotnet run --project 01-Beginner/01-LlmFundamentals/LlmFundamentals.csproj
```

---

## Expected Output

Ketika aplikasi berjalan dengan sukses, Anda akan melihat output seperti berikut di console:

```
══════════════════════════════════════════════════════════════
  LLM Fundamentals - Demonstrasi Interaksi dengan LLM
══════════════════════════════════════════════════════════════

[INFO] Koneksi ke Azure OpenAI berhasil.

── Demo 1: Temperature Rendah (0.2) ──────────────────────────
Mengirim prompt dengan temperature rendah untuk respons yang konsisten
dan deterministik.

Prompt: "Jelaskan apa itu machine learning dalam satu paragraf."
Response: Machine learning adalah cabang dari kecerdasan buatan yang
memungkinkan komputer belajar dari data tanpa diprogram secara eksplisit...

── Demo 2: Temperature Tinggi (0.9) ──────────────────────────
Mengirim prompt yang sama dengan temperature tinggi untuk respons yang
lebih kreatif dan bervariasi.

Prompt: "Jelaskan apa itu machine learning dalam satu paragraf."
Response: Bayangkan Anda memiliki asisten yang semakin pintar setiap
kali diberikan contoh baru - itulah esensi machine learning...

══════════════════════════════════════════════════════════════
  Perbandingan selesai. Perhatikan perbedaan gaya dan variasi
  antara kedua respons di atas.
══════════════════════════════════════════════════════════════
```

> ⚠️ Output aktual akan berbeda setiap kali dijalankan, terutama pada temperature tinggi.

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
- Pastikan menjalankan `dotnet run` dari folder `01-Beginner/01-LlmFundamentals/`
- Periksa format JSON (pastikan tidak ada trailing comma atau syntax error)
- Verifikasi bahwa field `Endpoint` dan `DeploymentName` terisi

---

### ❌ Error: "HTTP 404" atau "Resource not found"

**Penyebab**: Endpoint URL salah atau model deployment name tidak ditemukan.

**Solusi**:
- Periksa endpoint URL di `appsettings.json` (harus diakhiri dengan `/`)
- Pastikan `DeploymentName` sesuai dengan nama deployment di Azure AI Foundry (bukan nama model)
- Verifikasi resource melalui: `az cognitiveservices account list`

---

### ❌ Error: "Request timeout" atau response kosong

**Penyebab**: Koneksi lambat, model overloaded, atau deployment belum aktif.

**Solusi**:
- Periksa koneksi internet
- Coba jalankan ulang setelah beberapa saat
- Pastikan model deployment status adalah "Succeeded" di Azure Portal
- Pertimbangkan untuk menambah timeout jika koneksi konsisten lambat

---

### ❌ Error: "HTTP 429 - Too Many Requests"

**Penyebab**: Rate limit tercapai pada Azure OpenAI resource.

**Solusi**:
- Tunggu beberapa menit sebelum mencoba lagi
- Periksa quota dan rate limits di Azure Portal
- Pertimbangkan untuk menggunakan deployment dengan TPM (Tokens Per Minute) yang lebih tinggi

---

## Referensi

- [Azure OpenAI Service Documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [Quickstart: Get started using Azure OpenAI](https://learn.microsoft.com/en-us/azure/ai-services/openai/chatgpt-quickstart)
- [Azure.Identity - DefaultAzureCredential](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)
- [Microsoft.Extensions.AI - IChatClient](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
