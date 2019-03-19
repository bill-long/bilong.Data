using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.Configuration.Client;
using Couchbase.Core.Serialization;
using Couchbase.Linq;
using Couchbase.Linq.Extensions;
using Couchbase.N1QL;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace bilong.Data.Repository.Couchbase
{
    public class CouchbaseStorableRepository
    {
        public static ClientConfiguration GetClientConfiguration()
        {
            return new ClientConfiguration
            {
                // Use PascalCase, not camelCase, so names match the C# names and we don't have
                // to put JsonProperty attributes everywhere.
                Serializer = () => new DefaultSerializer(
                    new JsonSerializerSettings { ContractResolver = new DefaultContractResolver() },
                    new JsonSerializerSettings { ContractResolver = new DefaultContractResolver() })
            };
        }
    }

    public class CouchbaseStorableRepository<T> : IStorableRepository<T> where T : IStorable
    {
        private readonly IBucketContext _context;

        public CouchbaseStorableRepository()
        {
            _context = new BucketContext(ClusterHelper.GetBucket(typeof(T).Name));
        }

        public async Task<T> Add(T entity, string userId)
        {
            var time = DateTime.UtcNow;
            entity.CreatorId = userId;
            entity.CreatedTime = time;
            entity.ModifierId = userId;
            entity.ModifiedTime = time;
            var result = await _context.Bucket.InsertAsync(new Document<T>
            {
                Id = entity.Id,
                Content = entity
            });

            result.EnsureSuccess();

            return result.Content;
        }

        public async Task Delete(T entity, string userIdr)
        {
            var result = await _context.Bucket.RemoveAsync(new Document<T> {Id = entity.Id});
            result.EnsureSuccess();
        }

        public async Task<IEnumerable<T>> Find(Expression<Func<T, bool>> filter)
        {
            var result = await _context.Query<T>().Where(filter).ExecuteAsync();
            return result;
        }

        public async Task<IEnumerable<T>> FindAll()
        {
            var result = await _context.Query<T>().ExecuteAsync();
            return result;
        }

        public async Task<T> FindOne(Expression<Func<T, bool>> filter)
        {
            var result = await _context.Query<T>().Where(filter).Take(1).ExecuteAsync();
            return result.FirstOrDefault();
        }

        public async Task<T> Replace(T entity, string userId)
        {
            var oldEntityResult = await _context.Bucket.GetAsync<T>(entity.Id);
            oldEntityResult.EnsureSuccess();

            entity.CreatorId = oldEntityResult.Value.CreatorId;
            entity.CreatedTime = oldEntityResult.Value.CreatedTime;
            entity.ModifiedTime = DateTime.UtcNow;
            entity.ModifierId = userId;

            var replaceResult = await _context.Bucket.ReplaceAsync(new Document<T> {Id = entity.Id, Content = entity});
            replaceResult.EnsureSuccess();
            return replaceResult.Content;
        }
    }
}
