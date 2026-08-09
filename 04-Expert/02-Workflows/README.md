# Module 9: Workflows (Multi-Agent Orchestration)

## Overview

Module ini merupakan **capstone** dari seluruh learning path - mengintegrasikan semua konsep yang telah dipelajari dari Module 1 hingga Module 8 ke dalam satu sistem orkestrasi yang kohesif. Di sini Anda akan mempelajari **WorkflowBuilder** dan pendekatan *graph-based execution* untuk mengorkestrasi multiple agents dalam proses multi-step yang kompleks.

Yang didemonstrasikan dalam module ini:

- **WorkflowBuilder** - API deklaratif untuk mendefinisikan workflow graph yang terdiri dari nodes (executors), edges (transisi), dan conditions (branching logic)
- **Graph-based execution** - eksekusi workflow berdasarkan definisi graph di mana setiap node merepresentasikan satu step pemrosesan dan edges menentukan alur eksekusi
- **Sequential execution** - eksekusi step secara berurutan di mana output satu step menjadi input step berikutnya
- **Parallel execution (fan-out/fan-in)** - eksekusi beberapa step secara bersamaan dan penggabungan hasilnya sebelum melanjutkan ke step berikutnya
- **Conditional routing** - branching logic di mana path eksekusi ditentukan oleh output dari step sebelumnya (contoh: approved → output, rejected → revisi)
- **Step retry** - mekanisme retry otomatis pada step yang gagal dengan maksimal 3 percobaan, menampilkan informasi retry dan status akhir
- **Event monitoring** - observasi real-time terhadap workflow execution melalui `ExecutorCompletedEvent` untuk menampilkan progress dan status setiap step

Setelah menyelesaikan module ini, Anda akan mampu mendesain dan mengimplementasikan workflow graph yang kompleks, memahami kapan menggunakan sequential vs parallel vs conditional patterns, serta membangun robust orchestration systems dengan retry dan observability.

---

## Prerequisites

| Tool / Resource | Keterangan |
|-----------------|------------|
| .NET 9.0 SDK | Download di [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0) |
| Azure Subscription | Diperlukan untuk mengakses Azure OpenAI resources |
| Azure CLI (2.60+) | Untuk autentikasi via `az login` |
| Azure OpenAI Resource | Resource dengan minimal satu model yang sudah di-deploy (contoh: `gpt-4o-mini`) |

### ⚠️ Prerequisite: Module 8 - Agent-to-Agent Communication

Module ini **mengharuskan** Anda telah menyelesaikan Module 8 (Agent-to-Agent Communication).

Lihat: [Module 8: Agent-to-Agent Communication](../01-AgentToAgentCommunication/README.md)

Konsep yang harus sudah dikuasai dari Module 8:
- Pemahaman tentang *A2A protocol* dan komunikasi inter-agent - bagaimana agent-agent independen saling berkomunikasi melalui message passing
- Konsep *agent identity* dan *message queue* - setiap agent sebagai unit independen dengan identitas unik
- Pengalaman dengan *retry mechanism* dan *exponential backoff* - penanganan kegagalan komunikasi secara resilient
- Pemahaman tentang *message format* terstruktur (sender, receiver, timestamp, content)
- Konsep *distributed coordination* - bagaimana beberapa agent bekerja sama tanpa tight coupling

Workflow orchestration membangun di atas **seluruh konsep sebelumnya** - agents (Module 2), tools (Module 3), skills (Module 4), middleware (Module 5), context providers (Module 6), agent composition (Module 7), dan A2A communication (Module 8). WorkflowBuilder mengintegrasikan semua building blocks ini ke dalam satu graph-based execution model yang menyediakan centralized control, observability, dan fault tolerance untuk proses multi-step.

Jika Anda belum menyelesaikan Module 8, kembali ke module tersebut terlebih dahulu.

---

## Konsep yang Dipelajari

