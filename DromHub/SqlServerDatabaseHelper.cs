using Npgsql;
using System;
using System.Collections.Generic;

namespace DromHub
{
    public static class PostgresDatabaseHelper
    {
        public static readonly string connectionString = "Host=localhost;Port=5432;Username=postgres;Password=plane2004;Database=DromHubDB;";

        public static void InitializeDatabase()
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
        }

        public static List<Part> SearchPartsByNumber(string searchText)
        {
            var results = new List<Part>();
            if (string.IsNullOrWhiteSpace(searchText))
                return results;

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var cmd = new NpgsqlCommand(
                @"SELECT b.name AS Brand, p.catalog_number, ls.price_in
                  FROM parts p
                  JOIN brands b ON p.brand_id = b.id
                  LEFT JOIN local_stock ls ON ls.part_id = p.id
                  WHERE p.catalog_number LIKE @search", connection);
            cmd.Parameters.AddWithValue("search", $"%{searchText}%");

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new Part
                {
                    Brand = reader.GetString(0),
                    Number = reader.GetString(1),
                    Price = reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader.GetDecimal(2))
                });
            }
            return results;
        }
    }
}
