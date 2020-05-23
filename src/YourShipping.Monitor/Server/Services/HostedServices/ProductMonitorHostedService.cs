namespace YourShipping.Monitor.Server.Services.HostedServices
{
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.SignalR;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Hubs;
    using YourShipping.Monitor.Server.Models.Extensions;
    using YourShipping.Monitor.Server.Services.Attributes;
    using YourShipping.Monitor.Server.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    using Product = YourShipping.Monitor.Server.Models.Product;

    public class ProductMonitorHostedService : TimedHostedServiceBase
    {
        public ProductMonitorHostedService(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        [Execute]
        public async Task Execute(
            IRepository<Product, int> productRepository,
            IEntityScrapper<Product> productScrapper,
            IHubContext<MessagesHub> messageHubContext)
        {
            Log.Information("Running {Source} Monitor.", AlertSource.Products);

            var sourceChanged = false;
            foreach (var storedProduct in productRepository.All())
            {
                var entityChanged = false;
                var dateTime = DateTime.Now;
                var product = await productScrapper.GetAsync(storedProduct.Url, true);
                if (product == null)
                {
                    product = storedProduct;
                    if (product.IsAvailable)
                    {
                        entityChanged = true;
                        product.IsAvailable = false;
                        product.Updated = dateTime;
                        product.Sha256 = JsonSerializer.Serialize(storedProduct.IsAvailable).ComputeSHA256();
                        sourceChanged = true;

                        Log.Information(
                            "Product {Product} from {Store} has changed. Is Available: {IsAvailable}",
                            storedProduct.Name,
                            storedProduct.Store,
                            storedProduct.IsAvailable);
                    }
                }
                else if (product.Sha256 != storedProduct.Sha256)
                {
                    entityChanged = true;
                    product.Id = storedProduct.Id;
                    product.Updated = dateTime;
                    productRepository.TryAddOrUpdate(product, nameof(Product.Added), nameof(Product.Read));
                    sourceChanged = true;

                    Log.Information(
                        "Product {Product} from {Store} has changed. Is Available: {IsAvailable}",
                        product.Name,
                        product.Store,
                        product.IsAvailable);
                }

                if (entityChanged)
                {
                    Log.Information("Entity changed at source {Source}.", AlertSource.Departments);

                    await productRepository.SaveChangesAsync();

                    await messageHubContext.Clients.All.SendAsync(
                        ClientMethods.EntityChanged,
                        AlertSource.Products,
                        JsonSerializer.Serialize(product.ToDataTransferObject(true)));
                }
            }

            if (sourceChanged)
            {
                Log.Information("{Source} changes detected", AlertSource.Products);

                // await messageHubContext.Clients.All.SendAsync(ClientMethods.SourceChanged, AlertSource.Products);
            }
            else
            {
                Log.Information("No {Source} changes detected", AlertSource.Products);
            }
        }
    }
}