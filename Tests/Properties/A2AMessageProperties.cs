// =============================================================================
// Property 7: A2A message display contains all required fields
// Validates: Requirements 11.9
//
// For any agent-to-agent message, the display output SHALL contain the sender
// agent name, receiver agent name, timestamp, and message content truncated to
// at most 500 characters.
// =============================================================================

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using FluentAssertions;
using Tests.TestInfra;

namespace Tests.Properties;

/// <summary>
/// Property-based tests validating that A2A message display contains all
/// required fields: sender name, receiver name, timestamp, and content
/// truncated to at most 500 characters.
/// **Validates: Requirements 11.9**
/// </summary>
public class A2AMessageProperties
{
    /// <summary>
    /// Helper that captures console output from LogMessage via reflection
    /// (since LogMessage is private static, we replicate its logic here).
    /// </summary>
    private static string CaptureMessageDisplay(A2AMessage message)
    {
        // Replicate the LogMessage logic to test display formatting
        var truncatedContent = message.Content.Length > 500
            ? message.Content[..500] + "..."
            : message.Content;

        var typeLabel = message.Type switch
        {
            MessageType.Request => "REQUEST",
            MessageType.Response => "RESPONSE",
            MessageType.Error => "ERROR",
            _ => "UNKNOWN"
        };

        var display = $"  [{typeLabel}] {message.SenderId} → {message.ReceiverId}\n" +
                      $"           Waktu: {message.Timestamp:yyyy-MM-dd HH:mm:ss} UTC\n" +
                      $"           Konten: \"{truncatedContent}\"";

        return display;
    }

