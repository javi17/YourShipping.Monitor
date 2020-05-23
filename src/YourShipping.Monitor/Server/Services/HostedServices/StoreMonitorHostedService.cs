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

    using Store = YourShipping.Monitor.Server.Models.Store;

    public sealed class StoreMonitorHostedService : TimedHostedServiceBase
    {
        public StoreMonitorHostedService(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        [Execute]
        public async Task Execute(
            IRepository<Store, int> storeRepository,
            IEntityScrapper<Store> storeScrapper,
            IHubContext<MessagesHub> messageHubContext)
        {
            Log.Information("Running {Source} Monitor.", AlertSource.Stores);

            var sourceChanged = false;
            foreach (var storedStore in storeRepository.All())
            {
                var entityChanged = false;
                var dateTime = DateTime.Now;
                var store = await storeScrapper.GetAsync(storedStore.Url, true);

                if (store == null)
                {
                    store = storedStore;
                    if (store.IsAvailable)
                    {
                        store.IsAvailable = false;
                        store.Updated = dateTime;
                        store.Sha256 = JsonSerializer.Serialize(storedStore.IsAvailable).ComputeSHA256();
                        sourceChanged = true;
                        entityChanged = true;

                        Log.Information(
                            "Store {Store} from {Province} has changed. Is Available: {IsAvailable}",
                            storedStore.Name,
                            storedStore.Province,
                            storedStore.IsAvailable);
                    }
                }
                else if (store.Sha256 != storedStore.Sha256)
                {
                    store.Id = storedStore.Id;
                    store.Updated = dateTime;
                    storeRepository.TryAddOrUpdate(store, nameof(Store.Added), nameof(Store.Read));
                    sourceChanged = true;
                    entityChanged = true;

                    Log.Information(
                        "Store {Store} from {Province} has changed. Is Available: {IsAvailable}",
                        store.Name,
                        store.Province,
                        store.IsAvailable);
                }

                if (entityChanged)
                {
                    Log.Information("Entity changed at source {Source}.", AlertSource.Stores);

                    await storeRepository.SaveChangesAsync();

                    await messageHubContext.Clients.All.SendAsync(
                        ClientMethods.EntityChanged,
                        AlertSource.Stores,
                        JsonSerializer.Serialize(store.ToDataTransferObject(true)));
                }
            }

            if (sourceChanged)
            {
                Log.Information("{Source} changes detected", AlertSource.Stores);
                // await messageHubContext.Clients.All.SendAsync(ClientMethods.SourceChanged, AlertSource.Stores);
            }
            else
            {
                Log.Information("No {Source} changes detected", AlertSource.Stores);
            }
        }
    }
}