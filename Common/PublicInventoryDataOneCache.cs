using Common;
using Services;
using System;

namespace GTX.Common
{
    public sealed class PublicInventoryDataOneSnapshot
    {
        public GTXDTO[] Vehicles { get; set; } = Array.Empty<GTXDTO>();
        public DateTime Published { get; set; }
    }

    public static class PublicInventoryDataOneCache
    {
        public static PublicInventoryDataOneSnapshot Get(IInventoryService inventoryService, int minutes)
        {
            if (inventoryService == null) throw new ArgumentNullException(nameof(inventoryService));

            return AppCache.GetOrCreate(
                Constants.DATAONE_INVENTORY_CACHE,
                () =>
                {
                    var inventory = inventoryService.GetInventory(
                        includeHiddenInventory: false,
                        includeDataOneContent: true);
                    return new PublicInventoryDataOneSnapshot
                    {
                        Vehicles = inventory.vehicles ?? Array.Empty<GTXDTO>(),
                        Published = inventory.InventoryDate
                    };
                },
                minutes: Math.Max(1, Math.Min(60, minutes)));
        }
    }
}
