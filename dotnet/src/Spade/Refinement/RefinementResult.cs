namespace Spade.Refinement;

/// <summary>
/// Summary information about a refinement run.
/// </summary>
public sealed class RefinementResult
{
    public int AddedVertices { get; }
    public bool ReachedVertexLimit { get; }

    public RefinementResult(int addedVertices, bool reachedVertexLimit)
    {
        AddedVertices = addedVertices;
        ReachedVertexLimit = reachedVertexLimit;
    }
}
