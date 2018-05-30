using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using bilong.Data.Repository;
using bilong.Data.Repository.Mongo;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace bilong.Data.Test
{
    public class ObservableMongoRepositoryIntegrationTests
    {
        private readonly ITestOutputHelper _output;

        public ObservableMongoRepositoryIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async void WatchNotifiesOnAllChanges()
        {
            // Arrange
            var repo = new ObservableMongoRepository<TestEntity>(new MongoClient(),
                "ObservableMongoRepositoryIntegrationTests");
            var oldRecords = await repo.FindAll();
            foreach (var record in oldRecords)
            {
                await repo.Delete(record, "TestUserId");
            }

            // Allow time for deletions before we start watching
            Thread.Sleep(5000);

            // Now we can start watching for changes
            const int numberOfChanges = 100;
            var observer = new RepoObserver(_output);
            using (var unsubscriber = repo.Subscribe(observer))
            {
                // Act
                var range = Enumerable.Range(0, numberOfChanges);
                Parallel.ForEach(range, new ParallelOptions {MaxDegreeOfParallelism = 500}, i =>
                {
                    var newEntity = repo.Add(new TestEntity {Name = i.ToString(), Id = i.ToString()}, "testUserId")
                        .GetAwaiter().GetResult();
                });

                // Allow time for all changes to come through
                Thread.Sleep(5000);
            }

            // Assert
            Assert.Equal(numberOfChanges, observer.NumberOfCallsToOnNext);
        }
    }

    public class TestEntity : IStorable
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public DateTime CreatedTime { get; set; }
        public string CreatorId { get; set; }
        public DateTime ModifiedTime { get; set; }
        public string ModifierId { get; set; }
        public override string ToString()
        {
            return Name;
        }
    }

    public class RepoObserver : IObserver<RepositoryChange<TestEntity>>
    {
        private readonly ITestOutputHelper _output;
        public int NumberOfCallsToOnNext = 0;

        public RepoObserver(ITestOutputHelper output)
        {
            _output = output;
        }

        public void OnCompleted()
        {
            _output.WriteLine("RepoObserver.OnCompleted");
        }

        public void OnError(Exception error)
        {
            _output.WriteLine($"RepoObserver.OnError {error}");
        }

        public void OnNext(RepositoryChange<TestEntity> value)
        {
            _output.WriteLine($"RepoObserver.OnNext {value.OpType} {value.Item}");
            Interlocked.Increment(ref NumberOfCallsToOnNext);
        }
    }
}
