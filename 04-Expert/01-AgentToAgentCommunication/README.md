# Module 8: Agent-to-Agent Communication

## Overview

Module ini membangun di atas fondasi *agent composition* dari Module 7 dan memperkenalkan konsep **Agent-to-Agent (A2A) protocol** - teknik komunikasi inter-agent di mana setiap agent beroperasi sebagai unit independen dengan identity unik, berkomunikasi melalui message passing tanpa tight coupling.

Yang didemonstrasikan dalam module ini:

- **A2A protocol** - komunikasi antar agent yang berdiri sendiri menggunakan protocol standar, di mana setiap agent memiliki identity unik dan message queue tersendiri
- **Inter-agent messaging** - pengiriman dan penerimaan message antar agent dengan format terstruktur (sender, receiver, timestamp, content)
- **Round-trip communication** - demonstrasi request-response pattern di mana satu agent mengirim request dan menerima response dari agent lain
- **Collaboration pattern** - skenario kolaborasi di mana satu agent mendelegasikan sub-task ke agent lain dan menggabungkan hasil untuk menghasilkan output akhir
- **Retry with exponential backoff** - mekanisme retry otomatis dengan delay yang meningkat secara eksponensial (1s, 2s, 4s) ketika komunikasi antar agent gagal, maksimal 3 percobaan

Setelah menyelesaikan module ini, Anda akan memahami perbedaan antara in-process agent composition (Module 7) dengan distributed agent communication, cara mendesain message format untuk komunikasi antar agent, mekanisme retry untuk fault tolerance, serta kapan memilih A2A communication versus agent-as-tool pattern.

---

## Prerequisites

| Tool / Resource | Keterangan |
|-----------------|------------|
| .NET 9.0 SDK | Download di [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0) |
| Azure Subscription | Diperlukan untuk mengakses Azure OpenAI resources |
| Azure CLI (2.60+) | Untuk autentikasi via `az login` |
| Azure OpenAI Resource | Resource dengan minimal satu model yang sudah di-deploy (contoh: `gpt-4o-mini`) |

### ⚠️ Prerequisite: Module 7 - Agents as Tools

Module ini **mengharuskan** Anda telah menyelesaikan Module 7 (Agents as Tools).

Lihat: [Module 7: Agents as Tools](../../03-Advanced/02-AgentsAsTools/README.md)

Konsep yang harus sudah dikuasai dari Module 7:
- Pemahaman tentang *agent composition* dan delegation - bagaimana parent agent mendelegasikan task ke child agent melalui `AIFunctionFactory`
- Cara membuat specialized agents dengan expertise berbeda dan mendaftarkannya sebagai tools
- Konsep *routing logic* - bagaimana orchestrator memilih agent yang tepat berdasarkan konteks task
- Pemahaman tentang *communication flow* antara parent dan child agents (input → delegation → output)
- Pengalaman dengan *fallback strategy* ketika satu agent mengalami kegagalan

A2A communication membangun di atas konsep agent composition dengan evolusi dari **tight coupling** (in-process, parent-child) ke **loose coupling** (inter-process, peer-to-peer). Pada Module 7, child agents masih berada dalam satu proses dan dikontrol langsung oleh parent agent. Pada module ini, setiap agent beroperasi secara independen dengan identity dan message queue sendiri - mirip dengan evolusi dari monolith ke microservices dalam software architecture.

Jika Anda belum menyelesaikan Module 7, kembali ke module tersebut terlebih dahulu.

---

## Konsep yang Dipelajari

- **A2A protocol** - protocol komunikasi standar yang memungkinkan agent-agent independen saling bertukar message tanpa memerlukan referensi langsung satu sama lain, setiap agent memiliki identity unik sebagai pengenal
- **Message passing** - paradigma komunikasi di mana agent berkomunikasi melalui pengiriman message terstruktur (berisi sender ID, receiver ID, timestamp, dan content) alih-alih pemanggilan method langsung
- **Agent discovery** - mekanisme di mana agent dapat menemukan agent lain yang tersedia untuk berkomunikasi, termasuk identity management dan registration
- **Retry mechanism** - strategi penanganan kegagalan komunikasi dengan percobaan ulang otomatis, menggunakan counter dan delay untuk menghindari overloading sistem
- **Exponential backoff** - pola delay yang meningkat secara eksponensial antara percobaan retry (1s → 2s → 4s), memberikan waktu recovery yang cukup bagi sistem sebelum percobaan berikutnya

> 💡 Baca file `THEORY.md` terlebih dahulu untuk pemahaman konseptual yang lebih mendalam tentang A2A protocol, arsitektur distributed agent systems, perbedaan dengan in-process composition, communication patterns (request-response, pub-sub, broadcast), serta konsep distributed systems yang relevan seperti fault tolerance dan eventual consistency.

