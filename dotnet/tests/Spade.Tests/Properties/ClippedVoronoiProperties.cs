using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Spade;
using Spade.Advanced.Voronoi;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Properties;

/// <summary>
/// Property-based tests for ClippedVoronoi deterministic indexing.
/// **Feature: deterministic-indexing**
/// </summary>
[Trait("Category", "PropertyTests")]
public class ClippedVoronoiProperties
{
    /// <summary>
    /// **Feature: deterministic-indexing, Property 1: Generator index round-trip**
    /// 
    /// For any triangulation and clipping domain, if a cell is produced for generator index i,
    /// then cell.GeneratorIndex equals i and cell.Generator equals the vertex data at index i
    /// in the original triangulation.
    /// 
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratorIndex_RoundTrip()
    {
        var pointSetGen = ArbMap.Default.GeneratorFor<int>()
            .Where(x => x >= 3 && x <= 20)
            .SelectMany(count => 
                ArbMap.Default.GeneratorFor<int>()
                    .Where(x => x >= 0 && x <= 10000)
                    .Select(seed => GenerateUniquePoints(count, seed)));

        var domainScaleGen = ArbMap.Default.GeneratorFor<int>()
            .Where(x => x >= 2 && x <= 10)
            .Select(x => (double)x);

        return Prop.ForAll(
            pointSetGen.ToArbitrary(),
            domainScaleGen.ToArbitrary(),
            (points, domainScale) =>
            {
                // Skip if not enough points for a triangulation
                if (points.Count < 3)
                    return true;

                // Build triangulation and track insertion order
                var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
                var insertedPoints = new Dictionary<int, Point2<double>>();

                foreach (var point in points)
                {
                    var handle = triangulation.Insert(point);
                    insertedPoints[handle.Index] = point;
                }

                // Create a domain that encompasses all points
                var domain = CreateDomainFromPoints(points, domainScale);

                // Clip the Voronoi diagram
                var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

                // Verify the round-trip property for each cell
                foreach (var cell in diagram.Cells)
                {
                    var generatorIndex = cell.GeneratorIndex;

                    // Property 1a: GeneratorIndex should be a valid index in the triangulation
                    if (!insertedPoints.ContainsKey(generatorIndex))
                        return false;

                    // Property 1b: Generator should match the vertex data at that index
                    var expectedPosition = insertedPoints[generatorIndex];
                    var actualPosition = cell.Generator;

                    if (Math.Abs(expectedPosition.X - actualPosition.X) > 1e-12 ||
                        Math.Abs(expectedPosition.Y - actualPosition.Y) > 1e-12)
                        return false;
                }

                return true;
            });
    }

    /// <summary>
    /// **Feature: deterministic-indexing, Property 2: Cell ordering invariant**
    /// 
    /// For any clipped Voronoi diagram, iterating over Cells yields cells with strictly
    /// ascending GeneratorIndex values.
    /// 
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CellOrdering_Invariant()
    {
        var pointSetGen = ArbMap.Default.GeneratorFor<int>()
            .Where(x => x >= 3 && x <= 20)
            .SelectMany(count =>
                ArbMap.Default.GeneratorFor<int>()
                    .Where(x => x >= 0 && x <= 10000)
                    .Select(seed => GenerateUniquePoints(count, seed)));

        var domainScaleGen = ArbMap.Default.GeneratorFor<int>()
            .Where(x => x >= 2 && x <= 10)
            .Select(x => (double)x);

        return Prop.ForAll(
            pointSetGen.ToArbitrary(),
            domainScaleGen.ToArbitrary(),
            (points, domainScale) =>
            {
                // Skip if not enough points for a triangulation
                if (points.Count < 3)
                    return true;

                // Build triangulation
                var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

                foreach (var point in points)
                {
                    triangulation.Insert(point);
                }

                // Create a domain that encompasses all points
                var domain = CreateDomainFromPoints(points, domainScale);

                // Clip the Voronoi diagram
                var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

                // Verify the ordering invariant: cells must be in strictly ascending GeneratorIndex order
                var cells = diagram.Cells;
                
                // If there are 0 or 1 cells, the ordering is trivially satisfied
                if (cells.Count <= 1)
                    return true;

                // Check that each cell's GeneratorIndex is strictly greater than the previous
                for (var i = 1; i < cells.Count; i++)
                {
                    var previousIndex = cells[i - 1].GeneratorIndex;
                    var currentIndex = cells[i].GeneratorIndex;

                    // Strictly ascending: current must be greater than previous
                    if (currentIndex <= previousIndex)
                        return false;
                }

                return true;
            });
    }

