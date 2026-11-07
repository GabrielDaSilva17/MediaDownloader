using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace MediaDownloader.Helpers
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseTitle { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public string? DownloadUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public static class UpdateManager
    {
        public const string GitHubOwner = "GabrielDaSilva17";
        public const string GitHubRepo = "MediaDownloader";
        public static readonly string CurrentVersionString = GetCurrentVersion();

        private static string GetCurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
            return "1.0.0";
        }

        public static async Task<UpdateInfo> CheckForUpdateAsync()
        {
            var result = new UpdateInfo
            {
                CurrentVersion = CurrentVersionString
            };

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(8);
                // GitHub API exige User-Agent
                client.DefaultRequestHeaders.UserAgent.ParseAdd("MediaDownloader-App/1.0");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");

                string apiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
                var response = await client.GetAsync(apiUrl);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    result.ErrorMessage = "Nenhum release encontrado no repositório ainda.";
                    return result;
                }

                if (!response.IsSuccessStatusCode)
                {
                    result.ErrorMessage = $"GitHub retornou status {response.StatusCode}";
                    return result;
                }

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
                string releaseName = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? "" : "";
                string htmlUrl = root.TryGetProperty("html_url", out var urlElem) ? urlElem.GetString() ?? "" : "";
                string body = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";

                // Limpa tag: "v1.0.1" -> "1.0.1"
                string cleanTag = tagName.TrimStart('v', 'V').Trim();
                result.LatestVersion = cleanTag;
                result.ReleaseTitle = string.IsNullOrWhiteSpace(releaseName) ? tagName : releaseName;
                result.HtmlUrl = string.IsNullOrWhiteSpace(htmlUrl) ? $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases" : htmlUrl;
                result.ReleaseNotes = body;

                // Tenta encontrar asset .zip
                if (root.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsElem.EnumerateArray())
                    {
                        if (asset.TryGetProperty("name", out var assetNameElem) &&
                            asset.TryGetProperty("browser_download_url", out var dlElem))
                        {
                            string aName = assetNameElem.GetString() ?? "";
                            if (aName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                result.DownloadUrl = dlElem.GetString();
                                if (aName.Contains("Completo", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Prioriza o zip completo se existir
                                    break;
                                }
                            }
                        }
                    }
                }

                // Compara as versões
                if (TryParseVersion(cleanTag, out var remoteVer) && TryParseVersion(CurrentVersionString, out var currentVer))
                {
                    if (remoteVer > currentVer)
                    {
                        result.IsUpdateAvailable = true;
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private static bool TryParseVersion(string input, out Version version)
        {
            // Trata formatos como "1.0", "1.0.1", "1.0.0.0"
            string[] parts = input.Split(new[] { '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
            {
                int build = parts.Length >= 3 && int.TryParse(parts[2], out int b) ? b : 0;
                int rev = parts.Length >= 4 && int.TryParse(parts[3], out int r) ? r : 0;
                version = new Version(major, minor, build, rev);
                return true;
            }

            version = new Version(0, 0, 0, 0);
            return false;
        }
    }
}

