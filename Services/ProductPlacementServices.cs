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

                    // An item only needs one current placement per section.
                    using (var existsCommand = new SqliteCommand(
                        "SELECT EXISTS(SELECT 1 FROM ProductPlacements WHERE ItemNumber = @ItemNumber AND SectionID = @SectionID AND DateRemoved IS NULL)",
                        connection))
                    {
                        existsCommand.Parameters.AddWithValue("@ItemNumber", itemNumber);
                        existsCommand.Parameters.AddWithValue("@SectionID", sectionId);
                        if (Convert.ToInt32(await existsCommand.ExecuteScalarAsync()) == 1) return;
                    }

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
                    pp.ItemNumber,
                    pp.ProductID,
                    pp.DatePlaced,
                    pp.Revenue,
                    pp.QuantitySold,
                    pp.Grp
                FROM ProductPlacements pp
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
                                    ProductID = reader["ProductID"] != DBNull.Value ? Convert.ToInt32(reader["ProductID"]) : 0,
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
        /// Retrieves an item's current placement within one section.
        /// </summary>
        /// <returns>The placement details, or null if the item isn't in that section.</returns>
        public static async Task<ProductPlacement> GetPlacementInSectionAsync(string itemNumber, string sectionId)
        {
            using var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};");
            await connection.OpenAsync();

            using var command = new SqliteCommand(
                "SELECT PlacementID, ItemNumber, SectionID, DatePlaced FROM ProductPlacements WHERE ItemNumber = @ItemNumber AND SectionID = @SectionID AND DateRemoved IS NULL",
                connection);
            command.Parameters.AddWithValue("@ItemNumber", itemNumber);
            command.Parameters.AddWithValue("@SectionID", sectionId);

            using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new ProductPlacement
            {
                PlacementID = Convert.ToInt32(reader["PlacementID"]),
                ItemNumber = reader["ItemNumber"].ToString(),
                SectionID = reader["SectionID"].ToString(),
                DatePlaced = reader["DatePlaced"] != DBNull.Value ? Convert.ToDateTime(reader["DatePlaced"]) : DateTime.MinValue
            };
        }


        /// <summary>
        /// Removes an item from one section and records it in that section's archive.
        /// Both changes are saved together. Failures throw so the caller can report them.
        /// </summary>
        public static async Task RemoveItemFromSectionAsync(string itemNumber, string sectionId, DateTime dateRemoved, string removalNotes)
        {
            using var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};");
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            using (var archiveCommand = new SqliteCommand(@"
                INSERT INTO Archive (ProductID, SectionID, ItemNumber, QuantitySold, Revenue, DatePlaced, DateRemoved, RemovalNotes)
                SELECT ProductID, SectionID, ItemNumber, QuantitySold, Revenue, DatePlaced, @DateRemoved, @RemovalNotes
                FROM ProductPlacements
                WHERE ItemNumber = @ItemNumber AND SectionID = @SectionID AND DateRemoved IS NULL",
                connection, transaction))
            {
                archiveCommand.Parameters.AddWithValue("@ItemNumber", itemNumber);
                archiveCommand.Parameters.AddWithValue("@SectionID", sectionId);
                archiveCommand.Parameters.AddWithValue("@DateRemoved", dateRemoved);
                archiveCommand.Parameters.AddWithValue("@RemovalNotes", removalNotes ?? (object)DBNull.Value);
                await archiveCommand.ExecuteNonQueryAsync();
            }

            using (var updateCommand = new SqliteCommand(@"
                UPDATE ProductPlacements SET DateRemoved = @DateRemoved
                WHERE ItemNumber = @ItemNumber AND SectionID = @SectionID AND DateRemoved IS NULL",
                connection, transaction))
            {
                updateCommand.Parameters.AddWithValue("@ItemNumber", itemNumber);
                updateCommand.Parameters.AddWithValue("@SectionID", sectionId);
                updateCommand.Parameters.AddWithValue("@DateRemoved", dateRemoved);
                await updateCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }



        /// <summary>
        /// Archives a section's current products and deletes the section. Products removed earlier
        /// are already in the archive. All changes are saved together; failures throw so the
        /// caller doesn't report success or remove the section from the screen.
        /// </summary>
        public static async Task ArchiveAndDeleteSectionAsync(string sectionId)
        {
            using var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};");
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            using (var archiveCommand = new SqliteCommand(@"
                INSERT INTO Archive (ProductID, SectionID, ItemNumber, QuantitySold, Revenue, DatePlaced, DateRemoved, RemovalNotes)
                SELECT ProductID, SectionID, ItemNumber, QuantitySold, Revenue, DatePlaced, @DateRemoved, 'Section Deleted'
                FROM ProductPlacements
                WHERE SectionID = @SectionID AND DateRemoved IS NULL",
                connection, transaction))
            {
                archiveCommand.Parameters.AddWithValue("@SectionID", sectionId);
                archiveCommand.Parameters.AddWithValue("@DateRemoved", DateTime.Now);
                await archiveCommand.ExecuteNonQueryAsync();
            }

            using (var deletePlacements = new SqliteCommand("DELETE FROM ProductPlacements WHERE SectionID = @SectionID", connection, transaction))
            {
                deletePlacements.Parameters.AddWithValue("@SectionID", sectionId);
                await deletePlacements.ExecuteNonQueryAsync();
            }

            using (var deleteSection = new SqliteCommand("DELETE FROM Sections WHERE SectionID = @SectionID", connection, transaction))
            {
                deleteSection.Parameters.AddWithValue("@SectionID", sectionId);
                await deleteSection.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        /// <summary>
        /// Corrects an archived product's dates, and the matching placement history
        /// so section sales follow the corrected dates.
        /// </summary>
        public static async Task UpdateArchiveDatesAsync(int placementId, DateTime? dateAdded, DateTime? dateRemoved)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};");
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                if (dateAdded.HasValue && dateRemoved.HasValue)
                {
                    using var historyCommand = new SqliteCommand(@"
                        UPDATE ProductPlacements
                        SET DatePlaced = @DatePlaced, DateRemoved = @DateRemoved
                        WHERE PlacementID IN (
                            SELECT pp.PlacementID
                            FROM ProductPlacements pp
                            INNER JOIN Archive a
                                ON a.ItemNumber = pp.ItemNumber
                               AND a.SectionID = pp.SectionID
                               AND date(a.DateRemoved) = date(pp.DateRemoved)
                            WHERE a.PlacementID = @PlacementID)",
                        connection, transaction);
                    historyCommand.Parameters.AddWithValue("@DatePlaced", dateAdded.Value);
                    historyCommand.Parameters.AddWithValue("@DateRemoved", dateRemoved.Value);
                    historyCommand.Parameters.AddWithValue("@PlacementID", placementId);
                    await historyCommand.ExecuteNonQueryAsync();
                }

                using (var command = new SqliteCommand(@"
                    UPDATE Archive
                    SET DatePlaced = @DatePlaced,
                        DateRemoved = @DateRemoved
                    WHERE PlacementID = @PlacementID", connection, transaction))
                {
                    command.Parameters.AddWithValue("@DatePlaced", dateAdded.HasValue ? (object)dateAdded.Value : DBNull.Value);
                    command.Parameters.AddWithValue("@DateRemoved", dateRemoved.HasValue ? (object)dateRemoved.Value : DBNull.Value);
                    command.Parameters.AddWithValue("@PlacementID", placementId);
                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
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

        public static async Task UpdateDatePlacedAsync(string itemNumber, string sectionId, DateTime newDatePlaced)
        {
            using var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};");
            await connection.OpenAsync();

            using var command = new SqliteCommand(@"
                UPDATE ProductPlacements
                SET DatePlaced = @NewDatePlaced
                WHERE ItemNumber = @ItemNumber AND SectionID = @SectionID AND DateRemoved IS NULL", connection);
            command.Parameters.AddWithValue("@NewDatePlaced", newDatePlaced);
            command.Parameters.AddWithValue("@ItemNumber", itemNumber);
            command.Parameters.AddWithValue("@SectionID", sectionId);

            await command.ExecuteNonQueryAsync();
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
                SELECT a.PlacementID, a.ItemNumber, a.DatePlaced, a.DateRemoved,
                       (SELECT IFNULL(SUM(sd.Revenue), 0)
                        FROM SalesData sd
                        WHERE sd.ItemNumber = a.ItemNumber
                          AND date(sd.SaleDate) >= date(a.DatePlaced)
                          AND (a.DateRemoved IS NULL OR date(sd.SaleDate) <= date(a.DateRemoved))) AS Revenue
                FROM Archive a
                WHERE a.SectionID = @SectionID
                ORDER BY a.DateRemoved DESC";

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

            try
            {
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
              AND ItemNumber IN ({string.Join(",", paramNames)})";

                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();
                    using (var cmd = new SqliteCommand(sql, connection))
                    {
                        for (int i = 0; i < list.Count; i++)
                            cmd.Parameters.AddWithValue(paramNames[i], list[i]);

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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProductPlacementServices] GetSectionIdsForItemNumbersAsync: {ex}");
            }

            return ids;
        }




    }
}
