using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;
using System.Windows;
using Winton.Models;


namespace Winton.Services
{
    internal class CanvasService
    {
        /// <summary>
        /// Saves the perimeter points to the database.
        /// </summary>
        public static async Task SavePerimeterAsync(List<Point> perimeterPoints)
        {
            using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
            {
                await connection.OpenAsync();

                // Delete existing perimeter points
                string deleteQuery = "DELETE FROM Perimeter";
                using (var deleteCommand = new SqliteCommand(deleteQuery, connection))
                {
                    await deleteCommand.ExecuteNonQueryAsync();
                }

                // Insert new perimeter points
                string insertQuery = "INSERT INTO Perimeter (XPosition, YPosition) VALUES (@X, @Y)";
                using (var command = new SqliteCommand(insertQuery, connection))
                {
                    foreach (Point point in perimeterPoints)
                    {
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("@X", point.X);
                        command.Parameters.AddWithValue("@Y", point.Y);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Loads the perimeter points from the database.
        /// </summary>
        public static async Task<List<Point>> LoadPerimeterAsync()
        {
            List<Point> perimeterPoints = new();

            using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
            {
                await connection.OpenAsync();

                string query = "SELECT XPosition, YPosition FROM Perimeter";
                using (var command = new SqliteCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        double x = reader.GetDouble(0);
                        double y = reader.GetDouble(1);
                        perimeterPoints.Add(new Point(x, y));
                    }
                }
            }

            return perimeterPoints;
        }

        /// <summary>
        /// Saves a section to the database.
        /// </summary>
        public static async Task SaveSectionAsync(string sectionId, string name, double x, double y, double width, double height, double rotation, string shapeType)
        {
            MessageBox.Show($"SaveSectionAsync called for SectionID: {sectionId}", "Debug Info");

            using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
            {
                await connection.OpenAsync();

                string insertQuery = @"
            INSERT INTO Sections (SectionID, Name, XPosition, YPosition, Width, Height, Rotation, ShapeType)
            VALUES (@SectionID, @Name, @X, @Y, @Width, @Height, @Rotation, @ShapeType)
            ON CONFLICT(SectionID) DO UPDATE SET 
                Name = excluded.Name, 
                XPosition = excluded.XPosition, 
                YPosition = excluded.YPosition, 
                Width = excluded.Width, 
                Height = excluded.Height,
                Rotation = excluded.Rotation,
                ShapeType = excluded.ShapeType";

                using (var command = new SqliteCommand(insertQuery, connection))
                {
                    command.Parameters.AddWithValue("@SectionID", sectionId);
                    command.Parameters.AddWithValue("@Name", name);
                    command.Parameters.AddWithValue("@X", x);
                    command.Parameters.AddWithValue("@Y", y);
                    command.Parameters.AddWithValue("@Width", width);
                    command.Parameters.AddWithValue("@Height", height);
                    command.Parameters.AddWithValue("@Rotation", rotation);
                    command.Parameters.AddWithValue("@ShapeType", shapeType);

                    MessageBox.Show($"Saving Section: ID={sectionId}, Name={name}, X={x}, Y={y}, Width={width}, Height={height}", "Debug Info");
                    await command.ExecuteNonQueryAsync();
                }
            }
        }


        /// <summary>
        /// Loads all sections from the database.
        /// </summary>
        public static async Task<List<(string sectionId, string name, double x, double y, double width, double height, double rotation, string shapeType)>> LoadSectionsAsync()
        {
            List<(string, string, double, double, double, double, double, string)> sections = new();

            using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
            {
                await connection.OpenAsync();

                string query = "SELECT SectionID, Name, XPosition, YPosition, Width, Height, Rotation, ShapeType FROM Sections";
                using (var command = new SqliteCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string sectionId = reader.GetString(0);
                        string name = reader.GetString(1);
                        double x = reader.GetDouble(2);
                        double y = reader.GetDouble(3);
                        double width = reader.GetDouble(4);
                        double height = reader.GetDouble(5);
                        double rotation = reader.GetDouble(6);
                        string shapeType = reader.GetString(7);
                        sections.Add((sectionId, name, x, y, width, height, rotation, shapeType));
                    }
                }
            }

            return sections;
        }

        public static async Task UpdateSectionDimensionsAsync(string sectionId, double x, double y, double width, double height, double rotation)
        {
            using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
            {
                await connection.OpenAsync();
                string updateQuery = @"
            UPDATE Sections 
            SET XPosition = @X, 
                YPosition = @Y, 
                Width = @Width, 
                Height = @Height, 
                Rotation = @Rotation 
            WHERE SectionID = @SectionID";
                using (var command = new SqliteCommand(updateQuery, connection))
                {
                    command.Parameters.AddWithValue("@X", x);
                    command.Parameters.AddWithValue("@Y", y);
                    command.Parameters.AddWithValue("@Width", width);
                    command.Parameters.AddWithValue("@Height", height);
                    command.Parameters.AddWithValue("@Rotation", rotation);
                    command.Parameters.AddWithValue("@SectionID", sectionId);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public static async Task DeleteSectionAsync(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId))
                return;

            using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
            {
                await connection.OpenAsync();
                string deleteQuery = "DELETE FROM Sections WHERE SectionID = @SectionID";
                using (var command = new SqliteCommand(deleteQuery, connection))
                {
                    command.Parameters.AddWithValue("@SectionID", sectionId);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public static async Task UpdateSectionNameAsync(string sectionId, string newName)
        {
            using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
            {
                await connection.OpenAsync();

                string query = "UPDATE Sections SET Name = @Name WHERE SectionID = @SectionID";
                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Name", newName);
                    command.Parameters.AddWithValue("@SectionID", sectionId);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Saves all partitions to the database.
        /// </summary>
        public static async Task SavePartitionsAsync(List<Partition> partitions, List<string> deletedPartitionIds)
        {
            using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
            {
                await connection.OpenAsync();

                // 1. Delete any partitions marked for removal
                foreach (var partitionId in deletedPartitionIds)
                {
                    string deleteQuery = "DELETE FROM Partitions WHERE PartitionId = @PartitionId";
                    using (var deleteCommand = new SqliteCommand(deleteQuery, connection))
                    {
                        deleteCommand.Parameters.AddWithValue("@PartitionId", partitionId);
                        await deleteCommand.ExecuteNonQueryAsync();
                    }
                }

                // 2. Filter out deleted partitions so they aren't re-saved
                var activePartitions = partitions
                    .Where(p => !deletedPartitionIds.Contains(p.Id))
                    .ToList();

                // 3. Save (insert or update) only the active partitions
                string insertOrUpdateQuery = @"
            INSERT INTO Partitions (PartitionID, PointOrder, XPosition, YPosition) 
            VALUES (@PartitionID, @PointOrder, @X, @Y)
            ON CONFLICT(PartitionID, PointOrder) DO UPDATE SET 
                XPosition = excluded.XPosition, 
                YPosition = excluded.YPosition";

                using (var command = new SqliteCommand(insertOrUpdateQuery, connection))
                {
                    foreach (var partition in activePartitions)
                    {
                        for (int i = 0; i < partition.Points.Count; i++)
                        {
                            command.Parameters.Clear();
                            command.Parameters.AddWithValue("@PartitionID", partition.Id);
                            command.Parameters.AddWithValue("@PointOrder", i);
                            command.Parameters.AddWithValue("@X", partition.Points[i].X);
                            command.Parameters.AddWithValue("@Y", partition.Points[i].Y);
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
        }




        /// <summary>
        /// Loads all partitions from the database.
        /// </summary>
        public static async Task<List<Partition>> LoadPartitionsAsync()
        {
            List<Partition> partitions = new();

            using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
            {
                await connection.OpenAsync();

                string query = "SELECT PartitionID, PointOrder, XPosition, YPosition FROM Partitions ORDER BY PartitionID, PointOrder";
                using (var command = new SqliteCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string partitionId = reader.GetString(0);
                        int pointOrder = reader.GetInt32(1);
                        double x = reader.GetDouble(2);
                        double y = reader.GetDouble(3);

                        // Try to find an existing partition with this ID
                        Partition partition = partitions.FirstOrDefault(p => p.Id == partitionId);
                        if (partition == null)
                        {
                            partition = new Partition() { Id = partitionId };
                            partitions.Add(partition);
                        }
                        partition.Points.Add(new Point(x, y));
                    }
                }
            }

            return partitions;
        }

        public static async Task DeletePartitionAsync(string partitionId)
        {
            using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText =
                @"
            DELETE FROM Partitions
            WHERE PartitionId = $partitionId
        ";
                command.Parameters.AddWithValue("$partitionId", partitionId);

                await command.ExecuteNonQueryAsync();
            }
        }




    }
}
