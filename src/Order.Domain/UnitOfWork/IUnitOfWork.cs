namespace Order.Domain.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}