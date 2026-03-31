using System.IO;

namespace Winton.Services
{
    internal static class ThemeConfig
    {
        private static readonly string ThemeFilePath =
            Path.Combine(DatabaseConfig.AppDataFolder, "theme.txt");

        public static string LoadTheme()
        {
            try
            {
                if (File.Exists(ThemeFilePath))
                {
                    var saved = File.ReadAllText(ThemeFilePath).Trim();
                    if (saved == "Dark" || saved == "Light")
                        return saved;
                }
            }
            catch { }
            return "Light";
        }

        public static void SaveTheme(string themeName)
        {
            try
            {
                Directory.CreateDirectory(DatabaseConfig.AppDataFolder);
                File.WriteAllText(ThemeFilePath, themeName);
            }
            catch { }
        }
    }
}
