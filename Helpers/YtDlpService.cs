using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaDownloader.Models;

namespace MediaDownloader.Helpers
{
    public class YtDlpService
    {
        private static readonly Regex DownloadProgressRegex = new(
            @"\[download\]\s+(?<percent>\d+(?:\.\d+)?)%\s+of\s+~?(?<size>\S+)\s+at\s+(?<speed>\S+)\s+ETA\s+(?<eta>\S+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DownloadSimplePercentRegex = new(
            @"\[download\]\s+(?<percent>\d+(?:\.\d+)?)%",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PlaylistItemRegex = new(
            @"\[download\]\s+Downloading\s+item\s+(?<item>\d+)\s+of\s+(?<total>\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public async Task<MediaMetadata?> FetchInfoAsync(
            string url, 
            Action<string>? log = null, 
            CancellationToken ct = default)
        {
            if (!DependencyManager.AreDependenciesReady())
            {
                log?.Invoke("[Erro] Binários do yt-dlp ou FFmpeg não encontrados.");
                return null;
            }

            log?.Invoke($"[Info] Obtendo metadados para: {url} ...");

            // Otimização para playlist: --flat-playlist extrai manifest em segundos sem travar em vídeos indisponíveis
            bool isPlaylistUrl = url.Contains("list=");
            string flatArg = isPlaylistUrl ? "--flat-playlist " : "";

            var psi = new ProcessStartInfo
            {
                FileName = DependencyManager.YtDlpExe,
                Arguments = $"{flatArg}--dump-single-json --no-warnings --skip-download \"{url}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };
            var outputSb = new StringBuilder();
            var errorSb = new StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    outputSb.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorSb.AppendLine(e.Data);
                    log?.Invoke($"[yt-dlp] {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var reg = ct.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
            });

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                log?.Invoke($"[Erro] yt-dlp finalizou com código {process.ExitCode}: {errorSb}");
                return null;
            }

            try
            {
                string json = outputSb.ToString();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var meta = new MediaMetadata
                {
                    WebpageUrl = url,
                    Title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "Sem título" : "Vídeo",
                    Id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : ""
                };

                // Canal / Uploader
                if (root.TryGetProperty("uploader", out var upProp))
                    meta.Uploader = upProp.GetString() ?? "";
                else if (root.TryGetProperty("channel", out var chProp))
                    meta.Uploader = chProp.GetString() ?? "";

                // Duração
                if (root.TryGetProperty("duration", out var durProp) && durProp.TryGetDouble(out double durSec))
                {
                    var ts = TimeSpan.FromSeconds(durSec);
                    meta.DurationString = ts.TotalHours >= 1 
                        ? ts.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) 
                        : ts.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
                }

                // Thumbnail
                if (root.TryGetProperty("thumbnail", out var thumbProp))
                {
                    meta.ThumbnailUrl = thumbProp.GetString() ?? "";
                }
                else if (root.TryGetProperty("thumbnails", out var thumbsProp) && thumbsProp.ValueKind == JsonValueKind.Array)
                {
                    string bestThumb = "";
                    foreach (var thumb in thumbsProp.EnumerateArray())
                    {
                        if (thumb.TryGetProperty("url", out var u))
                            bestThumb = u.GetString() ?? bestThumb;
                    }
                    meta.ThumbnailUrl = bestThumb;
                }

                // Playlist check
                if (root.TryGetProperty("_type", out var typeProp) && typeProp.GetString() == "playlist")
                {
                    meta.IsPlaylist = true;
                    if (root.TryGetProperty("playlist_count", out var pCount))
                        meta.PlaylistCount = pCount.GetInt32();
                    else if (root.TryGetProperty("entries", out var entriesProp) && entriesProp.ValueKind == JsonValueKind.Array)
                        meta.PlaylistCount = entriesProp.GetArrayLength();

                    meta.DurationString = $"{meta.PlaylistCount} vídeos";

                    // Se a playlist não tinha thumbnail no topo, pega a capa do primeiro vídeo
                    if (string.IsNullOrEmpty(meta.ThumbnailUrl) && root.TryGetProperty("entries", out var entriesArr) && entriesArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var entry in entriesArr.EnumerateArray())
                        {
                            if (entry.TryGetProperty("thumbnails", out var eThumbs) && eThumbs.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var t in eThumbs.EnumerateArray())
                                {
                                    if (t.TryGetProperty("url", out var u))
                                    {
                                        meta.ThumbnailUrl = u.GetString() ?? "";
                                        break;
                                    }
                                }
                            }
                            if (!string.IsNullOrEmpty(meta.ThumbnailUrl)) break;
                        }
                    }
                }

