using System.Linq;
using FluentAssertions;
using Spade;
using Spade.Advanced.Voronoi;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests;

public class ClippedVoronoiTests
{
    [Fact]
    public void ClipToPolygon_ProducesCellsWithinDomain()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.Insert(new Point2<double>(0, 0));
        triangulation.Insert(new Point2<double>(2, 0));
        triangulation.Insert(new Point2<double>(0, 2));
        triangulation.Insert(new Point2<double>(2, 2));

        var domain = new ClipPolygon(new[]
        {
            new Point2<double>(-10, -10),
            new Point2<double>(10, -10),
            new Point2<double>(10, 10),
            new Point2<double>(-10, 10),
        });

        var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

        diagram.Should().NotBeNull();
        diagram.Cells.Count.Should().Be(4);

        foreach (var cell in diagram.Cells)
        {
            cell.Polygon.Count.Should().BeGreaterThanOrEqualTo(3);

            foreach (var p in cell.Polygon)
            {
                PointInsideConvexPolygon(p, domain.Vertices).Should().BeTrue();
            }
        }

        // Cells should partition the domain (within tolerance).
        var sumArea = diagram.Cells.Sum(c => PolygonArea(c.Polygon));
        var domainArea = PolygonArea(domain.Vertices);
        sumArea.Should().BeApproximately(domainArea, domainArea * 1e-6);
    }

    [Fact]
    public void ClipToPolygon_SetsIsClipped_WhenDomainIsSmall()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.Insert(new Point2<double>(-0.5, 0));
        triangulation.Insert(new Point2<double>(0.5, 0));
        triangulation.Insert(new Point2<double>(0, -0.5));
        triangulation.Insert(new Point2<double>(0, 0.5));
        triangulation.Insert(new Point2<double>(0, 0));

        var smallDomain = new ClipPolygon(new[]
        {
            new Point2<double>(-1, -1),
            new Point2<double>(1, -1),
            new Point2<double>(1, 1),
            new Point2<double>(-1, 1),
        });

        var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, smallDomain);

        diagram.Cells.Count.Should().Be(5);
        diagram.Cells.Any(c => c.IsClipped).Should().BeTrue();
    }

    private static bool PointInsideConvexPolygon(Point2<double> p, System.Collections.Generic.IReadOnlyList<Point2<double>> polygon)
    {
        int count = polygon.Count;
        for (int i = 0; i < count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % count];
            double cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            if (cross < -1e-6)
            {
                return false;
            }
        }
        return true;
    }

    private static double PolygonArea(System.Collections.Generic.IReadOnlyList<Point2<double>> polygon)
    {
        if (polygon.Count < 3)
        {
            return 0;
        }

        double sum = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return System.Math.Abs(sum) / 2.0;
    }

    #region Degenerate Cell Scenario Tests

    /// <summary>
    /// Test generator outside domain - Requirements 2.1, 2.2
    /// </summary>
    [Fact]
    public void ClipToPolygon_GeneratorOutsideDomain_RecordsInOutsideDomainCollection()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        
        // Insert points: some inside domain, some outside
        var insidePoint1 = triangulation.Insert(new Point2<double>(0, 0));
        var insidePoint2 = triangulation.Insert(new Point2<double>(1, 0));
        var insidePoint3 = triangulation.Insert(new Point2<double>(0.5, 1));
        var outsidePoint1 = triangulation.Insert(new Point2<double>(10, 10)); // Far outside
        var outsidePoint2 = triangulation.Insert(new Point2<double>(-10, -10)); // Far outside

        // Small domain that only contains the inside points
        var domain = new ClipPolygon(new[]
        {
            new Point2<double>(-1, -1),
            new Point2<double>(2, -1),
            new Point2<double>(2, 2),
            new Point2<double>(-1, 2),
        });

        var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

        // Verify that outside generators are recorded in OutsideDomain collection
        diagram.OutsideDomain.Should().Contain(outsidePoint1.Index);
        diagram.OutsideDomain.Should().Contain(outsidePoint2.Index);
        
        // Inside generators should not be in OutsideDomain collection
        diagram.OutsideDomain.Should().NotContain(insidePoint1.Index);
        diagram.OutsideDomain.Should().NotContain(insidePoint2.Index);
        diagram.OutsideDomain.Should().NotContain(insidePoint3.Index);

        // Outside generators should not have valid cells
        diagram.HasValidCell(outsidePoint1.Index).Should().BeFalse();
        diagram.HasValidCell(outsidePoint2.Index).Should().BeFalse();
        
        // TryGetCell should return false for outside generators
        diagram.TryGetCell(outsidePoint1.Index, out var cell1).Should().BeFalse();
        cell1.Should().BeNull();
        diagram.TryGetCell(outsidePoint2.Index, out var cell2).Should().BeFalse();
        cell2.Should().BeNull();

        // Indexer should return null for outside generators
        diagram[outsidePoint1.Index].Should().BeNull();
        diagram[outsidePoint2.Index].Should().BeNull();
    }

    /// <summary>
    /// Test generator on domain boundary - Requirements 2.1, 2.2
    /// </summary>
    [Fact]
    public void ClipToPolygon_GeneratorOnDomainBoundary_HandlesCorrectly()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        
        // Insert points: some inside, some exactly on boundary
        var insidePoint = triangulation.Insert(new Point2<double>(0, 0));
        var boundaryPoint1 = triangulation.Insert(new Point2<double>(1, 0)); // On bottom edge
        var boundaryPoint2 = triangulation.Insert(new Point2<double>(0, 1)); // On left edge
        var cornerPoint = triangulation.Insert(new Point2<double>(1, 1)); // On corner

        // Domain with boundary points exactly on edges
        var domain = new ClipPolygon(new[]
        {
            new Point2<double>(0, 0),
            new Point2<double>(1, 0),
            new Point2<double>(1, 1),
            new Point2<double>(0, 1),
        });

        var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

        // Boundary points should either have valid cells or be recorded as degenerate/outside
        // The exact behavior depends on the implementation, but they should be handled consistently
        foreach (var point in new[] { boundaryPoint1, boundaryPoint2, cornerPoint })
        {
            var hasValidCell = diagram.HasValidCell(point.Index);
            var isDegenerate = diagram.DegenerateCells.Contains(point.Index);
            var isOutside = diagram.OutsideDomain.Contains(point.Index);
            
            // Point should be in exactly one category: valid cell, degenerate, or outside
            var categoryCount = (hasValidCell ? 1 : 0) + (isDegenerate ? 1 : 0) + (isOutside ? 1 : 0);
            categoryCount.Should().Be(1, $"Boundary point {point.Index} should be in exactly one category");
            
            // TryGetCell should be consistent with HasValidCell
            var tryGetResult = diagram.TryGetCell(point.Index, out var cell);
            tryGetResult.Should().Be(hasValidCell);
            
            if (hasValidCell)
            {
                cell.Should().NotBeNull();
                cell!.GeneratorIndex.Should().Be(point.Index);
            }
            else
            {
                cell.Should().BeNull();
            }
        }

        // Inside point should definitely have a valid cell
        diagram.HasValidCell(insidePoint.Index).Should().BeTrue();
        diagram.DegenerateCells.Should().NotContain(insidePoint.Index);
        diagram.OutsideDomain.Should().NotContain(insidePoint.Index);
    }

    /// <summary>
    /// Test cell that clips to < 3 vertices - Requirements 2.1, 2.2
    /// </summary>
    [Fact]
    public void ClipToPolygon_CellClipsToFewerThanThreeVertices_RecordsInDegenerateCells()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        
        // Create a configuration where some cells will be clipped to < 3 vertices
        // Use points that will create cells that get severely clipped by a small domain
        var point1 = triangulation.Insert(new Point2<double>(0, 0));
        var point2 = triangulation.Insert(new Point2<double>(10, 0));
        var point3 = triangulation.Insert(new Point2<double>(5, 10));
        var point4 = triangulation.Insert(new Point2<double>(5, -10)); // This should create a cell that gets clipped severely
        var point5 = triangulation.Insert(new Point2<double>(-5, 0)); // This should also create a cell that gets clipped

        // Very small domain that will severely clip most cells
        var domain = new ClipPolygon(new[]
        {
            new Point2<double>(1, -1),
            new Point2<double>(2, -1),
            new Point2<double>(2, 1),
            new Point2<double>(1, 1),
        });

        var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

        // With this configuration, some cells should become degenerate or be outside domain
        // The exact behavior depends on the Voronoi cell shapes, but we should have some diagnostic entries
        var totalDiagnostics = diagram.DegenerateCells.Count + diagram.OutsideDomain.Count;
        totalDiagnostics.Should().BeGreaterThan(0, "The small domain should create some degenerate or outside-domain cells");

        // Verify that degenerate cells are handled correctly
        foreach (var degenerateIndex in diagram.DegenerateCells)
        {
            // Degenerate cells should not have valid cells
            diagram.HasValidCell(degenerateIndex).Should().BeFalse();
            
            // TryGetCell should return false for degenerate cells
            diagram.TryGetCell(degenerateIndex, out var cell).Should().BeFalse();
            cell.Should().BeNull();
            
            // Indexer should return null for degenerate cells
            diagram[degenerateIndex].Should().BeNull();
            
            // Degenerate cells should not appear in the Cells collection
            diagram.Cells.Should().NotContain(c => c.GeneratorIndex == degenerateIndex);
        }

        // All cells in the Cells collection should have >= 3 vertices
        foreach (var cell in diagram.Cells)
        {
            cell.Polygon.Count.Should().BeGreaterThanOrEqualTo(3, 
                $"Cell {cell.GeneratorIndex} should have at least 3 vertices after clipping");
        }

        // Verify that non-degenerate cells are not in the DegenerateCells collection
        foreach (var cell in diagram.Cells)
        {
            diagram.DegenerateCells.Should().NotContain(cell.GeneratorIndex,
                $"Valid cell {cell.GeneratorIndex} should not be in DegenerateCells collection");
        }
    }

    /// <summary>
    /// Test comprehensive degenerate scenario with mixed cases - Requirements 2.1, 2.2
    /// </summary>
    [Fact]
    public void ClipToPolygon_MixedDegenerateCases_HandlesAllScenariosCorrectly()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        
        // Create a mix of scenarios:
        var validPoint1 = triangulation.Insert(new Point2<double>(0, 0)); // Inside domain
        var validPoint2 = triangulation.Insert(new Point2<double>(1, 1)); // Inside domain
        var outsidePoint = triangulation.Insert(new Point2<double>(10, 10)); // Outside domain
        var nearBoundaryPoint = triangulation.Insert(new Point2<double>(1.9, 0.1)); // Near boundary, may be degenerate
        var farPoint = triangulation.Insert(new Point2<double>(20, 20)); // Very far outside

        // Medium-sized domain
        var domain = new ClipPolygon(new[]
        {
            new Point2<double>(-0.5, -0.5),
            new Point2<double>(1.5, -0.5),
            new Point2<double>(1.5, 1.5),
            new Point2<double>(-0.5, 1.5),
        });

        var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

        // Verify that all generator indices are accounted for in exactly one category
        var allIndices = new[] { validPoint1.Index, validPoint2.Index, outsidePoint.Index, nearBoundaryPoint.Index, farPoint.Index };
        
        foreach (var index in allIndices)
        {
            var hasValidCell = diagram.HasValidCell(index);
            var isDegenerate = diagram.DegenerateCells.Contains(index);
            var isOutside = diagram.OutsideDomain.Contains(index);
            
            // Each generator should be in exactly one category
            var categoryCount = (hasValidCell ? 1 : 0) + (isDegenerate ? 1 : 0) + (isOutside ? 1 : 0);
            categoryCount.Should().Be(1, $"Generator {index} should be in exactly one category");
            
            // Verify consistency between methods
            var tryGetResult = diagram.TryGetCell(index, out var cell);
            tryGetResult.Should().Be(hasValidCell);
            
            if (hasValidCell)
            {
                cell.Should().NotBeNull();
                cell!.GeneratorIndex.Should().Be(index);
                diagram.Cells.Should().Contain(c => c.GeneratorIndex == index);
            }
            else
            {
                cell.Should().BeNull();
                diagram.Cells.Should().NotContain(c => c.GeneratorIndex == index);
            }
        }

        // Verify that valid points are definitely valid
        diagram.HasValidCell(validPoint1.Index).Should().BeTrue();
        diagram.HasValidCell(validPoint2.Index).Should().BeTrue();
        
        // Verify that far outside points are definitely outside
        diagram.OutsideDomain.Should().Contain(outsidePoint.Index);
        diagram.OutsideDomain.Should().Contain(farPoint.Index);

        // Verify collections don't overlap
        var validCellIndices = diagram.Cells.Select(c => c.GeneratorIndex).ToHashSet();
        var degenerateIndices = diagram.DegenerateCells.ToHashSet();
        var outsideIndices = diagram.OutsideDomain.ToHashSet();
        
        validCellIndices.Should().NotIntersectWith(degenerateIndices);
        validCellIndices.Should().NotIntersectWith(outsideIndices);
        degenerateIndices.Should().NotIntersectWith(outsideIndices);
    }

    #endregion

    #region Lookup Method Tests

    /// <summary>
    /// Test TryGetCell with valid index - Requirements 5.1, 5.2, 5.3
    /// </summary>
    [Fact]
    public void TryGetCell_WithValidIndex_ReturnsTrue()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var vertex1 = triangulation.Insert(new Point2<double>(0, 0));
        var vertex2 = triangulation.Insert(new Point2<double>(2, 0));
        var vertex3 = triangulation.Insert(new Point2<double>(1, 2));

        var domain = new ClipPolygon(new[]
        {
            new Point2<double>(-5, -5),
            new Point2<double>(5, -5),
            new Point2<double>(5, 5),
            new Point2<double>(-5, 5),
        });

        var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

        // Test TryGetCell with valid indices
        var result1 = diagram.TryGetCell(vertex1.Index, out var cell1);
        result1.Should().BeTrue();
        cell1.Should().NotBeNull();
        cell1!.GeneratorIndex.Should().Be(vertex1.Index);
        cell1.Generator.Should().Be(new Point2<double>(0, 0));

        var result2 = diagram.TryGetCell(vertex2.Index, out var cell2);
        result2.Should().BeTrue();
        cell2.Should().NotBeNull();
        cell2!.GeneratorIndex.Should().Be(vertex2.Index);
        cell2.Generator.Should().Be(new Point2<double>(2, 0));

        var result3 = diagram.TryGetCell(vertex3.Index, out var cell3);
        result3.Should().BeTrue();
        cell3.Should().NotBeNull();
        cell3!.GeneratorIndex.Should().Be(vertex3.Index);
        cell3.Generator.Should().Be(new Point2<double>(1, 2));
    }

    /// <summary>
    /// Test TryGetCell with invalid index (negative, out of range) - Requirements 5.1, 5.2, 5.3
    /// </summary>
    [Fact]
    public void TryGetCell_WithInvalidIndex_ReturnsFalse()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var vertex1 = triangulation.Insert(new Point2<double>(0, 0));
        var vertex2 = triangulation.Insert(new Point2<double>(2, 0));

        var domain = new ClipPolygon(new[]
        {
            new Point2<double>(-5, -5),
            new Point2<double>(5, -5),
            new Point2<double>(5, 5),
            new Point2<double>(-5, 5),
        });

        var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

        // Test with negative index
        var result1 = diagram.TryGetCell(-1, out var cell1);
        result1.Should().BeFalse();
        cell1.Should().BeNull();

        var result2 = diagram.TryGetCell(-100, out var cell2);
        result2.Should().BeFalse();
        cell2.Should().BeNull();

        // Test with out-of-range positive index
        var maxValidIndex = System.Math.Max(vertex1.Index, vertex2.Index);
        var result3 = diagram.TryGetCell(maxValidIndex + 1, out var cell3);
        result3.Should().BeFalse();
        cell3.Should().BeNull();

        var result4 = diagram.TryGetCell(1000, out var cell4);
        result4.Should().BeFalse();
        cell4.Should().BeNull();

        // Test with index that doesn't exist (gap in indices)
        var result5 = diagram.TryGetCell(999, out var cell5);
        result5.Should().BeFalse();
        cell5.Should().BeNull();
    }

    /// <summary>
    /// Test indexer behavior with valid and invalid indices - Requirements 5.1, 5.2, 5.3
    /// </summary>
    [Fact]
    public void Indexer_WithValidIndex_ReturnsCell()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var vertex1 = triangulation.Insert(new Point2<double>(0, 0));
        var vertex2 = triangulation.Insert(new Point2<double>(2, 0));
        var vertex3 = triangulation.Insert(new Point2<double>(1, 2));

        var domain = new ClipPolygon(new[]
        {
            new Point2<double>(-5, -5),
            new Point2<double>(5, -5),
            new Point2<double>(5, 5),
            new Point2<double>(-5, 5),
        });

        var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

        // Test indexer with valid indices
        var cell1 = diagram[vertex1.Index];
        cell1.Should().NotBeNull();
        cell1!.GeneratorIndex.Should().Be(vertex1.Index);
        cell1.Generator.Should().Be(new Point2<double>(0, 0));

        var cell2 = diagram[vertex2.Index];
        cell2.Should().NotBeNull();
        cell2!.GeneratorIndex.Should().Be(vertex2.Index);
        cell2.Generator.Should().Be(new Point2<double>(2, 0));

        var cell3 = diagram[vertex3.Index];
        cell3.Should().NotBeNull();
        cell3!.GeneratorIndex.Should().Be(vertex3.Index);
        cell3.Generator.Should().Be(new Point2<double>(1, 2));
    }

    /// <summary>
    /// Test indexer behavior with invalid indices - Requirements 5.1, 5.2, 5.3
    /// </summary>
    [Fact]
    public void Indexer_WithInvalidIndex_ReturnsNull()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var vertex1 = triangulation.Insert(new Point2<double>(0, 0));
        var vertex2 = triangulation.Insert(new Point2<double>(2, 0));

        var domain = new ClipPolygon(new[]
        {
            new Point2<double>(-5, -5),
            new Point2<double>(5, -5),
            new Point2<double>(5, 5),
            new Point2<double>(-5, 5),
        });

        var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

        // Test indexer with negative indices
        diagram[-1].Should().BeNull();
        diagram[-100].Should().BeNull();

        // Test indexer with out-of-range positive indices
        var maxValidIndex = System.Math.Max(vertex1.Index, vertex2.Index);
        diagram[maxValidIndex + 1].Should().BeNull();
        diagram[1000].Should().BeNull();

        // Test indexer with non-existent index
        diagram[999].Should().BeNull();
    }

    /// <summary>
    /// Test HasValidCell consistency with TryGetCell and indexer - Requirements 5.1, 5.2, 5.3
    /// </summary>
    [Fact]
    public void HasValidCell_ConsistentWithOtherLookupMethods()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var vertex1 = triangulation.Insert(new Point2<double>(0, 0));
        var vertex2 = triangulation.Insert(new Point2<double>(2, 0));
        var vertex3 = triangulation.Insert(new Point2<double>(1, 2));

        var domain = new ClipPolygon(new[]
        {
            new Point2<double>(-5, -5),
            new Point2<double>(5, -5),
            new Point2<double>(5, 5),
            new Point2<double>(-5, 5),
        });

        var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

        // Test consistency for valid indices
        var validIndices = new[] { vertex1.Index, vertex2.Index, vertex3.Index };
        foreach (var index in validIndices)
        {
            var hasValidCell = diagram.HasValidCell(index);
            var tryGetResult = diagram.TryGetCell(index, out var cell);
            var indexerResult = diagram[index];

            hasValidCell.Should().BeTrue($"HasValidCell should return true for valid index {index}");
            tryGetResult.Should().BeTrue($"TryGetCell should return true for valid index {index}");
            cell.Should().NotBeNull($"TryGetCell should return non-null cell for valid index {index}");
            indexerResult.Should().NotBeNull($"Indexer should return non-null cell for valid index {index}");

            // Verify that TryGetCell and indexer return the same cell
            cell.Should().BeSameAs(indexerResult, $"TryGetCell and indexer should return same cell for index {index}");
        }

        // Test consistency for invalid indices
        var invalidIndices = new[] { -1, -100, 1000, 999 };
        foreach (var index in invalidIndices)
        {
            var hasValidCell = diagram.HasValidCell(index);
            var tryGetResult = diagram.TryGetCell(index, out var cell);
            var indexerResult = diagram[index];

            hasValidCell.Should().BeFalse($"HasValidCell should return false for invalid index {index}");
            tryGetResult.Should().BeFalse($"TryGetCell should return false for invalid index {index}");
            cell.Should().BeNull($"TryGetCell should return null cell for invalid index {index}");
            indexerResult.Should().BeNull($"Indexer should return null cell for invalid index {index}");
        }
    }

    /// <summary>
    /// Test lookup methods with degenerate and outside domain cases - Requirements 5.1, 5.2, 5.3
    /// </summary>
    [Fact]
    public void LookupMethods_WithDegenerateAndOutsideCases_ReturnConsistentResults()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        
        // Create a mix of valid, degenerate, and outside cases
        var validPoint = triangulation.Insert(new Point2<double>(0, 0));
        var outsidePoint = triangulation.Insert(new Point2<double>(10, 10));
        var potentiallyDegeneratePoint = triangulation.Insert(new Point2<double>(1.9, 1.9));

        // Small domain that will create some degenerate/outside cases
        var domain = new ClipPolygon(new[]
        {
            new Point2<double>(-1, -1),
            new Point2<double>(1, -1),
            new Point2<double>(1, 1),
            new Point2<double>(-1, 1),
        });

        var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

        // Test all generator indices for consistency
        var allIndices = new[] { validPoint.Index, outsidePoint.Index, potentiallyDegeneratePoint.Index };
        
        foreach (var index in allIndices)
        {
            var hasValidCell = diagram.HasValidCell(index);
            var tryGetResult = diagram.TryGetCell(index, out var cell);
            var indexerResult = diagram[index];
            var isDegenerate = diagram.DegenerateCells.Contains(index);
            var isOutside = diagram.OutsideDomain.Contains(index);

            // HasValidCell should be consistent with TryGetCell
            hasValidCell.Should().Be(tryGetResult, $"HasValidCell and TryGetCell should be consistent for index {index}");

            // If HasValidCell is true, both TryGetCell and indexer should return non-null
            if (hasValidCell)
            {
                cell.Should().NotBeNull($"TryGetCell should return non-null cell when HasValidCell is true for index {index}");
                indexerResult.Should().NotBeNull($"Indexer should return non-null cell when HasValidCell is true for index {index}");
                cell.Should().BeSameAs(indexerResult, $"TryGetCell and indexer should return same cell for index {index}");
                
                // Valid cells should not be in diagnostic collections
                isDegenerate.Should().BeFalse($"Valid cell {index} should not be in DegenerateCells");
                isOutside.Should().BeFalse($"Valid cell {index} should not be in OutsideDomain");
            }
            else
            {
                // If HasValidCell is false, both TryGetCell and indexer should return null
                cell.Should().BeNull($"TryGetCell should return null cell when HasValidCell is false for index {index}");
                indexerResult.Should().BeNull($"Indexer should return null cell when HasValidCell is false for index {index}");
                
                // Invalid cells should be in exactly one diagnostic collection
                var diagnosticCount = (isDegenerate ? 1 : 0) + (isOutside ? 1 : 0);
                diagnosticCount.Should().Be(1, $"Invalid cell {index} should be in exactly one diagnostic collection");
            }
        }

        // Verify that the valid point definitely has a valid cell
        diagram.HasValidCell(validPoint.Index).Should().BeTrue();
        
        // Verify that the outside point is definitely outside
        diagram.OutsideDomain.Should().Contain(outsidePoint.Index);
        diagram.HasValidCell(outsidePoint.Index).Should().BeFalse();
    }

    #endregion
}
