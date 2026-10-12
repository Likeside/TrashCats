namespace TrashCats.App
{
    public enum SaveUrgency
    {
        /// <summary>Save soon (throttled), e.g. after assigning an activity.</summary>
        Deferred = 0,

        /// <summary>Save right away, e.g. after buying an upgrade.</summary>
        Immediate = 1,
    }

    /// <summary>Lets the session request saves without knowing how saving works.</summary>
    public interface ISaveRequests
    {
        void MarkDirty(SaveUrgency urgency);
    }

    public sealed class NoSaveRequests : ISaveRequests
    {
        public static readonly NoSaveRequests Instance = new NoSaveRequests();

        public void MarkDirty(SaveUrgency urgency)
        {
        }
    }
}
