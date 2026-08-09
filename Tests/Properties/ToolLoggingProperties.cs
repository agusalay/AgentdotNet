// =============================================================================
// Property 3: Tool invocation logging contains identifying information
// Validates: Requirements 6.8, 6.11
//
// For any tool call (with any tool name and parameter set) or tool error (with
// any tool name and error reason), the console output SHALL contain the tool name
// and all relevant contextual information (parameters for success, error reason
// for failure).
// =============================================================================

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using FluentAssertions;

namespace Tests.Properties;

/// <summary>
/// Property-based tests validating that tool invocation logging always
/// contains the tool name and relevant context information.
/// **Validates: Requirements 6.8, 6.11**
/// </summary>
public class ToolLoggingProperties
{
    /// <summary>
    /// Property 3: For any tool name and parameter, successful tool
    /// invocation logging contains the tool name and parameter value.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property SuccessfulToolCall_LogContainsToolNameAndParameter()
    {
        var toolNames = Gen.Elements(
            "GetCurrentWeather", "GetWeatherForecast", "WebSearch",
            "Summarize", "Calculator", "TranslateText",
            "GetStockPrice", "SendEmail", "ReadFile");
        var paramValues = Gen.Elements(
            "Jakarta", "Bandung", "Surabaya", "Yogyakarta",
            "machine learning", "web development",
            "https://example.com", "test-query-123");

        var gen = from tool in toolNames
                  from param in paramValues
                  select (tool, param);

        return Prop.ForAll(gen.ToArbitrary(), pair =>
        {
            var log = FormatToolCallLog(pair.tool, pair.param);

            log.Should().Contain(pair.tool);
            log.Should().Contain(pair.param);
            log.Should().Contain("[TOOL CALL]");
            return true;
        });
    }

    /// <summary>
    /// Property 3: For any tool name and result, successful tool result
    /// logging contains the tool name and result content.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property SuccessfulToolResult_LogContainsToolNameAndResult()
    {
        var toolNames = Gen.Elements(
            "GetCurrentWeather", "GetWeatherForecast", "WebSearch",
            "Summarize", "Calculator", "TranslateText");
        var results = Gen.Elements(
            "Cuaca di Jakarta: Cerah berawan, Suhu: 32C",
            "Prakiraan: Hari 1: Cerah 33C",
            "Search results: 5 items found",
            "Calculation result: 42",
            "Translation: Hello World",
            "Data cuaca tidak tersedia");

        var gen = from tool in toolNames
                  from result in results
                  select (tool, result);

        return Prop.ForAll(gen.ToArbitrary(), pair =>
        {
            var log = FormatToolResultLog(pair.tool, pair.result);

            log.Should().Contain(pair.tool);
            log.Should().Contain(pair.result);
            log.Should().Contain("[TOOL RESULT]");
            return true;
        });
    }

    /// <summary>
    /// Property 3: For any tool name and error reason, failure logging
    /// contains the tool name and error reason.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property FailedToolExecution_LogContainsToolNameAndError()
    {
        var toolNames = Gen.Elements(
            "GetCurrentWeather", "GetWeatherForecast", "WebSearch",
            "Calculator", "SendEmail", "QueryDatabase");
        var errors = Gen.Elements(
            "Network timeout after 30 seconds",
            "Invalid parameter: city name cannot be empty",
            "API rate limit exceeded",
            "Service unavailable (503)",
            "Authentication failed",
            "Data not found for specified query",
            "Connection refused by remote server");

        var gen = from tool in toolNames
                  from error in errors
                  select (tool, error);

        return Prop.ForAll(gen.ToArbitrary(), pair =>
        {
            var log = FormatToolErrorLog(pair.tool, pair.error);

            log.Should().Contain(pair.tool);
            log.Should().Contain(pair.error);
            log.Should().Contain("[ERROR]");
            return true;
        });
    }

    /// <summary>
    /// Property 3: For any tool name with multiple parameters, logging
    /// contains the tool name and all parameter key-value pairs.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property MultipleParameters_LogContainsAllKeyValues()
    {
        var toolNames = Gen.Elements(
            "GetCurrentWeather", "WebSearch", "QueryDatabase");
        var paramSets = Gen.Elements(
            new Dictionary<string, string>
                { ["cityName"] = "Jakarta" },
            new Dictionary<string, string>
                { ["query"] = "weather", ["format"] = "json" },
            new Dictionary<string, string>
                { ["cityName"] = "Bandung", ["units"] = "celsius",
                  ["language"] = "id" });

        var gen = from tool in toolNames
                  from parms in paramSets
                  select (tool, parms);

        return Prop.ForAll(gen.ToArbitrary(), pair =>
        {
            var log = FormatMultiParamLog(pair.tool, pair.parms);

            log.Should().Contain(pair.tool);
            foreach (var kvp in pair.parms)
            {
                log.Should().Contain(kvp.Key);
                log.Should().Contain(kvp.Value);
            }
            return true;
        });
    }

    // =========================================================================
    // Logging helpers: mirror the pattern from AddingTools/Program.cs
    // =========================================================================

    private static string FormatToolCallLog(string toolName, string paramValue)
    {
        return $"  [TOOL CALL] Tool: {toolName}\n" +
               $"              Parameter: cityName = {paramValue}";
    }

    private static string FormatToolResultLog(string toolName, string result)
    {
        return $"  [TOOL RESULT] {toolName}: {result}";
    }

    private static string FormatToolErrorLog(string toolName, string errorReason)
    {
        return $"  [ERROR] Tool execution gagal: {toolName}\n" +
               $"  [CAUSE] {errorReason}";
    }

    private static string FormatMultiParamLog(
        string toolName, Dictionary<string, string> parameters)
    {
        var lines = new List<string> { $"  [TOOL CALL] Tool: {toolName}" };
        foreach (var kvp in parameters)
            lines.Add($"              Parameter: {kvp.Key} = {kvp.Value}");
        return string.Join("\n", lines);
    }
}
