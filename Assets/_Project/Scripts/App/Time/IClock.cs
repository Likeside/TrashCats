namespace TrashCats.App
{
    /// <summary>Wall-clock source. Real time is used only to compute deltas; game time lives in the world.</summary>
    public interface IClock
    {
        long UtcNowMs { get; }
    }
}
