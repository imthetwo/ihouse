using ihouse.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Room> Rooms { get; set; }
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<Contract> Contracts { get; set; }
    public DbSet<ContractTenant> ContractTenants { get; set; }
    public DbSet<ContractService> ContractServices { get; set; }
    public DbSet<MeterReading> MeterReadings { get; set; }
    public DbSet<ServicePrice> ServicePrices { get; set; }
    public DbSet<RoomService> RoomServices { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<InvoiceItem> InvoiceItems { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }
    public DbSet<MaintenanceCost> MaintenanceCosts { get; set; }
    public DbSet<TenantInvitation> TenantInvitations { get; set; }

    public DbSet<PaymentTransaction> PaymentTransactions
        => Set<PaymentTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ContractTenant>()
            .HasKey(ct => new
            {
                ct.ContractId,
                ct.TenantId
            });

        modelBuilder.Entity<ContractTenant>()
            .HasOne(ct => ct.Contract)
            .WithMany(c => c.ContractTenants)
            .HasForeignKey(ct => ct.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ContractTenant>()
            .HasOne(ct => ct.Tenant)
            .WithMany(t => t.ContractTenants)
            .HasForeignKey(ct => ct.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Contract>()
            .HasOne(c => c.Room)
            .WithMany(r => r.Contracts)
            .HasForeignKey(c => c.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ContractService>()
            .HasOne(cs => cs.Contract)
            .WithMany(c => c.ContractServices)
            .HasForeignKey(cs => cs.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MeterReading>()
            .HasOne(mr => mr.Room)
            .WithMany(r => r.MeterReadings)
            .HasForeignKey(mr => mr.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RoomService>()
            .HasOne(rs => rs.ServicePrice)
            .WithMany(sp => sp.RoomServices)
            .HasForeignKey(rs => rs.ServicePriceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RoomService>()
            .HasOne(rs => rs.Room)
            .WithMany(r => r.RoomServices)
            .HasForeignKey(rs => rs.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.Contract)
            .WithMany(c => c.Invoices)
            .HasForeignKey(i => i.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InvoiceItem>()
            .HasOne(ii => ii.Invoice)
            .WithMany(i => i.InvoiceItems)
            .HasForeignKey(ii => ii.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Invoice)
            .WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MaintenanceRequest>()
            .HasOne(mr => mr.Room)
            .WithMany(r => r.MaintenanceRequests)
            .HasForeignKey(mr => mr.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MaintenanceRequest>()
            .HasOne(mr => mr.Tenant)
            .WithMany(t => t.MaintenanceRequests)
            .HasForeignKey(mr => mr.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MaintenanceCost>()
            .HasOne(mc => mc.MaintenanceRequest)
            .WithMany(mr => mr.MaintenanceCosts)
            .HasForeignKey(mc => mc.MaintenanceRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Tenant>()
            .HasOne(t => t.ApplicationUser)
            .WithOne()
            .HasForeignKey<Tenant>(t => t.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.ApplicationUserId)
            .IsUnique();

        modelBuilder.Entity<TenantInvitation>()
            .HasIndex(x => x.CodeHash)
            .IsUnique();

        modelBuilder.Entity<PaymentTransaction>()
            .HasOne(t => t.Invoice)
            .WithMany()
            .HasForeignKey(t => t.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PaymentTransaction>()
            .HasIndex(t => t.OrderId)
            .IsUnique();

        modelBuilder.Entity<PaymentTransaction>()
            .Property(t => t.AmountUSD)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PaymentTransaction>()
            .Property(t => t.ExchangeRate)
            .HasPrecision(18, 6);

        modelBuilder.Entity<Room>()
            .Property(r => r.RentPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Contract>()
            .Property(c => c.RentPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Contract>()
            .Property(c => c.DepositAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ContractService>()
            .Property(cs => cs.UnitPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ServicePrice>()
            .Property(sp => sp.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Invoice>()
            .Property(i => i.TotalAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<InvoiceItem>()
            .Property(ii => ii.Quantity)
            .HasPrecision(18, 2);

        modelBuilder.Entity<InvoiceItem>()
            .Property(ii => ii.UnitPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<InvoiceItem>()
            .Property(ii => ii.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<MaintenanceCost>()
            .Property(mc => mc.Amount)
            .HasPrecision(18, 2);
    }
}