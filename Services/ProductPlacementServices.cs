using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Windows;
using Winton.Models;

namespace Winton.Services
{
    /// <summary>
    /// Service responsible for managing product placements within store sections.
    /// </summary>
    internal class ProductPlacementServices
    {

        /// <summary>
        /// Places a product in a specified section.
        /// </summary>
        /// <param name="itemNumber">The item number of the product.</param>
        /// <param name="sectionId">The section ID where the product should be placed.</param>
        public static async Task PlaceProductAsync(string itemNumber, string sectionId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();

                    // Query the Products table to get ProductID, Grp, and Category for the given itemNumber.
                    string query = "SELECT ProductID, Grp, Category FROM Products WHERE ItemNumber = @ItemNumber";
                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ItemNumber", itemNumber);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                int productId = Convert.ToInt32(reader["ProductID"]);
                                string grp = reader["Grp"].ToString();
                                string cat = reader["Category"].ToString();

                                // Insert a new product placement record
                                query = @"
                            INSERT INTO ProductPlacements (ProductID, SectionID, ItemNumber, DatePlaced, Grp, Cat) 
                            VALUES (@ProductID, @SectionID, @ItemNumber, @DatePlaced, @Grp, @Cat)";
                                using (var insertCommand = new SqliteCommand(query, connection))
                                {
                                    insertCommand.Parameters.AddWithValue("@ProductID", productId);
                                    insertCommand.Parameters.AddWithValue("@ItemNumber", itemNumber);
                                    insertCommand.Parameters.AddWithValue("@SectionID", sectionId);
                                    insertCommand.Parameters.AddWithValue("@DatePlaced", DateTime.Now);
                                    insertCommand.Parameters.AddWithValue("@Grp", grp);
                                    insertCommand.Parameters.AddWithValue("@Cat", cat);

                                    await insertCommand.ExecuteNonQueryAsync();
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"[ProductPlacementServices] No matching product found for ItemNumber: {itemNumber}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProductPlacementServices] PlaceProductAsync: {ex}");
            }
        }