---

## Langkah-Langkah Implementasi

Berikut ringkasan alur yang dilakukan oleh aplikasi console:

1. **Load konfigurasi** - Membaca `appsettings.json` untuk mendapatkan endpoint dan nama model deployment
2. **Setup Dependency Injection** - Mendaftarkan `IConfiguration` dan services ke DI container
3. **Buat koneksi** - Membuat instance `AzureOpenAIClient` menggunakan `DefaultAzureCredential`
4. **Buat agents dengan identity unik** - Membuat AnalysisAgent dan SummaryAgent, masing-masing dengan nama unik, identity, dan message queue tersendiri
5. **Demonstrasi round-trip message passing** - AnalysisAgent mengirim request ke SummaryAgent, SummaryAgent memproses dan mengirim response kembali, menampilkan seluruh alur komunikasi di console
6. **Demonstrasi kolaborasi** - AnalysisAgent melakukan analisis data, mengirim sub-task (ringkasan) ke SummaryAgent, menerima hasil kembali, dan menampilkan combined result sebagai output akhir
7. **Log setiap message** - Menampilkan sender name, receiver name, timestamp, dan content (maksimal 500 karakter) untuk setiap message yang dikirim/diterima
8. **Demonstrasi retry mechanism** - Mensimulasikan kegagalan komunikasi dan menampilkan retry behavior dengan exponential backoff (1s, 2s, 4s), termasuk error message dengan alasan kegagalan dan nomor percobaan

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
dotnet run --project 04-Expert/01-AgentToAgentCommunication/AgentToAgentCommunication.csproj
```

---

## Expected Output

Ketika aplikasi berjalan dengan sukses, Anda akan melihat output seperti berikut di console:

```
══════════════════════════════════════════════════════════════
  Agent-to-Agent Communication - A2A Protocol Demo
══════════════════════════════════════════════════════════════

[INFO] Koneksi ke Azure OpenAI berhasil.

── Inisialisasi Agents ────────────────────────────────────────
  [INIT] Agent 'AnalysisAgent' berhasil dibuat.
         Identity: analysis-agent-001
  [INIT] Agent 'SummaryAgent' berhasil dibuat.
         Identity: summary-agent-001

[INFO] Agent discovery selesai - 2 agents terdaftar.

── Demo 1: Round-Trip Message Passing ─────────────────────────
Mendemonstrasikan request-response communication antar agents.

[A2A MESSAGE]
  Sender   : AnalysisAgent
  Receiver : SummaryAgent
  Timestamp: 2025-01-15T10:30:00.000Z
  Content  : Analisis berikut data penjualan Q4: revenue
             meningkat 15%, customer acquisition naik 20%,
             churn rate turun 3%. Buatkan ringkasan eksekutif.

  [SummaryAgent] Memproses message...

[A2A MESSAGE]
  Sender   : SummaryAgent
  Receiver : AnalysisAgent
  Timestamp: 2025-01-15T10:30:02.150Z
  Content  : Ringkasan Eksekutif Q4: Performa bisnis
             menunjukkan tren positif - revenue +15%,
             akuisisi pelanggan +20%, dan retensi membaik
             dengan penurunan churn 3%.

✓ Round-trip selesai.

── Demo 2: Kolaborasi - Sub-task Delegation ───────────────────
Mendemonstrasikan skenario kolaborasi di mana agents bekerja
sama untuk menyelesaikan task kompleks.

[A2A MESSAGE]
  Sender   : AnalysisAgent
  Receiver : SummaryAgent
  Timestamp: 2025-01-15T10:30:05.000Z
  Content  : Berikut hasil analisis mendalam tentang tren
             teknologi 2025: (1) AI agents menjadi mainstream,
             (2) edge computing berkembang pesat, (3) quantum
             computing mencapai milestone baru. Ringkas menjadi
             format presentasi.

  [SummaryAgent] Memproses sub-task...

[A2A MESSAGE]
  Sender   : SummaryAgent
  Receiver : AnalysisAgent
  Timestamp: 2025-01-15T10:30:07.300Z
  Content  : ## Tren Teknologi 2025
             • AI Agents - adopsi massal di enterprise
             • Edge Computing - pertumbuhan signifikan
             • Quantum Computing - breakthrough baru

[COMBINED RESULT]
  AnalysisAgent menggabungkan hasil kolaborasi:
  Analisis detail + ringkasan presentasi berhasil dibuat
  melalui kolaborasi inter-agent.

✓ Kolaborasi selesai - combined result tersedia.

