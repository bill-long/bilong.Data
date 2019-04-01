using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace bilong.Data.Repository
{
    public interface IRepository<T>
    {
        Task<T> Add(T entity);
        Task<IEnumerable<T>> FindAll();
        Task<IEnumerable<T>> Find(Expression<Func<T, bool>> filter);
        Task<IEnumerable<T>> Find(string filter);
        Task<T> FindOne(Expression<Func<T, bool>> filter);
        Task<T> ReplaceOne(T entity, Expression<Func<T, bool>> selector, bool isUpsert = false);
        Task DeleteOne(Expression<Func<T, bool>> selector);
    }
}
