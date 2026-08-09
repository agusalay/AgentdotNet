// =============================================================================
// MessageBroker - Komponen routing pesan antar agent dalam sistem A2A
// Mengelola registrasi agent, pengiriman pesan, dan mekanisme retry
// =============================================================================

using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AgentToAgentCommunication.Agents;

/// <summary>
/// MessageBroker bertanggung jawab untuk routing pesan antar agent.
/// Setiap agent mendaftarkan channel (antrian) mereka ke broker,
/// dan broker mengarahkan pesan ke antrian agent tujuan.
/// </summary>
public class MessageBroker
{
    // Registry agent: memetakan ID agent ke channel writer mereka
    private readonly ConcurrentDictionary<string, ChannelWriter<A2AMessage>> _agents = new();

    // Log semua pesan yang melewati broker untuk audit trail
    private readonly ConcurrentQueue<A2AMessage> _messageLog = new();

    // Handler untuk memproses pesan masuk (digunakan untuk request-response)
    private readonly ConcurrentDictionary<string, Func<A2AMessage, CancellationToken, Task<string>>> _messageHandlers = new();

    // Flag untuk simulasi kegagalan (digunakan dalam demo retry)
    private bool _simulateFailure;

    // Jumlah kegagalan yang tersisa untuk disimulasikan
    private int _remainingFailures;

    /// <summary>
    /// Mendaftarkan agent ke broker agar bisa menerima pesan.
    /// Setiap agent harus mendaftar sebelum bisa berkomunikasi.
    /// </summary>
    /// <param name="agentId">ID unik agent yang mendaftar</param>
    /// <param name="writer">Channel writer untuk menulis pesan ke inbox agent</param>
    public void RegisterAgent(string agentId, ChannelWriter<A2AMessage> writer)
    {
        // Menyimpan referensi channel writer agent ke registry
        _agents[agentId] = writer;
    }

    /// <summary>
    /// Mendaftarkan handler pemrosesan pesan untuk agent tertentu.
    /// Handler ini dipanggil ketika agent menerima pesan request.
    /// </summary>
    /// <param name="agentId">ID agent yang menangani pesan</param>
    /// <param name="handler">Fungsi async yang memproses pesan dan mengembalikan respons</param>
    public void RegisterMessageHandler(
        string agentId, Func<A2AMessage, CancellationToken, Task<string>> handler)
    {
        // Menyimpan handler untuk digunakan saat pesan diterima
        _messageHandlers[agentId] = handler;
    }

    /// <summary>
    /// Mengaktifkan simulasi kegagalan untuk demonstrasi mekanisme retry.
    /// Sejumlah pengiriman berikutnya akan gagal secara sengaja.
    /// </summary>
    /// <param name="failureCount">Jumlah kegagalan yang akan disimulasikan</param>
    public void EnableFailureSimulation(int failureCount)
    {
        // Mengatur flag simulasi dan jumlah kegagalan
        _simulateFailure = true;
        _remainingFailures = failureCount;
    }

    /// <summary>
    /// Menonaktifkan simulasi kegagalan komunikasi.
    /// </summary>
    public void DisableFailureSimulation()
    {
        // Mereset flag simulasi kegagalan
        _simulateFailure = false;
        _remainingFailures = 0;
    }

