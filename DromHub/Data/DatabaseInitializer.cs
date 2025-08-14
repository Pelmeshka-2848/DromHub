using DromHub.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace DromHub.Data
{
    public static class DatabaseInitializer
    {
        public static async Task InitializeAsync(ApplicationDbContext context)
        {
            await context.Database.EnsureCreatedAsync();

            if (await context.Brands.AnyAsync())
            {
                return; // База уже инициализирована
            }

            await SeedBrands(context);
            await SeedParts(context);
            await SeedSuppliers(context);
            await SeedLocalStock(context);
        }

        private static async Task SeedBrands(ApplicationDbContext context)
        {
            var brands = new[]
            {
                new Brand { Id = Guid.NewGuid(), Name = "Bosch", IsOem = false },
                new Brand { Id = Guid.NewGuid(), Name = "Mann-Filter", IsOem = false },
                new Brand { Id = Guid.NewGuid(), Name = "Toyota", IsOem = true },
                new Brand { Id = Guid.NewGuid(), Name = "Volkswagen", IsOem = true }
            };

            await context.Brands.AddRangeAsync(brands);
            await context.SaveChangesAsync();
        }

        private static async Task SeedParts(ApplicationDbContext context)
        {
            var toyota = await context.Brands.FirstAsync(b => b.Name == "Toyota");
            var bosch = await context.Brands.FirstAsync(b => b.Name == "Bosch");

            var parts = new[]
            {
                new Part
                {
                    Id = Guid.NewGuid(),
                    BrandId = toyota.Id,
                    CatalogNumber = "04152-YZZA1",
                    Name = "Масляный фильтр Toyota Corolla"
                },
                new Part
                {
                    Id = Guid.NewGuid(),
                    BrandId = bosch.Id,
                    CatalogNumber = "AF 0125",
                    Name = "Воздушный фильтр Bosch"
                },
                new Part
                {
                    Id = Guid.NewGuid(),
                    BrandId = toyota.Id,
                    CatalogNumber = "87110-02040",
                    Name = "Тормозной диск Toyota Camry"
                }
            };

            await context.Parts.AddRangeAsync(parts);
            await context.SaveChangesAsync();
        }

        private static async Task SeedSuppliers(ApplicationDbContext context)
        {
            var localities = new[]
            {
                new SupplierLocality { Id = 1, Code = "RU", Name = "Россия", DeliveryDays = 1 },
                new SupplierLocality { Id = 2, Code = "DE", Name = "Германия", DeliveryDays = 5 }
            };

            await context.SupplierLocalities.AddRangeAsync(localities);
            await context.SaveChangesAsync();

            var suppliers = new[]
            {
                new Supplier
                {
                    Id = Guid.NewGuid(),
                    Name = "АвтоДаптека",
                    Email = "info@apteka-auto.ru",
                    LocalityId = 1,
                    IsActive = true
                },
                new Supplier
                {
                    Id = Guid.NewGuid(),
                    Name = "German Auto Parts",
                    Email = "sales@gap.de",
                    LocalityId = 2,
                    IsActive = true
                }
            };

            await context.Suppliers.AddRangeAsync(suppliers);
            await context.SaveChangesAsync();
        }

        private static async Task SeedLocalStock(ApplicationDbContext context)
        {
            var supplier1 = await context.Suppliers.FirstAsync();
            var parts = await context.Parts.ToListAsync();

            var stocks = new[]
            {
                new LocalStock
                {
                    Id = Guid.NewGuid(),
                    PartId = parts[0].Id,
                    SupplierId = supplier1.Id,
                    Quantity = 10,
                    PriceIn = 1500.50m
                },
                new LocalStock
                {
                    Id = Guid.NewGuid(),
                    PartId = parts[1].Id,
                    SupplierId = supplier1.Id,
                    Quantity = 5,
                    PriceIn = 850.00m
                }
            };

            await context.LocalStocks.AddRangeAsync(stocks);
            await context.SaveChangesAsync();
        }
    }
}