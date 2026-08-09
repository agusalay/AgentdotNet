using FluentAssertions;
using Xunit;

namespace Tests.Unit;

/// <summary>
/// Unit tests for workflow graph construction patterns.
/// Validates Requirements: 12.6, 12.7
/// Tests the WorkflowBuilder pattern — verifying graph node/edge construction logic.
/// </summary>
public class WorkflowGraphTests
{
    [Fact]
    public void WorkflowGraph_ShouldAddNodes()
    {
        // Arrange
        var builder = new WorkflowGraphBuilder();

        // Act
        builder.AddNode("research", "Research agent gathers information");
        builder.AddNode("write", "Writing agent composes content");

        var graph = builder.Build();

        // Assert
        graph.Nodes.Should().HaveCount(2);
        graph.Nodes.Should().Contain(n => n.Id == "research");
        graph.Nodes.Should().Contain(n => n.Id == "write");
    }

    [Fact]
    public void WorkflowGraph_ShouldAddEdgesBetweenNodes()
    {
        // Arrange
        var builder = new WorkflowGraphBuilder();
        builder.AddNode("step1", "First step");
        builder.AddNode("step2", "Second step");

        // Act
        builder.AddEdge("step1", "step2");
        var graph = builder.Build();

        // Assert
        graph.Edges.Should().ContainSingle()
            .Which.Should().Match<WorkflowEdge>(e => e.From == "step1" && e.To == "step2");
    }

    [Fact]
    public void WorkflowGraph_ShouldRejectEdgeToNonExistentNode()
    {
        // Arrange
        var builder = new WorkflowGraphBuilder();
        builder.AddNode("step1", "First step");

        // Act
        var act = () => builder.AddEdge("step1", "nonexistent");

        // Assert
        act.Should().Throw<InvalidOperationException>(
            "cannot create an edge to a node that doesn't exist in the graph");
    }

    [Fact]
    public void WorkflowGraph_ShouldRejectDuplicateNodeIds()
    {
        // Arrange
        var builder = new WorkflowGraphBuilder();
        builder.AddNode("step1", "First step");

        // Act
        var act = () => builder.AddNode("step1", "Duplicate step");

        // Assert
        act.Should().Throw<InvalidOperationException>(
            "duplicate node IDs should not be allowed");
    }

    [Fact]
    public void WorkflowGraph_ShouldSupportLinearPipeline()
    {
        // Arrange — a simple linear workflow: A → B → C
        var builder = new WorkflowGraphBuilder();
        builder.AddNode("A", "Step A");
        builder.AddNode("B", "Step B");
        builder.AddNode("C", "Step C");
        builder.AddEdge("A", "B");
        builder.AddEdge("B", "C");

        // Act
        var graph = builder.Build();

        // Assert
        graph.Nodes.Should().HaveCount(3);
        graph.Edges.Should().HaveCount(2);
        graph.GetSuccessors("A").Should().ContainSingle().Which.Should().Be("B");
        graph.GetSuccessors("B").Should().ContainSingle().Which.Should().Be("C");
        graph.GetSuccessors("C").Should().BeEmpty("terminal node has no successors");
    }

    [Fact]
    public void WorkflowGraph_ShouldSupportBranching()
    {
        // Arrange — branching: A → B, A → C (parallel paths)
        var builder = new WorkflowGraphBuilder();
        builder.AddNode("A", "Decision node");
        builder.AddNode("B", "Branch 1");
        builder.AddNode("C", "Branch 2");
        builder.AddEdge("A", "B");
        builder.AddEdge("A", "C");

        // Act
        var graph = builder.Build();

        // Assert
        graph.GetSuccessors("A").Should().HaveCount(2)
            .And.Contain("B")
            .And.Contain("C");
    }

    [Fact]
    public void WorkflowGraph_ShouldIdentifyEntryNodes()
    {
        // Arrange — entry node has no incoming edges
        var builder = new WorkflowGraphBuilder();
        builder.AddNode("start", "Entry point");
        builder.AddNode("middle", "Processing");
        builder.AddNode("end", "Exit point");
        builder.AddEdge("start", "middle");
        builder.AddEdge("middle", "end");

        // Act
        var graph = builder.Build();
        var entryNodes = graph.GetEntryNodes();

        // Assert
        entryNodes.Should().ContainSingle().Which.Should().Be("start");
    }

    [Fact]
    public void WorkflowGraph_Empty_ShouldHaveNoNodesOrEdges()
    {
        // Arrange & Act
        var builder = new WorkflowGraphBuilder();
        var graph = builder.Build();

        // Assert
        graph.Nodes.Should().BeEmpty();
        graph.Edges.Should().BeEmpty();
    }
}

#region Workflow Graph Infrastructure

/// <summary>
/// Builder for constructing a workflow graph (DAG) representing agent orchestration.
/// </summary>
public class WorkflowGraphBuilder
{
    private readonly List<WorkflowNode> _nodes = new();
    private readonly List<WorkflowEdge> _edges = new();
    private readonly HashSet<string> _nodeIds = new();

    public void AddNode(string id, string description)
    {
        if (_nodeIds.Contains(id))
            throw new InvalidOperationException($"Node '{id}' already exists in the workflow graph.");

        _nodeIds.Add(id);
        _nodes.Add(new WorkflowNode(id, description));
    }

    public void AddEdge(string fromId, string toId)
    {
        if (!_nodeIds.Contains(fromId))
            throw new InvalidOperationException($"Source node '{fromId}' does not exist in the graph.");
        if (!_nodeIds.Contains(toId))
            throw new InvalidOperationException($"Target node '{toId}' does not exist in the graph.");

        _edges.Add(new WorkflowEdge(fromId, toId));
    }

    public WorkflowGraph Build()
    {
        return new WorkflowGraph(_nodes.ToList(), _edges.ToList());
    }
}

public record WorkflowNode(string Id, string Description);
public record WorkflowEdge(string From, string To);

public class WorkflowGraph
{
    public IReadOnlyList<WorkflowNode> Nodes { get; }
    public IReadOnlyList<WorkflowEdge> Edges { get; }

    public WorkflowGraph(List<WorkflowNode> nodes, List<WorkflowEdge> edges)
    {
        Nodes = nodes.AsReadOnly();
        Edges = edges.AsReadOnly();
    }

    public IReadOnlyList<string> GetSuccessors(string nodeId)
    {
        return Edges.Where(e => e.From == nodeId).Select(e => e.To).ToList().AsReadOnly();
    }

    public IReadOnlyList<string> GetEntryNodes()
    {
        var nodesWithIncoming = Edges.Select(e => e.To).ToHashSet();
        return Nodes.Where(n => !nodesWithIncoming.Contains(n.Id))
                    .Select(n => n.Id)
                    .ToList()
                    .AsReadOnly();
    }
}

#endregion
