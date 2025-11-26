using Spade.Handles;
using Spade.Primitives;

namespace Spade.DCEL;

internal static class DcelOperations
{
    public static FixedVertexHandle AppendUnconnectedVertex<V, DE, UE, F>(
        Dcel<V, DE, UE, F> dcel,
        V vertex)
    {
        var result = new FixedVertexHandle(dcel.Vertices.Count);
        dcel.Vertices.Add(new VertexEntry<V>
        {
            Data = vertex,
            OutEdge = null
        });
        return result;
    }

    public static void SetupInitialTwoVertices<V, DE, UE, F>(
        Dcel<V, DE, UE, F> dcel,
        FixedVertexHandle firstVertex,
        FixedVertexHandle secondVertex)
        where DE : new()
        where UE : new()
        where F : new()
    {
        var normalized = new FixedDirectedEdgeHandle(0);
        var notNormalized = normalized.Rev();

        dcel.Edges.Add(new EdgeEntry<DE, UE>(
            new HalfEdgeEntry
            {
                Next = notNormalized,
                Prev = notNormalized,
                Face = new FixedFaceHandle(0), // Outer face
                Origin = firstVertex
            },
            new HalfEdgeEntry
            {
                Next = normalized,
                Prev = normalized,
                Face = new FixedFaceHandle(0), // Outer face
                Origin = secondVertex
            }
        ));

        // Update vertices
        var v1 = dcel.Vertices[firstVertex.Index];
        v1.OutEdge = normalized;
        dcel.Vertices[firstVertex.Index] = v1;

        var v2 = dcel.Vertices[secondVertex.Index];
        v2.OutEdge = notNormalized;
        dcel.Vertices[secondVertex.Index] = v2;

        // Update outer face
        var face = dcel.Faces[0];
        face.AdjacentEdge = normalized;
        dcel.Faces[0] = face;
    }

    public static FixedVertexHandle InsertIntoTriangle<V, DE, UE, F>(
        Dcel<V, DE, UE, F> dcel,
        FixedVertexHandle v,
        FixedFaceHandle f0)
        where DE : new()
        where UE : new()
        where F : new()
    {
        // Get adjacent edge of the face
        var e0 = dcel.Faces[f0.Index].AdjacentEdge ?? throw new InvalidOperationException("Face without adjacent edge");

        // Get edges of the triangle
        var e0Entry = dcel.GetHalfEdge(e0);
        var e1 = e0Entry.Next;
        var e1Entry = dcel.GetHalfEdge(e1);
        var e2 = e1Entry.Next;
        var e2Entry = dcel.GetHalfEdge(e2);

        // Create new edges
        var e3 = new FixedDirectedEdgeHandle(dcel.Edges.Count * 2);
        var e4 = e3.Rev();
        var e5 = new FixedDirectedEdgeHandle(dcel.Edges.Count * 2 + 2);
        var e6 = e5.Rev();
        var e7 = new FixedDirectedEdgeHandle(dcel.Edges.Count * 2 + 4);
        var e8 = e7.Rev();

        var v0 = e0Entry.Origin;
        var v1 = e1Entry.Origin;
        var v2 = e2Entry.Origin;

        var f1 = new FixedFaceHandle(dcel.Faces.Count);
        var f2 = new FixedFaceHandle(dcel.Faces.Count + 1);

        // Create new faces
        dcel.Faces.Add(new FaceEntry<F> { AdjacentEdge = e1, Data = new F(), Kind = FaceKind.Inner });
        dcel.Faces.Add(new FaceEntry<F> { AdjacentEdge = e2, Data = new F(), Kind = FaceKind.Inner });

        // Update vertex v
        var vertexV = dcel.Vertices[v.Index];
        vertexV.OutEdge = e4;
        dcel.Vertices[v.Index] = vertexV;

        // Update existing edges
        dcel.UpdateHalfEdge(e0, he => { he.Prev = e8; he.Next = e3; return he; });
        dcel.UpdateHalfEdge(e1, he => { he.Prev = e4; he.Next = e5; he.Face = f1; return he; });
        dcel.UpdateHalfEdge(e2, he => { he.Prev = e6; he.Next = e7; he.Face = f2; return he; });

        // Create new edges
        var edge3 = new HalfEdgeEntry { Next = e8, Prev = e0, Origin = v1, Face = f0 };
        var edge4 = new HalfEdgeEntry { Next = e1, Prev = e5, Origin = v, Face = f1 };
        
        var edge5 = new HalfEdgeEntry { Next = e4, Prev = e1, Origin = v2, Face = f1 };
        var edge6 = new HalfEdgeEntry { Next = e2, Prev = e7, Origin = v, Face = f2 };

        var edge7 = new HalfEdgeEntry { Next = e6, Prev = e2, Origin = v0, Face = f2 };
        var edge8 = new HalfEdgeEntry { Next = e0, Prev = e3, Origin = v, Face = f0 };

        dcel.Edges.Add(new EdgeEntry<DE, UE>(edge3, edge4));
        dcel.Edges.Add(new EdgeEntry<DE, UE>(edge5, edge6));
        dcel.Edges.Add(new EdgeEntry<DE, UE>(edge7, edge8));

        return v;
    }