    /// <summary>
    /// Generates a set of unique points with no duplicates.
    /// </summary>
    private static List<Point2<double>> GenerateUniquePoints(int count, int seed)
    {
        var rng = new Random(seed);
        var points = new List<Point2<double>>();
        var seen = new HashSet<(double, double)>();

        while (points.Count < count)
        {
            var x = (rng.NextDouble() - 0.5) * 100;
            var y = (rng.NextDouble() - 0.5) * 100;

            // Round to avoid floating point duplicates
            x = Math.Round(x, 6);
            y = Math.Round(y, 6);

            if (seen.Add((x, y)))
            {
                points.Add(new Point2<double>(x, y));
            }
        }

        return points;
    }

    /// <summary>
    /// **Feature: deterministic-indexing, Property 3: HasValidCell consistency**
    /// 
    /// For any clipped Voronoi diagram and generator index i, HasValidCell(i) returns true
    /// if and only if TryGetCell(i, out _) returns true and the cell is not in DegenerateCells.
    /// 
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property HasValidCell_Consistency()
    {
        var pointSetGen = ArbMap.Default.GeneratorFor<int>()
            .Where(x => x >= 3 && x <= 20)
            .SelectMany(count =>
                ArbMap.Default.GeneratorFor<int>()
                    .Where(x => x >= 0 && x <= 10000)
                    .Select(seed => GenerateUniquePoints(count, seed)));

        var domainScaleGen = ArbMap.Default.GeneratorFor<int>()
            .Where(x => x >= 1 && x <= 5)
            .Select(x => (double)x * 0.5); // Smaller domains to potentially create degenerate/outside cases

        return Prop.ForAll(
            pointSetGen.ToArbitrary(),
            domainScaleGen.ToArbitrary(),
            (points, domainScale) =>
            {
                if (points.Count < 3)
                    return true;

                // Build triangulation and track all inserted indices
                var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
                var allIndices = new List<int>();

                foreach (var point in points)
                {
                    var handle = triangulation.Insert(point);
                    allIndices.Add(handle.Index);
                }

                // Create domain
                var domain = CreateDomainFromPoints(points, domainScale);

                // Clip the Voronoi diagram
                var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

                // Test consistency for all generator indices that were inserted
                foreach (var index in allIndices)
                {
                    var hasValid = diagram.HasValidCell(index);
                    var tryGetResult = diagram.TryGetCell(index, out var cell);
                    var isDegenerate = diagram.DegenerateCells.Contains(index);

                    // Property: HasValidCell(i) == TryGetCell(i, out _) returns true
                    if (hasValid != tryGetResult)
                        return false;

                    // Property: If HasValidCell is true, the cell should NOT be in DegenerateCells
                    if (hasValid && isDegenerate)
                        return false;

                    // Property: If TryGetCell returns true, cell should not be null
                    if (tryGetResult && cell == null)
                        return false;

                    // Property: If TryGetCell returns false, cell should be null
                    if (!tryGetResult && cell != null)
                        return false;
                }

                // Also test some invalid indices (negative and out of range)
                var invalidIndices = new[] { -1, -100, allIndices.Max() + 1, allIndices.Max() + 100 };
                foreach (var invalidIndex in invalidIndices)
                {
                    var hasValid = diagram.HasValidCell(invalidIndex);
                    var tryGetResult = diagram.TryGetCell(invalidIndex, out var cell);

                    // Invalid indices should return false for both methods
                    if (hasValid)
                        return false;
                    if (tryGetResult)
                        return false;
                    if (cell != null)
                        return false;
                }

                return true;
            });
    }