── Demo 3: Retry dengan Exponential Backoff ───────────────────
Mendemonstrasikan mekanisme retry saat komunikasi gagal.

[A2A MESSAGE]
  Sender   : AnalysisAgent
  Receiver : SummaryAgent
  Timestamp: 2025-01-15T10:30:10.000Z
  Content  : Request yang akan mengalami timeout...

[RETRY] Percobaan 1/3 gagal.
  Alasan : Timeout - agent tujuan tidak merespons dalam 5 detik.
  Delay  : 1 detik sebelum percobaan berikutnya...

[RETRY] Percobaan 2/3 gagal.
  Alasan : Timeout - agent tujuan tidak merespons dalam 5 detik.
  Delay  : 2 detik sebelum percobaan berikutnya...

[RETRY] Percobaan 3/3 gagal.
  Alasan : Timeout - agent tujuan tidak merespons dalam 5 detik.

[ERROR] Semua percobaan gagal (3/3).
  Alasan : Komunikasi ke SummaryAgent timeout setelah 3 kali
           percobaan dengan exponential backoff.
  Saran  : Periksa ketersediaan agent tujuan dan koneksi
           jaringan.

══════════════════════════════════════════════════════════════
  Demonstrasi A2A Communication selesai.
  Anda telah melihat: round-trip messaging, kolaborasi
  inter-agent, dan retry mechanism dengan exponential backoff.
══════════════════════════════════════════════════════════════
```

> ⚠️ Output aktual akan berbeda setiap kali dijalankan karena sifat generatif dari LLM. Timestamp, content response, dan detail komunikasi yang ditampilkan akan sesuai dengan kondisi saat runtime.

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
- Pastikan menjalankan `dotnet run` dari folder `04-Expert/01-AgentToAgentCommunication/`
- Periksa format JSON (pastikan tidak ada trailing comma atau syntax error)
- Verifikasi bahwa field `Endpoint` dan `DeploymentName` terisi

---

### ❌ Error: Agent tidak merespons (message tidak sampai)

**Penyebab**: Agent tujuan belum diinisialisasi atau message queue tidak terhubung dengan benar.

**Solusi**:
- Periksa log console untuk memastikan kedua agents berhasil diinisialisasi (`[INIT]` message muncul untuk semua agents)
- Verifikasi bahwa agent discovery berhasil - semua agents harus terdaftar sebelum komunikasi dimulai
- Pastikan identity agent (sender/receiver ID) sudah benar dan tidak ada typo

---

### ❌ Error: Semua retry gagal (3/3 exhausted)

**Penyebab**: Agent tujuan tidak tersedia secara konsisten, atau terjadi masalah infrastruktur yang persisten.

**Solusi**:
- Periksa apakah agent tujuan masih aktif dan dapat menerima message
- Verifikasi koneksi ke Azure OpenAI stabil (rate limiting dapat menyebabkan timeout berulang)
- Pertimbangkan untuk meningkatkan timeout threshold jika response agent memerlukan waktu lebih lama
- Periksa quota dan rate limits di Azure Portal - multiple agent calls dapat menghabiskan TPM dengan cepat

---

### ❌ Error: Message content terpotong atau tidak lengkap

**Penyebab**: Content message melebihi 500 karakter dan di-truncate sesuai design.

**Solusi**:
- Ini adalah perilaku yang diharapkan - message display dibatasi 500 karakter untuk readability
- Content lengkap tetap dikirim ke agent tujuan; truncation hanya pada display di console
- Jika memerlukan visibility lebih, periksa log internal atau kurangi panjang prompt yang dikirim

---

### ❌ Error: "HTTP 429 - Too Many Requests"

**Penyebab**: Rate limit tercapai karena multiple agent calls dalam waktu singkat (A2A communication melibatkan beberapa LLM calls per round-trip).

**Solusi**:
- Tunggu beberapa menit sebelum mencoba lagi
- A2A communication menggunakan lebih banyak LLM calls dibanding single agent - pertimbangkan deployment dengan TPM yang lebih tinggi
- Periksa quota dan rate limits di Azure Portal
- Pertimbangkan untuk menambahkan delay antar demonstrasi jika konsisten terkena rate limit

---

## Referensi

- [Agent-to-Agent (A2A) Protocol - Microsoft Agent Framework](https://learn.microsoft.com/en-us/microsoft/agents/concepts/agent-to-agent)
- [Build Multi-Agent Systems with .NET](https://learn.microsoft.com/en-us/dotnet/ai/get-started/build-ai-agents)
- [Distributed Agent Communication Patterns](https://learn.microsoft.com/en-us/microsoft/agents/concepts/multi-agent)
- [Retry Patterns - Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/patterns/retry)
- [Microsoft.Extensions.AI - Overview](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
