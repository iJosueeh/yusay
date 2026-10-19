using Yusay.Application.Trends.Common;

namespace Yusay.Domain.UnitTests.Trends;

public sealed class MaximalCliqueFinderTests
{
    private sealed record TestVertex(Guid Id, string Name);

    [Fact]
    public void FindMaximalCliques_WithEmptyVertices_ShouldReturnEmpty()
    {
        var vertices = Array.Empty<TestVertex>();
        var edges = new HashSet<(Guid, Guid)>();

        var cliques = MaximalCliqueFinder.FindMaximalCliques(vertices, v => v.Id, edges);

        Assert.Empty(cliques);
    }

    [Fact]
    public void FindMaximalCliques_WithSingleVertex_ShouldReturnSingleClique()
    {
        var v1 = new TestVertex(Guid.NewGuid(), "V1");
        var vertices = new[] { v1 };
        var edges = new HashSet<(Guid, Guid)>();

        var cliques = MaximalCliqueFinder.FindMaximalCliques(vertices, v => v.Id, edges);

        Assert.Single(cliques);
        Assert.Single(cliques[0]);
        Assert.Equal(v1.Id, cliques[0][0].Id);
    }

    [Fact]
    public void FindMaximalCliques_WithTwoDisconnectedVertices_ShouldReturnTwoCliques()
    {
        var v1 = new TestVertex(Guid.NewGuid(), "V1");
        var v2 = new TestVertex(Guid.NewGuid(), "V2");
        var vertices = new[] { v1, v2 };
        var edges = new HashSet<(Guid, Guid)>();

        var cliques = MaximalCliqueFinder.FindMaximalCliques(vertices, v => v.Id, edges);

        Assert.Equal(2, cliques.Count);
        Assert.Contains(cliques, c => c.Count == 1 && c[0].Id == v1.Id);
        Assert.Contains(cliques, c => c.Count == 1 && c[0].Id == v2.Id);
    }

    [Fact]
    public void FindMaximalCliques_WithTwoConnectedVertices_ShouldReturnSingleCliqueWithBoth()
    {
        var v1 = new TestVertex(Guid.NewGuid(), "V1");
        var v2 = new TestVertex(Guid.NewGuid(), "V2");
        var vertices = new[] { v1, v2 };
        var edges = new HashSet<(Guid, Guid)>
        {
            (v1.Id, v2.Id),
            (v2.Id, v1.Id)
        };

        var cliques = MaximalCliqueFinder.FindMaximalCliques(vertices, v => v.Id, edges);

        Assert.Single(cliques);
        Assert.Equal(2, cliques[0].Count);
        Assert.Contains(cliques[0], v => v.Id == v1.Id);
        Assert.Contains(cliques[0], v => v.Id == v2.Id);
    }

    [Fact]
    public void FindMaximalCliques_WithV1V2AndV2V3Compatible_WithoutV1V3_ShouldReturnTwoSeparateCliques_NoTransitivity()
    {
        // V1 - V2 - V3 (no edge between V1 and V3)
        var v1 = new TestVertex(Guid.NewGuid(), "V1");
        var v2 = new TestVertex(Guid.NewGuid(), "V2");
        var v3 = new TestVertex(Guid.NewGuid(), "V3");

        var vertices = new[] { v1, v2, v3 };
        var edges = new HashSet<(Guid, Guid)>
        {
            (v1.Id, v2.Id),
            (v2.Id, v1.Id),
            (v2.Id, v3.Id),
            (v3.Id, v2.Id)
        };

        var cliques = MaximalCliqueFinder.FindMaximalCliques(vertices, v => v.Id, edges);

        // DR-DOM-003: declarar (v1, v2) y (v2, v3) no hace transitivo a (v1, v3).
        // Debe generar dos cliques maximales: {v1, v2} y {v2, v3}.
        Assert.Equal(2, cliques.Count);

        var clique1 = cliques.Single(c => c.Any(v => v.Id == v1.Id));
        var clique2 = cliques.Single(c => c.Any(v => v.Id == v3.Id));

        Assert.Equal(2, clique1.Count);
        Assert.Contains(clique1, v => v.Id == v1.Id);
        Assert.Contains(clique1, v => v.Id == v2.Id);

        Assert.Equal(2, clique2.Count);
        Assert.Contains(clique2, v => v.Id == v2.Id);
        Assert.Contains(clique2, v => v.Id == v3.Id);

        // Ningún clique debe contener a v1 y v3 juntos
        Assert.DoesNotContain(cliques, c => c.Any(v => v.Id == v1.Id) && c.Any(v => v.Id == v3.Id));
    }

    [Fact]
    public void FindMaximalCliques_WithTriangleK3_ShouldReturnSingleCliqueWithAllThree()
    {
        var v1 = new TestVertex(Guid.NewGuid(), "V1");
        var v2 = new TestVertex(Guid.NewGuid(), "V2");
        var v3 = new TestVertex(Guid.NewGuid(), "V3");

        var vertices = new[] { v1, v2, v3 };
        var edges = new HashSet<(Guid, Guid)>
        {
            (v1.Id, v2.Id), (v2.Id, v1.Id),
            (v2.Id, v3.Id), (v3.Id, v2.Id),
            (v1.Id, v3.Id), (v3.Id, v1.Id)
        };

        var cliques = MaximalCliqueFinder.FindMaximalCliques(vertices, v => v.Id, edges);

        Assert.Single(cliques);
        Assert.Equal(3, cliques[0].Count);
    }
}
