using Npgsql;
using System;
using System.Collections.Generic;

namespace DromHub
{
    public static class DataSeeder
    {
        private static readonly string connString = DatabaseHelper.ConnectionString;

        public static void SeedAll()
        {
            using var connection = new NpgsqlConnection(connString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                Console.WriteLine("Начинаем заполнение таблиц...");

                var brandIds = InsertBrands(connection, transaction);
                InsertBrandAliases(connection, transaction, brandIds);
                InsertBrandMarkups(connection, transaction, brandIds);

                var supplierLocalityIds = InsertSupplierLocalities(connection, transaction);
                var supplierIds = InsertSuppliers(connection, transaction, supplierLocalityIds);
                InsertSupplierMarkups(connection, transaction, supplierIds);
                InsertSupplierPricelistLayouts(connection, transaction, supplierIds);

                var partIds = InsertParts(connection, transaction, brandIds);
                InsertLocalStock(connection, transaction, partIds, supplierIds);
                InsertPartImages(connection, transaction, partIds);
                InsertOemCrosses(connection, transaction, partIds);
                InsertPriceMarkups(connection, transaction);

                transaction.Commit();

                Console.WriteLine("Заполнение таблиц прошло успешно.");
            }
            catch (PostgresException pgEx)
            {
                Console.WriteLine($"Postgres error ({pgEx.SqlState}): {pgEx.MessageText}");
                transaction.Rollback();
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Общая ошибка при заполнении: {ex.Message}");
                transaction.Rollback();
                throw;
            }
        }

        private static List<Guid> InsertBrands(NpgsqlConnection conn, NpgsqlTransaction tx)
        {
            Console.WriteLine("Вставляем бренды...");

            var brandIds = new List<Guid>();

            var brands = new List<(Guid id, string name, bool is_oem)>
            {
                (Guid.NewGuid(), "BrandA", false),
                (Guid.NewGuid(), "BrandB", true),
                (Guid.NewGuid(), "BrandC", false)
            };

            foreach (var brand in brands)
            {
                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO brands (id, name, is_oem, created_at, updated_at) 
                      VALUES (@id, @name, @is_oem, now(), now())", conn, tx);
                cmd.Parameters.AddWithValue("id", brand.id);
                cmd.Parameters.AddWithValue("name", brand.name);
                cmd.Parameters.AddWithValue("is_oem", brand.is_oem);
                cmd.ExecuteNonQuery();

                Console.WriteLine($"Добавлен бренд: {brand.name}");
                brandIds.Add(brand.id);
            }

            return brandIds;
        }

        private static void InsertBrandAliases(NpgsqlConnection conn, NpgsqlTransaction tx, List<Guid> brandIds)
        {
            Console.WriteLine("Вставляем брендовые алиасы...");

            using var cmd = new NpgsqlCommand(
                @"INSERT INTO brand_aliases (id, alias, brand_id, note) 
                  VALUES (gen_random_uuid(), @alias, @brand_id, NULL)", conn, tx);

            foreach (var brandId in brandIds)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("alias", $"Alias-{brandId.ToString().Substring(0, 8)}");
                cmd.Parameters.AddWithValue("brand_id", brandId);
                cmd.ExecuteNonQuery();

                Console.WriteLine($"Добавлен алиас для бренда {brandId}");
            }
        }

        private static void InsertBrandMarkups(NpgsqlConnection conn, NpgsqlTransaction tx, List<Guid> brandIds)
        {
            Console.WriteLine("Вставляем наценки для брендов...");

            using var cmd = new NpgsqlCommand(
                @"INSERT INTO brand_markups (id, brand_id, markup_pct, note, created_at, updated_at) 
                  VALUES (gen_random_uuid(), @brand_id, @markup_pct, NULL, now(), now())", conn, tx);

            var rnd = new Random();

            foreach (var brandId in brandIds)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("brand_id", brandId);
                // markup_pct - numeric, используем decimal
                decimal markup = (decimal)(rnd.NextDouble() * 0.2);
                cmd.Parameters.AddWithValue("markup_pct", markup);
                cmd.ExecuteNonQuery();

                Console.WriteLine($"Добавлена наценка {markup:P} для бренда {brandId}");
            }
        }

