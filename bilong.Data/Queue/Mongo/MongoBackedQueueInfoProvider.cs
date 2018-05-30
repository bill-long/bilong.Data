namespace bilong.Data.Queue.Mongo
{
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
