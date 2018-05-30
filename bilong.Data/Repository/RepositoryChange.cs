namespace bilong.Data.Repository
{
    /// <summary>
    /// Describes a change that has occurred in the repository. Used
    /// to emit this information to observers.
    /// </summary>
    public class RepositoryChange<T>
    {
        public OperationType OpType { get; set; }
        public T Item { get; set; }
    }
}
