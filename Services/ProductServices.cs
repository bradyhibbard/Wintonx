using Microsoft.Data.Sqlite;
using Winton.Models;

namespace Winton.Services
{
    internal sealed class ProductService
    {
        /// <summary>
        /// Retrieves all products from the database.
        /// </summary>
        /// <returns>List of products.</returns>
        public static async Task<List<Product>> GetProductsAsync()
        {
            var products = new List<Product>();

            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();
                    // Updated query: use "Category" instead of "Cat"
                    string sql = "SELECT ProductID, ItemNumber, ItemName, Vendor, Category, Grp FROM Products";

                    using (var command = new SqliteCommand(sql, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            products.Add(new Product
                            {
                                ProductID = reader.GetInt32(0),
                                ItemNumber = reader.GetString(1),
                                ItemName = reader.GetString(2),
                                Vendor = reader.IsDBNull(3) ? null : reader.GetString(3),
                                Cat = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Grp = reader.IsDBNull(5) ? null : reader.GetString(5)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving products: {ex.Message}");
            }

            return products;
        }






        /// <summary>
        /// Retrieves all product item numbers from the database.
        /// </summary>
        public static async Task<List<string>> GetProductItemNumbersAsync()
        {
            var itemNumbers = new List<string>();

            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();
                    string sql = "SELECT ItemNumber FROM Products";

                    using (var command = new SqliteCommand(sql, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            itemNumbers.Add(reader.GetString(0));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching product item numbers: {ex.Message}");
            }

            return itemNumbers;
        }

        public static async Task<List<string>> GetVendorsAsync()
        {
            var vendors = new List<string>();
            using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
            {
                await connection.OpenAsync();
                var command = new SqliteCommand("SELECT DISTINCT Vendor FROM Products ORDER BY Vendor", connection);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync()) vendors.Add(reader.GetString(0));
                }
            }
            return vendors;
        }

        public static async Task<List<string>> GetCategoriesByVendorAsync(string vendor)
        {
            var categories = new List<string>();
            using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
            {
                await connection.OpenAsync();
                var command = new SqliteCommand("SELECT DISTINCT Category FROM Products WHERE Vendor = @Vendor ORDER BY Category", connection);
                command.Parameters.AddWithValue("@Vendor", vendor);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync()) categories.Add(reader.GetString(0));
                }
            }
            return categories;
        }

        public static async Task<List<string>> GetGroupsByCategoryAsync(string category)
        {
            var groups = new List<string>();
            using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
            {
                await connection.OpenAsync();
                var command = new SqliteCommand("SELECT DISTINCT Grp FROM Products WHERE Category = @Category ORDER BY Grp", connection);
                command.Parameters.AddWithValue("@Category", category);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync()) groups.Add(reader.GetString(0));
                }
            }
            return groups;
        }

        public static async Task<List<string>> GetProductsByGroupAsync(string group)
        {
            var products = new List<string>();
            using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
            {
                await connection.OpenAsync();
                var command = new SqliteCommand("SELECT ItemNumber FROM Products WHERE Grp = @Grp ORDER BY ItemNumber", connection);
                command.Parameters.AddWithValue("@Grp", group);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync()) products.Add(reader.GetString(0));
                }
            }
            return products;
        }


    }
}

