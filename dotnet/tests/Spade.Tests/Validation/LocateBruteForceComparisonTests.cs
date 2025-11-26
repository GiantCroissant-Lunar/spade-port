using FluentAssertions;
using Spade;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Validation;

public class LocateBruteForceComparisonTests
{
    private static DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> CreateSimpleQuad()
    {
        var d = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        d.Insert(new Point2<double>(0.0, 0.0));
        d.Insert(new Point2<double>(1.0, 0.0));
        d.Insert(new Point2<double>(0.0, 1.0));
        d.Insert(new Point2<double>(1.0, 1.0));
        return d;
    }

    [Fact]
    public void LocateWithHint_AgreesWithBruteForce_OnFaceCenters_SimpleQuad()
    {
        var triangulation = CreateSimpleQuad();

        foreach (var face in triangulation.InnerFaces())
        {
            var edge = face.AdjacentEdge();
            if (edge is null)
            {
                continue;
            }

            var e0 = edge.Value;
            var v0 = e0.From().Data.Position;
            var v1 = e0.To().Data.Position;
            var v2 = e0.Next().To().Data.Position;

            var center = new Point2<double>(
                (v0.X + v1.X + v2.X) / 3.0,
                (v0.Y + v1.Y + v2.Y) / 3.0);

            var brute = TriangulationLocateOracle.BruteForceLocate(triangulation, center);
            var located = triangulation.LocateWithHintOptionCore(center, null);

            brute.Should().BeOfType<PositionInTriangulation.OnFace>();
            located.Should().BeOfType<PositionInTriangulation.OnFace>();

            var bruteFace = (PositionInTriangulation.OnFace)brute;
            var locatedFace = (PositionInTriangulation.OnFace)located;

            locatedFace.Face.Should().Be(bruteFace.Face);
        }
    }

    [Fact]
    public void LocateWithHint_AgreesWithBruteForce_OnFaceCenters_DeterministicRandomPoints()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        var rng = new Random(123456);
        for (int i = 0; i < 100; i++)
        {
            var x = rng.NextDouble();
            var y = rng.NextDouble();
            triangulation.Insert(new Point2<double>(x, y));
        }

        foreach (var face in triangulation.InnerFaces())
        {
            var edge = face.AdjacentEdge();
            if (edge is null)
            {
                continue;
            }

            var e0 = edge.Value;
            var v0 = e0.From().Data.Position;
            var v1 = e0.To().Data.Position;
            var v2 = e0.Next().To().Data.Position;

            var center = new Point2<double>(
                (v0.X + v1.X + v2.X) / 3.0,
                (v0.Y + v1.Y + v2.Y) / 3.0);

            var brute = TriangulationLocateOracle.BruteForceLocate(triangulation, center);
            var located = triangulation.LocateWithHintOptionCore(center, null);

            if (brute is not PositionInTriangulation.OnFace bruteFace)
            {
                throw new InvalidOperationException(
                    $"BruteForceLocate did not return OnFace at center=({center.X}, {center.Y}); got {brute.GetType().Name}.");
            }

            if (located is not PositionInTriangulation.OnFace locatedFace)
            {
                throw new InvalidOperationException(
                    $"LocateWithHint did not return OnFace at center=({center.X}, {center.Y}); got {located.GetType().Name}.");
            }

            if (!locatedFace.Face.Equals(bruteFace.Face))
            {
                throw new InvalidOperationException(
                    $"Locate mismatch at center=({center.X}, {center.Y}): " +
                    $"bruteFace={bruteFace.Face.Index}, locatedFace={locatedFace.Face.Index}");
            }
        }
    }

    [Fact]
    public void LocateWithHint_KnownRegressionPoint_MatchesBruteForce()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        var rng = new Random(123456);
        for (int i = 0; i < 100; i++)
        {
            var x = rng.NextDouble();
            var y = rng.NextDouble();
            triangulation.Insert(new Point2<double>(x, y));
        }

        var center = new Point2<double>(
            0.1629800727418531,
            0.3532573947154874);

        var brute = TriangulationLocateOracle.BruteForceLocate(triangulation, center);
        var located = triangulation.LocateWithHintOptionCore(center, null);

        brute.Should().BeOfType<PositionInTriangulation.OnFace>();
        located.Should().BeOfType<PositionInTriangulation.OnFace>();

        var bruteFace = (PositionInTriangulation.OnFace)brute;
        var locatedFace = (PositionInTriangulation.OnFace)located;

        locatedFace.Face.Should().Be(bruteFace.Face);
    }
}
