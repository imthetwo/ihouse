using ihouse.Data;
using ihouse.Models;
using ihouse.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Services;

public static class InvoiceCalculator
{
    public static async Task<string?> AddServicesAsync(ApplicationDbContext db, Contract contract, Invoice invoice)
    {
        // Contract prices take priority over services assigned directly to the room.
        var prices = await db.RoomServices.Where(s => s.RoomId == contract.RoomId && s.IsActive &&
                s.ServicePrice.IsActive && s.ServicePrice.EffectiveFrom <= invoice.InvoiceDate &&
                (s.ServicePrice.EffectiveTo == null || s.ServicePrice.EffectiveTo >= invoice.InvoiceDate))
            .Select(s => new { s.ServicePrice.ServiceType, s.ServicePrice.Price }).ToListAsync();
        var rates = new Dictionary<ServiceType, decimal>();
        foreach (var price in prices)
        {
            if (!rates.TryAdd(price.ServiceType, price.Price))
                return "Service charges are unavailable for this room. Please check its selected services.";
        }
        var services = await db.ContractServices.Where(s => s.ContractId == contract.Id && s.IsActive).ToListAsync();
        if (services.GroupBy(s => s.ServiceType).Any(g => g.Count() > 1))
            return "Select only one price for each service on this contract.";
        foreach (var service in services) rates[service.ServiceType] = service.UnitPrice;

        foreach (var rate in rates)
        {
            decimal quantity = 1;
            if (rate.Key is ServiceType.Electricity or ServiceType.Water)
            {
                // Bill the previous calendar month, or from check-in for a new contract.
                var end = new DateOnly(invoice.InvoiceDate.Year, invoice.InvoiceDate.Month, 1);
                var start = end.AddMonths(-1);
                if (start < contract.ContractStartDate) start = contract.ContractStartDate;
                if (start >= end) continue; // No utility period before this contract starts.
                var first = await db.MeterReadings.SingleOrDefaultAsync(r => r.RoomId == contract.RoomId && r.ReadingDay == start);
                var last = await db.MeterReadings.SingleOrDefaultAsync(r => r.RoomId == contract.RoomId && r.ReadingDay == end);
                if (first == null || last == null)
                    return $"Enter meter readings for {start:dd/MM/yyyy} and {end:dd/MM/yyyy} before creating this invoice.";
                quantity = rate.Key == ServiceType.Electricity
                    ? last.ElectricityReading - first.ElectricityReading
                    : last.WaterReading - first.WaterReading;
                if (quantity < 0) return "The closing meter reading must not be lower than the opening reading.";
                if (quantity == 0) continue;
            }
            var itemType = Enum.Parse<InvoiceItemType>(rate.Key.ToString());
            invoice.InvoiceItems.Add(new InvoiceItem
            {
                ItemType = itemType, Description = rate.Key.ToString(),
                Quantity = quantity, UnitPrice = rate.Value,
                Amount = CalculateAmount(quantity, rate.Value)
            });
        }
        var tenants = await db.ContractTenants.Where(t => t.ContractId == contract.Id)
            .Select(t => t.TenantId).ToListAsync();
        var startDate = contract.ContractStartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var throughDate = invoice.InvoiceDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var repairs = await db.MaintenanceRequests.Include(r => r.MaintenanceCosts)
            .Where(r => r.RoomId == contract.RoomId && tenants.Contains(r.TenantId) &&
                r.CreatedAt >= startDate && r.CompletedAt < throughDate &&
                r.Status == MaintenanceRqStatus.Completed &&
                r.ApprovalStatus == MaintenanceApprovalStatus.Approved && r.BilledInvoiceId == null)
            .ToListAsync();
        foreach (var repair in repairs)
        {
            decimal amount = repair.MaintenanceCosts.Where(c => c.ChargeToTenant).Sum(c => c.Amount);
            if (amount <= 0) continue;
            invoice.InvoiceItems.Add(new InvoiceItem
            {
                ItemType = InvoiceItemType.Maintenance, Description = $"Maintenance request #{repair.Id}",
                Quantity = 1, UnitPrice = amount, Amount = amount
            });
            repair.BilledInvoice = invoice;
        }
        invoice.TotalAmount = invoice.InvoiceItems.Sum(i => i.Amount);
        return null;
    }

    public static decimal CalculateAmount(decimal quantity, decimal unitPrice)
    {
        if (quantity < 0 || unitPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity and price must not be negative.");
        return decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
    }
}
