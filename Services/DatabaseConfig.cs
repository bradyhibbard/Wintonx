using System.IO;

namespace Winton.Services
{
    internal static class DatabaseConfig
    {
        public static readonly string AppDataFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wintonx");

        public static readonly string DbPath =
            Path.Combine(AppDataFolder, "WintonDatabase.db");
    }
}
