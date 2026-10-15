using Order.Domain.Commands;
using Order.Domain.Entities.Base;
using Order.Domain.Entities.Exceptions;
using Order.Domain.Entities.Messages;

namespace Order.Domain.Entities
{
    public sealed class Product : Aggregate<int>
    {
        /// <summary>
        /// For Entity Framework only.
        /// </summary>
        private Product()
            : base(0)
        {
            Name = string.Empty;
            Price = 0;
        }

        public Product(CreateProduct createProductCommand)
            : this()
        {
            Process(createProductCommand);
        }

        public string Name { get; private set; }

        public decimal Price { get; private set; }

        private void Process(CreateProduct createProductCommand)
        {
            if (string.IsNullOrWhiteSpace(createProductCommand.Name))
            {
                throw new DomainRuleException(DomainErrorMessages.ProductNameInvalidErrorMessage);
            }

            if (createProductCommand.Price < 0)
            {
                throw new DomainRuleException(DomainErrorMessages.ProductPriceInvalidErrorMessage);
            }

            Id = createProductCommand.Id;
            Name = createProductCommand.Name;
            Price = createProductCommand.Price;
        }
    }
}