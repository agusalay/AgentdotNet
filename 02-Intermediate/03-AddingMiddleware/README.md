# Module 5: Adding Middleware

## Overview

Module ini membangun di atas fondasi *agent*, *tools*, dan *skills* dari Module 2–4 dan memperkenalkan konsep **middleware** - mekanisme untuk mencegat dan memodifikasi perilaku agent melalui pipeline pattern.

Yang didemonstrasikan dalam module ini:

- **Middleware pipeline** - memproses request dan response melalui chain of middleware yang terurut sebelum dan sesudah agent execution
- **Logging middleware** - mencatat timestamp, isi request user, dan isi response agent untuk setiap interaksi
- **Guardrail middleware** - memvalidasi input user sebelum dikirim ke agent, dengan aturan seperti pembatasan panjang input
- **Short-circuit pattern** - memblokir request yang melanggar aturan tanpa meneruskannya ke agent
- **Runtime toggle** - mengaktifkan atau menonaktifkan middleware tertentu pada saat runtime tanpa restart aplikasi

Setelah menyelesaikan module ini, Anda akan memahami bagaimana middleware pipeline bekerja dalam konteks AI agent, cara mengimplementasikan cross-cutting concerns (logging, validation, guardrails) tanpa mengubah logika inti agent, serta cara mengontrol middleware secara dinamis saat runtime.

---

## Prerequisites

| Tool / Resource | Keterangan |
|-----------------|------------|
| .NET 9.0 SDK | Download di [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0) |
| Azure Subscription | Diperlukan untuk mengakses Azure OpenAI resources |
| Azure CLI (2.60+) | Untuk autentikasi via `az login` |
| Azure OpenAI Resource | Resource dengan minimal satu model yang sudah di-deploy (contoh: `gpt-4o-mini`) |

### ⚠️ Prerequisite: Module 2–4

Module ini **mengharuskan** Anda telah menyelesaikan module-module berikut:

#### Module 2 - From LLMs to Agents

Lihat: [Module 2: From LLMs to Agents](../../01-Beginner/02-FromLlmsToAgents/README.md)

Konsep yang harus sudah dikuasai:
- Cara membuat agent menggunakan `.AsAIAgent()` dengan custom *instructions*
- Pemahaman tentang *agent loop* dan *AgentSession* untuk conversation state
- Interactive loop dengan input/output ke agent
- Error handling pada agent call

#### Module 3 - Adding Tools

Lihat: [Module 3: Adding Tools](../01-AddingTools/README.md)

Konsep yang harus sudah dikuasai:
- Cara mendefinisikan *function tools* menggunakan `AIFunctionFactory.Create()` dengan `[Description]` attribute
- Cara mendaftarkan tools ke agent melalui `ChatClientAgentOptions.Tools`
- Pemahaman tentang *tool invocation cycle* dan bagaimana agent memilih tool berdasarkan konteks
- Penanganan error saat tool execution gagal

#### Module 4 - Adding Skills

Lihat: [Module 4: Adding Skills](../02-AddingSkills/README.md)

Konsep yang harus sudah dikuasai:
- Cara mengemas tools menjadi *skill* (unit reusable)
- Skill registration ke agent dan skill sharing antar agents
- Pemahaman tentang *flat-tools architecture* vs *skill-based architecture*

Jika Anda belum menyelesaikan salah satu module di atas, kembali ke module tersebut terlebih dahulu.

---

## Konsep yang Dipelajari

- Apa itu *middleware pattern* dan bagaimana diterapkan dalam software engineering - chain of responsibility untuk cross-cutting concerns
- *Pipeline architecture* - bagaimana request mengalir melalui serangkaian middleware sebelum mencapai agent, dan response kembali melalui pipeline yang sama
- *Chain of responsibility* - setiap middleware memutuskan apakah meneruskan request ke middleware berikutnya (`next()`) atau menghentikan pipeline (*short-circuit*)
- *Logging middleware* - mencatat timestamp, request content, dan response content untuk audit trail dan debugging
- *Guardrail middleware* - memvalidasi input sebelum dikirim ke agent, implementasi AI safety melalui content filtering dan input validation
- *Short-circuit pattern* - middleware memblokir request yang melanggar aturan tanpa meneruskan ke agent, menghemat resource dan mencegah penyalahgunaan
- *Runtime toggle* - mekanisme untuk mengaktifkan/menonaktifkan middleware tertentu saat aplikasi berjalan, memungkinkan konfigurasi dinamis tanpa restart

