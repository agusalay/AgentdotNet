# AgentdotNet - Learning Plan: Microsoft Agent Framework

## Overview

Repository ini merupakan *structured learning plan* untuk mempelajari **Microsoft Agent Framework** dari level beginner hingga expert. Learning path ini mengikuti pendekatan *progressive disclosure* - setiap module membangun di atas konsep module sebelumnya, sehingga Anda membangun pemahaman secara bertahap dan solid.

Setiap module terdiri dari dua komponen utama:

1. **Konten Teori (THEORY.md)** - Penjelasan komprehensif tentang konsep, arsitektur, mekanisme internal, use cases, dan trade-offs. Baca ini terlebih dahulu sebelum menulis kode.
2. **Implementasi Praktis (Console App)** - Aplikasi .NET console yang berdiri sendiri (*self-contained*) sebagai demonstrasi praktis dari konsep yang dipelajari.

Learning plan ini mencakup 10 module yang terorganisir dalam 4 skill level, mulai dari memahami dasar LLM hingga membangun MCP Server/Client dan mengorkestrasi multi-agent workflows.

---

## Prerequisites

Pastikan tools berikut sudah terinstal sebelum memulai:

| Tool | Versi Minimum | Keterangan |
|------|---------------|------------|
| .NET SDK | 9.0+ | Runtime dan build tools untuk C# console apps |
| Azure CLI | 2.60+ | Untuk autentikasi ke Azure services |
| Azure Subscription | - | Diperlukan untuk mengakses Azure AI Foundry dan model deployment |
| IDE/Editor | VS Code atau Visual Studio 2022+ | Dengan ekstensi C# Dev Kit (opsional tapi direkomendasikan) |
| Git | 2.30+ | Untuk clone dan version control |

### Akun dan Akses

- **Azure AI Foundry**: Anda memerlukan project yang sudah di-deploy dengan minimal satu model (contoh: GPT-4o atau GPT-4o-mini).
- **Autentikasi**: Semua module menggunakan `DefaultAzureCredential` dari package `Azure.Identity`. Pastikan Anda sudah login via `az login` sebelum menjalankan aplikasi.

---

## Progression Path

Tabel berikut menunjukkan urutan module, estimasi waktu, dan dependency antar module:

| # | Level | Module | Estimasi Waktu | Dependency |
|---|-------|--------|---------------|------------|
| 1 | 🟢 Beginner | 01-LlmFundamentals | 2–3 jam | - |
| 2 | 🟢 Beginner | 02-FromLlmsToAgents | 2–3 jam | Module 1 |
| 3 | 🟡 Intermediate | 01-AddingTools | 3–4 jam | Module 2 |
| 4 | 🟡 Intermediate | 02-AddingSkills | 2–3 jam | Module 3 |
| 5 | 🟡 Intermediate | 03-AddingMiddleware | 2–3 jam | Module 4 |
| 6 | 🟠 Advanced | 01-ContextProviders | 3–4 jam | Module 5 |
| 7 | 🟠 Advanced | 02-AgentsAsTools | 3–4 jam | Module 6 |
| 8 | 🔴 Expert | 01-AgentToAgentCommunication | 4–5 jam | Module 7 |
| 9 | 🔴 Expert | 02-Workflows | 4–5 jam | Module 8 |
| 10 | 🔴 Expert | 03-McpSdk | 4–5 jam | Module 3, Module 8 |

### Alur Pembelajaran

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        LEARNING PROGRESSION                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  🟢 BEGINNER                                                            │
│  ┌──────────────────┐      ┌──────────────────────┐                    │
│  │ 01-LlmFundamentals│─────▶│ 02-FromLlmsToAgents  │                    │
│  └──────────────────┘      └──────────┬───────────┘                    │
│                                       │                                 │
│  🟡 INTERMEDIATE                      ▼                                 │
│  ┌──────────────────┐      ┌──────────────────────┐                    │
│  │ 01-AddingTools    │◀─────┘                      │                    │
│  └────────┬─────────┘                              │                    │
│           ▼                                        │                    │
│  ┌──────────────────┐      ┌──────────────────────┐                    │
│  │ 02-AddingSkills   │─────▶│ 03-AddingMiddleware  │                    │
│  └──────────────────┘      └──────────┬───────────┘                    │
│                                       │                                 │
│  🟠 ADVANCED                          ▼                                 │
│  ┌──────────────────┐      ┌──────────────────────┐                    │
│  │ 01-ContextProviders│◀────┘                      │                    │
│  └────────┬─────────┘                              │                    │
│           ▼                                        │                    │
│  ┌──────────────────┐                              │                    │
│  │ 02-AgentsAsTools  │                              │                    │
│  └────────┬─────────┘                              │                    │
│           │                                        │                    │
│  🔴 EXPERT▼                                        │                    │
│  ┌────────────────────────────┐   ┌──────────────┐│                    │
│  │ 01-AgentToAgentCommunication│──▶│ 02-Workflows ││                    │
│  └────────────┬───────────────┘   └──────────────┘│                    │
│               │                                    │                    │
│               ▼                                    │                    │
│  ┌──────────────────┐                              │                    │
│  │ 03-McpSdk         │  (juga depends on Module 3) │                    │
│  └──────────────────┘                              │                    │
│                                                    │                    │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Setup dan Cara Menjalankan

