using Microsoft.Data.Sqlite;
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
                Console.WriteLine($"[DEBUG] PlaceProductAsync called with ItemNumber: {itemNumber}, SectionID: {sectionId}");

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

                                Console.WriteLine($"[DEBUG] Found product: ProductID={productId}, Grp={grp}, Cat={cat}");

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

                                    int rowsAffected = await insertCommand.ExecuteNonQueryAsync();
                                    if (rowsAffected > 0)
                                    {
                                        Console.WriteLine("[DEBUG] Product placement inserted successfully.");
                                    }
                                    else
                                    {
                                        Console.WriteLine("[DEBUG] Insert command executed, but no rows affected.");
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine($"[DEBUG] No matching product found in Products table for ItemNumber: {itemNumber}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] PlaceProductAsync: {ex.Message}");
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
                Console.WriteLine($"[Error] EnsureSectionExistsAsync: {ex.Message}");
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
                Console.WriteLine($"[Error] ProductExistsAsync: {ex.Message}");
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
                Console.WriteLine($"[Error] GetProductsBySectionAsync: {ex.Message}");
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
                Console.WriteLine($"[Error] GetCurrentProductPlacementAsync: {ex.Message}");
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
                Console.WriteLine($"[Error] GetRevenueDataAsync: {ex.Message}");
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
                Console.WriteLine($"[Error] RemoveProductFromSectionAsync: {ex.Message}");
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

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Archive the placement by copying it into the Archive table
                            string insertQuery = @"
                        INSERT INTO Archive (ProductID, SectionID, ItemNumber, DatePlaced, DateRemoved, QuantitySold, Revenue, RemovalNotes)
                        SELECT pp.ProductID, pp.SectionID, p.ItemNumber, pp.DatePlaced, @DateRemoved, @QuantitySold, @Revenue, @RemovalNotes
                        FROM ProductPlacements pp
                        INNER JOIN Products p ON pp.ProductID = p.ProductID
                        WHERE pp.ProductID = @ProductID AND pp.SectionID = @SectionID";

                            using (var insertCommand = new SqliteCommand(insertQuery, connection, transaction))
                            {
                                insertCommand.Parameters.AddWithValue("@ProductID", productId);
                                insertCommand.Parameters.AddWithValue("@SectionID", sectionId);
                                insertCommand.Parameters.AddWithValue("@DateRemoved", DateTime.UtcNow);  // Ensure removal timestamp is included
                                insertCommand.Parameters.AddWithValue("@QuantitySold", quantitySold);
                                insertCommand.Parameters.AddWithValue("@Revenue", revenue);
                                insertCommand.Parameters.AddWithValue("@RemovalNotes", removalNotes ?? "Removed manually");

                                await insertCommand.ExecuteNonQueryAsync();
                            }

                            // Remove the product from active placements
                            string deleteQuery = @"
                        DELETE FROM ProductPlacements 
                        WHERE ProductID = @ProductID AND SectionID = @SectionID";

                            using (var deleteCommand = new SqliteCommand(deleteQuery, connection, transaction))
                            {
                                deleteCommand.Parameters.AddWithValue("@ProductID", productId);
                                deleteCommand.Parameters.AddWithValue("@SectionID", sectionId);
                                await deleteCommand.ExecuteNonQueryAsync();
                            }

                            await transaction.CommitAsync();
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            Console.WriteLine($"Error archiving product placement: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ArchiveProductPlacementAsync: {ex.Message}");
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
                Console.WriteLine($"Error updating archive dates: {ex.Message}");
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
                Console.WriteLine($"[Error] GetPlacementByPlacementIdAsync: {ex.Message}");
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

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            Console.WriteLine($"DatePlaced updated for ProductID {productId} in Section {sectionId} to {newDatePlaced}");
                        }
                        else
                        {
                            Console.WriteLine($"No matching record found for ProductID {productId} in Section {sectionId}.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating DatePlaced: {ex.Message}");
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
                Console.WriteLine($"Error fetching archive data: {ex.Message}");
            }

            return archivedProducts;
        }


        public static async Task<List<string>> GetSectionIdsByFiltersAsync(
    List<string> itemNumbers,
    DateTime? startDate,
    DateTime? endDate)
        {
            var sectionIds = new List<string>();

            using var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};");
            await connection.OpenAsync();

            var conditions = new List<string>();
            var parameters = new List<SqliteParameter>();

            if (itemNumbers?.Any() == true)
            {
                conditions.Add($"ItemNumber IN ({string.Join(",", itemNumbers.Select((_, i) => $"@item{i}"))})");
                for (int i = 0; i < itemNumbers.Count; i++)
                {
                    parameters.Add(new SqliteParameter($"@item{i}", itemNumbers[i]));
                }
            }

            if (startDate.HasValue)
            {
                conditions.Add("DatePlaced >= @StartDate");
                parameters.Add(new SqliteParameter("@StartDate", startDate.Value.ToString("yyyy-MM-dd")));
            }

            if (endDate.HasValue)
            {
                conditions.Add("DatePlaced <= @EndDate");
                parameters.Add(new SqliteParameter("@EndDate", endDate.Value.ToString("yyyy-MM-dd")));
            }

            var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

            var commandText = $@"
        SELECT DISTINCT SectionID 
        FROM ProductPlacement 
        {whereClause}";

            using var command = new SqliteCommand(commandText, connection);
            command.Parameters.AddRange(parameters.ToArray());

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sectionIds.Add(reader["SectionID"].ToString());
            }

            return sectionIds;
        }

        public static async Task<List<string>> GetMatchingSectionsAsync(Dictionary<string, HashSet<string>> filters, string matchMode)
        {
            List<string> matchingSectionIds = new();

            using var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};");
            await connection.OpenAsync();

            var sql = @"
        SELECT DISTINCT pp.SectionId
        FROM ProductPlacements pp
        JOIN Products p ON pp.ProductId = p.ProductId
        LEFT JOIN SalesData sd ON sd.PlacementId = pp.PlacementId
        WHERE {conditions};
    ";

            var conditions = new List<string>();
            var parameters = new List<SqliteParameter>();

            // Helper to create OR/AND grouped conditions
            void AddFilterCondition(string column, HashSet<string> values, string tableAlias)
            {
                if (values.Count == 0) return;

                var conditionList = new List<string>();
                int i = 0;
                foreach (var val in values)
                {
                    string paramName = $"@{tableAlias}_{column}_{i}";
                    conditionList.Add($"{tableAlias}.{column} = {paramName}");
                    parameters.Add(new SqliteParameter(paramName, val));
                    i++;
                }

                string combined = matchMode == "MatchAll"
                    ? string.Join(" AND ", conditionList)
                    : $"({string.Join(" OR ", conditionList)})";

                conditions.Add(combined);
            }

            if (filters.TryGetValue("Vendor", out var vendors))
                AddFilterCondition("Vendor", vendors, "p");

            if (filters.TryGetValue("Category", out var categories))
                AddFilterCondition("Category", categories, "p");

            if (filters.TryGetValue("Group", out var groups))
                AddFilterCondition("GroupName", groups, "p");

            if (filters.TryGetValue("Product", out var products))
                AddFilterCondition("ItemNumber", products, "p");

            if (filters.TryGetValue("Start Date", out var startDates) && DateTime.TryParse(startDates.FirstOrDefault(), out var startDate))
            {
                conditions.Add("sd.Date >= @StartDate");
                parameters.Add(new SqliteParameter("@StartDate", startDate));
            }

            if (filters.TryGetValue("End Date", out var endDates) && DateTime.TryParse(endDates.FirstOrDefault(), out var endDate))
            {
                conditions.Add("sd.Date <= @EndDate");
                parameters.Add(new SqliteParameter("@EndDate", endDate));
            }

            string whereClause = conditions.Count > 0 ? string.Join(matchMode == "MatchAll" ? " AND " : " OR ", conditions) : "1=1";

            using var command = connection.CreateCommand();
            command.CommandText = sql.Replace("{conditions}", whereClause);

            foreach (var param in parameters)
                command.Parameters.Add(param);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                matchingSectionIds.Add(reader.GetString(0));
            }

            return matchingSectionIds;
        }


    }
}