    public static FixedDirectedEdgeHandle[] SplitHalfEdge<V, DE, UE, F>(
        Dcel<V, DE, UE, F> dcel,
        FixedDirectedEdgeHandle edgeHandle,
        FixedVertexHandle newVertexHandle)
        where DE : new()
        where UE : new()
        where F : new()
    {
        var edgeEntry = dcel.GetHalfEdge(edgeHandle);
        var v = dcel.GetHalfEdge(edgeEntry.Prev).Origin;
        var to = dcel.GetHalfEdge(edgeEntry.Next).Origin;
        
        var edgeNext = edgeEntry.Next;
        var edgePrev = edgeEntry.Prev;
        
        var edgeTwin = edgeHandle.Rev();
        var edgeTwinEntry = dcel.GetHalfEdge(edgeTwin);
        var edgeTwinPrev = edgeTwinEntry.Prev;
        var edgeTwinFace = edgeTwinEntry.Face;
        
        var f1 = edgeEntry.Face;
        var nf = new FixedFaceHandle(dcel.Faces.Count);
        
        var e1 = new FixedDirectedEdgeHandle(dcel.Edges.Count * 2);
        var t1 = e1.Rev();
        var e2 = new FixedDirectedEdgeHandle(dcel.Edges.Count * 2 + 2);
        var t2 = e2.Rev();
        
        var edge1 = new HalfEdgeEntry { Next = e2, Prev = edgeNext, Origin = v, Face = nf };
        var twin1 = new HalfEdgeEntry { Next = edgePrev, Prev = edgeHandle, Face = f1, Origin = newVertexHandle };
        
        var edge2 = new HalfEdgeEntry { Next = edgeNext, Prev = e1, Origin = newVertexHandle, Face = nf };
        var twin2 = new HalfEdgeEntry { Next = edgeTwin, Prev = edgeTwinPrev, Face = edgeTwinFace, Origin = to };
        
        var newFace = new FaceEntry<F> { AdjacentEdge = e2, Data = new F(), Kind = FaceKind.Inner };
        
        dcel.Edges.Add(new EdgeEntry<DE, UE>(edge1, twin1));
        dcel.Edges.Add(new EdgeEntry<DE, UE>(edge2, twin2));
        dcel.Faces.Add(newFace);
        
        var vertexNew = dcel.Vertices[newVertexHandle.Index];
        vertexNew.OutEdge = e2;
        dcel.Vertices[newVertexHandle.Index] = vertexNew;
        
        dcel.UpdateHalfEdge(edgeTwinPrev, he => { he.Next = t2; return he; });
        
        dcel.UpdateHalfEdge(edgeNext, he => { he.Prev = e2; he.Next = e1; he.Face = nf; return he; });
        dcel.UpdateHalfEdge(edgePrev, he => { he.Prev = t1; return he; });
        dcel.UpdateHalfEdge(edgeTwin, he => { he.Prev = t2; he.Origin = newVertexHandle; return he; });
        dcel.UpdateHalfEdge(edgeHandle, he => { he.Next = t1; return he; });
        
        var vertexTo = dcel.Vertices[to.Index];
        vertexTo.OutEdge = e2.Rev();
        dcel.Vertices[to.Index] = vertexTo;
        
        var face1 = dcel.Faces[f1.Index];
        face1.AdjacentEdge = edgeHandle;
        dcel.Faces[f1.Index] = face1;
        
        return new[] { edgeHandle, e2 };
    }