    /// <summary>
    /// **Feature: deterministic-indexing, Property 6: TryGetCell correctness**
    /// 
    /// For any clipped Voronoi diagram:
    /// - If generatorIndex is valid and has a non-degenerate cell, TryGetCell returns true with the correct cell
    /// - If generatorIndex is invalid (negative or >= numVertices) or degenerate, TryGetCell returns false without throwing
    /// 
    /// **Validates: Requirements 5.1, 5.2, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TryGetCell_Correctness()
    {
        var pointSetGen = ArbMap.Default.GeneratorFor<int>()
            .Where(x => x >= 3 && x <= 20)
            .SelectMany(count =>
                ArbMap.Default.GeneratorFor<int>()
                    .Where(x => x >= 0 && x <= 10000)
                    .Select(seed => GenerateUniquePoints(count, seed)));

        var domainScaleGen = ArbMap.Default.GeneratorFor<int>()
            .Where(x => x >= 1 && x <= 5)
            .Select(x => (double)x * 0.5); // Smaller domains to potentially create degenerate/outside cases

        return Prop.ForAll(
            pointSetGen.ToArbitrary(),
            domainScaleGen.ToArbitrary(),
            (points, domainScale) =>
            {
                if (points.Count < 3)
                    return true;

                // Build triangulation and track all inserted indices with their positions
                var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
                var insertedPoints = new Dictionary<int, Point2<double>>();

                foreach (var point in points)
                {
                    var handle = triangulation.Insert(point);
                    insertedPoints[handle.Index] = point;
                }

                // Create domain
                var domain = CreateDomainFromPoints(points, domainScale);

                // Clip the Voronoi diagram
                var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

                // Build a set of valid cell indices for quick lookup
                var validCellIndices = new HashSet<int>(diagram.Cells.Select(c => c.GeneratorIndex));

                // Requirement 5.1: Valid generator index returns the corresponding cell
                foreach (var cell in diagram.Cells)
                {
                    var generatorIndex = cell.GeneratorIndex;
                    var tryGetResult = diagram.TryGetCell(generatorIndex, out var retrievedCell);

                    // TryGetCell should return true for valid cells
                    if (!tryGetResult)
                        return false;

                    // Retrieved cell should not be null
                    if (retrievedCell == null)
                        return false;

                    // Retrieved cell should be the same cell (same generator index)
                    if (retrievedCell.GeneratorIndex != generatorIndex)
                        return false;

                    // Retrieved cell's generator should match the original point
                    if (insertedPoints.TryGetValue(generatorIndex, out var expectedPosition))
                    {
                        if (Math.Abs(retrievedCell.Generator.X - expectedPosition.X) > 1e-12 ||
                            Math.Abs(retrievedCell.Generator.Y - expectedPosition.Y) > 1e-12)
                            return false;
                    }

                    // Indexer should return the same cell
                    var indexerCell = diagram[generatorIndex];
                    if (indexerCell == null || indexerCell.GeneratorIndex != generatorIndex)
                        return false;
                }

                // Requirement 5.2: Invalid generator index returns false without throwing
                var invalidIndices = new[] { -1, -100, int.MinValue, insertedPoints.Keys.Max() + 1, insertedPoints.Keys.Max() + 100, int.MaxValue };
                foreach (var invalidIndex in invalidIndices)
                {
                    try
                    {
                        var tryGetResult = diagram.TryGetCell(invalidIndex, out var cell);

                        // Should return false for invalid indices
                        if (tryGetResult)
                            return false;

                        // Cell should be null for invalid indices
                        if (cell != null)
                            return false;

                        // Indexer should also return null for invalid indices
                        var indexerCell = diagram[invalidIndex];
                        if (indexerCell != null)
                            return false;
                    }
                    catch
                    {
                        // Should not throw exceptions for invalid indices
                        return false;
                    }
                }

                // Requirement 5.3: Degenerate cell indices return false
                foreach (var degenerateIndex in diagram.DegenerateCells)
                {
                    var tryGetResult = diagram.TryGetCell(degenerateIndex, out var cell);

                    // Should return false for degenerate cells
                    if (tryGetResult)
                        return false;

                    // Cell should be null for degenerate cells
                    if (cell != null)
                        return false;

                    // Indexer should also return null for degenerate cells
                    var indexerCell = diagram[degenerateIndex];
                    if (indexerCell != null)
                        return false;
                }

                // Also verify OutsideDomain indices return false
                foreach (var outsideIndex in diagram.OutsideDomain)
                {
                    var tryGetResult = diagram.TryGetCell(outsideIndex, out var cell);

                    // Should return false for outside domain cells
                    if (tryGetResult)
                        return false;

                    // Cell should be null for outside domain cells
                    if (cell != null)
                        return false;
                }

                return true;
            });
    }

