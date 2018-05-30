using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace bilong.Data.Repository
{
    public interface IRepository<T> where T : IStorable
    {
        Task<T> Add(T entity, string userId);
        Task<IEnumerable<T>> FindAll();
        Task<IEnumerable<T>> Find(Expression<Func<T, bool>> filter);
        Task<T> FindOne(Expression<Func<T, bool>> filter);
        Task<T> Replace(T entity, string userId);
        Task Delete(T entity, string userIdr);
    }
}
