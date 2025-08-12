namespace DromHub
{
    public static class DatabaseSeeder
    {
        public static void Seed()
        {
            if (!DatabaseHelper.DatabaseExists)
            {
                DatabaseHelper.InitializeDatabase();

                var data = new (string Brand, string Number, double Price)[]
                {
                    ("Toyota", "12345", 1200.50),
                    ("Honda", "67890", 850.00),
                    ("Nissan", "54321", 950.75),
                    ("Mazda", "11223", 1300.00),
                    ("Subaru", "99887", 1600.20)
                };

                foreach (var item in data)
                {
                    DatabaseHelper.InsertPart(item.Brand, item.Number, item.Price);
                }
            }
        }
    }
}
