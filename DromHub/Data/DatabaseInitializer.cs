using DromHub.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DromHub.Data
{
    /// <summary>
    /// Класс DatabaseInitializer отвечает за логику компонента DatabaseInitializer.
    /// </summary>
    public static class DatabaseInitializer
    {
        /// <summary>
        /// Метод InitializeAsync выполняет основную операцию класса.
        /// </summary>
        public static async Task InitializeAsync(ApplicationDbContext context, bool forceReset = false)
        {
            try
            {
                await context.Database.EnsureCreatedAsync();

                await EnsureChangeLogSchemaAsync(context);

                await ClearDatabase(context, forceReset);

                // Убрал внешнюю транзакцию, так как она уже есть в SeedBrands
                await SeedBrands(context);
                await SeedParts(context);
                await SeedSuppliers(context);
                await SeedLocalStock(context);
                await SeedChangeLog(context);

                Debug.WriteLine("Инициализация БД успешно завершена");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка инициализации БД: {ex.ToString()}");
                throw;
            }
        }
        /// <summary>
        /// Метод SeedBrands выполняет основную операцию класса.
        /// </summary>

        private static async Task SeedBrands(ApplicationDbContext context)
        {
            // Создаем стратегию выполнения с повторными попытками
            var executionStrategy = context.Database.CreateExecutionStrategy();

            await executionStrategy.ExecuteAsync(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    // Список брендов для добавления
                    var brandsToAdd = new[]
                    {
                new { Name = "Bosch", IsOem = false },
                new { Name = "Mann-Filter", IsOem = false },
                new { Name = "Toyota", IsOem = true },
                new { Name = "Volkswagen", IsOem = true },
                new { Name = "Honda", IsOem = true },
                new { Name = "BMW", IsOem = true },
                new { Name = "Mercedes-Benz", IsOem = true },
                new { Name = "Mahle", IsOem = false },
                new { Name = "Febi Bilstein", IsOem = false },
                new { Name = "Sakura", IsOem = false }
            };

                    foreach (var brand in brandsToAdd)
                    {
                        // Проверяем существование бренда по имени (без учета регистра)
                        var exists = await context.Brands
                            .AnyAsync(b => EF.Functions.ILike(b.Name, brand.Name));

                        if (!exists)
                        {
                            await context.Brands.AddAsync(new Brand
                            {
                                Id = Guid.NewGuid(),
                                Name = brand.Name,
                                IsOem = brand.IsOem
                            });
                        }
                    }

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        /// <summary>
        /// Метод SeedParts выполняет основную операцию класса.
        /// </summary>

        private static async Task SeedParts(ApplicationDbContext context)
        {
            try
            {
                // Get or create required brands
                var toyota = await GetOrCreateBrand(context, "Toyota");
                var bosch = await GetOrCreateBrand(context, "Bosch");
                var mann = await GetOrCreateBrand(context, "Mann-Filter");
                var vw = await GetOrCreateBrand(context, "Volkswagen");
                var honda = await GetOrCreateBrand(context, "Honda");

                // List of parts to add
                var partsToAdd = new[]
                {
                    new { Brand = toyota, CatalogNumber = "04152-YZZA1", Name = "Масляный фильтр Toyota Corolla" },
                    new { Brand = toyota, CatalogNumber = "17801-0P010", Name = "Воздушный фильтр Toyota Camry" },
                    new { Brand = bosch, CatalogNumber = "0 451 103 316", Name = "Топливный фильтр Bosch" },
                    new { Brand = mann, CatalogNumber = "WK 842/3", Name = "Салонный фильтр Mann" },
                    new { Brand = vw, CatalogNumber = "06A115561B", Name = "Свеча зажигания VW Golf" },
                    new { Brand = honda, CatalogNumber = "15400-PLM-A01", Name = "Масляный фильтр Honda Civic" },
                    new { Brand = bosch, CatalogNumber = "0 986 452 211", Name = "Тормозной диск Bosch" },
                    new { Brand = mann, CatalogNumber = "HU 925/4 X", Name = "Масляный фильтр Mann" },
                    new { Brand = vw, CatalogNumber = "1K0123301E", Name = "Амортизатор VW Passat" },
                    new { Brand = honda, CatalogNumber = "17220-R60-A01", Name = "Ремень ГРМ Honda Accord" }
                };

                foreach (var part in partsToAdd)
                {
                    var exists = await context.Parts
                        .AnyAsync(p => p.CatalogNumber == part.CatalogNumber);

                    if (!exists)
                    {
                        await context.Parts.AddAsync(new Part
                        {
                            Id = Guid.NewGuid(),
                            BrandId = part.Brand.Id,
                            CatalogNumber = part.CatalogNumber,
                            Name = part.Name,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка в SeedParts: {ex}");
                throw;
            }
        }
        /// <summary>
        /// Метод GetOrCreateBrand выполняет основную операцию класса.
        /// </summary>

        private static async Task<Brand> GetOrCreateBrand(ApplicationDbContext context, string brandName)
        {
            var brand = await context.Brands
                .FirstOrDefaultAsync(b => b.Name == brandName);

            if (brand == null)
            {
                brand = new Brand
                {
                    Id = Guid.NewGuid(),
                    Name = brandName,
                    IsOem = brandName == "Toyota" || brandName == "Volkswagen" || brandName == "Honda"
                };
                context.Brands.Add(brand);
                await context.SaveChangesAsync();
            }

            return brand;
        }
        /// <summary>
        /// Метод SeedSuppliers выполняет основную операцию класса.
        /// </summary>

        private static async Task SeedSuppliers(ApplicationDbContext context)
        {
            // Check if supplier localities already exist
            if (!await context.SupplierLocalities.AnyAsync())
            {
                var localities = new[]
                {
            new SupplierLocality { Id = 1, Code = "RU", Name = "Россия", DeliveryDays = 1 },
            new SupplierLocality { Id = 2, Code = "DE", Name = "Германия", DeliveryDays = 5 },
            new SupplierLocality { Id = 3, Code = "JP", Name = "Япония", DeliveryDays = 7 },
            new SupplierLocality { Id = 4, Code = "CN", Name = "Китай", DeliveryDays = 10 },
            new SupplierLocality { Id = 5, Code = "US", Name = "США", DeliveryDays = 8 }
        };

                await context.SupplierLocalities.AddRangeAsync(localities);
                await context.SaveChangesAsync();
            }

            // Add suppliers if they don't exist
            if (!await context.Suppliers.AnyAsync())
            {
                var suppliers = new[]
                {
            new Supplier
            {
                Id = Guid.NewGuid(),
                Name = "Автодеталь-Сервис",
                LocalityId = 1
            },
            new Supplier
            {
                Id = Guid.NewGuid(),
                Name = "German Auto Parts",
                LocalityId = 2
            },
            new Supplier
            {
                Id = Guid.NewGuid(),
                Name = "Japan Spare Inc",
                LocalityId = 3
            },
            new Supplier
            {
                Id = Guid.NewGuid(),
                Name = "China Auto Export",
                LocalityId = 4
            }
        };

                await context.Suppliers.AddRangeAsync(suppliers);
                await context.SaveChangesAsync();
            }
        }
        /// <summary>
        /// Метод SeedLocalStock выполняет основную операцию класса.
        /// </summary>

        private static async Task SeedLocalStock(ApplicationDbContext context)
        {
            if (!await context.LocalStocks.AnyAsync())
            {
                var suppliers = await context.Suppliers.ToListAsync();
                var parts = await context.Parts.ToListAsync();

                if (suppliers.Count == 0 || parts.Count == 0)
                {
                    Debug.WriteLine("Не удалось создать LocalStock - отсутствуют поставщики или детали");
                    return;
                }

                var random = new Random();
                var stocks = new System.Collections.Generic.List<LocalStock>();

                // Create stock entries for each part with random suppliers
                foreach (var part in parts)
                {
                    // Each part will be available from 1-3 suppliers
                    var supplierCount = random.Next(1, 4);
                    var selectedSuppliers = suppliers.OrderBy(x => random.Next()).Take(supplierCount);

                    foreach (var supplier in selectedSuppliers)
                    {
                        stocks.Add(new LocalStock
                        {
                            Id = Guid.NewGuid(),
                            PartId = part.Id,
                            SupplierId = supplier.Id,
                            Quantity = random.Next(1, 50),
                            PriceIn = Math.Round((decimal)(random.NextDouble() * 1000 + 100), 2),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                await context.LocalStocks.AddRangeAsync(stocks);
                await context.SaveChangesAsync();
            }
        }
        /// <summary>
        /// Метод SeedChangeLog выполняет основную операцию класса.
        /// </summary>

        private static async Task SeedChangeLog(ApplicationDbContext context)
        {
            if (await context.ChangeLogPatches.AnyAsync())
            {
                return;
            }

            var brandLookup = await context.Brands
                .AsNoTracking()
                .ToDictionaryAsync(b => b.Name, StringComparer.OrdinalIgnoreCase);

            var partLookup = await context.Parts
                .AsNoTracking()
                .ToDictionaryAsync(p => p.CatalogNumber, StringComparer.OrdinalIgnoreCase);

            Guid ResolveBrand(string name) =>
                brandLookup.TryGetValue(name, out var brand)
                    ? brand.Id
                    : throw new InvalidOperationException($"Не удалось найти бренд '{name}' для сидов ChangeLog.");

            Guid ResolvePart(string catalogNumber) =>
                partLookup.TryGetValue(catalogNumber, out var part)
                    ? part.Id
                    : throw new InvalidOperationException($"Не удалось найти деталь '{catalogNumber}' для сидов ChangeLog.");

            var toyotaFilterId = ResolvePart("04152-YZZA1");
            var camryAirFilterId = ResolvePart("17801-0P010");
            var boschBrakeDiscId = ResolvePart("0 986 452 211");
            var mannCabinFilterId = ResolvePart("WK 842/3");

            var toyotaBrandId = ResolveBrand("Toyota");
            var boschBrandId = ResolveBrand("Bosch");
            var mannBrandId = ResolveBrand("Mann-Filter");

            var toyotaStock = await context.LocalStocks
                .AsNoTracking()
                .Include(ls => ls.Supplier)
                .FirstOrDefaultAsync(ls => ls.PartId == toyotaFilterId);

            var mannStock = await context.LocalStocks
                .AsNoTracking()
                .Include(ls => ls.Supplier)
                .FirstOrDefaultAsync(ls => ls.PartId == mannCabinFilterId);

            var fallPatch = new ChangeLogPatch
            {
                Id = Guid.NewGuid(),
                Version = "2024.9",
                Title = "Осенний фокус",
                ReleaseDate = new DateTime(2024, 9, 17, 0, 0, 0, DateTimeKind.Utc),
                SortOrder = 0,
                Sections = new List<ChangeLogSection>
                {
                    new ChangeLogSection
                    {
                        Id = Guid.NewGuid(),
                        Title = "Брендовые инициативы",
                        Category = ChangeLogCategory.Brand,
                        SortOrder = 0,
                        Entries = new List<ChangeLogEntry>
                        {
                            new ChangeLogEntry
                            {
                                Id = Guid.NewGuid(),
                                BrandId = toyotaBrandId,
                                Headline = "Toyota: новый раздел про наследие",
                                Description = "Добавили расширенный рассказ о происхождении бренда и выделили достижения в гибридных технологиях. Описание автоматически отображается на карточке бренда.",
                                ImpactLevel = ChangeLogImpactLevel.Medium,
                                IconAsset = "/Assets/globe.svg",
                                SortOrder = 0
                            },
                            new ChangeLogEntry
                            {
                                Id = Guid.NewGuid(),
                                BrandId = boschBrandId,
                                Headline = "Bosch получает акцент на инновации",
                                Description = "На странице бренда появился блок с ключевыми инновациями и ссылками на свежие пресс-релизы. Это помогает продавцам подсвечивать сильные стороны марки.",
                                ImpactLevel = ChangeLogImpactLevel.Low,
                                IconAsset = "/Assets/star.fill.svg",
                                SortOrder = 1
                            }
                        }
                    },
                    new ChangeLogSection
                    {
                        Id = Guid.NewGuid(),
                        Title = "Цены и склад",
                        Category = ChangeLogCategory.Pricing,
                        SortOrder = 1,
                        Entries = new List<ChangeLogEntry>
                        {
                            new ChangeLogEntry
                            {
                                Id = Guid.NewGuid(),
                                PartId = toyotaFilterId,
                                Headline = "Camry: перерасчёт локального склада",
                                Description = toyotaStock is null
                                    ? "Пересчитали рекомендованную цену и остатки фильтров для Toyota Camry в локальном складе."
                                    : $"Пересчитали цену закупки и остатки фильтров Toyota Camry для поставщика {toyotaStock.Supplier?.Name ?? "—"}. Цена снижена на 7% для синхронизации с RRP.",
                                ImpactLevel = ChangeLogImpactLevel.High,
                                IconAsset = "/Assets/chart.line.uptrend.xyaxis.svg",
                                SortOrder = 0
                            },
                            new ChangeLogEntry
                            {
                                Id = Guid.NewGuid(),
                                PartId = mannCabinFilterId,
                                Headline = "Mann-Filter: корректировка остатков",
                                Description = mannStock is null
                                    ? "Уточнили доступные остатки по популярным фильтрам Mann-Filter."
                                    : $"Уточнили доступные остатки Mann-Filter для поставщика {mannStock.Supplier?.Name ?? "—"}; данные теперь обновляются ежедневно.",
                                ImpactLevel = ChangeLogImpactLevel.Medium,
                                IconAsset = "/Assets/square.stack.3d.up.svg",
                                SortOrder = 1
                            }
                        }
                    },
                    new ChangeLogSection
                    {
                        Id = Guid.NewGuid(),
                        Title = "Обновления ассортимента",
                        Category = ChangeLogCategory.Parts,
                        SortOrder = 2,
                        Entries = new List<ChangeLogEntry>
                        {
                            new ChangeLogEntry
                            {
                                Id = Guid.NewGuid(),
                                PartId = camryAirFilterId,
                                Headline = "Camry: улучшили карточку фильтра",
                                Description = "Карточка воздушного фильтра получила уточнённые размеры и список совместимых поколений авто. На страницу добавлены фотографии высокого разрешения.",
                                ImpactLevel = ChangeLogImpactLevel.Medium,
                                IconAsset = "/Assets/fan.svg",
                                SortOrder = 0
                            },
                            new ChangeLogEntry
                            {
                                Id = Guid.NewGuid(),
                                PartId = boschBrakeDiscId,
                                Headline = "Bosch: новая совместимость",
                                Description = "Тормозные диски Bosch теперь показываются в подборе для Volkswagen Golf и Passat благодаря расширенному кроссу. Фильтры OEM обновлены автоматически.",
                                ImpactLevel = ChangeLogImpactLevel.High,
                                IconAsset = "/Assets/steeringwheel.svg",
                                SortOrder = 1
                            }
                        }
                    }
                }
            };

            var summerPatch = new ChangeLogPatch
            {
                Id = Guid.NewGuid(),
                Version = "2024.6",
                Title = "Летний баланс",
                ReleaseDate = new DateTime(2024, 6, 5, 0, 0, 0, DateTimeKind.Utc),
                SortOrder = 1,
                Sections = new List<ChangeLogSection>
                {
                    new ChangeLogSection
                    {
                        Id = Guid.NewGuid(),
                        Title = "Общее",
                        Category = ChangeLogCategory.General,
                        SortOrder = 0,
                        Entries = new List<ChangeLogEntry>
                        {
                            new ChangeLogEntry
                            {
                                Id = Guid.NewGuid(),
                                Headline = "Глобальный поиск стал умнее",
                                Description = "Добавили подсветку брендов и деталей в результате поиска по каталогу. Это изменение видно всем пользователям, независимо от выбранного бренда.",
                                ImpactLevel = ChangeLogImpactLevel.Medium,
                                IconAsset = "/Assets/list.bullet.svg",
                                SortOrder = 0
                            }
                        }
                    },
                    new ChangeLogSection
                    {
                        Id = Guid.NewGuid(),
                        Title = "Логистика",
                        Category = ChangeLogCategory.Logistics,
                        SortOrder = 1,
                        Entries = new List<ChangeLogEntry>
                        {
                            new ChangeLogEntry
                            {
                                Id = Guid.NewGuid(),
                                BrandId = mannBrandId,
                                Headline = "Mann-Filter: обновлённые сроки",
                                Description = "Срок поставки из Германии сокращён до 5 дней благодаря новой схеме консолидации заказов. Эти данные теперь отображаются в карточке поставщика.",
                                ImpactLevel = ChangeLogImpactLevel.High,
                                IconAsset = "/Assets/shippingbox.svg",
                                SortOrder = 0
                            }
                        }
                    }
                }
            };

            await context.ChangeLogPatches.AddRangeAsync(fallPatch, summerPatch);
            await context.SaveChangesAsync();
        }
        /// <summary>
        /// Метод ClearDatabase выполняет основную операцию класса.
        /// </summary>

        private static async Task ClearDatabase(ApplicationDbContext context, bool forceReset)
        {
            if (!forceReset)
            {
                Debug.WriteLine("Принудительное очищение БД не запрошено. Операция TRUNCATE пропущена.");
                return;
            }

            var tables = new[]
            {
                "change_log_entries",
                "change_log_sections",
                "change_log_patches",
                "local_stocks",
                "parts",
                "brand_aliases",
                "brands",
                "suppliers",
                "supplier_localities"
            };
            var allSucceeded = true;

            foreach (var table in tables)
            {
                try
                {
                    await context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE IF EXISTS \"{table}\" RESTART IDENTITY CASCADE");
                    Debug.WriteLine($"Таблица {table} успешно очищена");
                }
                catch (Exception ex)
                {
                    allSucceeded = false;
                    Debug.WriteLine($"Не удалось очистить таблицу {table}: {ex.Message}");
                }
            }

            if (allSucceeded)
            {
                Debug.WriteLine("Очистка базы данных успешно завершена.");
            }
            else
            {
                Debug.WriteLine("Очистка базы данных завершена с ошибками. См. сообщения выше.");
            }
        }

        /// <summary>
        /// Гарантирует наличие таблиц патчноутов в PostgreSQL, создавая их и индексы при первом развёртывании без миграций.
        /// Используйте метод при инициализации разработки, когда схема уже существует частично и <see cref="DbContext.Database.EnsureCreatedAsync(System.Threading.CancellationToken)"/> не добавляет новые сущности.
        /// Соблюдает идемпотентность: повторные вызовы не влияют на существующую структуру, что позволяет выполнять метод при каждом старте приложения без риска повредить данные.
        /// </summary>
        /// <param name="context">Экземпляр <see cref="ApplicationDbContext"/>, обеспечивающий доступ к соединению и исполняющий DDL-скрипты.</param>
        /// <param name="cancellationToken">Токен отмены операции; при отмене возбуждает <see cref="OperationCanceledException"/> и не создаёт частично подготовленных таблиц.</param>
        /// <returns>Задача, завершающаяся после проверки наличия всех таблиц и, при необходимости, их создания.</returns>
        /// <exception cref="ArgumentNullException">Возникает, если <paramref name="context"/> не указан, поскольку требуется активный контекст БД.</exception>
        /// <exception cref="DbException">Пробрасывается при ошибках подключения или DDL-операций в PostgreSQL.</exception>
        /// <remarks>
        /// Предусловия: открыт доступ к PostgreSQL и корректно настроены базовые таблицы (бренды, детали), так как внешние ключи ссылаются на них.
        /// Постусловия: таблицы <c>change_log_patches</c>, <c>change_log_sections</c>, <c>change_log_entries</c> существуют с ожидаемыми индексами и ограничениями каскадного удаления.
        /// Побочные эффекты: выполняет DDL-команды <see cref="Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade, string, CancellationToken)"/>, влияющие на схему БД.
        /// Потокобезопасность: метод не потокобезопасен; вызывайте его в единственном потоке инициализации.
        /// Идемпотентность: полная; при наличии таблиц метод только проверяет их наличие без модификаций.
        /// Сложность: O(n) по количеству проверяемых таблиц, так как выполняется по одному запросу на таблицу.
        /// </remarks>
        /// <example>
        /// <code>
        /// await DatabaseInitializer.EnsureChangeLogSchemaAsync(context);
        /// </code>
        /// </example>
        private static async Task EnsureChangeLogSchemaAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var creationScripts = new Dictionary<string, (string Table, IReadOnlyList<string> Indexes)>
            {
                ["change_log_patches"] =
                (
                    """
                    CREATE TABLE IF NOT EXISTS "change_log_patches"
                    (
                        "id" uuid NOT NULL,
                        "version" character varying(64) NOT NULL,
                        "title" character varying(256),
                        "release_date" timestamp without time zone NOT NULL,
                        "sort_order" integer NOT NULL DEFAULT 0,
                        CONSTRAINT "pk_change_log_patches" PRIMARY KEY ("id")
                    );
                    """,
                    new[]
                    {
                        "CREATE INDEX IF NOT EXISTS \"ix_change_log_patches_release_date\" ON \"change_log_patches\" (\"release_date\");",
                        "CREATE INDEX IF NOT EXISTS \"ix_change_log_patches_sort_order\" ON \"change_log_patches\" (\"sort_order\");"
                    }
                ),
                ["change_log_sections"] =
                (
                    """
                    CREATE TABLE IF NOT EXISTS "change_log_sections"
                    (
                        "id" uuid NOT NULL,
                        "patch_id" uuid NOT NULL,
                        "title" character varying(256) NOT NULL,
                        "category" character varying(32) NOT NULL,
                        "sort_order" integer NOT NULL DEFAULT 0,
                        CONSTRAINT "pk_change_log_sections" PRIMARY KEY ("id"),
                        CONSTRAINT "fk_change_log_sections_change_log_patches_patch_id" FOREIGN KEY ("patch_id") REFERENCES "change_log_patches" ("id") ON DELETE CASCADE
                    );
                    """,
                    new[]
                    {
                        "CREATE INDEX IF NOT EXISTS \"ix_change_log_sections_patch_id_sort_order\" ON \"change_log_sections\" (\"patch_id\", \"sort_order\");"
                    }
                ),
                ["change_log_entries"] =
                (
                    """
                    CREATE TABLE IF NOT EXISTS "change_log_entries"
                    (
                        "id" uuid NOT NULL,
                        "section_id" uuid NOT NULL,
                        "headline" character varying(256),
                        "description" text NOT NULL,
                        "impact_level" character varying(32) NOT NULL,
                        "icon_asset" character varying(256),
                        "brand_id" uuid,
                        "part_id" uuid,
                        "sort_order" integer NOT NULL DEFAULT 0,
                        CONSTRAINT "pk_change_log_entries" PRIMARY KEY ("id"),
                        CONSTRAINT "fk_change_log_entries_change_log_sections_section_id" FOREIGN KEY ("section_id") REFERENCES "change_log_sections" ("id") ON DELETE CASCADE,
                        CONSTRAINT "fk_change_log_entries_brands_brand_id" FOREIGN KEY ("brand_id") REFERENCES "brands" ("id") ON DELETE SET NULL,
                        CONSTRAINT "fk_change_log_entries_parts_part_id" FOREIGN KEY ("part_id") REFERENCES "parts" ("id") ON DELETE SET NULL
                    );
                    """,
                    new[]
                    {
                        "CREATE INDEX IF NOT EXISTS \"ix_change_log_entries_section_id_sort_order\" ON \"change_log_entries\" (\"section_id\", \"sort_order\");",
                        "CREATE INDEX IF NOT EXISTS \"ix_change_log_entries_brand_id\" ON \"change_log_entries\" (\"brand_id\");",
                        "CREATE INDEX IF NOT EXISTS \"ix_change_log_entries_part_id\" ON \"change_log_entries\" (\"part_id\");"
                    }
                )
            };

            var connection = context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            try
            {
                foreach (var (tableName, scripts) in creationScripts)
                {
                    if (await TableExistsAsync(connection, tableName, cancellationToken).ConfigureAwait(false))
                    {
                        continue;
                    }

                    await context.Database.ExecuteSqlRawAsync(scripts.Table, cancellationToken).ConfigureAwait(false);

                    foreach (var indexSql in scripts.Indexes)
                    {
                        await context.Database.ExecuteSqlRawAsync(indexSql, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Проверяет наличие таблицы в схеме PostgreSQL, использующейся контекстом, через системный вызов <c>to_regclass</c>.
        /// Метод применяется для безопасной идемпотентной инициализации: отсутствующая таблица однозначно сигнализирует о необходимости запуска DDL.
        /// </summary>
        /// <param name="connection">Открытое подключение <see cref="DbConnection"/> к целевой базе данных.</param>
        /// <param name="tableName">Имя таблицы в нижнем регистре без схемы, например <c>change_log_patches</c>.</param>
        /// <param name="cancellationToken">Токен отмены операции; при отмене генерирует <see cref="OperationCanceledException"/>.</param>
        /// <returns><c>true</c>, если таблица зарегистрирована в схеме <c>public</c>; иначе <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Выбрасывается, когда <paramref name="connection"/> не задан.</exception>
        /// <exception cref="DbException">Возникает, если PostgreSQL отклоняет запрос проверки существования таблицы.</exception>
        /// <remarks>
        /// Предусловия: подключение находится в состоянии <see cref="ConnectionState.Open"/>.
        /// Постусловия: состояние подключения не изменяется, параметры команды очищаются.
        /// Потокобезопасность: не потокобезопасен; используйте отдельное подключение на поток.
        /// Сложность: O(1), выполняется один запрос <c>SELECT</c>.
        /// </remarks>
        /// <example>
        /// <code>
        /// var exists = await TableExistsAsync(connection, "change_log_entries", ct);
        /// </code>
        /// </example>
        private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(connection);

            await using var command = connection.CreateCommand();
            command.CommandText = "select to_regclass(@qualifiedName);";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@qualifiedName";
            parameter.Value = $"public.{tableName}";
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            return result is string { Length: > 0 };
        }
    }
}