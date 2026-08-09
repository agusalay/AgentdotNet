// =============================================================================
// SummaryAgent - Agent spesialis untuk merangkum dan menyintesis informasi
// Beroperasi sebagai unit independen dengan identity unik dalam sistem A2A
// =============================================================================

using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using System.Threading.Channels;

namespace AgentToAgentCommunication.Agents;

/// <summary>
/// SummaryAgent bertanggung jawab untuk merangkum dan menyintesis informasi.
/// Agent ini menerima pesan via channel (antrian pesan) dan memproses secara independen.
/// </summary>
public class SummaryAgent
{
    // Identitas unik agent dalam sistem A2A
    public const string AgentId = "SummaryAgent";
    public const string AgentDescription = "Spesialis perangkuman dan sintesis informasi";

    // Instruksi yang membentuk persona agent
    private const string Instructions =
        "Kamu adalah SummaryAgent, seorang spesialis perangkuman yang ahli dalam " +
        "menyintesis informasi kompleks menjadi ringkasan yang jelas dan padat. " +
        "Tugasmu: 1) Mengidentifikasi poin-poin kunci dari informasi yang diberikan. " +
        "2) Menyusun ringkasan yang terstruktur dan mudah dipahami. " +
        "3) Mempertahankan akurasi informasi dalam proses perangkuman. " +
        "Berikan ringkasan yang padat, jelas, dan informatif dalam bahasa Indonesia.";

    // Channel untuk menerima pesan masuk (antrian pesan per agent)
    private readonly Channel<A2AMessage> _inbox;

    // Instance AIAgent dari Microsoft Agent Framework
    private readonly AIAgent _agent;

    // Referensi ke message broker untuk mengirim pesan ke agent lain
    private readonly MessageBroker _broker;

    /// <summary>
    /// Konstruktor untuk membuat SummaryAgent dengan koneksi ke LLM dan broker.
    /// </summary>
    /// <param name="chatClient">Koneksi ke LLM untuk pemrosesan bahasa</param>
    /// <param name="broker">Message broker untuk routing pesan antar agent</param>
    public SummaryAgent(IChatClient chatClient, MessageBroker broker)
    {
        // Membuat antrian pesan tak terbatas untuk agent ini
        _inbox = Channel.CreateUnbounded<A2AMessage>();

        // Membuat instance AIAgent dengan instruksi spesifik perangkuman
        _agent = chatClient.AsAIAgent(
            instructions: Instructions,
            name: AgentId,
            description: AgentDescription);

        // Menyimpan referensi broker untuk komunikasi keluar
        _broker = broker;

        // Mendaftarkan inbox agent ke broker agar bisa menerima pesan
        _broker.RegisterAgent(AgentId, _inbox.Writer);

        Console.WriteLine($"  [INIT] Agent '{AgentId}' berhasil dibuat dan terdaftar.");
        Console.WriteLine($"         Deskripsi: {AgentDescription}");
    }

    /// <summary>
    /// Mengirim pesan ke agent lain melalui message broker dengan mekanisme retry.
    /// </summary>
    /// <param name="receiverId">ID agent penerima</param>
    /// <param name="content">Isi pesan yang akan dikirim</param>
    /// <param name="cancellationToken">Token pembatalan</param>
    /// <returns>Pesan respons dari agent penerima</returns>
    public async Task<A2AMessage> SendMessageAsync(
        string receiverId, string content, CancellationToken cancellationToken = default)
    {
        // Membuat pesan A2A dengan metadata lengkap
        var message = new A2AMessage(
            SenderId: AgentId,
            ReceiverId: receiverId,
            Timestamp: DateTime.UtcNow,
            Content: content,
            Type: MessageType.Request);

        // Mengirim pesan melalui broker dengan mekanisme retry
        return await _broker.SendWithRetryAsync(message, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Memproses pesan masuk menggunakan LLM dan mengembalikan respons.
    /// Method ini dipanggil ketika agent menerima pesan dari agent lain.
    /// </summary>
    /// <param name="message">Pesan masuk yang perlu diproses</param>
    /// <param name="cancellationToken">Token pembatalan</param>
    /// <returns>Hasil rangkuman dalam format string</returns>
    public async Task<string> ProcessMessageAsync(
        A2AMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            // Menjalankan perangkuman menggunakan LLM dengan konten pesan sebagai input
            var response = await _agent.RunAsync(message.Content, cancellationToken: cancellationToken);
            var result = response?.ToString() ?? string.Empty;

            // Mengembalikan hasil rangkuman atau pesan default
            return !string.IsNullOrWhiteSpace(result)
                ? result
                : "[SummaryAgent] Tidak dapat menghasilkan ringkasan untuk input tersebut.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Mengembalikan pesan error yang informatif
            return $"[SummaryAgent] Gagal memproses rangkuman: {ex.Message}";
        }
    }

    /// <summary>
    /// Mengambil pesan berikutnya dari inbox (antrian pesan masuk).
    /// Operasi ini akan menunggu (blocking) sampai ada pesan masuk.
    /// </summary>
    /// <param name="cancellationToken">Token pembatalan</param>
    /// <returns>Pesan yang diterima dari antrian</returns>
    public async Task<A2AMessage> ReceiveMessageAsync(CancellationToken cancellationToken = default)
    {
        // Membaca pesan dari channel (menunggu jika belum ada pesan)
        return await _inbox.Reader.ReadAsync(cancellationToken);
    }

    /// <summary>
    /// Memeriksa apakah ada pesan yang tersedia di inbox tanpa blocking.
    /// </summary>
    /// <returns>True jika ada pesan yang menunggu diproses</returns>
    public bool HasPendingMessages()
    {
        // Memeriksa ketersediaan pesan tanpa mengambilnya
        return _inbox.Reader.TryPeek(out _);
    }
}
