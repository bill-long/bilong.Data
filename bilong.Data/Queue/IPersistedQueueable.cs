using System;

namespace bilong.Data.Queue
{
    /// <summary>
    /// This interface must be implemented by any type that will be
    /// queued in an IPersistedQueue.
    /// </summary>
    public interface IPersistedQueueable
    {
        Guid Id { get; set; }
        DateTime LastStateChanged { get; set; }
        QueuedState State { get; set; }
    }
}
