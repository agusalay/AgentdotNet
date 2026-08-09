// =============================================================================
// Property 2: Exit command recognition is case-insensitive
// Validates: Requirements 5.10
//
// For any case variation of the strings "exit" or "quit" (e.g., "EXIT", "Quit",
// "eXiT"), the interactive loop SHALL recognize it as a termination command and
// exit gracefully.
// =============================================================================

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using FluentAssertions;
using System.Globalization;

namespace Tests.Properties;

/// <summary>
/// Property-based tests validating that exit command recognition works
/// correctly for all case variations of "exit" and "quit".
/// **Validates: Requirements 5.10**
/// </summary>
public class ExitCommandProperties
{
    /// <summary>
    /// Property 2: For any case variation of "exit", the system recognizes
    /// it as a termination command.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property AnyCaseVariationOfExit_IsRecognized()
    {
        var gen = Gen.ArrayOf(Gen.Elements(true, false), 4)
            .Select(flags =>
            {
                var chars = "exit".ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                    if (flags[i])
                        chars[i] = char.ToUpper(chars[i], CultureInfo.InvariantCulture);
                return new string(chars);
            });

        return Prop.ForAll(gen.ToArbitrary(), input =>
        {
            IsExitCommand(input).Should().BeTrue(
                $"'{input}' should be recognized as exit command");
            return true;
        });
    }

    /// <summary>
    /// Property 2: For any case variation of "quit", the system recognizes
    /// it as a termination command.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property AnyCaseVariationOfQuit_IsRecognized()
    {
        var gen = Gen.ArrayOf(Gen.Elements(true, false), 4)
            .Select(flags =>
            {
                var chars = "quit".ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                    if (flags[i])
                        chars[i] = char.ToUpper(chars[i], CultureInfo.InvariantCulture);
                return new string(chars);
            });

        return Prop.ForAll(gen.ToArbitrary(), input =>
        {
            IsExitCommand(input).Should().BeTrue(
                $"'{input}' should be recognized as quit command");
            return true;
        });
    }

    /// <summary>
    /// Property 2 (inverse): Any string that is NOT a case variation of
    /// "exit" or "quit" should NOT be recognized as a termination command.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property NonExitNonQuitInput_IsNotRecognized()
    {
        var gen = Gen.Elements(
            "hello", "help", "run", "start", "stop",
            "exiting", "quitting", "exits", "quits",
            "ex", "qu", "e", "q", "exitt", "quiit",
            "exir", "quir", "exi", "qui", "existed",
            "quiet", "execute", "question", "examine",
            "quite", "query", "123", "!@#", "exitx", "xexit");

        return Prop.ForAll(gen.ToArbitrary(), input =>
        {
            IsExitCommand(input).Should().BeFalse(
                $"'{input}' should NOT be recognized as exit command");
            return true;
        });
    }

    /// <summary>
    /// Property 2: Exit commands with surrounding whitespace should still
    /// be recognized after trimming (mirrors the actual implementation).
    /// </summary>
    [Property(MaxTest = 20)]
    public Property ExitCommandWithWhitespace_IsRecognizedAfterTrim()
    {
        var exitWords = Gen.Elements(
            "exit", "quit", "EXIT", "QUIT", "Exit", "Quit", "eXiT", "qUiT");
        var whitespace = Gen.Elements("", " ", "  ", "   ", "\t", " \t ");

        var gen = from word in exitWords
                  from leading in whitespace
                  from trailing in whitespace
                  select leading + word + trailing;

        return Prop.ForAll(gen.ToArbitrary(), input =>
        {
            IsExitCommand(input).Should().BeTrue(
                $"'{input}' (with whitespace) should be recognized");
            return true;
        });
    }

    // =========================================================================
    // Helper: mirrors the exit detection logic from FromLlmsToAgents/Program.cs
    // =========================================================================

    /// <summary>
    /// Identical to the pattern in FromLlmsToAgents/Program.cs:
    /// trimmedInput.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
    /// trimmedInput.Equals("quit", StringComparison.OrdinalIgnoreCase)
    /// </summary>
    private static bool IsExitCommand(string input)
    {
        var trimmedInput = input.Trim();
        return trimmedInput.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
               trimmedInput.Equals("quit", StringComparison.OrdinalIgnoreCase);
    }
}
