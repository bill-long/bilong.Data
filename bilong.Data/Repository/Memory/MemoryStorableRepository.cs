using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace bilong.Data.Repository.Memory
{
    public class MemoryStorableRepository<T> : IStorableRepository<T> where T : IStorable
    {
        private readonly Dictionary<object, T> _memoryStore = new Dictionary<object, T>();

        public Task<T> Add(T entity, string userId)
        {
            lock (_memoryStore)
            {
                if (string.IsNullOrEmpty(entity.Id)) entity.Id = Guid.NewGuid().ToString();
                var time = DateTime.UtcNow;
                entity.CreatedTime = time;
                entity.CreatorId = userId;
                entity.ModifiedTime = time;
                entity.ModifierId = userId;
                _memoryStore.Add(entity.Id, entity);
                return Task.FromResult(entity);
            }
        }

        public Task<IEnumerable<T>> FindAll()
        {
            lock (_memoryStore)
            {
                IEnumerable<T> result = _memoryStore.Values.ToList();
                return Task.FromResult(result);
            }
        }

        public Task<IEnumerable<T>> Find(Expression<Func<T, bool>> filter)
        {
            lock (_memoryStore)
            {
                IEnumerable<T> result = _memoryStore.Values.Where(filter.Compile()).ToList();
                return Task.FromResult(result);
            }
        }

        public Task<T> FindOne(Expression<Func<T, bool>> filter)
        {
            lock (_memoryStore)
            {
                return Task.FromResult(_memoryStore.Values.FirstOrDefault(filter.Compile()));
            }
        }

        public Task<T> Replace(T entity, string userId)
        {
            lock (_memoryStore)
            {
                if (string.IsNullOrEmpty(entity.Id))
                    throw new ArgumentNullException(nameof(entity.Id), "Id must contain a value");
                var existingEntity = _memoryStore[entity.Id];
                entity.CreatedTime = existingEntity.CreatedTime;
                entity.CreatorId = existingEntity.CreatorId;
                entity.ModifiedTime = DateTime.UtcNow;
                entity.ModifierId = userId;
                _memoryStore[entity.Id] = entity;
                return Task.FromResult(entity);
            }
        }

        public Task Delete(T entity, string userId)
        {
            lock (_memoryStore)
            {
                _memoryStore.Remove(entity.Id);
                return Task.CompletedTask;
            }
        }
    }
}