    public static FixedDirectedEdgeHandle[] SplitEdge<V, DE, UE, F>(
        Dcel<V, DE, UE, F> dcel,
        FixedDirectedEdgeHandle edgeHandle,
        FixedVertexHandle newVertexHandle)
        where DE : new()
        where UE : new()
        where F : new()
    {
        var edge = dcel.GetHalfEdge(edgeHandle);
        var twin = dcel.GetHalfEdge(edgeHandle.Rev());
        
        var f0 = edge.Face;
        var f1 = twin.Face;
        var f2 = new FixedFaceHandle(dcel.Faces.Count);
        var f3 = new FixedFaceHandle(dcel.Faces.Count + 1);
        
        var e0 = edgeHandle;
        var t0 = e0.Rev();
        var e1 = new FixedDirectedEdgeHandle(dcel.Edges.Count * 2);
        var t1 = e1.Rev();
        var e2 = new FixedDirectedEdgeHandle(dcel.Edges.Count * 2 + 2);
        var t2 = e2.Rev();
        var e3 = new FixedDirectedEdgeHandle(dcel.Edges.Count * 2 + 4);
        var t3 = e3.Rev();
        
        var ep = edge.Prev;
        var en = edge.Next;
        var tn = twin.Next;
        var tp = twin.Prev;
        
        var v0 = newVertexHandle;
        var v1 = edge.Origin;
        var v2 = dcel.GetHalfEdge(tp).Origin;
        var v3 = twin.Origin;
        var v4 = dcel.GetHalfEdge(ep).Origin;
        
        var edge0 = new HalfEdgeEntry { Next = t3, Prev = ep, Origin = v1, Face = f0 };
        var twin0 = new HalfEdgeEntry { Next = tn, Prev = e1, Origin = v0, Face = f1 };
        
        var edge1 = new HalfEdgeEntry { Next = t0, Prev = tn, Origin = v2, Face = f1 };
        var twin1 = new HalfEdgeEntry { Next = tp, Prev = e2, Origin = v0, Face = f2 };
        
        var edge2 = new HalfEdgeEntry { Next = t1, Prev = tp, Origin = v3, Face = f2 };
        var twin2 = new HalfEdgeEntry { Next = en, Prev = e3, Origin = v0, Face = f3 };
        
        var edge3 = new HalfEdgeEntry { Next = t2, Prev = en, Origin = v4, Face = f3 };
        var twin3 = new HalfEdgeEntry { Next = ep, Prev = e0, Origin = v0, Face = f0 };
        
        var face2 = new FaceEntry<F> { AdjacentEdge = e2, Data = new F(), Kind = FaceKind.Inner };
        var face3 = new FaceEntry<F> { AdjacentEdge = e3, Data = new F(), Kind = FaceKind.Inner };
        
        dcel.UpdateHalfEdge(e0, _ => edge0);
        dcel.UpdateHalfEdge(t0, _ => twin0);
        
        dcel.Edges.Add(new EdgeEntry<DE, UE>(edge1, twin1));
        dcel.Edges.Add(new EdgeEntry<DE, UE>(edge2, twin2));
        dcel.Edges.Add(new EdgeEntry<DE, UE>(edge3, twin3));
        
        dcel.UpdateHalfEdge(en, he => { he.Next = e3; he.Prev = t2; he.Face = f3; return he; });
        dcel.UpdateHalfEdge(tp, he => { he.Next = e2; he.Prev = t1; he.Face = f2; return he; });
        dcel.UpdateHalfEdge(tn, he => { he.Next = e1; return he; });
        dcel.UpdateHalfEdge(ep, he => { he.Prev = t3; return he; });
        
        var vertexNew = dcel.Vertices[newVertexHandle.Index];
        vertexNew.OutEdge = t0;
        dcel.Vertices[newVertexHandle.Index] = vertexNew;
        
        var vertexV3 = dcel.Vertices[v3.Index];
        vertexV3.OutEdge = e2;
        dcel.Vertices[v3.Index] = vertexV3;
        
        var face0 = dcel.Faces[f0.Index];
        face0.AdjacentEdge = e0;
        dcel.Faces[f0.Index] = face0;
        
        var face1 = dcel.Faces[f1.Index];
        face1.AdjacentEdge = e1;
        dcel.Faces[f1.Index] = face1;
        
        dcel.Faces.Add(face2);
        dcel.Faces.Add(face3);
        
        return new[] { e0, e2.Rev() };
    }