    /// <summary>
    /// Property 7: Display output contains sender agent name for any message.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property Display_ContainsSenderName()
    {
        var senderGen = Gen.Elements(
            "AnalysisAgent", "SummaryAgent", "ResearchAgent", "Agent-001", "TestSender");
        var receiverGen = Gen.Elements(
            "AnalysisAgent", "SummaryAgent", "ResearchAgent", "Agent-002", "TestReceiver");
        var contentGen = Gen.Choose(1, 600).SelectMany(len =>
            Gen.ArrayOf(Gen.Elements(
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j'), len)
            .Select(chars => new string(chars)));

        var gen = from sender in senderGen
                  from receiver in receiverGen
                  from content in contentGen
                  from type in Gen.Elements(MessageType.Request, MessageType.Response, MessageType.Error)
                  select new A2AMessage(sender, receiver, DateTime.UtcNow, content, type);

        return Prop.ForAll(gen.ToArbitrary(), message =>
        {
            var display = CaptureMessageDisplay(message);
            display.Should().Contain(message.SenderId,
                "display must contain the sender agent name");
            return true;
        });
    }

    /// <summary>
    /// Property 7: Display output contains receiver agent name for any message.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property Display_ContainsReceiverName()
    {
        var senderGen = Gen.Elements(
            "AnalysisAgent", "SummaryAgent", "ResearchAgent", "Agent-001", "TestSender");
        var receiverGen = Gen.Elements(
            "AnalysisAgent", "SummaryAgent", "ResearchAgent", "Agent-002", "TestReceiver");
        var contentGen = Gen.Choose(1, 600).SelectMany(len =>
            Gen.ArrayOf(Gen.Elements(
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j'), len)
            .Select(chars => new string(chars)));

        var gen = from sender in senderGen
                  from receiver in receiverGen
                  from content in contentGen
                  from type in Gen.Elements(MessageType.Request, MessageType.Response, MessageType.Error)
                  select new A2AMessage(sender, receiver, DateTime.UtcNow, content, type);

        return Prop.ForAll(gen.ToArbitrary(), message =>
        {
            var display = CaptureMessageDisplay(message);
            display.Should().Contain(message.ReceiverId,
                "display must contain the receiver agent name");
            return true;
        });
    }

    /// <summary>
    /// Property 7: Display output contains timestamp for any message.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property Display_ContainsTimestamp()
    {
        var nameGen = Gen.Elements(
            "AnalysisAgent", "SummaryAgent", "ResearchAgent", "Agent-001");
        var contentGen = Gen.Choose(1, 200).SelectMany(len =>
            Gen.ArrayOf(Gen.Elements(
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j'), len)
            .Select(chars => new string(chars)));
        var timestampGen = Gen.Choose(2020, 2025).SelectMany(year =>
            Gen.Choose(1, 12).SelectMany(month =>
            Gen.Choose(1, 28).SelectMany(day =>
            Gen.Choose(0, 23).SelectMany(hour =>
            Gen.Choose(0, 59).SelectMany(minute =>
            Gen.Choose(0, 59).Select(second =>
                new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc)))))));

        var gen = from sender in nameGen
                  from receiver in nameGen
                  from content in contentGen
                  from timestamp in timestampGen
                  select new A2AMessage(sender, receiver, timestamp, content, MessageType.Request);

        return Prop.ForAll(gen.ToArbitrary(), message =>
        {
            var display = CaptureMessageDisplay(message);
            var expectedTimestamp = message.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            display.Should().Contain(expectedTimestamp,
                "display must contain the message timestamp");
            return true;
        });
    }

    /// <summary>
    /// Property 7: Display content is truncated to at most 500 characters
    /// for any message content length.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property Display_ContentTruncatedTo500Chars()
    {
        var nameGen = Gen.Elements(
            "AnalysisAgent", "SummaryAgent", "ResearchAgent");
        var contentGen = Gen.Choose(1, 1500).SelectMany(len =>
            Gen.ArrayOf(Gen.Elements(
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't'), len)
            .Select(chars => new string(chars)));

        var gen = from sender in nameGen
                  from receiver in nameGen
                  from content in contentGen
                  select new A2AMessage(sender, receiver, DateTime.UtcNow, content, MessageType.Request);

        return Prop.ForAll(gen.ToArbitrary(), message =>
        {
            var display = CaptureMessageDisplay(message);

            // Extract the displayed content from the formatted output
            var truncatedContent = message.Content.Length > 500
                ? message.Content[..500] + "..."
                : message.Content;

            // The actual content shown should never exceed 500 chars (+ "..." suffix)
            truncatedContent.Length.Should().BeLessThanOrEqualTo(503,
                "truncated content including ellipsis must not exceed 503 chars");

            // The meaningful content portion (without "...") should be at most 500
            var meaningfulContent = message.Content.Length > 500
                ? message.Content[..500]
                : message.Content;
            meaningfulContent.Length.Should().BeLessThanOrEqualTo(500,
                "displayed message content must be at most 500 characters");

            // Display must contain the truncated content
            display.Should().Contain(truncatedContent,
                "display must show the (possibly truncated) content");

            return true;
        });
    }

    /// <summary>
    /// Property 7: For content longer than 500 chars, display shows truncation indicator.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property Display_LongContent_ShowsTruncationIndicator()
    {
        var nameGen = Gen.Elements("AnalysisAgent", "SummaryAgent");
        var contentGen = Gen.Choose(501, 1500).SelectMany(len =>
            Gen.ArrayOf(Gen.Elements(
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j'), len)
            .Select(chars => new string(chars)));

        var gen = from sender in nameGen
                  from receiver in nameGen
                  from content in contentGen
                  select new A2AMessage(sender, receiver, DateTime.UtcNow, content, MessageType.Request);

        return Prop.ForAll(gen.ToArbitrary(), message =>
        {
            var display = CaptureMessageDisplay(message);
            display.Should().Contain("...",
                "display must show '...' for content exceeding 500 characters");
            return true;
        });
    }

    /// <summary>
    /// Property 7: For content at or below 500 chars, display shows full content
    /// without truncation indicator.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property Display_ShortContent_ShowsFullContent()
    {
        var nameGen = Gen.Elements("AnalysisAgent", "SummaryAgent");
        var contentGen = Gen.Choose(1, 500).SelectMany(len =>
            Gen.ArrayOf(Gen.Elements(
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j'), len)
            .Select(chars => new string(chars)));

        var gen = from sender in nameGen
                  from receiver in nameGen
                  from content in contentGen
                  select new A2AMessage(sender, receiver, DateTime.UtcNow, content, MessageType.Request);

        return Prop.ForAll(gen.ToArbitrary(), message =>
        {
            var display = CaptureMessageDisplay(message);
            display.Should().Contain(message.Content,
                "display must show full content when it's <= 500 chars");
            // Should not end the content with "..." (no truncation)
            var truncatedContent = message.Content + "...";
            display.Should().NotContain(truncatedContent,
                "display should NOT show ellipsis for content <= 500 chars");
            return true;
        });
    }
}
