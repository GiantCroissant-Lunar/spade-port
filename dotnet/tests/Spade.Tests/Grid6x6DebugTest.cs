using Xunit;
using Spade;
using Spade.Primitives;
using System;
using System.Diagnostics;
using System.Linq;

namespace Spade.Tests;

public class Grid6x6DebugTest
{
    [Fact]
    public void Debug_6x6_DirectedEdges_Enumeration()
    {
        var sw = Stopwatch.StartNew();
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        Console.WriteLine("Inserting 6x6 grid...");
        for (int y = 0; y < 6; y++)
        {
            for (int x = 0; x < 6; x++)
            {
                triangulation.Insert(new Point2<double>(x, y));
            }
        }
        Console.WriteLine($"Insertion complete: V={triangulation.NumVertices}, F={triangulation.NumFaces}, DE={triangulation.NumDirectedEdges}");

        sw.Restart();
        Console.WriteLine($"Starting DirectedEdges() enumeration (expected {triangulation.NumDirectedEdges} edges)...");
        
        int count = 0;
        foreach (var edge in triangulation.DirectedEdges())
        {
            count++;
            if (count % 50 == 0)
            {
                Console.WriteLine($"  Enumerated {count} edges, elapsed: {sw.ElapsedMilliseconds}ms");
            }
            
            if (sw.ElapsedMilliseconds > 10000)
            {
                Console.WriteLine($"ERROR: Enumeration taking too long!");
                Console.WriteLine($"  Expected: {triangulation.NumDirectedEdges} edges");
                Console.WriteLine($"  Enumerated: {count} edges");
                Console.WriteLine($"  INFINITE LOOP DETECTED");
                throw new TimeoutException($"DirectedEdges() enumeration is infinite! Got {count} edges, expected {triangulation.NumDirectedEdges}");
            }
        }

        Console.WriteLine($"DirectedEdges() enumeration complete: {count} edges in {sw.ElapsedMilliseconds}ms");
        
        // Verify count matches
        if (count != triangulation.NumDirectedEdges)
        {
            throw new Exception($"Edge count mismatch! Enumerated {count}, but NumDirectedEdges={triangulation.NumDirectedEdges}");
        }
    }
}
