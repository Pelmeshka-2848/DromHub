using DromHub.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.Linq;
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

                await EnsurePartAuditInfrastructureAsync(context);

                await ClearDatabase(context, forceReset);

                // Убрал внешнюю транзакцию, так как она уже есть в SeedBrands
                await SeedBrands(context);
                await SeedParts(context);
                await SeedSuppliers(context);
                await SeedLocalStock(context);

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
        /// Метод ClearDatabase выполняет основную операцию класса.
        /// </summary>

        private static async Task ClearDatabase(ApplicationDbContext context, bool forceReset)
        {
            if (!forceReset)
            {
                Debug.WriteLine("Принудительное очищение БД не запрошено. Операция TRUNCATE пропущена.");
                return;
            }

            var tables = new[] { "local_stocks", "parts", "brand_aliases", "brands", "suppliers", "supplier_localities", "part_audit_log" };
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
        /// <para>Формирует инфраструктуру аудита запчастей, если она отсутствует в базе данных.</para>
        /// <para>Создает таблицу <c>part_audit_log</c>, индекс, функцию и триггер, обеспечивающие запись истории изменений.</para>
        /// <para>Вызывается при инициализации приложения, чтобы существующие инсталляции получили необходимые объекты без ручных миграций.</para>
        /// </summary>
        /// <param name="context">Активный контекст Entity Framework Core, используемый для выполнения SQL-команд.</param>
        /// <returns>Асинхронная задача, сигнализирующая о завершении проверки и (при необходимости) создания объектов.</returns>
        /// <exception cref="ArgumentNullException">Генерируется, когда <paramref name="context"/> не предоставлен.</exception>
        /// <exception cref="DbUpdateException">Выбрасывается, если инфраструктура EF Core не может выполнить DDL-команды.</exception>
        /// <exception cref="Npgsql.PostgresException">Возникает, когда сервер PostgreSQL отклоняет создание таблицы или триггера из-за ограничений безопасности либо конфликтов имен.</exception>
        /// <remarks>
        /// Предусловия: соединение с PostgreSQL активно; текущий пользователь обладает правами на создание таблиц и триггеров.<para/>
        /// Постусловия: таблица и триггер аудита существуют и готовы к записи; повторный вызов безопасен благодаря <c>IF NOT EXISTS</c> и <c>CREATE OR REPLACE</c>.<para/>
        /// Побочные эффекты: выполняет DDL-операции в схеме <c>public</c> базы данных; может инициировать перекомпиляцию зависимых планов.<para/>
        /// Потокобезопасность: метод не потокобезопасен; вызывайте его один раз при старте приложения.</remarks>
        private static async Task EnsurePartAuditInfrastructureAsync(ApplicationDbContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            const string sql = @"
CREATE TABLE IF NOT EXISTS public.part_audit_log
(
    event_id uuid PRIMARY KEY,
    part_id uuid NULL,
    action char(1) NOT NULL,
    changed_columns text[] NULL,
    old_data jsonb NULL,
    new_data jsonb NULL,
    actor text NULL,
    app_context text NULL,
    txid bigint NOT NULL,
    event_time timestamptz NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
    old_text text NULL,
    new_text text NULL
);

CREATE INDEX IF NOT EXISTS ix_part_audit_part_time
    ON public.part_audit_log (part_id, event_time DESC);

CREATE OR REPLACE FUNCTION public.fn_part_audit_capture()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_old jsonb;
    v_new jsonb;
    v_changed text[];
    v_event_id uuid := md5(random()::text || clock_timestamp()::text)::uuid;
BEGIN
    IF TG_OP = 'INSERT' THEN
        v_new := to_jsonb(NEW);
    ELSIF TG_OP = 'DELETE' THEN
        v_old := to_jsonb(OLD);
    ELSE
        v_old := to_jsonb(OLD);
        v_new := to_jsonb(NEW);
    END IF;

    IF TG_OP = 'UPDATE' THEN
        SELECT array_agg(diff.key ORDER BY diff.key)
        INTO v_changed
        FROM (
            SELECT DISTINCT key
            FROM jsonb_each(v_new) AS diff(key, value)
            WHERE v_old -> key IS DISTINCT FROM v_new -> key
              AND key <> 'updated_at'
        ) AS diff;
    END IF;

    IF TG_OP = 'UPDATE' AND (v_changed IS NULL OR array_length(v_changed, 1) = 0) THEN
        RETURN NULL;
    END IF;

    INSERT INTO public.part_audit_log (
        event_id,
        part_id,
        action,
        changed_columns,
        old_data,
        new_data,
        actor,
        app_context,
        txid,
        event_time,
        old_text,
        new_text)
    VALUES (
        v_event_id,
        COALESCE(NEW.id, OLD.id),
        CASE TG_OP WHEN 'INSERT' THEN 'I' WHEN 'UPDATE' THEN 'U' ELSE 'D' END,
        v_changed,
        v_old,
        v_new,
        current_user,
        current_setting('application_name', true),
        txid_current(),
        CURRENT_TIMESTAMP AT TIME ZONE 'UTC',
        CASE WHEN v_old IS NULL THEN NULL ELSE jsonb_pretty(v_old) END,
        CASE WHEN v_new IS NULL THEN NULL ELSE jsonb_pretty(v_new) END
    );

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS trg_part_audit ON public.parts;
CREATE TRIGGER trg_part_audit
    AFTER INSERT OR UPDATE OR DELETE
    ON public.parts
    FOR EACH ROW
    EXECUTE FUNCTION public.fn_part_audit_capture();
";

            await context.Database.ExecuteSqlRawAsync(sql);
        }
    }
}
