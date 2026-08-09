using FluentAssertions;
using Xunit;

namespace Tests.Unit;

/// <summary>
/// Unit tests for skill registration and tool grouping patterns.
/// Validates Requirements: 7.6, 7.7
/// Tests that skills correctly group tools and can be registered to agents.
/// </summary>
public class SkillRegistrationTests
{
    [Fact]
    public void Skill_ShouldGroupRelatedTools()
    {
        // Arrange
        var skill = new SkillDefinition
        {
            Name = "MathSkill",
            Description = "Mathematical operations skill",
            Tools = new List<ToolDefinition>
            {
                new() { Name = "Add", Description = "Adds two numbers", ParameterNames = new[] { "a", "b" } },
                new() { Name = "Multiply", Description = "Multiplies two numbers", ParameterNames = new[] { "a", "b" } }
            }
        };

        // Assert
        skill.Tools.Should().HaveCount(2, "a skill should package multiple related tools");
        skill.Name.Should().NotBeNullOrWhiteSpace();
        skill.Tools.Should().AllSatisfy(t =>
        {
            t.Name.Should().NotBeNullOrWhiteSpace("every tool must have a name");
            t.Description.Should().NotBeNullOrWhiteSpace("every tool must have a description for LLM discovery");
        });
    }

    [Fact]
    public void Skill_ToolDescriptions_ShouldBeDescriptiveForLlmDiscovery()
    {
        // Arrange — tool descriptions must help the LLM choose the right tool
        var tools = new List<ToolDefinition>
        {
            new() { Name = "SearchWeb", Description = "Searches the web for current information about a topic", ParameterNames = new[] { "query" } },
            new() { Name = "ReadFile", Description = "Reads content from a local file given a file path", ParameterNames = new[] { "filePath" } },
        };

        // Assert — descriptions should be non-trivial (at least 10 chars)
        tools.Should().AllSatisfy(t =>
            t.Description.Length.Should().BeGreaterThan(10,
                "tool descriptions must be descriptive enough for LLM to select correctly"));
    }

    [Fact]
    public void SkillRegistry_ShouldRegisterSkillToAgent()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = new SkillDefinition
        {
            Name = "FileSkill",
            Description = "File operations skill",
            Tools = new List<ToolDefinition>
            {
                new() { Name = "ReadFile", Description = "Reads a file from disk", ParameterNames = new[] { "path" } },
                new() { Name = "WriteFile", Description = "Writes content to a file on disk", ParameterNames = new[] { "path", "content" } }
            }
        };

        // Act
        registry.Register("Agent-1", skill);

        // Assert
        registry.GetSkillsForAgent("Agent-1").Should().ContainSingle()
            .Which.Name.Should().Be("FileSkill");
    }

    [Fact]
    public void SkillRegistry_ShouldPreventDuplicateSkillNames()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill1 = new SkillDefinition { Name = "MathSkill", Description = "Math ops", Tools = new List<ToolDefinition>() };
        var skill2 = new SkillDefinition { Name = "MathSkill", Description = "Duplicate math", Tools = new List<ToolDefinition>() };

        registry.Register("Agent-1", skill1);

        // Act
        var act = () => registry.Register("Agent-1", skill2);

        // Assert
        act.Should().Throw<InvalidOperationException>(
            "duplicate skill names should not be allowed on the same agent");
    }

    [Fact]
    public void SkillRegistry_SameSkillOnMultipleAgents_ShouldWork()
    {
        // Arrange — Requirement 7.7: one skill registered to multiple agents
        var registry = new SkillRegistry();
        var sharedSkill = new SkillDefinition
        {
            Name = "SharedSkill",
            Description = "Skill shared across agents",
            Tools = new List<ToolDefinition>
            {
                new() { Name = "CommonTool", Description = "A tool used by multiple agents", ParameterNames = new[] { "input" } }
            }
        };

        // Act
        registry.Register("Agent-A", sharedSkill);
        registry.Register("Agent-B", sharedSkill);

        // Assert
        registry.GetSkillsForAgent("Agent-A").Should().ContainSingle(s => s.Name == "SharedSkill");
        registry.GetSkillsForAgent("Agent-B").Should().ContainSingle(s => s.Name == "SharedSkill");
    }

    [Fact]
    public void SkillRegistry_ShouldReturnAllToolsAcrossSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill1 = new SkillDefinition
        {
            Name = "MathSkill",
            Description = "Math operations",
            Tools = new List<ToolDefinition>
            {
                new() { Name = "Add", Description = "Adds numbers", ParameterNames = new[] { "a", "b" } },
            }
        };
        var skill2 = new SkillDefinition
        {
            Name = "FileSkill",
            Description = "File operations",
            Tools = new List<ToolDefinition>
            {
                new() { Name = "Read", Description = "Reads file", ParameterNames = new[] { "path" } },
                new() { Name = "Write", Description = "Writes file", ParameterNames = new[] { "path", "content" } },
            }
        };

        registry.Register("Agent-1", skill1);
        registry.Register("Agent-1", skill2);

        // Act
        var allTools = registry.GetAllToolsForAgent("Agent-1");

        // Assert
        allTools.Should().HaveCount(3, "all tools from all skills should be accessible");
    }
}

/// <summary>
/// Represents a tool definition for testing purposes.
/// </summary>
public class ToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] ParameterNames { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Represents a skill (grouped set of related tools).
/// </summary>
public class SkillDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ToolDefinition> Tools { get; set; } = new();
}

/// <summary>
/// Simple skill registry for testing registration and grouping patterns.
/// </summary>
public class SkillRegistry
{
    private readonly Dictionary<string, List<SkillDefinition>> _agentSkills = new();

    public void Register(string agentId, SkillDefinition skill)
    {
        if (!_agentSkills.ContainsKey(agentId))
            _agentSkills[agentId] = new List<SkillDefinition>();

        if (_agentSkills[agentId].Any(s => s.Name == skill.Name))
            throw new InvalidOperationException(
                $"Skill '{skill.Name}' is already registered on agent '{agentId}'.");

        _agentSkills[agentId].Add(skill);
    }

    public IReadOnlyList<SkillDefinition> GetSkillsForAgent(string agentId)
    {
        return _agentSkills.TryGetValue(agentId, out var skills)
            ? skills.AsReadOnly()
            : new List<SkillDefinition>().AsReadOnly();
    }

    public IReadOnlyList<ToolDefinition> GetAllToolsForAgent(string agentId)
    {
        return _agentSkills.TryGetValue(agentId, out var skills)
            ? skills.SelectMany(s => s.Tools).ToList().AsReadOnly()
            : new List<ToolDefinition>().AsReadOnly();
    }
}
