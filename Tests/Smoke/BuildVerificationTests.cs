using FluentAssertions;
using Xunit;

namespace Tests.Smoke;

/// <summary>
/// Smoke tests for build verification — ensures solution structure and file integrity.
/// Validates Requirements: 1.3, 1.6, 2.7
/// Verifies that all required files exist in each module at the expected paths.
/// </summary>
public class BuildVerificationTests
{
    private static readonly Lazy<string> SolutionRoot = new(FindSolutionRoot);

    /// <summary>
    /// Walks up from test output directory to find AgentdotNet.sln.
    /// </summary>
    private static string FindSolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "AgentdotNet.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find AgentdotNet.sln. Ensure tests are run from within the solution directory tree.");
    }

    private string Root => SolutionRoot.Value;

    // ─── Requirement 1.3: Each module has project folder with correct naming ───

    [Theory]
    [InlineData("01-Beginner", "01-LlmFundamentals")]
    [InlineData("01-Beginner", "02-FromLlmsToAgents")]
    [InlineData("02-Intermediate", "01-AddingTools")]
    [InlineData("02-Intermediate", "02-AddingSkills")]
    [InlineData("02-Intermediate", "03-AddingMiddleware")]
    [InlineData("03-Advanced", "01-ContextProviders")]
    [InlineData("03-Advanced", "02-AgentsAsTools")]
    [InlineData("04-Expert", "01-AgentToAgentCommunication")]
    [InlineData("04-Expert", "02-Workflows")]
    public void Module_FolderShouldExist(string level, string module)
    {
        var path = Path.Combine(Root, level, module);
        Directory.Exists(path).Should().BeTrue(
            $"module folder '{level}/{module}' should exist in the solution");
    }

    // ─── Requirement 1.6: Each module has Program.cs, .csproj, and THEORY.md ───

    [Theory]
    [InlineData("01-Beginner", "01-LlmFundamentals", "Program.cs")]
    [InlineData("01-Beginner", "01-LlmFundamentals", "LlmFundamentals.csproj")]
    [InlineData("01-Beginner", "01-LlmFundamentals", "THEORY.md")]
    [InlineData("01-Beginner", "02-FromLlmsToAgents", "Program.cs")]
    [InlineData("01-Beginner", "02-FromLlmsToAgents", "FromLlmsToAgents.csproj")]
    [InlineData("01-Beginner", "02-FromLlmsToAgents", "THEORY.md")]
    [InlineData("02-Intermediate", "01-AddingTools", "Program.cs")]
    [InlineData("02-Intermediate", "01-AddingTools", "AddingTools.csproj")]
    [InlineData("02-Intermediate", "01-AddingTools", "THEORY.md")]
    [InlineData("02-Intermediate", "02-AddingSkills", "Program.cs")]
    [InlineData("02-Intermediate", "02-AddingSkills", "AddingSkills.csproj")]
    [InlineData("02-Intermediate", "02-AddingSkills", "THEORY.md")]
    [InlineData("02-Intermediate", "03-AddingMiddleware", "Program.cs")]
    [InlineData("02-Intermediate", "03-AddingMiddleware", "AddingMiddleware.csproj")]
    [InlineData("02-Intermediate", "03-AddingMiddleware", "THEORY.md")]
    [InlineData("03-Advanced", "01-ContextProviders", "Program.cs")]
    [InlineData("03-Advanced", "01-ContextProviders", "ContextProviders.csproj")]
    [InlineData("03-Advanced", "01-ContextProviders", "THEORY.md")]
    [InlineData("03-Advanced", "02-AgentsAsTools", "Program.cs")]
    [InlineData("03-Advanced", "02-AgentsAsTools", "AgentsAsTools.csproj")]
    [InlineData("03-Advanced", "02-AgentsAsTools", "THEORY.md")]
    [InlineData("04-Expert", "01-AgentToAgentCommunication", "Program.cs")]
    [InlineData("04-Expert", "01-AgentToAgentCommunication", "AgentToAgentCommunication.csproj")]
    [InlineData("04-Expert", "01-AgentToAgentCommunication", "THEORY.md")]
    [InlineData("04-Expert", "02-Workflows", "Program.cs")]
    [InlineData("04-Expert", "02-Workflows", "Workflows.csproj")]
    [InlineData("04-Expert", "02-Workflows", "THEORY.md")]
    public void Module_RequiredFile_ShouldExist(string level, string module, string fileName)
    {
        var path = Path.Combine(Root, level, module, fileName);
        File.Exists(path).Should().BeTrue(
            $"file '{fileName}' should exist in '{level}/{module}'");
    }

    // ─── Requirement 1.5: Each module has a README.md ───

    [Theory]
    [InlineData("01-Beginner", "01-LlmFundamentals")]
    [InlineData("01-Beginner", "02-FromLlmsToAgents")]
    [InlineData("02-Intermediate", "01-AddingTools")]
    [InlineData("02-Intermediate", "02-AddingSkills")]
    [InlineData("02-Intermediate", "03-AddingMiddleware")]
    [InlineData("03-Advanced", "01-ContextProviders")]
    [InlineData("03-Advanced", "02-AgentsAsTools")]
    [InlineData("04-Expert", "01-AgentToAgentCommunication")]
    [InlineData("04-Expert", "02-Workflows")]
    public void Module_ReadmeShouldExist(string level, string module)
    {
        var path = Path.Combine(Root, level, module, "README.md");
        File.Exists(path).Should().BeTrue(
            $"README.md should exist in '{level}/{module}'");
    }

    // ─── Requirement 2.7: Shared appsettings.json exists at solution root ───

    [Fact]
    public void SharedAppSettings_ShouldExistAtRoot()
    {
        var path = Path.Combine(Root, "appsettings.json");
        File.Exists(path).Should().BeTrue(
            "shared appsettings.json should exist at solution root (single config for all modules)");
    }

    // ─── Requirement 2.5: Each module has .env.example ───

    [Theory]
    [InlineData("01-Beginner", "01-LlmFundamentals")]
    [InlineData("01-Beginner", "02-FromLlmsToAgents")]
    [InlineData("02-Intermediate", "01-AddingTools")]
    [InlineData("02-Intermediate", "02-AddingSkills")]
    [InlineData("02-Intermediate", "03-AddingMiddleware")]
    [InlineData("03-Advanced", "01-ContextProviders")]
    [InlineData("03-Advanced", "02-AgentsAsTools")]
    [InlineData("04-Expert", "01-AgentToAgentCommunication")]
    [InlineData("04-Expert", "02-Workflows")]
    public void Module_EnvExampleShouldExist(string level, string module)
    {
        var path = Path.Combine(Root, level, module, ".env.example");
        File.Exists(path).Should().BeTrue(
            $".env.example should exist in '{level}/{module}'");
    }

    // ─── Solution-level files ───

    [Fact]
    public void SolutionFile_ShouldExist()
    {
        var path = Path.Combine(Root, "AgentdotNet.sln");
        File.Exists(path).Should().BeTrue("solution file must exist at root");
    }

    [Fact]
    public void RootReadme_ShouldExist()
    {
        var path = Path.Combine(Root, "README.md");
        File.Exists(path).Should().BeTrue("root README.md must exist");
    }

    [Fact]
    public void DirectoryBuildProps_ShouldExist()
    {
        var path = Path.Combine(Root, "Directory.Build.props");
        File.Exists(path).Should().BeTrue("Directory.Build.props must exist for shared build config");
    }

    [Fact]
    public void GitIgnore_ShouldExist()
    {
        var path = Path.Combine(Root, ".gitignore");
        File.Exists(path).Should().BeTrue(".gitignore must exist at root");
    }

    // ─── Requirement 1.1: Solution references all projects ───

    [Fact]
    public void SolutionFile_ShouldReferenceAllProjects()
    {
        var slnPath = Path.Combine(Root, "AgentdotNet.sln");
        var slnContent = File.ReadAllText(slnPath);

        var expectedProjects = new[]
        {
            "LlmFundamentals",
            "FromLlmsToAgents",
            "AddingTools",
            "AddingSkills",
            "AddingMiddleware",
            "ContextProviders",
            "AgentsAsTools",
            "AgentToAgentCommunication",
            "Workflows"
        };

        foreach (var project in expectedProjects)
        {
            slnContent.Should().Contain(project,
                $"solution file should reference project '{project}'");
        }
    }

    // ─── Tests project structure ───

    [Fact]
    public void TestsProject_ShouldExist()
    {
        var path = Path.Combine(Root, "Tests", "Tests.csproj");
        File.Exists(path).Should().BeTrue("Tests project must exist");
    }

    [Fact]
    public void TestInfra_ShouldContainCoreFiles()
    {
        var infraPath = Path.Combine(Root, "Tests", "TestInfra");
        Directory.Exists(infraPath).Should().BeTrue("TestInfra directory must exist");

        var expectedFiles = new[]
        {
            "IAgentMiddleware.cs",
            "GuardrailMiddleware.cs",
            "MessageBroker.cs",
            "A2AMessage.cs",
            "IContextProvider.cs",
            "ConversationHistoryProvider.cs"
        };

        foreach (var file in expectedFiles)
        {
            File.Exists(Path.Combine(infraPath, file)).Should().BeTrue(
                $"TestInfra should contain '{file}'");
        }
    }
}
