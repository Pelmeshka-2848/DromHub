using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DromHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DromHub.Data
{
    /// <summary>
    /// Класс ApplicationDbContext отвечает за логику компонента ApplicationDbContext.
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        /// <summary>
        /// Конструктор ApplicationDbContext инициализирует экземпляр класса.
        /// </summary>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets для всех таблиц (УДАЛИТЬ Cart и CartItem)
        /// <summary>
        /// Свойство Brands предоставляет доступ к данным Brands.
        /// </summary>
        public DbSet<Brand> Brands { get; set; }
        /// <summary>
        /// Свойство BrandAliases предоставляет доступ к данным BrandAliases.
        /// </summary>
        public DbSet<BrandAlias> BrandAliases { get; set; }
        /// <summary>
        /// Свойство BrandMarkups предоставляет доступ к данным BrandMarkups.
        /// </summary>
        public DbSet<BrandMarkup> BrandMarkups { get; set; }
        /// <summary>
        /// Свойство Parts предоставляет доступ к данным Parts.
        /// </summary>
        public DbSet<Part> Parts { get; set; }
        /// <summary>
        /// Свойство PartImages предоставляет доступ к данным PartImages.
        /// </summary>
        public DbSet<PartImage> PartImages { get; set; }
        /// <summary>
        /// Свойство LocalStocks предоставляет доступ к данным LocalStocks.
        /// </summary>
        public DbSet<LocalStock> LocalStocks { get; set; }
        /// <summary>
        /// Свойство OemCrosses предоставляет доступ к данным OemCrosses.
        /// </summary>
        public DbSet<OemCross> OemCrosses { get; set; }
        /// <summary>
        /// Свойство Suppliers предоставляет доступ к данным Suppliers.
        /// </summary>
        public DbSet<Supplier> Suppliers { get; set; }
        /// <summary>
        /// Свойство SupplierLocalities предоставляет доступ к данным SupplierLocalities.
        /// </summary>
        public DbSet<SupplierLocality> SupplierLocalities { get; set; }
        /// <summary>
        /// Свойство SupplierMarkups предоставляет доступ к данным SupplierMarkups.
        /// </summary>
        public DbSet<SupplierMarkup> SupplierMarkups { get; set; }
        /// <summary>
        /// Свойство SupplierPricelistLayouts предоставляет доступ к данным SupplierPricelistLayouts.
        /// </summary>
        public DbSet<SupplierPricelistLayout> SupplierPricelistLayouts { get; set; }
        /// <summary>
        /// Свойство PriceMarkups предоставляет доступ к данным PriceMarkups.
        /// </summary>
        public DbSet<PriceMarkup> PriceMarkups { get; set; }
        /// <summary>
        /// Свойство Countries предоставляет доступ к данным Countries.
        /// </summary>
        public DbSet<Country> Countries { get; set; }
        // УДАЛИТЬ эти строки:
        // public DbSet<Cart> Carts { get; set; }
        // public DbSet<CartItem> CartItems { get; set; }
        public DbSet<BrandAuditLog> BrandAuditLogs => Set<BrandAuditLog>();


        /// <summary>
        /// Метод OnModelCreating выполняет основную операцию класса.
        /// </summary>

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

            modelBuilder.Entity<BrandAuditLog>(ConfigureBrandAudit);
        }

        private static void ConfigureBrandAudit(EntityTypeBuilder<BrandAuditLog> e)
        {
            e.ToTable("brand_audit_log");
            e.HasKey(x => x.Id);

            e.Property(x => x.EventId).HasColumnName("event_id");
            e.Property(x => x.BrandId).HasColumnName("brand_id");
            e.Property(x => x.Action).HasColumnName("action");
            e.Property(x => x.ChangedColumns).HasColumnName("changed_columns").HasColumnType("text[]");

            e.Property(x => x.OldData).HasColumnName("old_data").HasColumnType("jsonb");
            e.Property(x => x.NewData).HasColumnName("new_data").HasColumnType("jsonb");

            e.Property(x => x.Actor).HasColumnName("actor");
            e.Property(x => x.AppContext).HasColumnName("app_context");
            e.Property(x => x.TxId).HasColumnName("txid");
            e.Property(x => x.EventTime).HasColumnName("event_time");

            // generated stored columns (read-only)
            e.Property(x => x.OldText).HasColumnName("old_text").ValueGeneratedOnAddOrUpdate();
            e.Property(x => x.NewText).HasColumnName("new_text").ValueGeneratedOnAddOrUpdate();

            e.HasIndex(x => new { x.BrandId, x.EventTime }).HasDatabaseName("ix_brand_audit_brand_time");
        }
        /// <summary>
        /// Метод SaveChanges выполняет основную операцию класса.
        /// </summary>

        public override int SaveChanges()
        {
            AddTimestamps();
            return base.SaveChanges();
        }
        /// <summary>
        /// Метод SaveChangesAsync выполняет основную операцию класса.
        /// </summary>

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            AddTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }
        /// <summary>
        /// Метод AddTimestamps выполняет основную операцию класса.
        /// </summary>

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
    /// <summary>
    /// Класс BaseEntity отвечает за логику компонента BaseEntity.
    /// </summary>

    public abstract class BaseEntity
    {
        /// <summary>
        /// Свойство CreatedAt предоставляет доступ к данным CreatedAt.
        /// </summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>
        /// Свойство UpdatedAt предоставляет доступ к данным UpdatedAt.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}