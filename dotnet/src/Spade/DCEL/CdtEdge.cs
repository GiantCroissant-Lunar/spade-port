namespace Spade.DCEL;

public struct CdtEdge<UE> where UE : new()
{
    private bool _isConstraintEdge;
    public UE Data;

    public CdtEdge()
    {
        _isConstraintEdge = false;
        Data = new UE();
    }

    public CdtEdge(bool isConstraintEdge, UE data)
    {
        _isConstraintEdge = isConstraintEdge;
        Data = data;
    }

    public bool IsConstraintEdge => _isConstraintEdge;

    public void MakeConstraintEdge()
    {
        // assert(!IsConstraintEdge);
        _isConstraintEdge = true;
    }

    public void UnmakeConstraintEdge()
    {
        // assert(IsConstraintEdge);
        _isConstraintEdge = false;
    }
}
