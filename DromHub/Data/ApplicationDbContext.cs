using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DromHub.Models;
using Microsoft.EntityFrameworkCore;

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
        /// <summary>
        /// Свойство ChangeLogPatches предоставляет доступ к данным ChangeLogPatches.
        /// </summary>
        public DbSet<ChangeLogPatch> ChangeLogPatches { get; set; }
        /// <summary>
        /// Свойство ChangeLogSections предоставляет доступ к данным ChangeLogSections.
        /// </summary>
        public DbSet<ChangeLogSection> ChangeLogSections { get; set; }
        /// <summary>
        /// Свойство ChangeLogEntries предоставляет доступ к данным ChangeLogEntries.
        /// </summary>
        public DbSet<ChangeLogEntry> ChangeLogEntries { get; set; }
        // УДАЛИТЬ эти строки:
        // public DbSet<Cart> Carts { get; set; }
        // public DbSet<CartItem> CartItems { get; set; }
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

            // Конфигурация ChangeLogPatch
            modelBuilder.Entity<ChangeLogPatch>(entity =>
            {
                entity.HasIndex(p => p.ReleaseDate);
                entity.HasIndex(p => p.SortOrder);

                entity.Property(p => p.Version)
                    .HasMaxLength(64)
                    .IsRequired();

                entity.Property(p => p.Title)
                    .HasMaxLength(256);

                entity.Property(p => p.SortOrder)
                    .HasDefaultValue(0);

                entity.HasMany(p => p.Sections)
                    .WithOne(s => s.Patch)
                    .HasForeignKey(s => s.PatchId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Конфигурация ChangeLogSection
            modelBuilder.Entity<ChangeLogSection>(entity =>
            {
                entity.HasIndex(s => new { s.PatchId, s.SortOrder });

                entity.Property(s => s.Title)
                    .HasMaxLength(256)
                    .IsRequired();

                entity.Property(s => s.Category)
                    .HasConversion<string>()
                    .HasMaxLength(32)
                    .IsRequired();

                entity.Property(s => s.SortOrder)
                    .HasDefaultValue(0);

                entity.HasMany(s => s.Entries)
                    .WithOne(e => e.Section)
                    .HasForeignKey(e => e.SectionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Конфигурация ChangeLogEntry
            modelBuilder.Entity<ChangeLogEntry>(entity =>
            {
                entity.HasIndex(e => new { e.SectionId, e.SortOrder });
                entity.HasIndex(e => e.BrandId);
                entity.HasIndex(e => e.PartId);

                entity.Property(e => e.ImpactLevel)
                    .HasConversion<string>()
                    .HasMaxLength(32)
                    .IsRequired();

                entity.Property(e => e.IconAsset)
                    .HasMaxLength(256);

                entity.Property(e => e.Description)
                    .IsRequired();

                entity.Property(e => e.SortOrder)
                    .HasDefaultValue(0);

                entity.HasOne(e => e.Brand)
                    .WithMany()
                    .HasForeignKey(e => e.BrandId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Part)
                    .WithMany()
                    .HasForeignKey(e => e.PartId)
                    .OnDelete(DeleteBehavior.SetNull);
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
        /// <summary>
        /// Метод SaveChanges выполняет основную операцию класса.
        /// </summary>

        public override int SaveChanges()
        {
            AddTimestamps();
            CaptureBrandRenameChangeLogEntries();
            return base.SaveChanges();
        }
        /// <summary>
        /// Метод SaveChangesAsync выполняет основную операцию класса.
        /// </summary>

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            AddTimestamps();
            CaptureBrandRenameChangeLogEntries();
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

        /// <summary>
        /// Формирует автоматические записи патчноутов при переименовании брендов, чтобы UI истории отражал реальные изменения данных.
        /// Метод анализирует отслеживаемые сущности <see cref="Brand"/>, группирует их в ежедневный патч и добавляет записи в раздел «Обновления брендов».
        /// </summary>
        /// <remarks>
        /// Предусловия: в контексте присутствуют изменённые сущности <see cref="Brand"/> с модифицированным свойством <see cref="Brand.Name"/>.
        /// Постусловия: при наличии переименований в коллекции <see cref="ChangeLogEntries"/> добавляются новые записи.
        /// Потокобезопасность: метод не потокобезопасен и рассчитан на использование внутри одного экземпляра контекста.
        /// Побочные эффекты: выполняет чтение и запись в таблицы <see cref="ChangeLogPatch"/>, <see cref="ChangeLogSection"/> и <see cref="ChangeLogEntry"/>.
        /// Сложность: O(n), где n — количество переименованных брендов в текущей транзакции.
        /// </remarks>
        private void CaptureBrandRenameChangeLogEntries()
        {
            var renameCandidates = ChangeTracker
                .Entries<Brand>()
                .Where(entry => entry.State == EntityState.Modified && entry.Property(nameof(Brand.Name)).IsModified)
                .Select(entry => new
                {
                    Entity = entry.Entity,
                    OriginalName = entry.Property(nameof(Brand.Name)).OriginalValue as string,
                    CurrentName = entry.Property(nameof(Brand.Name)).CurrentValue as string
                })
                .Where(result => !string.IsNullOrWhiteSpace(result.OriginalName)
                                 && !string.IsNullOrWhiteSpace(result.CurrentName)
                                 && !string.Equals(result.OriginalName, result.CurrentName, StringComparison.Ordinal))
                .ToList();

            if (renameCandidates.Count == 0)
            {
                return;
            }

            var utcNow = DateTime.UtcNow;
            var patch = GetOrCreateAutomaticPatch(utcNow);
            var section = GetOrCreateAutomaticBrandSection(patch);

            var nextSortOrder = section.Entries.Count > 0 ? section.Entries.Max(e => e.SortOrder) + 1 : 0;

            for (var index = 0; index < renameCandidates.Count; index++)
            {
                var candidate = renameCandidates[index];
                var entry = CreateBrandRenameEntry(candidate.Entity.Id, candidate.OriginalName!, candidate.CurrentName!, utcNow, nextSortOrder + index);
                entry.Section = section;
                entry.SectionId = section.Id;
                section.Entries.Add(entry);
                ChangeLogEntries.Add(entry);
            }
        }

        /// <summary>
        /// Находит или создаёт ежедневный патч автоматических изменений каталога с версией формата <c>auto-yyyyMMdd</c>.
        /// Используется для группировки всех автоматических событий в рамках суток.
        /// </summary>
        /// <param name="timestampUtc">Время изменения в UTC; дата используется для выбора патча.</param>
        /// <returns>Отслеживаемый <see cref="ChangeLogPatch"/> с инициализированной коллекцией разделов.</returns>
        /// <remarks>
        /// Потокобезопасность: метод не потокобезопасен; вызывайте только в пределах одного контекста EF.
        /// Побочные эффекты: при отсутствии подходящего патча создаёт новый объект и добавляет его в контекст.
        /// </remarks>
        private ChangeLogPatch GetOrCreateAutomaticPatch(DateTime timestampUtc)
        {
            var version = string.Format(CultureInfo.InvariantCulture, "auto-{0:yyyyMMdd}", timestampUtc);

            var patch = ChangeLogPatches.Local.FirstOrDefault(p => p.Version == version);

            if (patch is null)
            {
                patch = ChangeLogPatches
                    .Include(p => p.Sections)
                    .ThenInclude(s => s.Entries)
                    .FirstOrDefault(p => p.Version == version);
            }

            if (patch is null)
            {
                patch = new ChangeLogPatch
                {
                    Id = Guid.NewGuid(),
                    Version = version,
                    Title = "Автоматические обновления каталога",
                    ReleaseDate = timestampUtc,
                    SortOrder = int.MaxValue - 1024,
                    Sections = new List<ChangeLogSection>()
                };

                ChangeLogPatches.Add(patch);
            }
            else if (patch.Sections is null)
            {
                patch.Sections = new List<ChangeLogSection>();
            }

            return patch;
        }

        /// <summary>
        /// Гарантирует наличие раздела «Обновления брендов» внутри патча автоматических изменений и готовит список записей.
        /// Создаёт раздел при первом обращении, чтобы сохранить неизменным формат данных для UI.
        /// </summary>
        /// <param name="patch">Патч автоматических обновлений; не должен быть <see langword="null"/>.</param>
        /// <returns>Экземпляр <see cref="ChangeLogSection"/> с непустой коллекцией <see cref="ChangeLogSection.Entries"/>.</returns>
        /// <remarks>
        /// Потокобезопасность: не потокобезопасен; используйте в рамках одного контекста EF.
        /// Побочные эффекты: при отсутствии раздела добавляет новую запись в <see cref="ChangeLogSections"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="patch"/> равен <see langword="null"/>.</exception>
        private ChangeLogSection GetOrCreateAutomaticBrandSection(ChangeLogPatch patch)
        {
            if (patch is null)
            {
                throw new ArgumentNullException(nameof(patch));
            }

            var section = patch.Sections.FirstOrDefault(s => s.Category == ChangeLogCategory.Brand);

            if (section is null)
            {
                section = new ChangeLogSection
                {
                    Id = Guid.NewGuid(),
                    Patch = patch,
                    PatchId = patch.Id,
                    Title = "Обновления брендов",
                    Category = ChangeLogCategory.Brand,
                    SortOrder = patch.Sections.Count > 0 ? patch.Sections.Max(s => s.SortOrder) + 1 : 0,
                    Entries = new List<ChangeLogEntry>()
                };

                patch.Sections.Add(section);
                ChangeLogSections.Add(section);
            }
            else if (section.Entries is null)
            {
                section.Entries = new List<ChangeLogEntry>();
            }

            return section;
        }

        /// <summary>
        /// Создаёт сущность патчноута, описывающую переименование бренда с указанием временной метки.
        /// Используется для добавления записей в раздел автоматических изменений без дублирования логики форматирования.
        /// </summary>
        /// <param name="brandId">Идентификатор бренда; не должен быть <see cref="Guid.Empty"/>.</param>
        /// <param name="originalName">Старое имя бренда; непустая строка.</param>
        /// <param name="currentName">Новое имя бренда; непустая строка.</param>
        /// <param name="timestampUtc">Время фиксации изменения в UTC.</param>
        /// <param name="sortOrder">Порядок следования записи в разделе; неотрицательное значение.</param>
        /// <returns>Инициализированный <see cref="ChangeLogEntry"/>, готовый к добавлению в контекст.</returns>
        /// <remarks>
        /// Потокобезопасность: метод детерминирован и безопасен при вызове из одного потока.
        /// Побочные эффекты отсутствуют — возвращается новый объект без модификации контекста.
        /// </remarks>
        /// <exception cref="ArgumentException">Выбрасывается, когда передан пустой идентификатор бренда либо пустые имена.</exception>
        private static ChangeLogEntry CreateBrandRenameEntry(Guid brandId, string originalName, string currentName, DateTime timestampUtc, int sortOrder)
        {
            if (brandId == Guid.Empty)
            {
                throw new ArgumentException("Идентификатор бренда не должен быть пустым.", nameof(brandId));
            }

            if (string.IsNullOrWhiteSpace(originalName))
            {
                throw new ArgumentException("Старое имя бренда должно быть указано.", nameof(originalName));
            }

            if (string.IsNullOrWhiteSpace(currentName))
            {
                throw new ArgumentException("Новое имя бренда должно быть указано.", nameof(currentName));
            }

            var headline = string.Format(CultureInfo.InvariantCulture, "Переименование бренда: {0} → {1}", originalName, currentName);
            var description = string.Format(
                CultureInfo.InvariantCulture,
                "Название бренда обновлено с \"{0}\" на \"{1}\" {2:dd.MM.yyyy HH:mm} UTC для единообразия каталога.",
                originalName,
                currentName,
                timestampUtc);

            return new ChangeLogEntry
            {
                Id = Guid.NewGuid(),
                BrandId = brandId,
                Headline = headline,
                Description = description,
                ImpactLevel = ChangeLogImpactLevel.Medium,
                IconAsset = "/Assets/pencil.and.outline.svg",
                SortOrder = sortOrder
            };
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