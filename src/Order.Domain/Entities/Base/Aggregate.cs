using System.Diagnostics.CodeAnalysis;

namespace Order.Domain.Entities.Base
{
    /// <summary>
    /// Consistency boundary. Only aggregate roots are loaded and saved through repositories;
    /// their child entities are changed through the root.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public abstract class Aggregate<T> : Entity<T>
    {
        protected Aggregate(T id)
            : base(id)
        {
        }
    }
}