- **WorkflowBuilder** - API builder pattern untuk mendefinisikan workflow graph secara deklaratif, menspesifikasikan nodes, edges, dan conditions sebelum workflow dieksekusi
- **Graph definition** - representasi workflow sebagai directed graph di mana nodes adalah executors (unit pemrosesan) dan edges adalah transisi yang menghubungkan satu node ke node berikutnya
- **Executors** - unit eksekusi independen dalam workflow yang menerima input, melakukan pemrosesan (biasanya melibatkan agent/LLM call), dan menghasilkan output yang diteruskan ke node berikutnya
- **Edges** - koneksi antara executors yang mendefinisikan alur data dan kontrol dalam workflow graph, dapat berupa unconditional (selalu diikuti) atau conditional (diikuti hanya jika kondisi terpenuhi)
- **Conditions** - fungsi predikat pada edges yang menentukan apakah transisi dilakukan berdasarkan output dari executor sebelumnya, memungkinkan branching dan looping dalam graph
- **Fan-out/Fan-in** - pattern eksekusi parallel di mana satu node memiliki multiple outgoing edges (fan-out) yang dieksekusi secara bersamaan, dan hasilnya digabungkan pada satu convergence node (fan-in)
- **Retry** - mekanisme fault tolerance di mana executor yang gagal akan dicoba ulang hingga maksimal 3 kali, dengan tracking nomor percobaan dan pelaporan status akhir (berhasil setelah retry atau gagal permanen)
- **Events** - sistem observability melalui `ExecutorCompletedEvent` dan event lainnya yang memungkinkan monitoring real-time terhadap progress workflow, termasuk status setiap step (pending, running, completed, failed)

> 💡 Baca file `THEORY.md` terlebih dahulu untuk pemahaman konseptual yang lebih mendalam tentang workflow orchestration, arsitektur workflow engine, perbedaan orchestration vs choreography, execution patterns (sequential, parallel, conditional, looping), design principles (decomposition, state passing, idempotency, observability), serta bagaimana workflow mengintegrasikan semua building blocks dari module sebelumnya.

---

## Langkah-Langkah Implementasi

Berikut ringkasan alur yang dilakukan oleh aplikasi console:

1. **Load konfigurasi** - Membaca `appsettings.json` untuk mendapatkan endpoint dan nama model deployment
2. **Setup Dependency Injection** - Mendaftarkan `IConfiguration` dan services ke DI container
3. **Buat koneksi** - Membuat instance `AzureOpenAIClient` menggunakan `DefaultAzureCredential`
4. **Definisikan executors** - Membuat tiga executor (`ResearchExecutor`, `DraftExecutor`, `ReviewExecutor`) yang masing-masing memiliki role distinct dalam content creation pipeline
5. **Bangun workflow graph** - Menggunakan `WorkflowBuilder` untuk mendefinisikan graph secara deklaratif: nodes, edges, dan conditional routing
6. **Demonstrasi sequential execution** - Menjalankan path Research → Draft → Review secara berurutan, menampilkan output setiap step
7. **Demonstrasi parallel execution (fan-out/fan-in)** - Menjalankan Research dan Validation secara parallel, lalu menggabungkan hasilnya di Review node
8. **Demonstrasi conditional routing** - Review menghasilkan keputusan "approved" atau "rejected", di mana rejected mengarahkan kembali ke Draft (loop) dan approved menghasilkan final output
9. **Jalankan workflow** - Menggunakan `InProcessExecution.RunAsync()` untuk mengeksekusi workflow graph yang sudah didefinisikan
10. **Monitor events** - Menangkap `ExecutorCompletedEvent` untuk setiap step yang selesai dan menampilkan visualisasi progress real-time di console
11. **Demonstrasi retry** - Mensimulasikan kegagalan pada salah satu step dan menampilkan retry behavior (maksimal 3 percobaan) dengan status setiap percobaan
12. **Tampilkan workflow summary** - Menampilkan ringkasan eksekusi: total steps, durasi, status akhir setiap node, dan final output dari pipeline

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
dotnet run --project 04-Expert/02-Workflows/Workflows.csproj
```

---

## Expected Output

Ketika aplikasi berjalan dengan sukses, Anda akan melihat output seperti berikut di console:

```
══════════════════════════════════════════════════════════════
  Workflows - Multi-Agent Graph Orchestration Demo
══════════════════════════════════════════════════════════════

[INFO] Koneksi ke Azure OpenAI berhasil.

