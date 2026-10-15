namespace Order.Domain.UnitOfWork
{
    /// <summary>
    /// Each call returns a new unit of work (its own DbContext). Dispose it when the operation completes.
    /// </summary>
    public interface IUnitOfWorkFactory<out T>
        where T : IUnitOfWork
    {
        public T Create();
    }
}