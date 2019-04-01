using System;
using System.Collections.Generic;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace bilong.Data.Repository.Mongo
{
    public class MongoRepository<T> : IRepository<T>
    {
        protected readonly IMongoCollection<T> Collection;

        public MongoRepository(IMongoClient mongoClient, string databaseName, string collectionName = null)
        {
            var db = mongoClient.GetDatabase(databaseName);
            Collection = db.GetCollection<T>(collectionName ?? typeof(T).Name);
        }

        public async Task<T> Add(T entity)
        {
            var time = DateTime.UtcNow;
            await Collection.InsertOneAsync(entity);
            return entity;
        }

        public async Task<IEnumerable<T>> FindAll()
        {
            var result = await Collection.FindAsync(new BsonDocument());
            return result.ToEnumerable();
        }

        public async Task<IEnumerable<T>> Find(Expression<Func<T, bool>> filter)
        {
            var result = await Collection.FindAsync(filter);
            return result.ToEnumerable();
        }

        public async Task<IEnumerable<T>> Find(string filter)
        {
            var exp = DynamicExpressionParser.ParseLambda<T, bool>(null, false, filter);
            return await Find(exp);
        }

        public async Task<T> FindOne(Expression<Func<T, bool>> filter)
        {
            var cursor = await Collection.FindAsync(filter);
            var result = await cursor.FirstOrDefaultAsync();
            return result;
        }

        public async Task<T> ReplaceOne(T entity, Expression<Func<T, bool>> selector, bool isUpsert = false)
        {
            var result = await Collection.ReplaceOneAsync(selector, entity, new UpdateOptions{IsUpsert = isUpsert});
            return entity;
        }

        public async Task DeleteOne(Expression<Func<T, bool>> selector)
        {
            await Collection.DeleteOneAsync(selector);
        }
    }
}
