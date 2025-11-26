using System;
using System.Collections.Generic;
using Spade.DCEL;
using Spade.Handles;
using Spade.Primitives;

namespace Spade;

public static class HullCenterExtensions
{
    public static DelaunayTriangulation<V, DE, UE, F, L> BuildHullCenterStarCopy<V, DE, UE, F, L>(
        this DelaunayTriangulation<V, DE, UE, F, L> source)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        if (!source.TryGetHullCenterPattern(out var center, out var hullRing))
        {
            var clone = new DelaunayTriangulation<V, DE, UE, F, L>();

            for (int i = 0; i < source.NumVertices; i++)
            {
                var handle = new FixedVertexHandle(i);
                var vertex = source.Vertex(handle).Data;
                clone.Insert(vertex);
            }

            return clone;
        }

        var starDcel = BuildHullCenterStarDcel(source, center, hullRing);

        var result = new DelaunayTriangulation<V, DE, UE, F, L>();
        result._dcel = starDcel;
        result._hintGenerator = new L();

        return result;
    }

    private static Dcel<V, DE, UE, F> BuildHullCenterStarDcel<V, DE, UE, F, L>(
        DelaunayTriangulation<V, DE, UE, F, L> source,
        FixedVertexHandle center,
        List<FixedVertexHandle> hullRing)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        var dcel = new Dcel<V, DE, UE, F>();

        for (int i = 0; i < source.NumVertices; i++)
        {
            var handle = new FixedVertexHandle(i);
            var vertex = source.Vertex(handle).Data;
            DcelOperations.AppendUnconnectedVertex(dcel, vertex);
        }

        var hullCount = hullRing.Count;
        if (hullCount < 3)
        {
            throw new ArgumentException("Hull ring must contain at least three vertices.", nameof(hullRing));
        }

        for (int i = 0; i < hullCount; i++)
        {
            dcel.Faces.Add(new FaceEntry<F>
            {
                AdjacentEdge = null,
                Data = new F(),
                Kind = FaceKind.Inner
            });
        }

        var innerFaces = new FixedFaceHandle[hullCount];
        for (int i = 0; i < hullCount; i++)
        {
            innerFaces[i] = new FixedFaceHandle(i + 1);
        }

        var outerFace = new FixedFaceHandle(0);

        FixedDirectedEdgeHandle HullInner(int i) => new FixedDirectedEdgeHandle(i * 2);
        FixedDirectedEdgeHandle HullOuter(int i) => new FixedDirectedEdgeHandle(i * 2 + 1);
        FixedDirectedEdgeHandle RadCtoH(int j) => new FixedDirectedEdgeHandle((hullCount + j) * 2);
        FixedDirectedEdgeHandle RadHtoC(int j) => new FixedDirectedEdgeHandle((hullCount + j) * 2 + 1);

        for (int i = 0; i < hullCount; i++)
        {
            var from = hullRing[i];
            var to = hullRing[(i + 1) % hullCount];

            var face = innerFaces[i];

            var inner = new HalfEdgeEntry
            {
                Origin = from,
                Face = face,
                // For each inner triangle face, we want the ring
                //   center -> hull[i] -> hull[i+1] -> center
                // in CCW order. That implies:
                //   RadCtoH(i).Next = HullInner(i)
                //   HullInner(i).Next = RadHtoC(i+1)
                //   RadHtoC(i+1).Next = RadCtoH(i)
                // so Prev must be the opposite direction.
                Prev = RadCtoH(i),
                Next = RadHtoC((i + 1) % hullCount)
            };

            var outer = new HalfEdgeEntry
            {
                Origin = to,
                Face = outerFace,
                Next = HullOuter((i - 1 + hullCount) % hullCount),
                Prev = HullOuter((i + 1) % hullCount)
            };

            dcel.Edges.Add(new EdgeEntry<DE, UE>(inner, outer));
        }

        for (int j = 0; j < hullCount; j++)
        {
            var faceForward = innerFaces[j];
            var faceBackward = innerFaces[(j - 1 + hullCount) % hullCount];

            var cToH = new HalfEdgeEntry
            {
                Origin = center,
                Face = faceForward,
                Next = HullInner(j),
                Prev = RadHtoC((j + 1) % hullCount)
            };

            var hToC = new HalfEdgeEntry
            {
                Origin = hullRing[j],
                Face = faceBackward,
                Next = RadCtoH((j - 1 + hullCount) % hullCount),
                Prev = HullInner((j - 1 + hullCount) % hullCount)
            };

            dcel.Edges.Add(new EdgeEntry<DE, UE>(cToH, hToC));
        }

        var outerFaceEntry = dcel.Faces[0];
        outerFaceEntry.AdjacentEdge = HullOuter(0);
        dcel.Faces[0] = outerFaceEntry;

        for (int i = 0; i < hullCount; i++)
        {
            var faceEntry = dcel.Faces[i + 1];
            faceEntry.AdjacentEdge = RadCtoH(i);
            dcel.Faces[i + 1] = faceEntry;
        }

        var centerVertex = dcel.Vertices[center.Index];
        centerVertex.OutEdge = RadCtoH(0);
        dcel.Vertices[center.Index] = centerVertex;

        for (int i = 0; i < hullCount; i++)
        {
            var hv = hullRing[i];
            var vertexEntry = dcel.Vertices[hv.Index];
            vertexEntry.OutEdge = HullInner(i);
            dcel.Vertices[hv.Index] = vertexEntry;
        }

        return dcel;
    }
}
