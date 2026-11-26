using Services;
using System;
using System.Linq;

namespace GTX {
    internal static class InventoryDashboardProjection {
        internal static InventoryDashboardSummary WithoutLocationTotals(InventoryDashboardSummary dashboard) {
            // Keep each vehicle's code while removing the per-location breakdown from the shared summary.
            return new InventoryDashboardSummary {
                Days = dashboard.Days,
                PeriodStartUtc = dashboard.PeriodStartUtc,
                PeriodEndUtc = dashboard.PeriodEndUtc,
                AddedCount = dashboard.AddedCount,
                StaleCount = dashboard.StaleCount,
                RemovedCount = dashboard.RemovedCount,
                StatusCounts = dashboard.StatusCounts,
                StatusTrend = (dashboard.StatusTrend ?? Array.Empty<InventoryDashboardStatusTrendPoint>())
                    .GroupBy(point => new { point.DateUtc, point.DateKey, point.Status, point.StatusText })
                    .Select(group => new InventoryDashboardStatusTrendPoint {
                        DateUtc = group.Key.DateUtc,
                        DateKey = group.Key.DateKey,
                        Status = group.Key.Status,
                        StatusText = group.Key.StatusText,
                        VehicleCount = group.Sum(point => point.VehicleCount)
                    }).ToArray(),
                Vehicles = dashboard.Vehicles
            };
        }
    }
}
