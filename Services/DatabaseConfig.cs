using System.IO;

namespace Winton.Services
{
    internal static class DatabaseConfig
    {
        public static readonly string AppDataFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wintonx");

        // Settable so automated tests can use a temporary database instead of real data.
        public static string DbPath { get; set; } =
            Path.Combine(AppDataFolder, "WintonDatabase.db");

        public static string BackupFolder { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Winton", "DatabaseBackups");
    }
}