    /// <summary>
    /// **Feature: deterministic-indexing, Property 4: ClipToPolygon idempotence**
    /// 
    /// For any triangulation and clipping domain, calling ClipToPolygon twice with identical
    /// inputs produces identical results: same cell count, same cell polygons (within epsilon),
    /// same ordering, and same diagnostic collections.
    /// 
    /// **Validates: Requirements 3.1, 3.2, 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ClipToPolygon_Idempotence()
    {
        var pointSetGen = ArbMap.Default.GeneratorFor<int>()
            .Where(x => x >= 3 && x <= 20)
            .SelectMany(count =>
                ArbMap.Default.GeneratorFor<int>()
                    .Where(x => x >= 0 && x <= 10000)
                    .Select(seed => GenerateUniquePoints(count, seed)));

        var domainScaleGen = ArbMap.Default.GeneratorFor<int>()
            .Where(x => x >= 1 && x <= 5)
            .Select(x => (double)x);

        return Prop.ForAll(
            pointSetGen.ToArbitrary(),
            domainScaleGen.ToArbitrary(),
            (points, domainScale) =>
            {
                if (points.Count < 3)
                    return true;

                // Build triangulation
                var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

                foreach (var point in points)
                {
                    triangulation.Insert(point);
                }

                // Create domain
                var domain = CreateDomainFromPoints(points, domainScale);

                // Call ClipToPolygon twice with identical inputs
                var diagram1 = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);
                var diagram2 = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

                // Requirement 3.2: Same cell count and ordering
                if (diagram1.Cells.Count != diagram2.Cells.Count)
                    return false;

                // Requirement 3.1: Same cell polygons (within epsilon)
                const double epsilon = 1e-12;
                for (var i = 0; i < diagram1.Cells.Count; i++)
                {
                    var cell1 = diagram1.Cells[i];
                    var cell2 = diagram2.Cells[i];

                    // Same generator index
                    if (cell1.GeneratorIndex != cell2.GeneratorIndex)
                        return false;

                    // Same polygon vertex count
                    if (cell1.Polygon.Count != cell2.Polygon.Count)
                        return false;

                    // Same polygon vertices (within epsilon)
                    for (var j = 0; j < cell1.Polygon.Count; j++)
                    {
                        var v1 = cell1.Polygon[j];
                        var v2 = cell2.Polygon[j];

                        if (Math.Abs(v1.X - v2.X) > epsilon || Math.Abs(v1.Y - v2.Y) > epsilon)
                            return false;
                    }

                    // Same IsClipped flag
                    if (cell1.IsClipped != cell2.IsClipped)
                        return false;
                }

                // Requirement 3.3: Same diagnostic collections
                if (diagram1.DegenerateCells.Count != diagram2.DegenerateCells.Count)
                    return false;

                for (var i = 0; i < diagram1.DegenerateCells.Count; i++)
                {
                    if (diagram1.DegenerateCells[i] != diagram2.DegenerateCells[i])
                        return false;
                }

                if (diagram1.OutsideDomain.Count != diagram2.OutsideDomain.Count)
                    return false;

                for (var i = 0; i < diagram1.OutsideDomain.Count; i++)
                {
                    if (diagram1.OutsideDomain[i] != diagram2.OutsideDomain[i])
                        return false;
                }

                return true;
            });
    }

    /// <summary>
    /// **Feature: deterministic-indexing, Property 5: Orient2D consistency**
    /// 
    /// For any three points (including near-collinear configurations), repeated calls to
    /// RobustPredicates.Orient2D with the same inputs return the same sign.
    /// 
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Orient2D_Consistency()
    {
        // Generate three points, including near-collinear configurations
        var pointGen = ArbMap.Default.GeneratorFor<double>()
            .Where(x => !double.IsNaN(x) && !double.IsInfinity(x) && Math.Abs(x) < 1e10);

        var point2Gen = from x in pointGen
                        from y in pointGen
                        select new Point2<double>(x, y);

        var threePointsGen = from p1 in point2Gen
                             from p2 in point2Gen
                             from p3 in point2Gen
                             select (p1, p2, p3);

        // Also generate near-collinear points to stress test
        var nearCollinearGen = ArbMap.Default.GeneratorFor<int>()
            .Where(x => x >= 0 && x <= 10000)
            .Select(seed =>
            {
                var rng = new Random(seed);
                var p1 = new Point2<double>(rng.NextDouble() * 1e6, rng.NextDouble() * 1e6);
                var p2 = new Point2<double>(rng.NextDouble() * 1e6, rng.NextDouble() * 1e6);
                
                // Create a point very close to the line p1-p2
                var t = rng.NextDouble();
                var onLine = new Point2<double>(
                    p1.X + t * (p2.X - p1.X),
                    p1.Y + t * (p2.Y - p1.Y));
                
                // Add a tiny perturbation
                var perturbation = (rng.NextDouble() - 0.5) * 1e-10;
                var p3 = new Point2<double>(onLine.X + perturbation, onLine.Y + perturbation);
                
                return (p1, p2, p3);
            });

        // Test with random points
        var randomPointsProperty = Prop.ForAll(
            threePointsGen.ToArbitrary(),
            points =>
            {
                var (p1, p2, p3) = points;

                // Call Orient2D multiple times with the same inputs
                var result1 = RobustPredicates.Orient2D(p1, p2, p3);
                var result2 = RobustPredicates.Orient2D(p1, p2, p3);
                var result3 = RobustPredicates.Orient2D(p1, p2, p3);

                // All results should be identical (same value, not just same sign)
                if (result1 != result2 || result2 != result3)
                    return false;

                // The sign should be consistent
                var sign1 = Math.Sign(result1);
                var sign2 = Math.Sign(result2);
                var sign3 = Math.Sign(result3);

                return sign1 == sign2 && sign2 == sign3;
            });

        // Test with near-collinear points
        var nearCollinearProperty = Prop.ForAll(
            nearCollinearGen.ToArbitrary(),
            points =>
            {
                var (p1, p2, p3) = points;

                // Call Orient2D multiple times with the same inputs
                var result1 = RobustPredicates.Orient2D(p1, p2, p3);
                var result2 = RobustPredicates.Orient2D(p1, p2, p3);
                var result3 = RobustPredicates.Orient2D(p1, p2, p3);

                // All results should be identical
                if (result1 != result2 || result2 != result3)
                    return false;

                // The sign should be consistent
                var sign1 = Math.Sign(result1);
                var sign2 = Math.Sign(result2);
                var sign3 = Math.Sign(result3);

                return sign1 == sign2 && sign2 == sign3;
            });

        return randomPointsProperty.And(nearCollinearProperty);
    }

    /// <summary>
    /// Creates a rectangular domain that encompasses all points with some margin.
    /// </summary>
    private static ClipPolygon CreateDomainFromPoints(List<Point2<double>> points, double scale)
    {
        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);

        var width = maxX - minX;
        var height = maxY - minY;
        var margin = Math.Max(width, height) * scale;

        return new ClipPolygon(new[]
        {
            new Point2<double>(minX - margin, minY - margin),
            new Point2<double>(maxX + margin, minY - margin),
            new Point2<double>(maxX + margin, maxY + margin),
            new Point2<double>(minX - margin, maxY + margin),
        });
    }
}