### 1. Clone Repository

```bash
git clone <repository-url>
cd AgentdotNet
```

### 2. Restore Dependencies

Restore seluruh solution sekaligus dari root directory:

```bash
dotnet restore
```

Atau restore module tertentu saja:

```bash
dotnet restore 01-Beginner/01-LlmFundamentals/LlmFundamentals.csproj
```

### 3. Konfigurasi Azure Credentials

Login ke Azure CLI untuk mengaktifkan `DefaultAzureCredential`:

```bash
az login
```

### 4. Konfigurasi Azure OpenAI (Sekali Saja)

Seluruh module membaca konfigurasi dari **satu file `appsettings.json` di root** (selevel `AgentdotNet.sln`). Anda cukup set endpoint dan model deployment sekali, otomatis dipakai semua 9 module.

Edit file `appsettings.json` di root:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://nama-resource-anda.openai.azure.com/",
    "DeploymentName": "gpt-4o-mini"
  }
}
```

> 💡 **Tidak perlu copy-paste ke setiap folder module.** `Directory.Build.props` secara otomatis meng-copy file ini ke output directory setiap project saat build. Jika suatu saat Anda butuh config berbeda untuk module tertentu, cukup taruh `appsettings.json` lokal di folder module tersebut — file lokal akan menang karena di-load terakhir oleh `ConfigurationBuilder`.

### 5. Build Solution

Build seluruh solution:

```bash
dotnet build
```

### 6. Menjalankan Module

Jalankan module tertentu menggunakan `dotnet run` dari folder module:

```bash
cd 01-Beginner/01-LlmFundamentals
dotnet run
```

Atau jalankan langsung dari root menggunakan `--project` flag:

```bash
dotnet run --project 01-Beginner/01-LlmFundamentals/LlmFundamentals.csproj
```

---

## Struktur Folder

```
AgentdotNet/
├── AgentdotNet.sln
├── README.md                          ← Anda di sini
├── Directory.Build.props              ← Shared build config (auto-copy appsettings.json)
├── appsettings.json                   ← ⭐ Konfigurasi Azure OpenAI (set sekali, pakai semua module)
├── .gitignore
├── 01-Beginner/
│   ├── 01-LlmFundamentals/
│   │   ├── LlmFundamentals.csproj
│   │   ├── Program.cs
│   │   ├── THEORY.md
│   │   ├── README.md
│   │   └── .env.example
│   └── 02-FromLlmsToAgents/
│       └── ...
├── 02-Intermediate/
│   ├── 01-AddingTools/
│   ├── 02-AddingSkills/
│   └── 03-AddingMiddleware/
├── 03-Advanced/
│   ├── 01-ContextProviders/
│   └── 02-AgentsAsTools/
├── 04-Expert/
│   ├── 01-AgentToAgentCommunication/
│   ├── 02-Workflows/
│   └── 03-McpSdk/
│       ├── THEORY.md
│       ├── README.md
│       ├── McpSdk.Server/
│       │   ├── McpSdk.Server.csproj
│       │   ├── Program.cs
│       │   ├── Models.cs
│       │   └── Tools/WeatherTools.cs
│       └── McpSdk.Client/
│           ├── McpSdk.Client.csproj
│           ├── Program.cs
│           ├── InteractiveLoopHelpers.cs
│           └── .env.example
└── Tests/
    ├── Tests.csproj
    ├── TestInfra/
    ├── Properties/                    ← Property-based tests (FsCheck)
    ├── Unit/                          ← Unit tests
    └── Smoke/                         ← Build verification tests
```

---

## Tips Pembelajaran

- **Baca THEORY.md terlebih dahulu** sebelum melihat kode. Memahami "mengapa" lebih penting dari "bagaimana".
- **Jalankan module secara berurutan**. Setiap module membangun di atas konsep sebelumnya.
- **Eksperimen dengan kode**. Ubah parameter, modifikasi instructions, dan amati perbedaan output.
- **Gunakan inline comments** sebagai panduan saat membaca source code.

---

## Referensi

- [Microsoft Agent Framework Documentation](https://learn.microsoft.com/en-us/microsoft-cloud/dev/ai/agent-framework)
- [Azure AI Foundry](https://learn.microsoft.com/en-us/azure/ai-studio/)
- [Azure.Identity - DefaultAzureCredential](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)
