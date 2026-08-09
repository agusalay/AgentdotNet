using FluentAssertions;
using Tests.TestInfra;
using Xunit;

namespace Tests.Unit;

/// <summary>
/// Unit tests for agent creation patterns.
/// Validates Requirements: 5.6, 7.6
/// Tests agent creation with custom instructions and persona definition patterns
/// without making actual Azure OpenAI calls.
/// </summary>
public class AgentCreationTests
{
    [Fact]
    public void Agent_WithCustomInstructions_ShouldHaveNonEmptyInstructions()
    {
        // Arrange — simulate agent configuration pattern
        var agentConfig = new AgentConfiguration
        {
            Name = "ResearchAgent",
            Instructions = "You are a research assistant. Respond in a formal tone. Focus on academic topics.",
            Description = "Agent specialized in research tasks"
        };

        // Act & Assert
        agentConfig.Instructions.Should().NotBeNullOrWhiteSpace(
            "an agent must have non-empty instructions to define its persona");
        agentConfig.Name.Should().NotBeNullOrWhiteSpace(
            "an agent must have a name for identification");
    }

    [Fact]
    public void Agent_InstructionsShouldDefinePersona_WithSpecificBehavior()
    {
        // Arrange — two agents with different instructions
        var formalAgent = new AgentConfiguration
        {
            Name = "FormalAgent",
            Instructions = "You are a formal business assistant. Always respond professionally.",
            Description = "Formal business assistant"
        };

        var casualAgent = new AgentConfiguration
        {
            Name = "CasualAgent",
            Instructions = "You are a friendly casual helper. Use informal language.",
            Description = "Casual friendly helper"
        };

        // Assert — instructions should be distinct per agent
        formalAgent.Instructions.Should().NotBe(casualAgent.Instructions,
            "different agents should have distinct instructions defining their persona");
        formalAgent.Name.Should().NotBe(casualAgent.Name,
            "each agent should have a unique name");
    }

    [Fact]
    public void Agent_Configuration_ShouldValidateRequiredFields()
    {
        // Arrange
        var validConfig = new AgentConfiguration
        {
            Name = "TestAgent",
            Instructions = "You are a test agent.",
            Description = "Test description"
        };

        // Act
        var isValid = validConfig.IsValid();

        // Assert
        isValid.Should().BeTrue("a configuration with name and instructions should be valid");
    }

    [Fact]
    public void Agent_Configuration_ShouldRejectEmptyInstructions()
    {
        // Arrange
        var invalidConfig = new AgentConfiguration
        {
            Name = "TestAgent",
            Instructions = "",
            Description = "Test description"
        };

        // Act
        var isValid = invalidConfig.IsValid();

        // Assert
        isValid.Should().BeFalse("an agent without instructions cannot define behavior");
    }

    [Fact]
    public void Agent_Configuration_ShouldRejectEmptyName()
    {
        // Arrange
        var invalidConfig = new AgentConfiguration
        {
            Name = "",
            Instructions = "You are a helpful assistant.",
            Description = "Test description"
        };

        // Act
        var isValid = invalidConfig.IsValid();

        // Assert
        isValid.Should().BeFalse("an agent must have a name for identification");
    }

    [Fact]
    public void Agent_MultipleAgentsWithDifferentInstructions_ShouldBeIndependent()
    {
        // Arrange — simulating Requirement 5.7: same prompt to different agents
        var agents = new[]
        {
            new AgentConfiguration { Name = "Agent-A", Instructions = "Always respond in English.", Description = "English agent" },
            new AgentConfiguration { Name = "Agent-B", Instructions = "Always respond in Bahasa Indonesia.", Description = "Indonesian agent" },
        };

        // Assert — each agent is independently valid with unique instructions
        agents.Should().AllSatisfy(a => a.IsValid().Should().BeTrue());
        agents.Select(a => a.Instructions).Distinct().Should().HaveCount(2,
            "each agent should have distinct instructions");
    }
}

/// <summary>
/// Minimal agent configuration record for testing patterns
/// without depending on Microsoft.Agents SDK.
/// </summary>
public class AgentConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Name)
            && !string.IsNullOrWhiteSpace(Instructions);
    }
}