    public static FixedDirectedEdgeHandle[] SplitEdgeWhenAllVerticesOnLine<V, DE, UE, F>(
        Dcel<V, DE, UE, F> dcel,
        FixedDirectedEdgeHandle edgeHandle,
        FixedVertexHandle newVertexHandle)
        where DE : new()
        where UE : new()
        where F : new()
    {
        var edge = edgeHandle;
        var rev = edge.Rev();
        
        var newEdge = new FixedDirectedEdgeHandle(dcel.Edges.Count * 2);
        var newEdgeRev = newEdge.Rev();
        
        var edgeNext = dcel.GetHalfEdge(edge).Next;
        var revPrev = dcel.GetHalfEdge(rev).Prev;
        
        var toVertex = dcel.GetHalfEdge(edgeNext).Origin;
        
        var face = dcel.GetHalfEdge(edge).Face;
        
        var isIsolated = edgeNext == rev;
        
        dcel.UpdateHalfEdge(edge, he => { he.Next = newEdge; return he; });
        dcel.UpdateHalfEdge(rev, he => { he.Prev = newEdgeRev; he.Origin = newVertexHandle; return he; });
        
        var vertexTo = dcel.Vertices[toVertex.Index];
        vertexTo.OutEdge = newEdgeRev;
        dcel.Vertices[toVertex.Index] = vertexTo;
        
        FixedDirectedEdgeHandle newEdgeNext, newRevPrev;
        
        if (isIsolated)
        {
            newEdgeNext = newEdgeRev;
            newRevPrev = newEdge;
        }
        else
        {
            dcel.UpdateHalfEdge(edgeNext, he => { he.Prev = newEdge; return he; });
            dcel.UpdateHalfEdge(revPrev, he => { he.Next = newEdgeRev; return he; });
            newEdgeNext = edgeNext;
            newRevPrev = revPrev;
        }
        
        dcel.Edges.Add(new EdgeEntry<DE, UE>(
            new HalfEdgeEntry { Next = newEdgeNext, Prev = edge, Face = face, Origin = newVertexHandle },
            new HalfEdgeEntry { Next = rev, Prev = newRevPrev, Face = face, Origin = toVertex }
        ));
        
        var vertexNew = dcel.Vertices[newVertexHandle.Index];
        vertexNew.OutEdge = newEdge;
        dcel.Vertices[newVertexHandle.Index] = vertexNew;
        
        return new[] { edge, newEdge };
    }

    public static FixedVertexHandle ExtendLine<V, DE, UE, F>(
        Dcel<V, DE, UE, F> dcel,
        FixedVertexHandle endVertex,
        FixedVertexHandle newVertexHandle)
        where DE : new()
        where UE : new()
        where F : new()
    {
        var endVertexEntry = dcel.Vertices[endVertex.Index];
        var outEdge = endVertexEntry.OutEdge ?? throw new InvalidOperationException("End vertex must not be isolated");
        var inEdge = outEdge.Rev();
        
        var newEdge = new FixedDirectedEdgeHandle(dcel.Edges.Count * 2);
        var newEdgeRev = newEdge.Rev();
        
        var face = dcel.GetHalfEdge(outEdge).Face;
        
        dcel.UpdateHalfEdge(outEdge, he => { he.Prev = newEdge; return he; });
        dcel.UpdateHalfEdge(inEdge, he => { he.Next = newEdgeRev; return he; });
        
        dcel.Edges.Add(new EdgeEntry<DE, UE>(
            new HalfEdgeEntry { Next = outEdge, Prev = newEdgeRev, Face = face, Origin = newVertexHandle },
            new HalfEdgeEntry { Next = newEdge, Prev = inEdge, Face = face, Origin = endVertex }
        ));
        
        var vertexNew = dcel.Vertices[newVertexHandle.Index];
        vertexNew.OutEdge = newEdge;
        dcel.Vertices[newVertexHandle.Index] = vertexNew;
        
        return newVertexHandle;
    }

