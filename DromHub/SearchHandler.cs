using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

namespace DromHub
{
    public static class SearchHandler
    {
        private static readonly string connectionString = PostgresDatabaseHelper.connectionString;

        public static async Task LogToFileAsync(string text)
        {
            StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
            StorageFile logFile = await storageFolder.CreateFileAsync("debug_log.txt", CreationCollisionOption.OpenIfExists);
            await FileIO.AppendTextAsync(logFile, text + "\n");
        }

        public static async Task<List<Part>> SearchPartsByNumberAsync(string searchText)
        {
            var results = new List<Part>();

            if (string.IsNullOrWhiteSpace(searchText))
                return results;

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                string sql = 
                    @"
                    SELECT 
                        b.name AS Brand,
                        p.catalog_number AS Number,
                        p.name AS Name,
                        p.article AS Article,
                        COALESCE(ls.qty, 0) AS Qty,
                        COALESCE(s.name, '') AS Supplier,
                        COALESCE(ls.price_in, 0) AS Price
                    FROM parts p
                    JOIN brands b ON p.brand_id = b.id
                    LEFT JOIN local_stock ls ON ls.part_id = p.id
                    LEFT JOIN suppliers s ON ls.supplier_id = s.id
                    WHERE p.catalog_number ILIKE @search
                    LIMIT 100
                    ";

                using var cmd = new NpgsqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("search", $"%{searchText.Trim()}%");

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var part = new Part
                    {
                        Brand = reader.IsDBNull(0) ? "" : reader.GetString(0),
                        Number = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Name = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Article = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Qty = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                        Supplier = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        Price = reader.IsDBNull(6) ? 0 : Convert.ToDouble(reader.GetValue(6))
                    };

                    await LogToFileAsync($"Brand: {part.Brand}, Number: {part.Number}, Name: {part.Name}, Article: {part.Article}, Supplier: {part.Supplier}, Qty: {part.Qty}, Price: {part.Price}");

                    results.Add(part);
                }
            }
            catch (Exception ex)
            {
                await LogToFileAsync("Error: " + ex.Message);
            }

            return results;
        }
    }
}
