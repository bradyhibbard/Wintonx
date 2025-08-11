using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Winton.Views
{
    /// <summary>
    /// GitHub Release-based updater for WPF (.NET).
    /// - Auto-checks on startup (throttled to once/day via %LocalAppData%\Winton\update_state.json).
    /// - Compares against AssemblyInformationalVersion (fallback: AssemblyVersion).
    /// - Prompts user; downloads & launches best asset (.appinstaller/.msix/.exe/.zip fallback).
    /// </summary>
    public class UpdateChecker
    {
        // TODO: Confirm these to match your repo
        private const string Owner = "bradyhibbard";
        private const string Repo = "Wintonx";
        private static readonly string LatestReleaseUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";

        private static readonly string AppFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Winton");
        private static readonly string StatePath = Path.Combine(AppFolder, "update_state.json");

        private static readonly HttpClient Http = BuildHttpClient();

        public record UpdateState([property: JsonPropertyName("last_check_utc")] DateTime? LastCheckUtc);
        public record GithubAsset(
            [property: JsonPropertyName("name")] string Name,
            [property: JsonPropertyName("browser_download_url")] string DownloadUrl);
        public record GithubRelease(
            [property: JsonPropertyName("tag_name")] string TagName,
            [property: JsonPropertyName("prerelease")] bool PreRelease,
            [property: JsonPropertyName("draft")] bool Draft,
            [property: JsonPropertyName("assets")] GithubAsset[] Assets);

        /// <summary>Call on startup. Will silently skip if already checked today.</summary>
        public async Task AutoCheckOnStartupAsync(bool showNoUpdateToast = false, CancellationToken ct = default)
        {
            try
            {
                if (!ShouldCheckToday())
                    return;

                var result = await CheckForUpdateAsync(ct);
                SaveLastCheck();

                if (result is null)
                {
                    if (showNoUpdateToast)
                        ShowInfo("You're on the latest version.");
                    return;
                }

                var (latestVersion, asset) = result.Value;
                var msg = $"A new version {latestVersion} is available.\n\nDownload and install now?";
                var choice = MessageBox.Show(msg, "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (choice == MessageBoxResult.Yes)
                {
                    await DownloadAndInstallAsync(asset, ct);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateChecker] AutoCheck failed: {ex}");
                // swallow on startup
            }
        }

        /// <summary>Call from a menu item or button: runs a check immediately.</summary>
        public async Task ManualCheckAsync(CancellationToken ct = default)
        {
            try
            {
                var result = await CheckForUpdateAsync(ct);
                SaveLastCheck();

                if (result is null)
                {
                    ShowInfo("You're on the latest version.");
                    return;
                }

                var (latestVersion, asset) = result.Value;
                var msg = $"A new version {latestVersion} is available.\n\nDownload and install now?";
                var choice = MessageBox.Show(msg, "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (choice == MessageBoxResult.Yes)
                {
                    await DownloadAndInstallAsync(asset, ct);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Update check failed:\n\n{ex.Message}");
            }
        }

        private static HttpClient BuildHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Winton", GetCurrentVersionString()));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            client.Timeout = TimeSpan.FromSeconds(20);
            return client;
        }

        private static Version GetCurrentVersion()
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info) && Version.TryParse(TrimVersionPrefix(info), out var v1))
                return v1;

            var v = asm.GetName().Version ?? new Version(0, 0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
        }

        private static string GetCurrentVersionString() => GetCurrentVersion().ToString();

        private static string TrimVersionPrefix(string v) =>
            v.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? v[1..] : v;

        private async Task<(Version latestVersion, GithubAsset asset)?> CheckForUpdateAsync(CancellationToken ct)
        {
            var current = GetCurrentVersion();
            var release = await GetLatestReleaseAsync(ct);
            if (release is null || release.Draft) return null;

            var latestTag = TrimVersionPrefix(release.TagName ?? "");
            if (!Version.TryParse(latestTag, out var latest)) return null;

            if (latest <= current) return null;

            var asset = ChooseBestAsset(release.Assets);
            if (asset is null) return null;

            return (latest, asset);
        }

        private static GithubAsset? ChooseBestAsset(GithubAsset[] assets)
        {
            if (assets is null || assets.Length == 0) return null;

            // Prefer appinstaller/msix → exe → zip
            GithubAsset? pick = null;
            pick ??= Array.Find(assets, a => a.Name.EndsWith(".appinstaller", StringComparison.OrdinalIgnoreCase));
            pick ??= Array.Find(assets, a => a.Name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase) || a.Name.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase));
            pick ??= Array.Find(assets, a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            pick ??= Array.Find(assets, a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            return pick;
        }

        private async Task<GithubRelease?> GetLatestReleaseAsync(CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var release = await JsonSerializer.DeserializeAsync<GithubRelease>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }, ct);
            return release;
        }

        private async Task DownloadAndInstallAsync(GithubAsset asset, CancellationToken ct)
        {
            // Best UX: let Windows handle appinstaller/msix
            if (asset.Name.EndsWith(".appinstaller", StringComparison.OrdinalIgnoreCase) ||
                asset.Name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase) ||
                asset.Name.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase))
            {
                LaunchUrl(asset.DownloadUrl);
                Application.Current?.Shutdown();
                return;
            }

            // Otherwise: download to temp and run
            var tempDir = Path.Combine(Path.GetTempPath(), "Winton_Update");
            Directory.CreateDirectory(tempDir);
            var destPath = Path.Combine(tempDir, asset.Name);

            await DownloadFileAsync(asset.DownloadUrl, destPath, ct);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = destPath,
                    UseShellExecute = true,
                    Verb = "open",
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to launch installer:\n{ex.Message}");
                return;
            }

            Application.Current?.Shutdown();
        }

        private static async Task DownloadFileAsync(string url, string destPath, CancellationToken ct)
        {
            using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            await using var input = await resp.Content.ReadAsStreamAsync(ct);
            await using var output = File.Create(destPath);
            await input.CopyToAsync(output, ct);
        }

        private static void LaunchUrl(string url)
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
                Verb = "open"
            };
            Process.Start(psi);
        }

        private static bool ShouldCheckToday()
        {
            try
            {
                if (!File.Exists(StatePath)) return true;
                var json = File.ReadAllText(StatePath);
                var state = JsonSerializer.Deserialize<UpdateState>(json);
                if (state?.LastCheckUtc is null) return true;
                return (DateTime.UtcNow - state.LastCheckUtc.Value) > TimeSpan.FromDays(1);
            }
            catch
            {
                return true;
            }
        }

        private static void SaveLastCheck()
        {
            try
            {
                Directory.CreateDirectory(AppFolder);
                var json = JsonSerializer.Serialize(new UpdateState(DateTime.UtcNow));
                File.WriteAllText(StatePath, json);
            }
            catch { /* noop */ }
        }

        private static void ShowInfo(string msg) =>
            MessageBox.Show(msg, "Winton", MessageBoxButton.OK, MessageBoxImage.Information);

        private static void ShowError(string msg) =>
            MessageBox.Show(msg, "Winton", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