    public static FixedVertexHandle CreateNewFaceAdjacentToEdge<V, DE, UE, F>(
        Dcel<V, DE, UE, F> dcel,
        FixedDirectedEdgeHandle edge,
        FixedVertexHandle newVertexHandle)
        where DE : new()
        where UE : new()
        where F : new()
    {
        var edgeEntry = dcel.GetHalfEdge(edge);
        var edgeFrom = edgeEntry.Origin;
        var edgeTo = dcel.GetHalfEdge(edgeEntry.Next).Origin;
        
        var newNextHandle = new FixedUndirectedEdgeHandle(dcel.Edges.Count);
        var newPrevHandle = new FixedUndirectedEdgeHandle(dcel.Edges.Count + 1);
        
        var newFaceHandle = new FixedFaceHandle(dcel.Faces.Count);
        
        var newNext = new HalfEdgeEntry
        {
            Next = new FixedDirectedEdgeHandle(newPrevHandle.Index * 2), // Normalized
            Prev = edge,
            Face = newFaceHandle,
            Origin = edgeTo
        };
        
        var newNextRev = new HalfEdgeEntry
        {
            Next = edgeEntry.Next,
            Prev = new FixedDirectedEdgeHandle(newPrevHandle.Index * 2 + 1), // Not normalized
            Face = edgeEntry.Face,
            Origin = newVertexHandle
        };
        
        var newPrev = new HalfEdgeEntry
        {
            Next = edge,
            Prev = new FixedDirectedEdgeHandle(newNextHandle.Index * 2), // Normalized
            Face = newFaceHandle,
            Origin = newVertexHandle
        };
        
        var newPrevRev = new HalfEdgeEntry
        {
            Next = new FixedDirectedEdgeHandle(newNextHandle.Index * 2 + 1), // Not normalized
            Prev = edgeEntry.Prev,
            Face = edgeEntry.Face,
            Origin = edgeFrom
        };
        
        dcel.Edges.Add(new EdgeEntry<DE, UE>(newNext, newNextRev));
        dcel.Edges.Add(new EdgeEntry<DE, UE>(newPrev, newPrevRev));
        
        dcel.Faces.Add(new FaceEntry<F> { AdjacentEdge = edge, Data = new F(), Kind = FaceKind.Inner });
        
        var vertexNew = dcel.Vertices[newVertexHandle.Index];
        vertexNew.OutEdge = new FixedDirectedEdgeHandle(newPrevHandle.Index * 2); // Normalized
        dcel.Vertices[newVertexHandle.Index] = vertexNew;
        
        dcel.UpdateHalfEdge(edge, he => 
        { 
            he.Prev = new FixedDirectedEdgeHandle(newPrevHandle.Index * 2); 
            he.Next = new FixedDirectedEdgeHandle(newNextHandle.Index * 2);
            he.Face = newFaceHandle;
            return he;
        });
        
        var face = dcel.Faces[edgeEntry.Face.Index];
        face.AdjacentEdge = new FixedDirectedEdgeHandle(newPrevHandle.Index * 2 + 1); // Not normalized
        dcel.Faces[edgeEntry.Face.Index] = face;
        
        dcel.UpdateHalfEdge(edgeEntry.Next, he => { he.Prev = new FixedDirectedEdgeHandle(newNextHandle.Index * 2 + 1); return he; });
        dcel.UpdateHalfEdge(edgeEntry.Prev, he => { he.Next = new FixedDirectedEdgeHandle(newPrevHandle.Index * 2 + 1); return he; });
        
        return newVertexHandle;
    }

