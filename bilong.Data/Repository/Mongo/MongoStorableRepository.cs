using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace bilong.Data.Repository.Mongo
{
    public class MongoStorableRepository<T> : IStorableRepository<T> where T : IStorable
    {
        protected readonly IMongoCollection<T> Collection;

        public MongoStorableRepository(IMongoClient mongoClient, string databaseName)
        {
            var db = mongoClient.GetDatabase(databaseName);
            Collection = db.GetCollection<T>(typeof(T).Name);
        }

        #region IStorable

        public async Task<T> Add(T entity, string userId)
        {
            var time = DateTime.UtcNow;
            entity.CreatorId = userId;
            entity.CreatedTime = time;
            entity.ModifierId = userId;
            entity.ModifiedTime = time;
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

        public async Task<T> FindOne(Expression<Func<T, bool>> filter)
        {
            var cursor = await Collection.FindAsync(filter);
            var result = await cursor.FirstOrDefaultAsync();
            return result;
        }

        public async Task<T> Replace(T entity, string userId)
        {
            // We want to replace the document, except for the Creator
            // properties on IStorable.

            // First read the current object
            var cursor = await Collection.FindAsync(e => e.Id == entity.Id);
            var currentDoc = await cursor.FirstAsync();

            // Now overwrite the values on the updated object with the ones from the old
            entity.CreatedTime = currentDoc.CreatedTime;
            entity.CreatorId = currentDoc.CreatorId;

            // Also set the modifier values
            entity.ModifiedTime = DateTime.UtcNow;
            entity.ModifierId = userId;

            var result = await Collection.ReplaceOneAsync(e => e.Id == entity.Id, entity);
            return entity;
        }

        public async Task Delete(T entity, string userId)
        {
            await Collection.DeleteOneAsync(d => d.Id == entity.Id);
        }

        #endregion
    }
}
