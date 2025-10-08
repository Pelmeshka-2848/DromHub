using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DromHub.Models;
using Microsoft.EntityFrameworkCore;

namespace DromHub.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets для всех таблиц (УДАЛИТЬ Cart и CartItem)
        public DbSet<Brand> Brands { get; set; }
        public DbSet<BrandAlias> BrandAliases { get; set; }
        public DbSet<BrandMarkup> BrandMarkups { get; set; }
        public DbSet<Part> Parts { get; set; }
        public DbSet<PartImage> PartImages { get; set; }
        public DbSet<LocalStock> LocalStocks { get; set; }
        public DbSet<OemCross> OemCrosses { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierLocality> SupplierLocalities { get; set; }
        public DbSet<SupplierMarkup> SupplierMarkups { get; set; }
        public DbSet<SupplierPricelistLayout> SupplierPricelistLayouts { get; set; }
        public DbSet<PriceMarkup> PriceMarkups { get; set; }
        // УДАЛИТЬ эти строки:
        // public DbSet<Cart> Carts { get; set; }
        // public DbSet<CartItem> CartItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Конфигурация Brand
            modelBuilder.Entity<Brand>(entity =>
            {
                entity.HasIndex(b => b.NormalizedName).IsUnique();
                entity.Property(b => b.UpdatedAt)
                    .HasDefaultValueSql("NOW()")
                    .ValueGeneratedOnAddOrUpdate();
            });

            // Конфигурация BrandAlias
            modelBuilder.Entity<BrandAlias>(entity =>
            {
                entity.HasOne(ba => ba.Brand)
                    .WithMany(b => b.Aliases)
                    .HasForeignKey(ba => ba.BrandId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Конфигурация BrandMarkup
            modelBuilder.Entity<BrandMarkup>(entity =>
            {
                entity.HasOne(bm => bm.Brand)
                    .WithOne(b => b.Markup)
                    .HasForeignKey<BrandMarkup>(bm => bm.BrandId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Конфигурация Part
            modelBuilder.Entity<Part>(entity =>
            {
                entity.HasIndex(p => new { p.BrandId, p.Article }).IsUnique();

                entity.HasOne(p => p.Brand)
                    .WithMany(b => b.Parts)
                    .HasForeignKey(p => p.BrandId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(p => p.UpdatedAt)
                    .HasDefaultValueSql("NOW()")
                    .ValueGeneratedOnAddOrUpdate();
            });

            // Конфигурация PartImage
            modelBuilder.Entity<PartImage>(entity =>
            {
                entity.HasOne(pi => pi.Part)
                    .WithMany(p => p.Images)
                    .HasForeignKey(pi => pi.PartId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Конфигурация LocalStock
            modelBuilder.Entity<LocalStock>(entity =>
            {
                entity.HasIndex(ls => new { ls.PartId, ls.SupplierId }).IsUnique();

                entity.HasOne(ls => ls.Part)
                    .WithMany(p => p.LocalStocks)
                    .HasForeignKey(ls => ls.PartId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ls => ls.Supplier)
                    .WithMany(s => s.LocalStocks)
                    .HasForeignKey(ls => ls.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(ls => ls.UpdatedAt)
                    .HasDefaultValueSql("NOW()")
                    .ValueGeneratedOnAddOrUpdate();
            });

            // Конфигурация OemCross
            modelBuilder.Entity<OemCross>(entity =>
            {
                entity.HasIndex(oc => new { oc.OemPartId, oc.AftermarketPartId }).IsUnique();

                entity.HasOne(oc => oc.OemPart)
                    .WithMany(p => p.OemCrossesAsOem)
                    .HasForeignKey(oc => oc.OemPartId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(oc => oc.AftermarketPart)
                    .WithMany(p => p.OemCrossesAsAftermarket)
                    .HasForeignKey(oc => oc.AftermarketPartId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(oc => oc.UpdatedAt)
                    .HasDefaultValueSql("NOW()")
                    .ValueGeneratedOnAddOrUpdate();
            });

            // Конфигурация Supplier
            modelBuilder.Entity<Supplier>(entity =>
            {
                entity.HasOne(s => s.Locality)
                    .WithMany(l => l.Suppliers)
                    .HasForeignKey(s => s.LocalityId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(s => s.UpdatedAt)
                    .HasDefaultValueSql("NOW()")
                    .ValueGeneratedOnAddOrUpdate();
            });

            // Конфигурация SupplierMarkup
            modelBuilder.Entity<SupplierMarkup>(entity =>
            {
                entity.HasOne(sm => sm.Supplier)
                    .WithOne(s => s.Markup)
                    .HasForeignKey<SupplierMarkup>(sm => sm.SupplierId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(sm => sm.UpdatedAt)
                    .HasDefaultValueSql("NOW()")
                    .ValueGeneratedOnAddOrUpdate();
            });

            // Конфигурация SupplierPricelistLayout
            modelBuilder.Entity<SupplierPricelistLayout>(entity =>
            {
                entity.HasOne(spl => spl.Supplier)
                    .WithOne(s => s.PricelistLayout)
                    .HasForeignKey<SupplierPricelistLayout>(spl => spl.SupplierId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(spl => spl.UpdatedAt)
                    .HasDefaultValueSql("NOW()")
                    .ValueGeneratedOnAddOrUpdate();

                // Конфигурация для JSON-полей
                entity.Property(e => e.ColumnsMap)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                        v => JsonSerializer.Deserialize<JsonDocument>(v, (JsonSerializerOptions)null));

                entity.Property(e => e.Options)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                        v => JsonSerializer.Deserialize<JsonDocument>(v, (JsonSerializerOptions)null));
            });

            // Конфигурация PriceMarkup
            modelBuilder.Entity<PriceMarkup>(entity =>
            {
                entity.HasIndex(pm => pm.MaxPrice).IsUnique();

                entity.Property(pm => pm.UpdatedAt)
                    .HasDefaultValueSql("NOW()")
                    .ValueGeneratedOnAddOrUpdate();
            });
        }

        public override int SaveChanges()
        {
            AddTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            AddTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void AddTimestamps()
        {
            var entities = ChangeTracker.Entries()
                .Where(x => x.Entity is BaseEntity &&
                           (x.State == EntityState.Added || x.State == EntityState.Modified));

            foreach (var entity in entities)
            {
                var now = DateTime.UtcNow;

                if (entity.State == EntityState.Added)
                {
                    ((BaseEntity)entity.Entity).CreatedAt = now;
                }
                ((BaseEntity)entity.Entity).UpdatedAt = now;
            }
        }
    }

    public abstract class BaseEntity
    {
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}