    public static FixedDirectedEdgeHandle CreateSingleFaceBetweenEdgeAndNext<V, DE, UE, F>(
        Dcel<V, DE, UE, F> dcel,
        FixedDirectedEdgeHandle edge)
        where DE : new()
        where UE : new()
        where F : new()
    {
        var edgeEntry = dcel.GetHalfEdge(edge);
        var nextEntry = dcel.GetHalfEdge(edgeEntry.Next);
        var nextTo = dcel.GetHalfEdge(nextEntry.Next).Origin;
        
        var newFaceHandle = new FixedFaceHandle(dcel.Faces.Count);
        
        var innerEdgeEntry = new HalfEdgeEntry
        {
            Next = edge,
            Prev = edgeEntry.Next,
            Face = newFaceHandle,
            Origin = nextTo
        };
        
        var outerEdgeEntry = new HalfEdgeEntry
        {
            Next = nextEntry.Next,
            Prev = edgeEntry.Prev,
            Face = new FixedFaceHandle(0), // Outer face
            Origin = edgeEntry.Origin
        };
        
        var newEdgeHandle = new FixedUndirectedEdgeHandle(dcel.Edges.Count);
        var newInnerHandle = new FixedDirectedEdgeHandle(newEdgeHandle.Index * 2);
        var newOuterHandle = newInnerHandle.Rev();
        
        dcel.UpdateHalfEdge(edgeEntry.Prev, he => { he.Next = newOuterHandle; return he; });
        dcel.UpdateHalfEdge(edge, he => { he.Prev = newInnerHandle; he.Face = newFaceHandle; return he; });
        
        dcel.UpdateHalfEdge(edgeEntry.Next, he => { he.Next = newInnerHandle; he.Face = newFaceHandle; return he; });
        dcel.UpdateHalfEdge(nextEntry.Next, he => { he.Prev = newOuterHandle; return he; });
        
        var outerFace = dcel.Faces[0];
        outerFace.AdjacentEdge = newOuterHandle;
        dcel.Faces[0] = outerFace;
        
        dcel.Edges.Add(new EdgeEntry<DE, UE>(innerEdgeEntry, outerEdgeEntry));
        dcel.Faces.Add(new FaceEntry<F> { AdjacentEdge = newInnerHandle, Data = new F(), Kind = FaceKind.Inner });
        
        return newOuterHandle;
    }

    public static void FlipCw<V, DE, UE, F>(
        Dcel<V, DE, UE, F> dcel,
        FixedUndirectedEdgeHandle e)
    {
        var edge = new FixedDirectedEdgeHandle(e.Index * 2);
        var edgeEntry = dcel.GetHalfEdge(edge);
        var en = edgeEntry.Next;
        var ep = edgeEntry.Prev;
        var eFace = edgeEntry.Face;
        var eOrigin = edgeEntry.Origin;
        
        var t = edge.Rev();
        var tEntry = dcel.GetHalfEdge(t);
        var tn = tEntry.Next;
        var tp = tEntry.Prev;
        var tFace = tEntry.Face;
        var tOrigin = tEntry.Origin;
        
        dcel.UpdateHalfEdge(en, he => { he.Next = edge; he.Prev = tp; return he; });
        dcel.UpdateHalfEdge(edge, he => { he.Next = tp; he.Prev = en; he.Origin = dcel.GetHalfEdge(ep).Origin; return he; });
        dcel.UpdateHalfEdge(tp, he => { he.Next = en; he.Prev = edge; he.Face = eFace; return he; });
        
        dcel.UpdateHalfEdge(tn, he => { he.Next = t; he.Prev = ep; return he; });
        dcel.UpdateHalfEdge(t, he => { he.Next = ep; he.Prev = tn; he.Origin = dcel.GetHalfEdge(tp).Origin; return he; });
        dcel.UpdateHalfEdge(ep, he => { he.Next = tn; he.Prev = t; he.Face = tFace; return he; });
        
        var vEOrigin = dcel.Vertices[eOrigin.Index];
        vEOrigin.OutEdge = tn;
        dcel.Vertices[eOrigin.Index] = vEOrigin;
        
        var vTOrigin = dcel.Vertices[tOrigin.Index];
        vTOrigin.OutEdge = en;
        dcel.Vertices[tOrigin.Index] = vTOrigin;
        
        var fE = dcel.Faces[eFace.Index];
        fE.AdjacentEdge = edge;
        dcel.Faces[eFace.Index] = fE;
        
        var fT = dcel.Faces[tFace.Index];
        fT.AdjacentEdge = t;
        dcel.Faces[tFace.Index] = fT;
    }
}
