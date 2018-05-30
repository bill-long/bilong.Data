using System.Threading.Tasks;

namespace bilong.Data.Queue
{
    /// <summary>
    /// Interface to be implemented by a persisted queue - that is, a
    /// queue backed by a database.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IPersistedQueue<T> where T : IPersistedQueueable
    {
        Task Enqueue(T item);
        Task<T> DequeueBegin();
        Task DequeueComplete(T item);
    }
}
