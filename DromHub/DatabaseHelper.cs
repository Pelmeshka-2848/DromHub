using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace DromHub
{

    public static class DatabaseHelper
    {
        public static readonly string DbPath = Path.Combine(AppContext.BaseDirectory, "Assets", "parts.db");

        public static void InitializeDatabase()
        {

            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();

            using var cmd = connection.CreateCommand();

            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Parts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Brand TEXT NOT NULL,
                    Number TEXT NOT NULL UNIQUE,
                    Price REAL NOT NULL
                );";
            cmd.ExecuteNonQuery();
        }

        public static void InsertPart(string brand, string number, double price)
        {
            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO Parts (Brand, Number, Price) VALUES ($brand, $number, $price)";
            cmd.Parameters.AddWithValue("$brand", brand);
            cmd.Parameters.AddWithValue("$number", number);
            cmd.Parameters.AddWithValue("$price", price);
            cmd.ExecuteNonQuery();
        }

        public static List<Part> SearchParts(string query)
        {
            var results = new List<Part>();

            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Brand, Number, Price
                FROM Parts
                WHERE Brand LIKE $query OR Number LIKE $query
            ";
            cmd.Parameters.AddWithValue("$query", $"%{query}%");

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new Part
                {
                    Brand = reader.GetString(0),
                    Number = reader.GetString(1),
                    Price = reader.GetDouble(2)
                });
            }

            return results;
        }
    }
}