> 💡 Baca file `THEORY.md` terlebih dahulu untuk pemahaman konseptual yang lebih mendalam tentang arsitektur middleware pipeline, perbedaan tipe-tipe middleware, konsep guardrails dalam AI safety, dan hubungan middleware dengan komponen agent lainnya.

---

## Langkah-Langkah Implementasi

Berikut ringkasan alur yang dilakukan oleh aplikasi console:

1. **Load konfigurasi** - Membaca `appsettings.json` untuk mendapatkan endpoint dan nama model deployment
2. **Setup Dependency Injection** - Mendaftarkan `IConfiguration` dan services ke DI container
3. **Buat koneksi** - Membuat instance `AzureOpenAIClient` menggunakan `DefaultAzureCredential`
4. **Implementasi LoggingMiddleware** - Membuat middleware yang mencatat timestamp, isi request, dan isi response ke console output
5. **Implementasi GuardrailMiddleware** - Membuat middleware yang memvalidasi panjang input user (≤ 500 karakter), memblokir request yang melebihi batas
6. **Registrasi middleware ke pipeline** - Mendaftarkan kedua middleware ke agent pipeline dengan urutan tertentu (logging → guardrail → agent)
7. **Demonstrasi pipeline execution order** - Mengirim prompt normal dan menampilkan urutan eksekusi setiap middleware dalam pipeline
8. **Demonstrasi short-circuit** - Mengirim input yang melebihi 500 karakter untuk menunjukkan bahwa guardrail middleware memblokir request tanpa meneruskan ke agent
9. **Implementasi runtime toggle** - Menambahkan mekanisme command untuk mengaktifkan/menonaktifkan middleware tertentu saat runtime
10. **Demonstrasi toggle** - Menonaktifkan logging middleware dan menunjukkan bahwa pipeline masih berfungsi tanpa logging output
11. **Handle error** - Menangani kegagalan middleware execution dengan pesan error yang informatif

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
dotnet run --project 02-Intermediate/03-AddingMiddleware/AddingMiddleware.csproj
```

---

## Expected Output

Ketika aplikasi berjalan dengan sukses, Anda akan melihat output seperti berikut di console:

```
══════════════════════════════════════════════════════════════
  Adding Middleware - Mencegat dan Memodifikasi Perilaku Agent
══════════════════════════════════════════════════════════════

[INFO] Koneksi ke Azure OpenAI berhasil.

── Demo 1: Middleware Pipeline Execution Order ────────────────
Mengirim prompt normal melalui pipeline middleware.

Prompt: "Jelaskan apa itu middleware pattern"

[PIPELINE] Middleware execution order:
  [1] LoggingMiddleware  → ENTER
  [2] GuardrailMiddleware → ENTER (validasi: 38 karakter ✓)
  [3] Agent Core         → processing...
  [2] GuardrailMiddleware → EXIT
  [1] LoggingMiddleware  → EXIT

[LOG] [14:32:05] Request: "Jelaskan apa itu middleware pattern"
[LOG] [14:32:07] Response: "Middleware pattern adalah design..."

Agent Response:
Middleware pattern adalah design pattern yang memungkinkan Anda
untuk mencegat dan memodifikasi request/response dalam pipeline
pemrosesan...

── Demo 2: Guardrail Short-Circuit ────────────────────────────
Mengirim input yang melebihi 500 karakter untuk demonstrasi
blocking.

Prompt: "Lorem ipsum dolor sit amet... [512 karakter]"

[PIPELINE] Middleware execution order:
  [1] LoggingMiddleware  → ENTER
  [2] GuardrailMiddleware → BLOCKED ✗