        private static List<int> InsertSupplierLocalities(NpgsqlConnection conn, NpgsqlTransaction tx)
        {
            Console.WriteLine("Вставляем локализации поставщиков...");

            var localityIds = new List<int> { 1, 2 };

            using var cmd = new NpgsqlCommand(
                @"INSERT INTO supplier_localities (id, code, name, delivery_days) 
                    VALUES (@id, @code, @name, @days)", conn, tx);

            var localities = new List<(int id, string code, string name, int days)>
            {
                (1, "LOC1", "Locality 1", 3),
                (2, "LOC2", "Locality 2", 5)
            };

            foreach (var loc in localities)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("id", loc.id);
                cmd.Parameters.AddWithValue("code", loc.code);
                cmd.Parameters.AddWithValue("name", loc.name);
                cmd.Parameters.AddWithValue("days", loc.days);
                cmd.ExecuteNonQuery();

                Console.WriteLine($"Добавлена локализация: {loc.name}");
            }

            return localityIds;
        }

        private static List<Guid> InsertSuppliers(NpgsqlConnection conn, NpgsqlTransaction tx, List<int> localityIds)
        {
            Console.WriteLine("Вставляем поставщиков...");

            var supplierIds = new List<Guid>();

            var suppliers = new List<(Guid id, string name, string? email, bool is_active, string price_source, int locality_id)>
            {
                (Guid.NewGuid(), "Supplier1", "supplier1@example.com", true, "disk", localityIds[0]),
                (Guid.NewGuid(), "Supplier2", null, true, "disk", localityIds[1])
            };

            foreach (var sup in suppliers)
            {
                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO suppliers (id, name, email, is_active, price_source, locality_id, created_at, updated_at) 
                      VALUES (@id, @name, @email, @is_active, @price_source, @locality_id, now(), now())", conn, tx);

                cmd.Parameters.AddWithValue("id", sup.id);
                cmd.Parameters.AddWithValue("name", sup.name);
                cmd.Parameters.AddWithValue("email", (object?)sup.email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("is_active", sup.is_active);
                cmd.Parameters.AddWithValue("price_source", sup.price_source);
                cmd.Parameters.AddWithValue("locality_id", sup.locality_id);

                cmd.ExecuteNonQuery();

                Console.WriteLine($"Добавлен поставщик: {sup.name}");

                supplierIds.Add(sup.id);
            }

            return supplierIds;
        }

        private static void InsertSupplierMarkups(NpgsqlConnection conn, NpgsqlTransaction tx, List<Guid> supplierIds)
        {
            Console.WriteLine("Вставляем наценки для поставщиков...");

            using var cmd = new NpgsqlCommand(
                @"INSERT INTO supplier_markups (id, supplier_id, markup_pct, note, created_at, updated_at) 
                  VALUES (gen_random_uuid(), @supplier_id, @markup_pct, NULL, now(), now())", conn, tx);

            var rnd = new Random();

            foreach (var supId in supplierIds)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("supplier_id", supId);
                decimal markup = (decimal)(rnd.NextDouble() * 0.2);
                cmd.Parameters.AddWithValue("markup_pct", markup);

                cmd.ExecuteNonQuery();

                Console.WriteLine($"Добавлена наценка {markup:P} для поставщика {supId}");
            }
        }

        private static void InsertSupplierPricelistLayouts(NpgsqlConnection conn, NpgsqlTransaction tx, List<Guid> supplierIds)
        {
            Console.WriteLine("Вставляем layouts прайслистов поставщиков...");

            using var cmd = new NpgsqlCommand(
                @"INSERT INTO supplier_pricelist_layouts 
                    (id, supplier_id, name, file_type, file_mask, columns_map, options, note, created_at, updated_at)
                  VALUES 
                    (gen_random_uuid(), @supplier_id, 'Default Layout', 'csv', NULL, '{}'::jsonb, NULL, NULL, now(), now())", conn, tx);

            foreach (var supId in supplierIds)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("supplier_id", supId);

                cmd.ExecuteNonQuery();

                Console.WriteLine($"Добавлен pricelist layout для поставщика {supId}");
            }
        }

        private static List<Guid> InsertParts(NpgsqlConnection conn, NpgsqlTransaction tx, List<Guid> brandIds)
        {
            Console.WriteLine("Вставляем детали...");

            var partIds = new List<Guid>();

            foreach (var brandId in brandIds)
            {
                var partId = Guid.NewGuid();

                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO parts (id, brand_id, catalog_number, name, created_at, updated_at)
      VALUES (@id, @brand_id, @catalog_number, @name, now(), now())", conn, tx);

                cmd.Parameters.AddWithValue("id", partId);
                cmd.Parameters.AddWithValue("brand_id", brandId);
                cmd.Parameters.AddWithValue("catalog_number", $"CAT-{partId.ToString().Substring(0, 8).ToUpper()}");
                cmd.Parameters.AddWithValue("name", $"Part-{partId.ToString().Substring(0, 4).ToUpper()}");


                cmd.ExecuteNonQuery();

                Console.WriteLine($"Добавлена деталь {partId} для бренда {brandId}");
                partIds.Add(partId);
            }

            return partIds;
        }

        private static void InsertLocalStock(NpgsqlConnection conn, NpgsqlTransaction tx, List<Guid> partIds, List<Guid> supplierIds)
        {
            Console.WriteLine("Вставляем локальные склады...");

            var rnd = new Random();

            foreach (var partId in partIds)
            {
                foreach (var supId in supplierIds)
                {
                    using var cmd = new NpgsqlCommand(
                        @"INSERT INTO local_stock (id, part_id, supplier_id, qty, multiplicity, price_in, note, created_at, updated_at) 
                          VALUES (gen_random_uuid(), @part_id, @supplier_id, @qty, 1, @price_in, NULL, now(), now())", conn, tx);

                    cmd.Parameters.AddWithValue("part_id", partId);
                    cmd.Parameters.AddWithValue("supplier_id", supId);
                    cmd.Parameters.AddWithValue("qty", rnd.Next(1, 100));
                    decimal price = (decimal)(rnd.NextDouble() * 1000);
                    cmd.Parameters.AddWithValue("price_in", price);

                    cmd.ExecuteNonQuery();

                    Console.WriteLine($"Добавлен local_stock: part {partId}, supplier {supId}, qty {rnd}, price {price}");
                }
            }
        }

        private static void InsertPartImages(NpgsqlConnection conn, NpgsqlTransaction tx, List<Guid> partIds)
        {
            Console.WriteLine("Вставляем изображения деталей...");

            foreach (var partId in partIds)
            {
                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO part_images (id, part_id, url, is_primary, added_at, status) 
      VALUES (gen_random_uuid(), @part_id, @url, true, now(), 'pending')", conn, tx);


                cmd.Parameters.AddWithValue("part_id", partId);
                cmd.Parameters.AddWithValue("url", $"https://example.com/images/{partId}.jpg");

                cmd.ExecuteNonQuery();

                Console.WriteLine($"Добавлено изображение для детали {partId}");
            }
        }

        private static void InsertOemCrosses(NpgsqlConnection conn, NpgsqlTransaction tx, List<Guid> partIds)
        {
            Console.WriteLine("Вставляем OEM кроссы...");

            if (partIds.Count < 2)
            {
                Console.WriteLine("Недостаточно деталей для создания OEM кроссов.");
                return;
            }

            using var cmd = new NpgsqlCommand(
                @"INSERT INTO oem_crosses (id, oem_part_id, aftermarket_part_id, source, note, created_at, updated_at) 
                  VALUES (gen_random_uuid(), @oem_part_id, @aftermarket_part_id, 'seed', NULL, now(), now())", conn, tx);

            cmd.Parameters.AddWithValue("oem_part_id", partIds[0]);
            cmd.Parameters.AddWithValue("aftermarket_part_id", partIds[1]);

            cmd.ExecuteNonQuery();

            Console.WriteLine($"Добавлен OEM кросс между {partIds[0]} и {partIds[1]}");
        }

        private static void InsertPriceMarkups(NpgsqlConnection conn, NpgsqlTransaction tx)
        {
            Console.WriteLine("Вставляем ценовые наценки...");

            using var cmd = new NpgsqlCommand(
                @"INSERT INTO price_markups (id, max_price, markup_pct, note, created_at, updated_at) 
                  VALUES (gen_random_uuid(), 1000, 0.1, NULL, now(), now())", conn, tx);

            cmd.ExecuteNonQuery();

            Console.WriteLine("Добавлена ценовая наценка.");
        }
    }
}
