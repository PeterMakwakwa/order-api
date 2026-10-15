using Microsoft.Extensions.DependencyInjection;
using Order.Domain.Entities;
using Order.Domain.UnitOfWork;

namespace Order.Tests.Setup
{
    public static class DefaultDataSetup
    {
        /// <summary>
        /// The default products and promotions (the in-memory equivalent of the *.ENV.LOCAL.sql seed scripts).
        /// </summary>
        public static async Task AddDefaultDataAsync(IServiceProvider serviceProvider)
        {
            IUnitOfWorkFactory<IOrderUnitOfWork> unitOfWorkFactory = serviceProvider.GetRequiredService<IUnitOfWorkFactory<IOrderUnitOfWork>>();
            using (IOrderUnitOfWork unitOfWork = unitOfWorkFactory.Create())
            {
                await unitOfWork.ProductRepository.AddRangeAsync(
                [
                    new Product(DefaultProducts.Keyboard),
                    new Product(DefaultProducts.Mouse),
                    new Product(DefaultProducts.Monitor)
                ]);
                await unitOfWork.PromotionRepository.AddRangeAsync(
                [
                    new Promotion(DefaultPromotions.Welcome10),
                    new Promotion(DefaultPromotions.Expired),
                    new Promotion(DefaultPromotions.Inactive)
                ]);
                await unitOfWork.SaveChangesAsync();
            }
        }
    }
}