    /// <summary>
    /// Mengirim pesan ke agent tujuan dengan mekanisme retry dan exponential backoff.
    /// Jika pengiriman gagal, sistem akan mencoba ulang dengan delay yang meningkat.
    /// Pola delay: 1 detik, 2 detik, 4 detik (exponential backoff 2^n).
    /// </summary>
    /// <param name="message">Pesan A2A yang akan dikirim</param>
    /// <param name="cancellationToken">Token pembatalan</param>
    /// <param name="maxRetries">Jumlah maksimal percobaan (default: 3)</param>
    /// <returns>Pesan respons dari agent penerima</returns>
    /// <exception cref="AgentCommunicationException">Dilempar jika semua retry habis</exception>
    public async Task<A2AMessage> SendWithRetryAsync(
        A2AMessage message, int maxRetries = 3, CancellationToken cancellationToken = default)
    {
        // Mencatat pesan ke log audit
        _messageLog.Enqueue(message);

        // Menampilkan log pesan keluar dengan format standar
        LogMessage(message);

        // Loop retry dengan exponential backoff
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Memeriksa apakah simulasi kegagalan aktif
                if (_simulateFailure && _remainingFailures > 0)
                {
                    // Mengurangi sisa kegagalan dan melempar exception simulasi
                    Interlocked.Decrement(ref _remainingFailures);
                    throw new TimeoutException(
                        $"Simulasi timeout: agent '{message.ReceiverId}' tidak merespons dalam 5 detik.");
                }

                // Mengirim pesan dan menunggu respons dari agent tujuan
                var response = await DeliverAndProcessAsync(message, cancellationToken);

                // Mencatat respons ke log audit
                _messageLog.Enqueue(response);
                LogMessage(response);

                return response;
            }
            catch (TimeoutException ex)
            {
                // Menghitung delay exponential backoff: 2^attempt detik (1s, 2s, 4s)
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                var attemptNumber = attempt + 1;

                // Menampilkan informasi retry ke console
                Console.WriteLine($"  [RETRY]  Percobaan {attemptNumber}/{maxRetries} gagal: {ex.Message}");
                Console.WriteLine($"           Menunggu {delay.TotalSeconds:F0}s sebelum retry berikutnya...");

                // Jika ini bukan percobaan terakhir, tunggu sebelum retry
                if (attempt < maxRetries - 1)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        // Semua percobaan retry telah habis - tampilkan error dan lempar exception
        var failureReason = $"Agent '{message.ReceiverId}' tidak merespons setelah {maxRetries} percobaan";
        Console.WriteLine($"  [ERROR]  Semua retry habis ({maxRetries} percobaan). Alasan: {failureReason}");

        throw new AgentCommunicationException(
            message: $"Komunikasi A2A gagal: {failureReason}",
            attemptCount: maxRetries,
            failureReason: failureReason);
    }

    /// <summary>
    /// Mengirim pesan ke agent tujuan dan memproses respons secara langsung.
    /// Digunakan untuk pola request-response sinkron.
    /// </summary>
    /// <param name="message">Pesan yang akan dikirim</param>
    /// <param name="cancellationToken">Token pembatalan</param>
    /// <returns>Pesan respons dari agent penerima</returns>
    private async Task<A2AMessage> DeliverAndProcessAsync(
        A2AMessage message, CancellationToken cancellationToken)
    {
        // Memeriksa apakah agent tujuan terdaftar di broker
        if (!_agents.TryGetValue(message.ReceiverId, out var writer))
        {
            throw new TimeoutException(
                $"Agent '{message.ReceiverId}' tidak ditemukan atau tidak tersedia.");
        }

        // Memeriksa apakah handler tersedia untuk memproses pesan
        if (!_messageHandlers.TryGetValue(message.ReceiverId, out var handler))
        {
            throw new TimeoutException(
                $"Handler untuk agent '{message.ReceiverId}' tidak terdaftar.");
        }
        // Menulis pesan ke inbox agent tujuan
        await writer.WriteAsync(message, cancellationToken);

        // Memproses pesan menggunakan handler agent tujuan
        var responseContent = await handler(message, cancellationToken);

        // Membuat pesan respons dengan metadata lengkap
        return new A2AMessage(
            SenderId: message.ReceiverId,
            ReceiverId: message.SenderId,
            Timestamp: DateTime.UtcNow,
            Content: responseContent,
            Type: MessageType.Response);
    }

    /// <summary>
    /// Mencatat pesan ke console dengan format standar.
    /// Konten pesan dipotong maksimal 500 karakter sesuai requirement.
    /// </summary>
    /// <param name="message">Pesan yang akan dicatat</param>
    private static void LogMessage(A2AMessage message)
    {
        // Memotong konten pesan jika melebihi 500 karakter
        var truncatedContent = message.Content.Length > 500
            ? message.Content[..500] + "..."
            : message.Content;

        // Menampilkan log dengan format: sender → receiver, timestamp, konten
        var typeLabel = message.Type switch
        {
            MessageType.Request => "REQUEST",
            MessageType.Response => "RESPONSE",
            MessageType.Error => "ERROR",
            _ => "UNKNOWN"
        };

        Console.WriteLine($"  [{typeLabel}] {message.SenderId} → {message.ReceiverId}");
        Console.WriteLine($"           Waktu: {message.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"           Konten: \"{truncatedContent}\"");
    }

    /// <summary>
    /// Mendapatkan semua pesan yang tercatat dalam log audit.
    /// Berguna untuk analisis dan debugging komunikasi antar agent.
    /// </summary>
    /// <returns>Koleksi pesan dalam urutan kronologis</returns>
    public IReadOnlyCollection<A2AMessage> GetMessageLog()
    {
        // Mengembalikan salinan log pesan sebagai array read-only
        return _messageLog.ToArray();
    }
}
