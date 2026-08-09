// =============================================================================
// A2AMessage - Model pesan untuk komunikasi Agent-to-Agent (A2A)
// Mendefinisikan format pesan standar yang digunakan antar agent
// =============================================================================

namespace AgentToAgentCommunication.Agents;

/// <summary>
/// Record immutable yang merepresentasikan pesan dalam protokol A2A.
/// Setiap pesan memiliki identitas pengirim, penerima, timestamp, dan konten.
/// </summary>
/// <param name="SenderId">Identitas unik agent pengirim pesan</param>
/// <param name="ReceiverId">Identitas unik agent penerima pesan</param>
/// <param name="Timestamp">Waktu pesan dibuat dalam format UTC</param>
/// <param name="Content">Isi pesan yang dikirim (maksimal 500 karakter untuk tampilan log)</param>
/// <param name="Type">Tipe pesan: Request, Response, atau Error</param>
public record A2AMessage(
    string SenderId,
    string ReceiverId,
    DateTime Timestamp,
    string Content,
    MessageType Type = MessageType.Request);

/// <summary>
/// Enum yang mendefinisikan tipe-tipe pesan dalam protokol A2A.
/// </summary>
public enum MessageType
{
    // Pesan permintaan dari agent pengirim ke agent penerima
    Request,

    // Pesan respons dari agent penerima kembali ke agent pengirim
    Response,

    // Pesan error ketika pemrosesan gagal
    Error
}

/// <summary>
/// Exception khusus untuk kegagalan komunikasi antar agent.
/// Dilempar ketika semua percobaan retry telah habis.
/// </summary>
public class AgentCommunicationException : Exception
{
    // Jumlah percobaan yang telah dilakukan sebelum gagal
    public int AttemptCount { get; }

    // Alasan kegagalan komunikasi
    public string FailureReason { get; }

    /// <summary>
    /// Membuat exception komunikasi agent dengan informasi kegagalan.
    /// </summary>
    /// <param name="message">Pesan error utama</param>
    /// <param name="attemptCount">Jumlah percobaan yang dilakukan</param>
    /// <param name="failureReason">Alasan spesifik kegagalan</param>
    /// <param name="innerException">Exception asli yang menyebabkan kegagalan</param>
    public AgentCommunicationException(
        string message,
        int attemptCount = 0,
        string failureReason = "",
        Exception? innerException = null)
        : base(message, innerException)
    {
        AttemptCount = attemptCount;
        FailureReason = failureReason;
    }
}