── Inisialisasi Workflow ──────────────────────────────────────
  [INIT] Executor 'ResearchExecutor' terdaftar.
  [INIT] Executor 'DraftExecutor' terdaftar.
  [INIT] Executor 'ReviewExecutor' terdaftar.

[INFO] Workflow graph berhasil dibangun.
  Nodes: 3 | Edges: 4 | Conditions: 1

── Workflow Graph Visualization ───────────────────────────────
  [Research] ──→ [Draft] ──→ [Review]
       │                        │
       └──→ [Validation] ──────┘
                              ↓
                    approved → Output
                    rejected → [Draft] (loop)

── Demo 1: Sequential Execution ───────────────────────────────
Mendemonstrasikan eksekusi step secara berurutan.

  [STEP 1/3] ResearchExecutor
    Status : ▶ Running
    Input  : "Riset tentang manfaat AI agents di enterprise"

  [✓] ResearchExecutor: Completed (2.1s)
    Output : AI agents meningkatkan produktivitas 40%,
             mengurangi waktu response customer support,
             dan mengotomasi data processing tasks...

  [STEP 2/3] DraftExecutor
    Status : ▶ Running
    Input  : (output dari ResearchExecutor)

  [✓] DraftExecutor: Completed (3.4s)
    Output : Draft artikel: "AI Agents di Enterprise -
             Revolusi Produktivitas Modern"...

  [STEP 3/3] ReviewExecutor
    Status : ▶ Running
    Input  : (output dari DraftExecutor)

  [✓] ReviewExecutor: Completed (1.8s)
    Output : Review: APPROVED - konten berkualitas baik,
             struktur jelas, data mendukung argumen.

✓ Sequential execution selesai (7.3s total).

── Demo 2: Parallel Execution (Fan-out/Fan-in) ────────────────
Mendemonstrasikan eksekusi parallel dan penggabungan hasil.

  [PARALLEL] Fan-out: 2 executors berjalan bersamaan
    ├─ ResearchExecutor  : ▶ Running
    └─ ValidationExecutor: ▶ Running

  [✓] ValidationExecutor: Completed (1.5s)
  [✓] ResearchExecutor: Completed (2.3s)

  [FAN-IN] Menggabungkan hasil di ReviewExecutor...

  [✓] ReviewExecutor: Completed (1.9s)
    Combined Input : Research result + Validation result
    Output         : Review berdasarkan riset dan validasi:
                     konten terverifikasi dan akurat.

✓ Parallel execution selesai (4.2s total).

── Demo 3: Conditional Routing ────────────────────────────────
Mendemonstrasikan branching berdasarkan output step.

  [STEP] DraftExecutor → ReviewExecutor
  [✓] ReviewExecutor: Completed
    Decision: REJECTED - perlu perbaikan pada bagian
              kesimpulan dan tambahkan data kuantitatif.

  [CONDITION] result.IsApproved == false
    Route  : ReviewExecutor ──→ DraftExecutor (loop back)

  [STEP] DraftExecutor (revisi)
    Status : ▶ Running (iterasi ke-2)

  [✓] DraftExecutor: Completed (2.8s)
    Output : Draft revisi dengan kesimpulan yang diperkuat
             dan data kuantitatif ditambahkan...

  [STEP] ReviewExecutor (re-review)
  [✓] ReviewExecutor: Completed
    Decision: APPROVED - revisi memenuhi semua kriteria.

  [CONDITION] result.IsApproved == true
    Route  : ReviewExecutor ──→ Final Output

✓ Conditional routing selesai (loop terjadi 1 kali).

── Demo 4: Step Retry pada Failure ────────────────────────────
Mendemonstrasikan retry mechanism saat workflow step gagal.

  [STEP] DraftExecutor
    Status : ▶ Running

  [✗] DraftExecutor: FAILED
    Error  : Timeout - LLM tidak merespons dalam waktu yang
             ditentukan.

  [RETRY] Percobaan 1/3 gagal.
    Step   : DraftExecutor
    Alasan : Timeout
    Status : Menunggu sebelum retry...

  [RETRY] Percobaan 2/3...
  [✓] DraftExecutor: Completed (retry berhasil)
    Output : Draft berhasil dibuat setelah retry ke-2.

✓ Step retry berhasil pada percobaan ke-2.

