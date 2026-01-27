using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Servisnaknizka.Models;

namespace Servisnaknizka.Data
{
    /// <summary>
    /// Hlavn˝ datab·zov˝ kontext pre SQL Server
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // DbSet pre naöe entity
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<ServiceRecord> ServiceRecords { get; set; }
        public DbSet<Permission> Permissions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Konfigur·cia tabuliek Identity s vlastn˝mi n·zvami
            builder.Entity<User>().ToTable("Users");
            builder.Entity<IdentityRole<int>>().ToTable("Roles");
            builder.Entity<IdentityUserRole<int>>().ToTable("UserRoles");
            builder.Entity<IdentityUserClaim<int>>().ToTable("UserClaims");
            builder.Entity<IdentityUserLogin<int>>().ToTable("UserLogins");
            builder.Entity<IdentityRoleClaim<int>>().ToTable("RoleClaims");
            builder.Entity<IdentityUserToken<int>>().ToTable("UserTokens");

            // SQL Server öpecifickÈ nastavenia pre User
            builder.Entity<User>(entity =>
            {
                entity.Property(u => u.FirstName)
                    .HasMaxLength(50)
                    .IsRequired()
                    .HasColumnType("nvarchar(50)");

                entity.Property(u => u.LastName)
                    .HasMaxLength(50)
                    .IsRequired()
                    .HasColumnType("nvarchar(50)");

                entity.Property(u => u.Role)
                    .HasConversion<int>()
                    .IsRequired();

                entity.Property(u => u.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()")
                    .HasColumnType("datetime2");

                entity.HasIndex(u => u.Email).IsUnique();
            });

            // SQL Server öpecifickÈ nastavenia pre Vehicle
            builder.Entity<Vehicle>(entity =>
            {
                entity.Property(v => v.VIN)
                    .HasMaxLength(17)
                    .IsRequired()
                    .HasColumnType("varchar(17)");

                entity.Property(v => v.Brand)
                    .HasMaxLength(50)
                    .IsRequired()
                    .HasColumnType("nvarchar(50)");

                entity.Property(v => v.Model)
                    .HasMaxLength(50)
                    .IsRequired()
                    .HasColumnType("nvarchar(50)");

                entity.Property(v => v.LicensePlate)
                    .HasMaxLength(20)
                    .IsRequired()
                    .HasColumnType("varchar(20)");

                entity.Property(v => v.Color)
                    .HasMaxLength(30)
                    .HasColumnType("nvarchar(30)");

                entity.Property(v => v.EngineType)
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                entity.Property(v => v.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()")
                    .HasColumnType("datetime2");

                entity.HasIndex(v => v.VIN).IsUnique();
                entity.HasIndex(v => v.LicensePlate);
                entity.HasIndex(v => new { v.OwnerId, v.IsActive });
            });

            // SQL Server öpecifickÈ nastavenia pre ServiceRecord
            builder.Entity<ServiceRecord>(entity =>
            {
                entity.Property(sr => sr.Description)
                    .HasMaxLength(500)
                    .IsRequired()
                    .HasColumnType("nvarchar(500)");

                entity.Property(sr => sr.ServiceType)
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                entity.Property(sr => sr.PartsUsed)
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                entity.Property(sr => sr.Notes)
                    .HasMaxLength(1000)
                    .HasColumnType("nvarchar(1000)");

                entity.Property(sr => sr.Cost)
                    .HasColumnType("decimal(10,2)");

                entity.Property(sr => sr.ServiceDate)
                    .HasColumnType("date");

                entity.Property(sr => sr.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()")
                    .HasColumnType("datetime2");

                entity.HasIndex(sr => new { sr.VehicleId, sr.ServiceDate });
                entity.HasIndex(sr => sr.CreatedById);
            });

            // SQL Server öpecifickÈ nastavenia pre Permission
            builder.Entity<Permission>(entity =>
            {
                entity.Property(p => p.Notes)
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                entity.Property(p => p.GrantedAt)
                    .HasDefaultValueSql("GETUTCDATE()")
                    .HasColumnType("datetime2");

                entity.HasIndex(p => new { p.ServiceId, p.VehicleId }).IsUnique();
                entity.HasIndex(p => new { p.VehicleId, p.IsActive });
            });

            // Konfigur·cia vzùahov
            ConfigureRelationships(builder);
        }

        /// <summary>
        /// Konfigur·cia vzùahov medzi entitami
        /// </summary>
        private static void ConfigureRelationships(ModelBuilder builder)
        {
            // Vehicle -> Owner
            builder.Entity<Vehicle>()
                .HasOne(v => v.Owner)
                .WithMany(u => u.OwnedVehicles)
                .HasForeignKey(v => v.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // ServiceRecord -> Vehicle
            builder.Entity<ServiceRecord>()
                .HasOne(sr => sr.Vehicle)
                .WithMany(v => v.ServiceRecords)
                .HasForeignKey(sr => sr.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            // ServiceRecord -> CreatedBy
            builder.Entity<ServiceRecord>()
                .HasOne(sr => sr.CreatedBy)
                .WithMany(u => u.CreatedServiceRecords)
                .HasForeignKey(sr => sr.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Permission -> Service
            builder.Entity<Permission>()
                .HasOne(p => p.Service)
                .WithMany(u => u.ServicePermissions)
                .HasForeignKey(p => p.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Permission -> Vehicle
            builder.Entity<Permission>()
                .HasOne(p => p.Vehicle)
                .WithMany(v => v.ServicePermissions)
                .HasForeignKey(p => p.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            // Permission -> GrantedBy
            builder.Entity<Permission>()
                .HasOne(p => p.GrantedBy)
                .WithMany()
                .HasForeignKey(p => p.GrantedById)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}