        /// <summary>
        /// Ensures that a section exists in the database, creating it if necessary.
        /// </summary>
        public static async Task EnsureSectionExistsAsync(string sectionId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();

                    string checkQuery = "SELECT COUNT(*) FROM Sections WHERE SectionID = @SectionID";
                    using (var checkCmd = new SqliteCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@SectionID", sectionId);
                        int exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                        if (exists == 0)
                        {
                            string insertQuery = "INSERT INTO Sections (SectionID) VALUES (@SectionID)";
                            using (var insertCmd = new SqliteCommand(insertQuery, connection))
                            {
                                insertCmd.Parameters.AddWithValue("@SectionID", sectionId);
                                await insertCmd.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProductPlacementServices] EnsureSectionExistsAsync: {ex}");
            }
        }


        /// <summary>
        /// Checks if a product exists in the database based on the item number.
        /// </summary>
        public static async Task<bool> ProductExistsAsync(string itemNumber)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();

                    string query = "SELECT COUNT(*) FROM Products WHERE ItemNumber = @ItemNumber";
                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ItemNumber", itemNumber);
                        int count = Convert.ToInt32(await command.ExecuteScalarAsync());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProductPlacementServices] ProductExistsAsync: {ex}");
                return false;
            }
        }



        /// <summary>
        /// Retrieves all products currently placed in a given section.
        /// </summary>
        /// <param name="sectionID">The section identifier.</param>
        /// <returns>List of products placed in the section.</returns>
        public static async Task<List<ProductPlacement>> GetProductsBySectionAsync(string sectionID)
        {
            var products = new List<ProductPlacement>();

            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();

                    string sql = @"
                SELECT 
                    pp.PlacementID,
                    pp.SectionID,
                    p.ItemNumber,
                    p.ProductID,
                    pp.DatePlaced,
                    pp.Revenue,
                    pp.QuantitySold,
                    pp.Grp
                FROM ProductPlacements pp
                JOIN Products p ON pp.ProductID = p.ProductID
                WHERE pp.SectionID = @SectionID AND pp.DateRemoved IS NULL";

                    using (var command = new SqliteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@SectionID", sectionID);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                products.Add(new ProductPlacement
                                {
                                    PlacementID = reader["PlacementID"] != DBNull.Value ? Convert.ToInt32(reader["PlacementID"]) : 0,
                                    SectionID = reader["SectionID"].ToString(),
                                    ProductID = Convert.ToInt32(reader["ProductID"]),
                                    ItemNumber = reader["ItemNumber"].ToString(),
                                    QuantitySold = reader["QuantitySold"] != DBNull.Value ? Convert.ToInt32(reader["QuantitySold"]) : 0,
                                    Revenue = reader["Revenue"] != DBNull.Value ? Convert.ToDecimal(reader["Revenue"]) : 0m,
                                    DatePlaced = reader["DatePlaced"] != DBNull.Value ? Convert.ToDateTime(reader["DatePlaced"]) : DateTime.MinValue,
                                    Grp = reader["Grp"] != DBNull.Value ? reader["Grp"].ToString() : string.Empty
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProductPlacementServices] GetProductsBySectionAsync: {ex}");
            }

            return products;
        }


        /// <summary>
        /// Retrieves the current placement of a product based on its item number.
        /// </summary>
        /// <param name="itemNumber">The product's item number.</param>
        /// <returns>The product placement details, or null if not found.</returns>
        public static async Task<ProductPlacement> GetCurrentProductPlacementAsync(string itemNumber)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();
                    string query = "SELECT * FROM ProductPlacements WHERE ItemNumber = @ItemNumber AND DateRemoved IS NULL";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ItemNumber", itemNumber);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new ProductPlacement
                                {
                                    ProductID = reader["ProductID"] != DBNull.Value ? Convert.ToInt32(reader["ProductID"]) : 0,
                                    SectionID = reader["SectionID"].ToString(),
                                    ItemNumber = reader["ItemNumber"].ToString(),
                                    QuantitySold = reader["QuantitySold"] != DBNull.Value ? Convert.ToInt32(reader["QuantitySold"]) : 0,
                                    Revenue = reader["Revenue"] != DBNull.Value ? Convert.ToDecimal(reader["Revenue"]) : 0m,
                                    DatePlaced = reader["DatePlaced"] != DBNull.Value ? Convert.ToDateTime(reader["DatePlaced"]) : DateTime.MinValue
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProductPlacementServices] GetCurrentProductPlacementAsync: {ex}");
            }

            return null;
        }

        /// <summary>
        /// Retrieves revenue data grouped by section within a given date range.
        /// </summary>
        /// <param name="startDate">Start date of the range.</param>
        /// <param name="endDate">End date of the range.</param>
        /// <returns>A dictionary mapping section IDs to total revenue.</returns>
        public static async Task<Dictionary<string, decimal>> GetRevenueDataAsync(DateTime startDate, DateTime endDate)
        {
            var revenueData = new Dictionary<string, decimal>();

            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();
                    string query = @"
                        SELECT SectionID, SUM(Revenue) as TotalRevenue
                        FROM ProductPlacements
                        WHERE DatePlaced BETWEEN @StartDate AND @EndDate
                        GROUP BY SectionID
                        ORDER BY TotalRevenue DESC";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string sectionId = reader["SectionID"].ToString();
                                decimal totalRevenue = reader["TotalRevenue"] != DBNull.Value ? Convert.ToDecimal(reader["TotalRevenue"]) : 0m;
                                revenueData[sectionId] = totalRevenue;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProductPlacementServices] GetRevenueDataAsync: {ex}");
            }

            return revenueData;
        }

        /// <summary>
        /// Removes a product from a section by setting its removal date.
        /// </summary>
        public static async Task RemoveProductFromSectionAsync(int productId, string sectionId, DateTime dateRemoved)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();
                    string query = "UPDATE ProductPlacements SET DateRemoved = @DateRemoved WHERE ProductID = @ProductID AND SectionID = @SectionID AND DateRemoved IS NULL";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ProductID", productId);
                        command.Parameters.AddWithValue("@SectionID", sectionId);
                        command.Parameters.AddWithValue("@DateRemoved", dateRemoved);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProductPlacementServices] RemoveProductFromSectionAsync: {ex}");
            }
        }

        /// <summary>
        /// Archives a product placement by moving it to the archive table and removing it from active placements.
        /// </summary>
        public static async Task ArchiveProductPlacementAsync(int productId, string sectionId, string removalNotes, int quantitySold, decimal revenue)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();

                    string query = @"
                INSERT INTO Archive (ProductID, SectionID, ItemNumber, DatePlaced, DateRemoved, QuantitySold, Revenue)
                SELECT pp.ProductID, pp.SectionID, p.ItemNumber, pp.DatePlaced, pp.DateRemoved, @QuantitySold, @Revenue
                FROM ProductPlacements pp
                INNER JOIN Products p ON pp.ProductID = p.ProductID
                WHERE pp.ProductID = @ProductID AND pp.SectionID = @SectionID";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ProductID", productId);
                        command.Parameters.AddWithValue("@SectionID", sectionId);
                        command.Parameters.AddWithValue("@QuantitySold", quantitySold);
                        command.Parameters.AddWithValue("@Revenue", revenue);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProductPlacementServices] ArchiveProductPlacementAsync: {ex}");
            }
        }


        public static async Task ArchiveAndDeleteSectionAsync(string sectionId)
        {
            using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Step 1: Get all ProductPlacements tied to the section
                        string selectQuery = @"
                    SELECT pp.PlacementID, pp.ProductID, pp.SectionID, pp.ItemNumber, pp.QuantitySold, 
                           pp.Revenue, pp.DatePlaced, pp.DateRemoved
                    FROM ProductPlacements pp
                    WHERE pp.SectionID = @SectionID";

                        List<ProductPlacement> placements = new List<ProductPlacement>();

                        using (var selectCommand = new SqliteCommand(selectQuery, connection, transaction))
                        {
                            selectCommand.Parameters.AddWithValue("@SectionID", sectionId);

                            using (var reader = await selectCommand.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    placements.Add(new ProductPlacement
                                    {
                                        PlacementID = Convert.ToInt32(reader["PlacementID"]),
                                        ProductID = Convert.ToInt32(reader["ProductID"]),
                                        SectionID = reader["SectionID"].ToString(),
                                        ItemNumber = reader["ItemNumber"].ToString(),
                                        QuantitySold = reader["QuantitySold"] != DBNull.Value ? Convert.ToInt32(reader["QuantitySold"]) : 0,
                                        Revenue = reader["Revenue"] != DBNull.Value ? Convert.ToDecimal(reader["Revenue"]) : 0m,
                                        DatePlaced = reader["DatePlaced"] != DBNull.Value ? Convert.ToDateTime(reader["DatePlaced"]) : DateTime.MinValue,
                                        DateRemoved = DateTime.UtcNow
                                    });
                                }
                            }
                        }

                        // Step 2: Insert each placement into Archive
                        foreach (var placement in placements)
                        {
                            string insertArchiveQuery = @"
                        INSERT INTO Archive (PlacementID, ProductID, SectionID, ItemNumber, QuantitySold, Revenue, DatePlaced, DateRemoved, RemovalNotes)
                        VALUES (@PlacementID, @ProductID, @SectionID, @ItemNumber, @QuantitySold, @Revenue, @DatePlaced, @DateRemoved, @RemovalNotes)";

                            using (var insertCommand = new SqliteCommand(insertArchiveQuery, connection, transaction))
                            {
                                insertCommand.Parameters.AddWithValue("@PlacementID", placement.PlacementID);
                                insertCommand.Parameters.AddWithValue("@ProductID", placement.ProductID);
                                insertCommand.Parameters.AddWithValue("@SectionID", placement.SectionID);
                                insertCommand.Parameters.AddWithValue("@ItemNumber", placement.ItemNumber);  // ✅ Now includes ItemNumber
                                insertCommand.Parameters.AddWithValue("@QuantitySold", placement.QuantitySold);
                                insertCommand.Parameters.AddWithValue("@Revenue", placement.Revenue);
                                insertCommand.Parameters.AddWithValue("@DatePlaced", placement.DatePlaced);
                                insertCommand.Parameters.AddWithValue("@DateRemoved", placement.DateRemoved);
                                insertCommand.Parameters.AddWithValue("@RemovalNotes", "Section Deleted");

                                await insertCommand.ExecuteNonQueryAsync();
                            }
                        }

                        // Step 3: Delete the product placements tied to this section
                        string deletePlacementsQuery = "DELETE FROM ProductPlacements WHERE SectionID = @SectionID";
                        using (var deletePlacementsCommand = new SqliteCommand(deletePlacementsQuery, connection, transaction))
                        {
                            deletePlacementsCommand.Parameters.AddWithValue("@SectionID", sectionId);
                            await deletePlacementsCommand.ExecuteNonQueryAsync();
                        }

                        // Step 4: Delete the section itself
                        string deleteSectionQuery = "DELETE FROM Sections WHERE SectionID = @SectionID";
                        using (var deleteSectionCommand = new SqliteCommand(deleteSectionQuery, connection, transaction))
                        {
                            deleteSectionCommand.Parameters.AddWithValue("@SectionID", sectionId);
                            await deleteSectionCommand.ExecuteNonQueryAsync();
                        }

                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        MessageBox.Show($"[Error] ArchiveAndDeleteSectionAsync: {ex.Message}");
                    }
                }
            }
        }

        public static async Task UpdateArchiveDatesAsync(int placementId, DateTime? dateAdded, DateTime? dateRemoved)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();

                    string query = @"
                UPDATE Archive 
                SET DatePlaced = @DatePlaced, 
                    DateRemoved = @DateRemoved 
                WHERE PlacementID = @PlacementID";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@DatePlaced", dateAdded.HasValue ? (object)dateAdded.Value : DBNull.Value);
                        command.Parameters.AddWithValue("@DateRemoved", dateRemoved.HasValue ? (object)dateRemoved.Value : DBNull.Value);
                        command.Parameters.AddWithValue("@PlacementID", placementId);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProductPlacementServices] UpdateArchiveDatesAsync: {ex}");
            }
        }



        public static async Task<ProductPlacement> GetPlacementByPlacementIdAsync(int placementId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();
                    string query = "SELECT * FROM ProductPlacements WHERE PlacementID = @PlacementID AND DateRemoved IS NULL";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@PlacementID", placementId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new ProductPlacement
                                {
                                    PlacementID = reader["PlacementID"] != DBNull.Value ? Convert.ToInt32(reader["PlacementID"]) : 0,
                                    ProductID = reader["ProductID"] != DBNull.Value ? Convert.ToInt32(reader["ProductID"]) : 0,
                                    SectionID = reader["SectionID"].ToString(),
                                    ItemNumber = reader["ItemNumber"].ToString(),
                                    QuantitySold = reader["QuantitySold"] != DBNull.Value ? Convert.ToInt32(reader["QuantitySold"]) : 0,
                                    Revenue = reader["Revenue"] != DBNull.Value ? Convert.ToDecimal(reader["Revenue"]) : 0m,
                                    DatePlaced = reader["DatePlaced"] != DBNull.Value ? Convert.ToDateTime(reader["DatePlaced"]) : DateTime.MinValue
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProductPlacementServices] GetPlacementByPlacementIdAsync: {ex}");
            }

            return null;
        }

        public static async Task UpdateDatePlacedAsync(int productId, string sectionId, DateTime newDatePlaced)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();

                    string query = @"
                UPDATE ProductPlacements 
                SET DatePlaced = @NewDatePlaced 
                WHERE ProductID = @ProductID AND SectionID = @SectionID AND DateRemoved IS NULL";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@NewDatePlaced", newDatePlaced);
                        command.Parameters.AddWithValue("@ProductID", productId);
                        command.Parameters.AddWithValue("@SectionID", sectionId);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProductPlacementServices] UpdateDatePlacedAsync: {ex}");
            }
        }

        public static async Task<List<ProductPlacement>> GetArchivedProductsBySectionAsync(string sectionId)
        {
            var archivedProducts = new List<ProductPlacement>();

            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();

                    string query = @"
                SELECT PlacementID, ItemNumber, DatePlaced, DateRemoved, Revenue
                FROM Archive
                WHERE SectionID = @SectionID
                ORDER BY DateRemoved DESC";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@SectionID", sectionId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                archivedProducts.Add(new ProductPlacement
                                {
                                    PlacementID = reader["PlacementID"] != DBNull.Value ? Convert.ToInt32(reader["PlacementID"]) : 0,
                                    ItemNumber = reader["ItemNumber"].ToString(),
                                    DatePlaced = reader["DatePlaced"] != DBNull.Value ? Convert.ToDateTime(reader["DatePlaced"]) : DateTime.MinValue,
                                    DateRemoved = reader["DateRemoved"] != DBNull.Value ? Convert.ToDateTime(reader["DateRemoved"]) : (DateTime?)null,
                                    Revenue = reader["Revenue"] != DBNull.Value ? Convert.ToDecimal(reader["Revenue"]) : 0m
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProductPlacementServices] GetArchivedProductsBySectionAsync: {ex}");
            }

            return archivedProducts;
        }


        internal static async Task<HashSet<string>> GetSectionIdsForItemNumbersAsync(IEnumerable<string> itemNumbers)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var list = itemNumbers?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (list == null || list.Count == 0)
                return ids;

            // Build a parameterized IN clause: @p0, @p1, ...
            var paramNames = list.Select((_, i) => $"@p{i}").ToList();

            var sql = $@"
        SELECT DISTINCT SectionID
        FROM ProductPlacements
        WHERE DateRemoved IS NULL
          AND ItemNumber IN ({string.Join(",", paramNames)})
    ";

            using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
            {
                await connection.OpenAsync();
                using (var cmd = new SqliteCommand(sql, connection))
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        cmd.Parameters.AddWithValue(paramNames[i], list[i]);
                    }

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var sectionId = reader["SectionID"]?.ToString();
                            if (!string.IsNullOrEmpty(sectionId))
                                ids.Add(sectionId);
                        }
                    }
                }
            }

            return ids;
        }




    }
}
