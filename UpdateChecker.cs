using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace Winton.Views
{
    public class UpdateChecker
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/bradyhibbard/Winton/releases/latest";
        private const string CurrentVersion = "v1.0.0"; // Replace with actual app version
        private const string DownloadFolder = "C:\\Temp\\WintonUpdate";  // Temp download folder for the update

        // Replace this with your GitHub Personal Access Token (keep it secure)
        private const string GitHubToken = "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"; // Add your GitHub token here

        public async Task<string> UpdateApplication()
        {
            using (var client = new HttpClient())
            {
                // Set up the request headers
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WintonApp", "1.0"));

                // Add the Authorization header with the GitHub token (for private repositories or avoiding rate limits)
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", GitHubToken);

                HttpResponseMessage response = await client.GetAsync(GitHubApiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return $"Failed to check for updates. Status code: {response.StatusCode}";
                }

                // Parse the response
                string jsonResponse = await response.Content.ReadAsStringAsync();
                var latestRelease = JsonSerializer.Deserialize<GitHubRelease>(jsonResponse);

                // Compare versions
                if (latestRelease.TagName == CurrentVersion)
                {
                    return "You are already using the latest version.";
                }

                // Download the release asset
                var asset = latestRelease.Assets[0];  // Assuming the first asset is the correct one
                string downloadUrl = asset.BrowserDownloadUrl;

                await DownloadAndInstallUpdate(downloadUrl, asset.Name);
                return "Update downloaded and installed.";
            }
        }

        private async Task DownloadAndInstallUpdate(string downloadUrl, string fileName)
        {
            if (!Directory.Exists(DownloadFolder))
            {
                Directory.CreateDirectory(DownloadFolder);
            }

            string downloadFilePath = Path.Combine(DownloadFolder, fileName);

            // Download the file
            using (var client = new HttpClient())
            {
                var downloadStream = await client.GetStreamAsync(downloadUrl);
                using (var fileStream = new FileStream(downloadFilePath, FileMode.Create))
                {
                    await downloadStream.CopyToAsync(fileStream);
                }
            }

            // Unzip the file if it's a zip archive
            if (fileName.EndsWith(".zip"))
            {
                string extractPath = Path.Combine(DownloadFolder, "extracted");
                ZipFile.ExtractToDirectory(downloadFilePath, extractPath);

                ReplaceApplication(extractPath);
            }
        }

        private void ReplaceApplication(string extractPath)
        {
            string appExecutablePath = Path.Combine(extractPath, "Winton.exe");

            // Run the new executable (installer or replace the current executable)
            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = appExecutablePath,
                UseShellExecute = true
            };

            // Optionally kill the current process before starting the update
            Process.Start(processStartInfo);

            // Optionally: Kill the current application after launching the update
            Application.Current.Shutdown();
        }

        private class GitHubRelease
        {
            public string TagName { get; set; }
            public GitHubAsset[] Assets { get; set; }
        }

        private class GitHubAsset
        {
            public string Name { get; set; }
            public string BrowserDownloadUrl { get; set; }
        }
    }
}
