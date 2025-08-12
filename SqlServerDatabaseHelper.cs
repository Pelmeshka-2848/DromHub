using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

namespace DromHub
{
    public static class SqlServerDatabaseHelper
    {
        private static readonly string connectionString = "Server=localhost;Database=DromHubDB;User Id=DESKTOP-MB54F4E\\sheve;Password=your_password;";
        public static void InitializeDatabase()
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            var createTableCmd = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Parts' AND xtype='U')
                CREATE TABLE Parts (
                    Id INT IDENTITY PRIMARY KEY,
                    Brand NVARCHAR(100) NOT NULL,
                    Number NVARCHAR(100) NOT NULL UNIQUE,
                    Price FLOAT NOT NULL
                );
            ";

            using var command = new SqlCommand(createTableCmd, connection);
            command.ExecuteNonQuery();
        }

        // Метод для заполнения таблицы тестовыми данными
        public static void SeedData()
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            var countCmd = new SqlCommand("SELECT COUNT(*) FROM Parts", connection);
            int count = (int)countCmd.ExecuteScalar();

            if (count > 0)
                return; // Данные уже есть

            using var transaction = connection.BeginTransaction();

            var insertCmd = new SqlCommand("INSERT INTO Parts (Brand, Number, Price) VALUES (@brand, @number, @price)", connection, transaction);

            insertCmd.Parameters.Add(new SqlParameter("@brand", System.Data.SqlDbType.NVarChar));
            insertCmd.Parameters.Add(new SqlParameter("@number", System.Data.SqlDbType.NVarChar));
            insertCmd.Parameters.Add(new SqlParameter("@price", System.Data.SqlDbType.Float));

            var random = new Random();

            for (int i = 1; i <= 1000; i++)
            {
                insertCmd.Parameters["@brand"].Value = $"Brand{i % 10 + 1}";
                insertCmd.Parameters["@number"].Value = $"NUM{i:00000}";
                insertCmd.Parameters["@price"].Value = Math.Round(random.NextDouble() * 1000 + 50, 2);

                insertCmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        // Метод поиска по полю Number
        public static List<Part> SearchPartsByNumber(string searchText)
        {
            var results = new List<Part>();

            if (string.IsNullOrWhiteSpace(searchText))
                return results;

            using var connection = new SqlConnection(connectionString);
            connection.Open();

            var cmd = new SqlCommand("SELECT Brand, Number, Price FROM Parts WHERE Number LIKE @search", connection);
            cmd.Parameters.AddWithValue("@search", $"%{searchText}%");

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