using System;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace bilong.Data.Repository.Mongo
{
    /// <summary>
    /// A MongoDB repository that implements IObservable to notify observers of changes to the collection.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ObservableMongoRepository<T> : MongoRepository<T>, IObservable<RepositoryChange<T>>, IDisposable where T : IStorable
    {
        private IObserver<RepositoryChange<T>>[] _observers;
        private Task _watchTask;

        public ObservableMongoRepository(IMongoClient mongoClient, string databaseName) : base(mongoClient, databaseName)
        {
            _observers = new IObserver<RepositoryChange<T>>[0];
            StartWatching();
        }

        #region Observable

        // Observer pattern implementation adapted from https://docs.microsoft.com/en-us/dotnet/api/system.iobservable-1
        // Note that in our case we are using a copy-on-write approach to allow thread safety without locks.

        public IDisposable Subscribe(IObserver<RepositoryChange<T>> observer)
        {
            lock (_observers)
            {
                if (!_observers.Contains(observer))
                {
                    _observers = _observers.Concat(new[] {observer}).ToArray();
                }

                return new Unsubscriber(() => Unsubscribe(observer));
            }
        }

        private void Unsubscribe(IObserver<RepositoryChange<T>> observer)
        {
            lock (_observers)
            {
                if (_observers.Contains(observer))
                {
                    _observers = _observers.Where(o => o != observer).ToArray();
                }
            }
        }

        private void End()
        {
            lock (_observers)
            {
                foreach (var observer in _observers.ToArray())
                {
                    if (_observers.Contains(observer))
                    {
                        observer.OnCompleted();
                    }
                }

                _observers = new IObserver<RepositoryChange<T>>[0];
            }
        }

        /// <summary>
        /// This class is returned to the subscriber. The subscriber should let us know
        /// they want to unsubscribe by disposing this object.
        /// </summary>
        private class Unsubscriber : IDisposable
        {
            private readonly Action _unsubAction;

            public Unsubscriber(Action unsubAction)
            {
                _unsubAction = unsubAction;
            }

            public void Dispose()
            {
                _unsubAction.Invoke();
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            End();
        }

        #endregion

        private void StartWatching()
        {
            var options = new ChangeStreamOptions { FullDocument = ChangeStreamFullDocumentOption.UpdateLookup };
            var pipeline = new EmptyPipelineDefinition<ChangeStreamDocument<T>>();

            // It's important to perform this on the main thread so exceptions bubble up.
            // This will fail if mongodb is not a replica set.
            var changeStream = Collection.Watch(pipeline, options);

            // If that succeeds, then we can start watching the stream on a different thread.
            _watchTask = Task.Run(() => WatchForChanges(changeStream));
        }

        private async Task WatchForChanges(
            IAsyncCursor<ChangeStreamDocument<T>> changeStream)
        {
            while (true)
            {
                await changeStream.MoveNextAsync();
                foreach (var change in changeStream.Current)
                {
                    OperationType opType;
                    switch (change.OperationType)
                    {
                        case ChangeStreamOperationType.Delete:
                            opType = OperationType.Delete;
                            break;
                        case ChangeStreamOperationType.Insert:
                            opType = OperationType.Insert;
                            break;
                        case ChangeStreamOperationType.Invalidate:
                            // Occurs if the collection is dropped or renamed
                            End();
                            throw new InvalidOperationException("Change stream was invalidated");
                        case ChangeStreamOperationType.Replace:
                            opType = OperationType.Replace;
                            break;
                        case ChangeStreamOperationType.Update:
                            opType = OperationType.Update;
                            break;
                        default:
                            throw new InvalidOperationException("Unknown operation type");
                    }

                    // We purposely do not lock on this read access to _observers. This is because
                    // the snapshot is immutable - it can't change underneath us. All write operations
                    // are within a lock and are performed as a copy-on-write.

                    // ReSharper disable once InconsistentlySynchronizedField
                    var snapshot = _observers;
                    foreach (var observer in snapshot)
                    {
                        observer.OnNext(new RepositoryChange<T> {OpType = opType, Item = change.FullDocument});
                    }
                }
            }
        }
    }
}
