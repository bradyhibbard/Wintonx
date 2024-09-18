using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;

public class UpdateChecker
{
    private const string GitHubOwner = "bradyhibbard";  // Replace with your GitHub username or organization name
    private const string GitHubRepo = "Winton";         // Replace with the name of your GitHub repository
    private const string CurrentVersion = "1.0.0";      // Replace with your current app version
    private const string GitHubToken = "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";  // GitHub token for API access

    // Check for updates by comparing the latest GitHub release with the current version
    public async Task<string> CheckForUpdate()
    {
        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UpdateCheckerApp");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", GitHubToken);

            string url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
            HttpResponseMessage response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = Newtonsoft.Json.Linq.JObject.Parse(content);

                string latestVersion = json["tag_name"].ToString();  // Assuming tag_name contains the version number

                if (latestVersion != CurrentVersion)
                {
                    return $"A new version {latestVersion} is available! You are on version {CurrentVersion}.";
                }
                else
                {
                    return "You are already on the latest version.";
                }
            }
            else
            {
                return $"Failed to check for updates. Error: {response.ReasonPhrase}";
            }
        }
    }

    // Download the update from the given download URL to the specified path
    public async Task<string> DownloadUpdate(string downloadUrl, string destinationPath)
    {
        using (HttpClient client = new HttpClient())
        {
            HttpResponseMessage response = await client.GetAsync(downloadUrl);
            if (response.IsSuccessStatusCode)
            {
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fileStream);
                }
                return "Update downloaded successfully!";
            }
            else
            {
                return $"Failed to download the update. Error: {response.ReasonPhrase}";
            }
        }
    }

    // Install the update by running the installer or replacing the files
    public void InstallUpdate(string installerPath)
    {
        var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = installerPath;
        process.StartInfo.UseShellExecute = true;

        // Optional: Add arguments for silent installation if available
        process.StartInfo.Arguments = "/quiet";

        process.Start();

        // Close the current application
        Application.Current.Shutdown();
    }

    // Restart the application after the update is complete
    public void RestartApplication()
    {
        var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = System.Reflection.Assembly.GetExecutingAssembly().Location;
        process.StartInfo.UseShellExecute = true;
        process.Start();

        // Shutdown the current instance
        Application.Current.Shutdown();
    }

    // Full update process: Check for updates, download, install, and restart
    public async Task<string> UpdateApplication()
    {
        // Step 1: Check for updates
        string updateCheckResult = await CheckForUpdate();

        if (updateCheckResult.Contains("new version"))
        {
            // If a new version is available, download the update
            string downloadUrl = $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest/download/installer.exe";
            string destinationPath = Path.Combine(Path.GetTempPath(), "installer.exe");

            string downloadResult = await DownloadUpdate(downloadUrl, destinationPath);
            if (downloadResult.Contains("successfully"))
            {
                // Step 2: Install the update
                InstallUpdate(destinationPath);

                // After installation, the application will shut down, and the user can restart it manually or through the installer.
                return "The application will now update and restart.";
            }
            else
            {
                return $"Failed to download the update: {downloadResult}";
            }
        }
        else
        {
            return updateCheckResult;  // "You are on the latest version."
        }
    }
}
