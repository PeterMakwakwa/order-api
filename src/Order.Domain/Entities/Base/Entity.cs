using System.Diagnostics.CodeAnalysis;

namespace Order.Domain.Entities.Base
{
    [ExcludeFromCodeCoverage]
    public abstract class Entity<T>
    {
        protected Entity(T id)
        {
            Id = id;
        }

        /// <summary>
        /// Set by the database (identity columns) or by the entity itself; never from outside.
        /// </summary>
        public T Id { get; protected set; }
    }
}