[GUARDRAIL] Request ditolak: input melebihi batas 500 karakter
            (diterima: 512 karakter, maksimum: 500 karakter)
[LOG] [14:32:08] Request: "Lorem ipsum dolor sit amet..."
[LOG] [14:32:08] Response: "[BLOCKED] Input melebihi 500 karakter"

⚠️  Request tidak diteruskan ke agent.

── Demo 3: Runtime Toggle ─────────────────────────────────────
Menonaktifkan LoggingMiddleware saat runtime.

[TOGGLE] LoggingMiddleware → DISABLED ✗
[INFO] Middleware aktif: [GuardrailMiddleware]

Prompt: "Apa manfaat middleware?"

[PIPELINE] Middleware execution order:
  [1] GuardrailMiddleware → ENTER (validasi: 22 karakter ✓)
  [2] Agent Core          → processing...
  [1] GuardrailMiddleware → EXIT

Agent Response:
Middleware memberikan beberapa manfaat utama: separation of
concerns, reusability, dan konfigurasi yang fleksibel...

[TOGGLE] LoggingMiddleware → ENABLED ✓
[INFO] Middleware aktif: [LoggingMiddleware, GuardrailMiddleware]

══════════════════════════════════════════════════════════════
```

> ⚠️ Output aktual akan berbeda setiap kali dijalankan karena sifat generatif dari LLM. Timestamp, response content, dan detail middleware execution yang ditampilkan di log akan sesuai dengan kondisi saat runtime.

---

## Troubleshooting

### ❌ Error: Middleware tidak tereksekusi atau pipeline order salah

**Penyebab**: Middleware tidak didaftarkan dengan benar ke pipeline, atau urutan registrasi terbalik.

**Solusi**:
- Pastikan semua middleware didaftarkan ke pipeline sebelum agent digunakan
- Verifikasi urutan registrasi - middleware didaftarkan pertama akan dieksekusi pertama pada request masuk
- Periksa bahwa setiap middleware memanggil `next()` untuk meneruskan ke middleware berikutnya (kecuali saat short-circuit)
- Periksa output console untuk konfirmasi middleware mana saja yang aktif

---

### ❌ Error: Toggle tidak berfungsi - middleware tetap aktif/nonaktif setelah toggle

**Penyebab**: State toggle tidak diperiksa pada setiap request, atau middleware dibuat ulang (new instance) pada setiap invokasi.

**Solusi**:
- Pastikan pengecekan status enabled/disabled dilakukan di awal method `InvokeAsync` setiap middleware
- Verifikasi bahwa command toggle mengubah state yang benar (shared reference, bukan copy)
- Periksa bahwa middleware yang di-toggle skip eksekusi logikanya tetapi tetap memanggil `next()` saat disabled

---

### ❌ Error: Guardrail memblokir input yang seharusnya valid

**Penyebab**: Penghitungan panjang input menyertakan whitespace tambahan atau encoding yang berbeda.

**Solusi**:
- Verifikasi bahwa penghitungan panjang menggunakan `string.Length` pada input yang sudah di-trim
- Periksa apakah ada karakter Unicode multi-byte yang mempengaruhi hitungan
- Pastikan batas 500 karakter dihitung dari content user saja, bukan termasuk formatting atau metadata

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
- Pastikan menjalankan `dotnet run` dari folder `02-Intermediate/03-AddingMiddleware/`
- Periksa format JSON (pastikan tidak ada trailing comma atau syntax error)
- Verifikasi bahwa field `Endpoint` dan `DeploymentName` terisi

---

## Referensi

- [Middleware in Microsoft Agent Framework](https://learn.microsoft.com/en-us/microsoft/agents/concepts/middleware)
- [AI Safety and Guardrails](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/content-filter)
- [Chain of Responsibility Pattern - .NET Design Patterns](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures)
- [Build AI Agents with .NET](https://learn.microsoft.com/en-us/dotnet/ai/get-started/build-ai-agents)
- [Microsoft.Extensions.AI - Overview](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
