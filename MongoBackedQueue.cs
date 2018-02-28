using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace bilong.MongoUtils
{
    /// <summary>
    /// Implements a queue backed by MongoDB. Note that we do not guarantee that items will be
    /// dequeued in the order they were enqueued. Also, this class is intended to be threadsafe,
    /// and state changes are atomic due to the use of Mongo's FindAndUpdate operators.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MongoBackedQueue<T> : IPersistedQueue<T>, IDisposable where T: IPersistedQueueable
    {
        private readonly ILogger<MongoBackedQueue<T>> _logger;
        private readonly IMongoCollection<T> _collection;
        private readonly int _staleMinutes;
        private readonly Timer _staleItemChecker;

        /// <summary>
        /// Create an instance of MongoBackedQueue. Only a single instance of this should be needed
        /// in a typical application, but multiple instances should work fine.
        /// </summary>
        public MongoBackedQueue(MongoBackedQueueInfoProvider<T> infoProvider, ILogger<MongoBackedQueue<T>> logger)
        {
            _logger = logger;
            _staleMinutes = infoProvider.StaleMinutes;
            _collection = new MongoClient().GetDatabase(infoProvider.DatabaseName).GetCollection<T>(infoProvider.CollectionName);
            _staleItemChecker = new Timer((state) => RequeueStaleItems(), null, _staleMinutes * 60 * 1000, _staleMinutes * 60 * 1000);
            logger.LogTrace($"MongoBackedQueue instantiated for type {typeof(T)}");
        }

        /// <summary>
        /// Adds an item to the queue.
        /// </summary>
        /// <param name="item">The item to queue</param>
        public async Task Enqueue(T item)
        {
            _logger.LogTrace($"Enqueuing {typeof(T).Name} item with Id {item.Id}");

            if (item.Id == Guid.Empty)
            {
                item.Id = Guid.NewGuid();
                _logger.LogTrace($"Generated Id {item.Id}");
            }

            var existingItemCursor = await _collection.FindAsync(i => i.Id == item.Id);
            var existingItem = await existingItemCursor.FirstOrDefaultAsync();
            if (existingItem != null)
            {
                throw new QueuedItemIdExistsException("Cannot enqueue item because this id already exists");
            }

            item.State = QueuedState.Waiting;
            item.LastStateChanged = DateTime.UtcNow;

            await _collection.InsertOneAsync(item);

            _logger.LogTrace($"Enqueued {typeof(T).Name} item Id {item.Id}");
        }

        /// <summary>
        /// Returns an item that is waiting in the queue, if any such items exist. Otherwise, returns null. If an
        /// item is returned, its state changes to Processing. If the caller does not call DequeueComplete on this
        /// item within staleMinutes, its state changes back to Waiting and it may be returned to a new caller.
        /// </summary>
        /// <returns></returns>
        public async Task<T> DequeueBegin()
        {
            _logger.LogTrace($"DequeueBegin was called for {typeof(T).Name}");

            var updateDefinition = Builders<T>.Update.Set(i => i.State, QueuedState.Processing).Set(i => i.LastStateChanged, DateTime.UtcNow);
            var result = await _collection.FindOneAndUpdateAsync(i => i.State == QueuedState.Waiting, updateDefinition);

            _logger.LogTrace($"DequeueBegin returning {typeof(T).Name} {(result == null ? "null" : $"item with Id {result.Id}")}");

            // Note this returns the document as it looked *before* the update. This shouldn't matter, because the caller shouldn't care
            // about State or LastStateChanged. We could change this behavior by passing the ReturnDocument option.
            return result;
        }

        /// <summary>
        /// Call this method to delete an item from the queue after processing is complete.
        /// </summary>
        /// <param name="item"></param>
        public async Task DequeueComplete(T item)
        {
            _logger.LogTrace($"DequeueComplete was called for {typeof(T).Name} item with Id {item.Id}");
            var result = await _collection.FindOneAndDeleteAsync(i => i.State == QueuedState.Processing && i.Id == item.Id);
            if (result == null)
            {
                throw new DequeueFailedException("DequeueComplete failed because the item was not found in the queue");
            }
        }

        private void RequeueStaleItems()
        {
            _logger.LogTrace($"RequeueStaleItems was called for {typeof(T).Name}");

            var updateDefinition = Builders<T>.Update.Set(i => i.State, QueuedState.Waiting)
                .Set(i => i.LastStateChanged, DateTime.UtcNow);
            var staleTime = DateTime.UtcNow.AddMinutes(_staleMinutes * -1);

            T staleItem;
            do
            {
                staleItem = _collection.FindOneAndUpdate(i =>
                        i.State == QueuedState.Processing &&
                        i.LastStateChanged < staleTime,
                    updateDefinition);

                if (staleItem != null)
                {
                    _logger.LogTrace($"Stale {typeof(T).Name} item was requeued with Id {staleItem.Id}");
                }
            } while (staleItem != null);

            _logger.LogTrace($"RequeueStaleItems finished for {typeof(T).Name}");
        }

        public void Dispose()
        {
            _staleItemChecker.Dispose();
        }

        public class DequeueFailedException : Exception
        {
            public DequeueFailedException(string message) : base(message) { }
        }

        public class QueuedItemIdExistsException : Exception
        {
            public QueuedItemIdExistsException(string message) : base(message) { }
        }
    }

    public enum QueuedState
    {
        Waiting,
        Processing
    }

    public interface IPersistedQueue<T> where T: IPersistedQueueable
    {
        Task Enqueue(T item);
        Task<T> DequeueBegin();
        Task DequeueComplete(T item);
    }

    public interface IPersistedQueueable
    {
        Guid Id { get; set; }
        DateTime LastStateChanged { get; set; }
        QueuedState State { get; set; }
    }

    public class MongoBackedQueueInfoProvider<T>
    {
        public MongoBackedQueueInfoProvider(string databaseName, string collectionName, int staleMinutes)
        {
            DatabaseName = databaseName;
            CollectionName = collectionName;
            StaleMinutes = staleMinutes;
        }

        public string DatabaseName { get; private set; }

        public string CollectionName { get; private set; }

        public int StaleMinutes { get; private set; }
    }
}
