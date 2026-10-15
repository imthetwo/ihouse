using ihouse.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // =========================
        // DbSets
        // =========================

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


        // =========================
        // Model Configuration
        // =========================

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            

            // =========================
            // ContractTenant
            // Composite Primary Key
            // =========================

            modelBuilder.Entity<ContractTenant>()
                .HasKey(ct => new
                {
                    ct.ContractId,
                    ct.TenantId
                });


            // =========================
            // ContractTenant
            // Contract 1 - N ContractTenant
            // =========================

            modelBuilder.Entity<ContractTenant>()
                .HasOne(ct => ct.Contract)
                .WithMany(c => c.ContractTenants)
                .HasForeignKey(ct => ct.ContractId)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================
            // ContractTenant
            // Tenant 1 - N ContractTenant
            // =========================

            modelBuilder.Entity<ContractTenant>()
                .HasOne(ct => ct.Tenant)
                .WithMany(t => t.ContractTenants)
                .HasForeignKey(ct => ct.TenantId)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================
            // Room - Contract
            // Room 1 - N Contract
            // =========================

            modelBuilder.Entity<Contract>()
                .HasOne(c => c.Room)
                .WithMany(r => r.Contracts)
                .HasForeignKey(c => c.RoomId)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================
            // Contract - ContractService
            // Contract 1 - N ContractService
            // =========================

            modelBuilder.Entity<ContractService>()
                .HasOne(cs => cs.Contract)
                .WithMany(c => c.ContractServices)
                .HasForeignKey(cs => cs.ContractId)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================
            // Room - MeterReading
            // Room 1 - N MeterReading
            // =========================

            modelBuilder.Entity<MeterReading>()
                .HasOne(mr => mr.Room)
                .WithMany(r => r.MeterReadings)
                .HasForeignKey(mr => mr.RoomId)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================
            // ServicePrice - RoomService
            // ServicePrice 1 - N RoomService
            // =========================

            modelBuilder.Entity<RoomService>()
                .HasOne(rs => rs.ServicePrice)
                .WithMany(sp => sp.RoomServices)
                .HasForeignKey(rs => rs.ServicePriceId)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================
            // Room - RoomService
            // Room 1 - N RoomService
            // =========================

            modelBuilder.Entity<RoomService>()
                .HasOne(rs => rs.Room)
                .WithMany(r => r.RoomServices)
                .HasForeignKey(rs => rs.RoomId)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================
            // Contract - Invoice
            // Contract 1 - N Invoice
            // =========================

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Contract)
                .WithMany(c => c.Invoices)
                .HasForeignKey(i => i.ContractId)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================
            // Invoice - InvoiceItem
            // Invoice 1 - N InvoiceItem
            // =========================

            modelBuilder.Entity<InvoiceItem>()
                .HasOne(ii => ii.Invoice)
                .WithMany(i => i.InvoiceItems)
                .HasForeignKey(ii => ii.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================
            // Invoice - Payment
            // Invoice 1 - N Payment
            // =========================

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(p => p.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================
            // Room - MaintenanceRequest
            // Room 1 - N MaintenanceRequest
            // =========================

            modelBuilder.Entity<MaintenanceRequest>()
                .HasOne(mr => mr.Room)
                .WithMany(r => r.MaintenanceRequests)
                .HasForeignKey(mr => mr.RoomId)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================
            // Tenant - MaintenanceRequest
            // Tenant 1 - N MaintenanceRequest
            // =========================

            modelBuilder.Entity<MaintenanceRequest>()
                .HasOne(mr => mr.Tenant)
                .WithMany(t => t.MaintenanceRequests)
                .HasForeignKey(mr => mr.TenantId)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================
            // MaintenanceRequest - MaintenanceCost
            // MaintenanceRequest 1 - N MaintenanceCost
            // =========================

            modelBuilder.Entity<MaintenanceCost>()
                .HasOne(mc => mc.MaintenanceRequest)
                .WithMany(mr => mr.MaintenanceCosts)
                .HasForeignKey(mc => mc.MaintenanceRequestId)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================
            // Decimal Precision
            // =========================

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
}