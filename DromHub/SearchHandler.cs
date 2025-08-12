using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace DromHub
{
    public static class SearchHandler
    {
        public static List<Part> HandleSearchText(string searchText)
        {
            var results = new List<Part>();

            if (string.IsNullOrWhiteSpace(searchText))
                return results;

            using var connection = new SqliteConnection($"Data Source={DatabaseHelper.DbPath}");
            connection.Open();

            using var cmd = connection.CreateCommand();

            // »щем только по Number, использу€ LIKE дл€ подстрочного поиска
            cmd.CommandText = "SELECT Brand, Number, Price FROM Parts WHERE Number LIKE $search";
            cmd.Parameters.AddWithValue("$search", $"%{searchText}%");

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