                log?.Invoke($"[Sucesso] Metadados carregados: \"{meta.Title}\" ({meta.DurationString})");
                return meta;
            }
            catch (Exception ex)
            {
                log?.Invoke($"[Erro] Falha ao analisar dados JSON: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> DownloadAsync(
            DownloadRequest request,
            Action<double, string>? onProgress = null,
            Action<string>? log = null,
            CancellationToken ct = default)
        {
            if (!DependencyManager.AreDependenciesReady())
            {
                log?.Invoke("[Erro] As dependências (yt-dlp/ffmpeg) não estão prontas.");
                return false;
            }

            if (!Directory.Exists(request.DestinationFolder))
            {
                Directory.CreateDirectory(request.DestinationFolder);
            }

            // Construção dos argumentos
            var sb = new StringBuilder();

            // Localização do ffmpeg
            sb.Append($"--ffmpeg-location \"{DependencyManager.ToolsDir}\" ");
            sb.Append("--newline ");
            sb.Append("--no-mtime ");

            // Diretório de saída
            sb.Append($"-P \"{request.DestinationFolder}\" ");

            // Nomenclatura do arquivo e playlist
            if (request.DownloadPlaylist)
            {
                sb.Append("-o \"%(playlist_title)s/%(playlist_index)02d - %(title)s [%(id)s].%(ext)s\" ");
                sb.Append("--yes-playlist ");
                sb.Append("-i "); // Ignora vídeos indisponíveis/excluídos na playlist
            }
            else
            {
                sb.Append("-o \"%(title)s [%(id)s].%(ext)s\" ");
                sb.Append("--no-playlist ");
            }

            // Metadados e Capítulos
            if (request.EmbedMetadata)
            {
                sb.Append("--embed-metadata ");
                sb.Append("--embed-chapters ");
            }

            // Legendas automáticas/oficiais embutidas
            if (request.EmbedSubtitles)
            {
                sb.Append("--write-auto-subs --embed-subs --sub-langs \"pt.*,en.*\" ");
            }

            // Áudio ou Vídeo
            if (request.IsAudioOnly)
            {
                sb.Append("-x ");
                sb.Append($"--audio-format {request.AudioFormat.ToLowerInvariant()} ");

                string qualityValue = request.AudioQuality switch
                {
                    var q when q.Contains("320") => "0", // yt-dlp VBR/CBR 0 (best)
                    var q when q.Contains("256") => "2",
                    var q when q.Contains("192") => "4",
                    var q when q.Contains("128") => "5",
                    _ => "0"
                };
                sb.Append($"--audio-quality {qualityValue} ");
                sb.Append("--embed-thumbnail ");
            }
            else
            {
                // Formato de vídeo e resolução
                string formatSelection = request.VideoQuality switch
                {
                    var q when q.Contains("4K") || q.Contains("2160") => "bv*[height<=2160]+ba/b[height<=2160]",
                    var q when q.Contains("1440") || q.Contains("2K") => "bv*[height<=1440]+ba/b[height<=1440]",
                    var q when q.Contains("1080") => "bv*[height<=1080]+ba/b[height<=1080]",
                    var q when q.Contains("720") => "bv*[height<=720]+ba/b[height<=720]",
                    var q when q.Contains("480") => "bv*[height<=480]+ba/b[height<=480]",
                    var q when q.Contains("360") => "bv*[height<=360]+ba/b[height<=360]",
                    _ => "bv*+ba/b" // Melhor disponível
                };

                sb.Append($"-f \"{formatSelection}\" ");
                sb.Append($"--merge-output-format {request.VideoFormat.ToLowerInvariant()} ");
            }

            // URL
            sb.Append($"\"{request.Url}\"");

            log?.Invoke($"[Comando] Executando yt-dlp com formato selecionado...");

            var psi = new ProcessStartInfo
            {
                FileName = DependencyManager.YtDlpExe,
                Arguments = sb.ToString(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };
            int currentPlaylistItem = 0;
            int totalPlaylistItems = 0;

            process.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;

                string line = e.Data.Trim();
                log?.Invoke(line);

                var itemMatch = PlaylistItemRegex.Match(line);
                if (itemMatch.Success)
                {
                    int.TryParse(itemMatch.Groups["item"].Value, out currentPlaylistItem);
                    int.TryParse(itemMatch.Groups["total"].Value, out totalPlaylistItems);
                }

                var match = DownloadProgressRegex.Match(line);
                if (match.Success)
                {
                    if (double.TryParse(match.Groups["percent"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double p))
                    {
                        string size = match.Groups["size"].Value;
                        string speed = match.Groups["speed"].Value;
                        string eta = match.Groups["eta"].Value;

                        double displayPercent = p;
                        string detail;

                        if (totalPlaylistItems > 0 && currentPlaylistItem > 0)
                        {
                            displayPercent = ((currentPlaylistItem - 1 + (p / 100.0)) / totalPlaylistItems) * 100.0;
                            detail = $"[Vídeo {currentPlaylistItem}/{totalPlaylistItems}] {p:F1}% de {size} a {speed} (Restante: {eta})";
                        }
                        else
                        {
                            detail = $"{p:F1}% de {size} a {speed} (Restante: {eta})";
                        }

                        onProgress?.Invoke(displayPercent, detail);
                    }
                }
                else
                {
                    var simpleMatch = DownloadSimplePercentRegex.Match(line);
                    if (simpleMatch.Success)
                    {
                        if (double.TryParse(simpleMatch.Groups["percent"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double p))
                        {
                            onProgress?.Invoke(p, $"{p:F1}%");
                        }
                    }
                    else if (line.Contains("[Merger]", StringComparison.OrdinalIgnoreCase))
                    {
                        onProgress?.Invoke(98.0, "FFmpeg: Mesclando vídeo e áudio...");
                    }
                    else if (line.Contains("[ExtractAudio]", StringComparison.OrdinalIgnoreCase))
                    {
                        onProgress?.Invoke(98.0, "FFmpeg: Extraindo e convertendo faixa de áudio...");
                    }
                    else if (line.Contains("[ThumbnailsConvertor]", StringComparison.OrdinalIgnoreCase))
                    {
                        onProgress?.Invoke(99.0, "Incorporando arte da capa/thumbnail...");
                    }
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    log?.Invoke($"[yt-dlp stderr] {e.Data.Trim()}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var reg = ct.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        log?.Invoke("[Sistema] Cancelamento solicitado pelo usuário. Finalizando processo...");
                        process.Kill(true);
                    }
                }
                catch { }
            });

            await process.WaitForExitAsync(ct);

            if (ct.IsCancellationRequested)
            {
                log?.Invoke("[Sistema] Download cancelado.");
                return false;
            }

            bool success = process.ExitCode == 0;
            if (success)
            {
                onProgress?.Invoke(100.0, "Download concluído com sucesso!");
                log?.Invoke("[Sucesso] O download foi finalizado com êxito.");
            }
            else
            {
                log?.Invoke($"[Erro] O download falhou com código de saída {process.ExitCode}.");
            }

            return success;
        }
    }
}

