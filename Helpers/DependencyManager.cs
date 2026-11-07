using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MediaDownloader.Helpers
{
    public class DependencyProgressInfo
    {
        public string ComponentName { get; set; } = string.Empty;
        public double Percentage { get; set; }
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }
        public string SpeedText { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
    }

    public static class DependencyManager
    {
        public static readonly string ToolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
        public static readonly string YtDlpExe = Path.Combine(ToolsDir, "yt-dlp.exe");
        public static readonly string FfmpegExe = Path.Combine(ToolsDir, "ffmpeg.exe");
        public static readonly string FfprobeExe = Path.Combine(ToolsDir, "ffprobe.exe");

        // URLs oficiais criptografadas com AES-256 (CryptoHelper)
        public const string EncryptedYtDlpUrl = "2N8Nkix3nusDiMHQ8JVBBQ/dWhRmQBPxL9Iqqw1RvzZaYKW6Z8Z/aocyXOC4c92JQKJt/5xgCmQHxLkvLmeGIbc3KiT65FI3cR7d7wR3Jk0=";
        // GitHub Mirror como primário por ser CDN de alta velocidade (15-50 MB/s)
        public const string EncryptedFfmpegUrl = "2N8Nkix3nusDiMHQ8JVBBUb4Upn+/ouVPmNFzA8sa5CaL6En+1Zb675CFJYmlp9ZlsAdKqOLMtcF/ASO7JSoFidCozv2LDqTfYHOBWa+2HHX3E9I0OZowSnEO0Mb81OSKbyUqeeCPg6VS1QwOjbesQ==";
        // Servidor secundário/fallback (gyan.dev)
        public const string EncryptedFfmpegFallbackUrl = "sWjXhpMlB61ohyRGCphzjbTL1n+/cE8vD+SoznKhgFjJshBM/cZcQ0RdscF1njvY+PAdGxJKX++ANouV2KAELtBelpfyAqQQI38QhabV8yY=";

        public static string GetYtDlpUrl() => CryptoHelper.Decrypt(EncryptedYtDlpUrl);
        public static string GetFfmpegUrl() => CryptoHelper.Decrypt(EncryptedFfmpegUrl);
        public static string GetFfmpegFallbackUrl() => CryptoHelper.Decrypt(EncryptedFfmpegFallbackUrl);

        public static bool AreDependenciesReady()
        {
            return File.Exists(YtDlpExe) && File.Exists(FfmpegExe) &&
                   new FileInfo(YtDlpExe).Length > 1024 &&
                   new FileInfo(FfmpegExe).Length > 1024;
        }

        public static async Task EnsureDependenciesAsync(
            Action<string>? log = null,
            Action<DependencyProgressInfo>? onProgress = null,
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(ToolsDir))
            {
                Directory.CreateDirectory(ToolsDir);
            }

            using var httpClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = true
            });
            httpClient.Timeout = TimeSpan.FromMinutes(10);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko)");

            // 0. Verificar se já existem na pasta tools do projeto raiz ou cache local
            string projectTools = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "tools");
            if (Directory.Exists(projectTools))
            {
                string srcYt = Path.Combine(projectTools, "yt-dlp.exe");
                string srcFf = Path.Combine(projectTools, "ffmpeg.exe");
                string srcPr = Path.Combine(projectTools, "ffprobe.exe");

                if (!File.Exists(YtDlpExe) && File.Exists(srcYt))
                    try { File.Copy(srcYt, YtDlpExe, true); } catch { }

                if (!File.Exists(FfmpegExe) && File.Exists(srcFf))
                    try { File.Copy(srcFf, FfmpegExe, true); } catch { }

                if (!File.Exists(FfprobeExe) && File.Exists(srcPr))
                    try { File.Copy(srcPr, FfprobeExe, true); } catch { }
            }

            // 1. Verificar e baixar yt-dlp.exe
            if (!File.Exists(YtDlpExe) || new FileInfo(YtDlpExe).Length < 1024)
            {
                log?.Invoke("[Instalador] Baixando yt-dlp oficial (descriptografando URL segura)...");
                string ytUrl = GetYtDlpUrl();
                string tempYtDlp = Path.Combine(ToolsDir, "yt-dlp.exe.download");

                if (File.Exists(tempYtDlp))
                    File.Delete(tempYtDlp);

                await DownloadWithProgressAsync(httpClient, ytUrl, tempYtDlp, "yt-dlp", onProgress, cancellationToken);

                if (File.Exists(YtDlpExe))
                    File.Delete(YtDlpExe);

                File.Move(tempYtDlp, YtDlpExe);
                log?.Invoke("[Instalador] yt-dlp.exe instalado e pronto com sucesso.");
            }
            else
            {
                log?.Invoke("[Instalador] yt-dlp.exe já está instalado.");
            }

            // 2. Verificar e baixar ffmpeg.exe
            if (!File.Exists(FfmpegExe) || new FileInfo(FfmpegExe).Length < 1024)
            {
                string tempZip = Path.Combine(ToolsDir, "ffmpeg_temp.zip");
                if (File.Exists(tempZip))
                    File.Delete(tempZip);

                bool downloaded = false;
                try
                {
                    log?.Invoke("[Instalador] Baixando pacote FFmpeg oficial em alta velocidade (GitHub CDN)...");
                    string ffmpegUrl = GetFfmpegUrl();
                    await DownloadWithProgressAsync(httpClient, ffmpegUrl, tempZip, "FFmpeg (Alta Velocidade)", onProgress, cancellationToken);
                    downloaded = true;
                }
                catch (Exception ex)
                {
                    log?.Invoke($"[Aviso] Falha no servidor primário ({ex.Message}). Alternando para mirror secundário...");
                    if (File.Exists(tempZip))
                        File.Delete(tempZip);

                    string fallbackUrl = GetFfmpegFallbackUrl();
                    await DownloadWithProgressAsync(httpClient, fallbackUrl, tempZip, "FFmpeg (Fallback)", onProgress, cancellationToken);
                    downloaded = true;
                }

                if (downloaded && File.Exists(tempZip))
                {
                    log?.Invoke("[Instalador] Extraindo binários essenciais (ffmpeg.exe e ffprobe.exe)...");

                    await Task.Run(() =>
                    {
                        using var archive = ZipFile.OpenRead(tempZip);
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                entry.ExtractToFile(FfmpegExe, overwrite: true);
                            }
                            else if (entry.Name.Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                entry.ExtractToFile(FfprobeExe, overwrite: true);
                            }
                        }
                    }, cancellationToken);

                    try { File.Delete(tempZip); } catch { /* Ignore */ }

                    log?.Invoke("[Instalador] FFmpeg configurado e pronto para uso.");
                }
            }
            else
            {
                log?.Invoke("[Instalador] ffmpeg.exe já está instalado.");
            }
        }

        private static async Task DownloadWithProgressAsync(
            HttpClient client,
            string url,
            string destination,
            string componentName,
            Action<DependencyProgressInfo>? onProgress,
            CancellationToken cancellationToken)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true);

            var buffer = new byte[16384];
            long totalRead = 0;
            int bytesRead;

            var lastReportTime = DateTime.UtcNow;
            long bytesSinceLastReport = 0;
            string currentSpeed = "0 KB/s";

            while ((bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalRead += bytesRead;
                bytesSinceLastReport += bytesRead;

                var now = DateTime.UtcNow;
                var elapsed = (now - lastReportTime).TotalSeconds;
                if (elapsed >= 0.3)
                {
                    double bytesPerSec = bytesSinceLastReport / elapsed;
                    currentSpeed = FormatSpeed(bytesPerSec);
                    bytesSinceLastReport = 0;
                    lastReportTime = now;

                    double pct = totalBytes > 0 ? ((double)totalRead / totalBytes) * 100.0 : 0;
                    onProgress?.Invoke(new DependencyProgressInfo
                    {
                        ComponentName = componentName,
                        Percentage = Math.Min(100, Math.Max(0, pct)),
                        BytesDownloaded = totalRead,
                        TotalBytes = totalBytes,
                        SpeedText = currentSpeed,
                        StatusText = totalBytes > 0 
                            ? $"{FormatBytes(totalRead)} / {FormatBytes(totalBytes)} ({pct:F1}%) a {currentSpeed}"
                            : $"{FormatBytes(totalRead)} baixados a {currentSpeed}"
                    });
                }
            }

            // Report final 100%
            onProgress?.Invoke(new DependencyProgressInfo
            {
                ComponentName = componentName,
                Percentage = 100,
                BytesDownloaded = totalRead,
                TotalBytes = totalBytes > 0 ? totalBytes : totalRead,
                SpeedText = currentSpeed,
                StatusText = $"{FormatBytes(totalRead)} baixados com sucesso!"
            });
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private static string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec < 1024)
                return $"{bytesPerSec:0} B/s";
            if (bytesPerSec < 1024 * 1024)
                return $"{bytesPerSec / 1024:0.0} KB/s";
            return $"{bytesPerSec / (1024 * 1024):0.0} MB/s";
        }
    }
}