── Workflow Execution Summary ─────────────────────────────────
  Total Steps    : 3 executors
  Total Duration : 18.7s
  Status:
    ResearchExecutor  : ✓ Completed
    DraftExecutor     : ✓ Completed (1 retry)
    ReviewExecutor    : ✓ Completed
  Final Output   : Artikel "AI Agents di Enterprise" -
                   approved dan siap dipublikasikan.

══════════════════════════════════════════════════════════════
  Demonstrasi Workflows selesai.
  Anda telah melihat: sequential execution, parallel
  fan-out/fan-in, conditional routing, step retry,
  dan real-time event monitoring.
══════════════════════════════════════════════════════════════
```

> ⚠️ Output aktual akan berbeda setiap kali dijalankan karena sifat generatif dari LLM. Timestamp, content response, durasi eksekusi, dan jumlah loop/retry yang ditampilkan akan sesuai dengan kondisi saat runtime.

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
- Pastikan menjalankan `dotnet run` dari folder `04-Expert/02-Workflows/`
- Periksa format JSON (pastikan tidak ada trailing comma atau syntax error)
- Verifikasi bahwa field `Endpoint` dan `DeploymentName` terisi

---

### ❌ Error: "WorkflowBuilder graph validation failed"

**Penyebab**: Definisi graph tidak valid - mungkin ada circular dependency tanpa exit condition, node yang tidak terhubung, atau edge yang mereferensikan executor yang belum didaftarkan.

**Solusi**:
- Periksa bahwa setiap conditional edge memiliki exit condition yang dapat tercapai (hindari infinite loop)
- Pastikan semua executor yang direferensikan di `AddEdge()` sudah didefinisikan sebelumnya
- Verifikasi bahwa `WithOutputFrom()` mereferensikan executor yang ada di dalam graph
- Periksa bahwa graph memiliki minimal satu entry point dan satu output point

---

### ❌ Error: Semua retry gagal (3/3 exhausted) pada workflow step

**Penyebab**: Executor gagal secara konsisten, biasanya karena LLM timeout atau response yang tidak dapat di-parse.

**Solusi**:
- Periksa koneksi ke Azure OpenAI - pastikan rate limit belum tercapai
- Workflows melibatkan banyak LLM calls (setiap executor melakukan minimal satu call) - TPM quota dapat habis dengan cepat
- Pertimbangkan deployment dengan quota lebih tinggi atau batasi parallelism
- Periksa log console untuk melihat error spesifik pada step yang gagal

---

### ❌ Error: "HTTP 429 - Too Many Requests"

**Penyebab**: Rate limit tercapai karena workflow melakukan multiple LLM calls dalam waktu singkat (terutama pada parallel execution).

**Solusi**:
- Tunggu beberapa menit sebelum menjalankan ulang
- Workflows menggunakan banyak LLM calls - parallel fan-out melipatgandakan jumlah concurrent requests
- Periksa quota dan rate limits di Azure Portal
- Pertimbangkan untuk mengurangi jumlah parallel steps atau menambahkan delay antar step

---

### ❌ Error: Step output kosong atau unexpected format

**Penyebab**: LLM menghasilkan output yang tidak sesuai dengan format yang diharapkan oleh executor berikutnya dalam pipeline.

**Solusi**:
- Periksa instructions pada setiap executor - pastikan format output dispesifikasikan dengan jelas
- Verifikasi bahwa temperature pada konfigurasi model tidak terlalu tinggi (gunakan ≤ 0.5 untuk workflow yang memerlukan output terstruktur)
- Jika conditional routing tidak berfungsi, periksa bahwa output ReviewExecutor berisi kata kunci yang diharapkan condition function

---

## Referensi

- [Workflow Orchestration - Microsoft Agent Framework](https://learn.microsoft.com/en-us/microsoft/agents/concepts/workflows)
- [Build Multi-Agent Systems with .NET](https://learn.microsoft.com/en-us/dotnet/ai/get-started/build-ai-agents)
- [WorkflowBuilder API Reference](https://learn.microsoft.com/en-us/microsoft/agents/api/workflow-builder)
- [Orchestration Patterns - Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/patterns/choreography)
- [Microsoft.Extensions.AI - Overview](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
