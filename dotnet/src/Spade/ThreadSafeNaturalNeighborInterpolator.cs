using System;
using System.Collections.Generic;
using System.Threading;
using Spade.Handles;
using Spade.Primitives;

namespace Spade;

/// <summary>
/// A thread-safe wrapper over <see cref="NaturalNeighborInterpolator{V,DE,UE,F,L}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type uses a <see cref="ThreadLocal{T}"/> instance of <see cref="NaturalNeighborInterpolator{V,DE,UE,F,L}"/>
/// per OS thread, all sharing the same underlying <see cref="DelaunayTriangulation{V,DE,UE,F,L}"/>.
/// The triangulation is accessed in a read-only fashion, so concurrent calls are safe
/// as long as no other code mutates the triangulation concurrently.
/// </para>
/// <para>
/// For single-threaded use or when you can allocate one interpolator instance per thread
/// manually, prefer <see cref="DelaunayTriangulationExtensions.NaturalNeighbor{V,DE,UE,F,L}(DelaunayTriangulation{V,DE,UE,F,L})"/>.
/// </para>
/// </remarks>
public sealed class ThreadSafeNaturalNeighborInterpolator<V, DE, UE, F, L>
    where V : IHasPosition<double>, new()
    where DE : new()
    where UE : new()
    where F : new()
    where L : IHintGenerator<double>, new()
{
    private readonly DelaunayTriangulation<V, DE, UE, F, L> _triangulation;
    private readonly ThreadLocal<NaturalNeighborInterpolator<V, DE, UE, F, L>> _localInterpolator;

    /// <summary>
    /// Creates a new thread-safe natural neighbor interpolator over the given triangulation.
    /// </summary>
    /// <param name="triangulation">A Delaunay triangulation that will be accessed in a read-only fashion.</param>
    internal ThreadSafeNaturalNeighborInterpolator(DelaunayTriangulation<V, DE, UE, F, L> triangulation)
    {
        _triangulation = triangulation ?? throw new ArgumentNullException(nameof(triangulation));
        _localInterpolator = new ThreadLocal<NaturalNeighborInterpolator<V, DE, UE, F, L>>(
            () => new NaturalNeighborInterpolator<V, DE, UE, F, L>(_triangulation));
    }

    private NaturalNeighborInterpolator<V, DE, UE, F, L> Instance => _localInterpolator.Value!;

    /// <summary>
    /// Computes natural neighbor weights for the specified query position.
    /// </summary>
    /// <param name="position">Query position in the same coordinate system as the triangulation.</param>
    /// <param name="result">A list that will be filled with (vertex, weight) pairs.</param>
    public void GetWeights(Point2<double> position, IList<(FixedVertexHandle Vertex, double Weight)> result)
    {
        Instance.GetWeights(position, result);
    }

    /// <summary>
    /// Performs natural neighbor interpolation of a scalar vertex attribute at the given position.
    /// </summary>
    /// <param name="selector">Selector mapping a vertex handle to the scalar value to interpolate.</param>
    /// <param name="position">Query position in the same coordinate system as the triangulation.</param>
    /// <returns>The interpolated value, or <c>null</c> if the query lies outside the convex hull.</returns>
    public double? Interpolate(Func<VertexHandle<V, DE, UE, F>, double> selector, Point2<double> position)
    {
        return Instance.Interpolate(selector, position);
    }

    /// <summary>
    /// Performs C1-style natural neighbor interpolation using both values and gradients at vertices.
    /// </summary>
    /// <param name="value">Selector mapping a vertex handle to its scalar value.</param>
    /// <param name="gradient">Selector mapping a vertex handle to its gradient vector.</param>
    /// <param name="flatness">A positive exponent controlling the influence radius of gradients.</param>
    /// <param name="position">Query position in the same coordinate system as the triangulation.</param>
    /// <returns>The interpolated value, or <c>null</c> if the query lies outside the convex hull.</returns>
    public double? InterpolateGradient(
        Func<VertexHandle<V, DE, UE, F>, double> value,
        Func<VertexHandle<V, DE, UE, F>, Point2<double>> gradient,
        double flatness,
        Point2<double> position)
    {
        return Instance.InterpolateGradient(value, gradient, flatness, position);
    }

    /// <summary>
    /// Estimates the gradient at a single vertex using its neighbors in the triangulation.
    /// </summary>
    /// <param name="vertex">Vertex at which to estimate the gradient.</param>
    /// <param name="value">Selector mapping a vertex handle to its scalar value.</param>
    /// <returns>An estimated gradient vector at the specified vertex.</returns>
    public Point2<double> EstimateGradient(
        VertexHandle<V, DE, UE, F> vertex,
        Func<VertexHandle<V, DE, UE, F>, double> value)
    {
        return Instance.EstimateGradient(vertex, value);
    }

    /// <summary>
    /// Pre-computes gradient estimates at all vertices and returns a function for lookup.
    /// </summary>
    /// <param name="value">Selector mapping a vertex handle to its scalar value.</param>
    /// <returns>A function mapping vertex handles to their estimated gradients.</returns>
    public Func<VertexHandle<V, DE, UE, F>, Point2<double>> EstimateGradients(
        Func<VertexHandle<V, DE, UE, F>, double> value)
    {
        return Instance.EstimateGradients(value);
    }
}
