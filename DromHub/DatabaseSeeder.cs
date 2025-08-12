using Microsoft.Data.Sqlite;
using System;

namespace DromHub
{
    public static class DatabaseSeeder
    {
        public static void Seed()
        {
            // Предполагается, что база и таблица уже созданы
            using var connection = new SqliteConnection($"Data Source={DatabaseHelper.DbPath}");
            connection.Open();

            using var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Parts";
            var count = (long)countCmd.ExecuteScalar();

            if (count > 0)
                return; // Уже есть данные — не заполняем заново

            using var transaction = connection.BeginTransaction();

            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO Parts (Brand, Number, Price) VALUES ($brand, $number, $price)";

            var brandParam = insertCmd.CreateParameter();
            brandParam.ParameterName = "$brand";
            insertCmd.Parameters.Add(brandParam);

            var numberParam = insertCmd.CreateParameter();
            numberParam.ParameterName = "$number";
            insertCmd.Parameters.Add(numberParam);

            var priceParam = insertCmd.CreateParameter();
            priceParam.ParameterName = "$price";
            insertCmd.Parameters.Add(priceParam);

            var random = new Random();

            for (int i = 1; i <= 1000; i++)
            {
                brandParam.Value = $"Brand{i % 10 + 1}"; // 10 разных брендов
                numberParam.Value = $"NUM{i:00000}"; // Номер детали с ведущими нулями
                priceParam.Value = Math.Round(random.NextDouble() * 1000 + 50, 2); // Цена от 50 до 1050

                insertCmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }
}
