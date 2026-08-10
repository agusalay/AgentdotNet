# =============================================================================
# Dockerfile untuk McpAdvanced.Server
# =============================================================================
# Multi-stage build untuk menghasilkan image Docker yang optimal:
# - Stage 1 (build): Menggunakan SDK image untuk restore dan publish
# - Stage 2 (runtime): Menggunakan ASP.NET runtime image yang lebih kecil
#
# Build context: 04-Expert/04-McpAdvanced/
#
# Cara build:
#   docker build -t mcpadvanced-server .
#
# Cara menjalankan:
#   docker run -d -p 5100:5100 --name mcp-server mcpadvanced-server
#
# Client connect ke containerized server:
#   Dari host machine, client terhubung via http://localhost:5100/mcp
#   (port 5100 di-publish dari container ke host)
#
#   Jika client berjalan di container lain dalam Docker network yang sama:
#   Gunakan http://mcp-server:5100/mcp (menggunakan container name sebagai hostname)
#
#   Contoh dengan Docker network:
#     docker network create mcp-net
#     docker run -d --network mcp-net --name mcp-server -p 5100:5100 mcpadvanced-server
#     (client di host tetap menggunakan http://localhost:5100/mcp)
# =============================================================================

# ---------------------------------------------------------------------------
# Stage 1: Build — menggunakan .NET 9 SDK untuk compile dan publish aplikasi
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Set working directory di dalam container untuk proses build
WORKDIR /src

# Salin file project (.csproj) terlebih dahulu untuk memanfaatkan Docker layer caching.
# Jika dependencies tidak berubah, layer ini akan di-cache dan restore tidak perlu diulang.
COPY McpAdvanced.Server/McpAdvanced.Server.csproj McpAdvanced.Server/

# Restore NuGet packages — dilakukan terpisah agar ter-cache secara efisien.
# Layer ini hanya berubah ketika file .csproj berubah (penambahan/penghapusan package).
RUN dotnet restore McpAdvanced.Server/McpAdvanced.Server.csproj

# Salin seluruh source code server setelah restore selesai
COPY McpAdvanced.Server/ McpAdvanced.Server/

# Publish aplikasi dalam mode Release ke folder /app/publish.
# --no-restore: skip restore karena sudah dilakukan di langkah sebelumnya.
# -o /app/publish: output directory untuk artifacts yang siap di-deploy.
RUN dotnet publish McpAdvanced.Server/McpAdvanced.Server.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

# ---------------------------------------------------------------------------
# Stage 2: Runtime — menggunakan ASP.NET runtime image yang lebih kecil
# ---------------------------------------------------------------------------
# Image ini hanya berisi .NET runtime dan ASP.NET Core runtime,
# tanpa SDK tools (dotnet build, dotnet restore, dll.) sehingga ukurannya lebih kecil
# dan attack surface-nya lebih rendah.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Metadata label untuk identifikasi image
LABEL maintainer="AgentdotNet Learning Module"
LABEL description="McpAdvanced.Server - MCP Advanced Knowledge Base Server"

# Set working directory untuk runtime
WORKDIR /app

# Buat non-root user untuk menjalankan aplikasi (security best practice).
# Menjalankan container sebagai root meningkatkan risiko jika ada vulnerability:
# - Container escape bisa memberikan akses root ke host
# - Proses di dalam container bisa memodifikasi system files
# Dengan non-root user, dampak eksploitasi vulnerability menjadi terbatas.
RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --ingroup appgroup appuser

# Salin published artifacts dari build stage
COPY --from=build /app/publish .

# Override AllowedHosts untuk menerima koneksi dari luar container.
# Di environment Docker, client mungkin terhubung via hostname yang berbeda
# (misalnya container name, IP address, atau 'localhost' dari host machine).
# Nilai "*" mengizinkan semua host — dalam production, sebaiknya dibatasi
# ke hostname spesifik sesuai deployment environment.
ENV AllowedHosts="*"

# Konfigurasi ASP.NET Core untuk listen pada semua interfaces (0.0.0.0).
# Secara default ASP.NET Core listen pada localhost saja, yang tidak dapat
# diakses dari luar container. Menggunakan 0.0.0.0 memungkinkan traffic
# dari Docker port mapping masuk ke aplikasi.
ENV ASPNETCORE_URLS="http://0.0.0.0:5100"

# Expose port 5100 — port yang digunakan server MCP.
# Instruksi EXPOSE bersifat dokumentatif (memberitahu port yang digunakan),
# port masih perlu di-publish saat docker run dengan flag -p.
EXPOSE 5100

# Health check — Docker akan memeriksa apakah container masih sehat.
# Menggunakan wget ke endpoint "/" yang mengembalikan "McpAdvanced.Server is running".
# wget digunakan karena tersedia secara default di Debian-based ASP.NET images
# (curl perlu di-install terpisah yang menambah ukuran image).
# Interval 30 detik, timeout 10 detik, retry 3 kali sebelum dinyatakan unhealthy.
# Start period 10 detik memberi waktu aplikasi untuk startup sebelum health check dimulai.
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=10s \
    CMD wget --no-verbose --tries=1 --spider http://localhost:5100/ || exit 1

# Ganti ke non-root user sebelum menjalankan aplikasi
USER appuser

# Jalankan aplikasi — entrypoint menggunakan dotnet runtime
ENTRYPOINT ["dotnet", "McpAdvanced.Server.dll"]
