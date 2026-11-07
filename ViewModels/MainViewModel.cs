using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MediaDownloader.Helpers;
using MediaDownloader.Models;
using Microsoft.Win32;

namespace MediaDownloader.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
        {
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly YtDlpService _ytDlpService = new();
        private CancellationTokenSource? _activeCts;

        // Propriedades vinculáveis
        private string _url = string.Empty;
        private string _destinationFolder;
        private bool _isAudioOnly = false;
        private string _selectedVideoQuality = "Melhor Qualidade (Best)";
        private string _selectedVideoFormat = "mp4";
        private string _selectedAudioQuality = "320 kbps (Melhor)";
        private string _selectedAudioFormat = "mp3";
        private bool _downloadPlaylist = false;
        private bool _embedMetadata = true;
        private bool _embedSubtitles = false;

        private bool _isMediaLoaded = false;
        private string _mediaTitle = string.Empty;
        private string _mediaUploader = string.Empty;
        private string _mediaDuration = string.Empty;
        private string _thumbnailUrl = string.Empty;
        private bool _isPlaylistDetected = false;
        private string _playlistCountText = string.Empty;

        private bool _isBusy = false;
        private bool _isDownloading = false;
        private string _statusMessage = "Aguardando inicialização...";
        private double _downloadProgress = 0;
        private string _downloadDetail = string.Empty;

        private bool _isLauncherActive = false;
        private double _launcherProgress = 0;
        private string _launcherStatus = string.Empty;
        private bool _isDependenciesReady = false;

        private bool _isUpdateAvailable = false;
        private string _latestVersion = string.Empty;
        private string _releaseUrl = string.Empty;
        private string _releaseNotes = string.Empty;
        private string _currentAppVersion = UpdateManager.CurrentVersionString;

        private string _logText = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        public Action? RequestScrollLogToEnd { get; set; }

        public MainViewModel()
        {
            // Pasta de Downloads padrão do usuário
            string defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                "Downloads");
            if (!Directory.Exists(defaultPath))
            {
                defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            }
            _destinationFolder = defaultPath;

            // Listas de opções
            AvailableVideoQualities = new ObservableCollection<string>
            {
                "Melhor Qualidade (Best)",
                "4K (2160p)",
                "1440p (2K)",
                "1080p (Full HD)",
                "720p (HD)",
                "480p",
                "360p"
            };

            AvailableVideoFormats = new ObservableCollection<string>
            {
                "mp4",
                "mkv",
                "webm"
            };

            AvailableAudioQualities = new ObservableCollection<string>
            {
                "320 kbps (Melhor)",
                "256 kbps (Muito Boa)",
                "192 kbps (Boa)",
                "128 kbps (Padrão)"
            };

            AvailableAudioFormats = new ObservableCollection<string>
            {
                "mp3",
                "m4a",
                "wav",
                "flac",
                "opus"
            };

            // Comandos
            FetchInfoCommand = new RelayCommand(async () => await FetchInfoAsync(), () => CanFetchInfo);
            StartDownloadCommand = new RelayCommand(async () => await StartDownloadAsync(), () => CanStartDownload);
            CancelCommand = new RelayCommand(CancelOperation, () => IsBusy);
            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            OpenFolderCommand = new RelayCommand(OpenFolder);
            ClearLogCommand = new RelayCommand(() => LogText = string.Empty);
            PasteUrlCommand = new RelayCommand(PasteUrl);
            CheckDependenciesCommand = new RelayCommand(async () => await EnsureDependenciesAsync());
            UpdateYtDlpCommand = new RelayCommand(async () => await UpdateYtDlpAsync(), () => !IsBusy && IsDependenciesReady);
            CheckAppUpdateCommand = new RelayCommand(async () => await CheckAppUpdateAsync(manual: true));
            OpenReleaseUrlCommand = new RelayCommand(OpenReleaseUrl);
            DismissUpdateBannerCommand = new RelayCommand(() => IsUpdateAvailable = false);

            AppendLog("[Sistema] Aplicativo iniciado. Verificando dependências...");
        }

        #region Getters and Setters

        public string Url
        {
            get => _url;
            set
            {
                if (SetField(ref _url, value))
                {
                    OnPropertyChanged(nameof(CanFetchInfo));
                    OnPropertyChanged(nameof(CanStartDownload));

                    if (!string.IsNullOrWhiteSpace(_url) && _url.Contains("list="))
                    {
                        DownloadPlaylist = true;
                        IsPlaylistDetected = true;
                        PlaylistCountText = "Playlist detectada";
                    }
                }
            }
        }

        public string DestinationFolder
        {
            get => _destinationFolder;
            set => SetField(ref _destinationFolder, value);
        }

        public bool IsAudioOnly
        {
            get => _isAudioOnly;
            set
            {
                if (SetField(ref _isAudioOnly, value))
                {
                    OnPropertyChanged(nameof(IsVideoOnly));
                }
            }
        }

        public bool IsVideoOnly
        {
            get => !IsAudioOnly;
            set
            {
                if (value)
                {
                    IsAudioOnly = false;
                }
            }
        }

        public string SelectedVideoQuality
        {
            get => _selectedVideoQuality;
            set => SetField(ref _selectedVideoQuality, value);
        }

        public string SelectedVideoFormat
        {
            get => _selectedVideoFormat;
            set => SetField(ref _selectedVideoFormat, value);
        }

        public string SelectedAudioQuality
        {
            get => _selectedAudioQuality;
            set => SetField(ref _selectedAudioQuality, value);
        }

        public string SelectedAudioFormat
        {
            get => _selectedAudioFormat;
            set => SetField(ref _selectedAudioFormat, value);
        }

        public bool DownloadPlaylist
        {
            get => _downloadPlaylist;
            set => SetField(ref _downloadPlaylist, value);
        }

        public bool EmbedMetadata
        {
            get => _embedMetadata;
            set => SetField(ref _embedMetadata, value);
        }

        public bool EmbedSubtitles
        {
            get => _embedSubtitles;
            set => SetField(ref _embedSubtitles, value);
        }

        public bool IsMediaLoaded
        {
            get => _isMediaLoaded;
            set => SetField(ref _isMediaLoaded, value);
        }

        public string MediaTitle
        {
            get => _mediaTitle;
            set => SetField(ref _mediaTitle, value);
        }

        public string MediaUploader
        {
            get => _mediaUploader;
            set => SetField(ref _mediaUploader, value);
        }

        public string MediaDuration
        {
            get => _mediaDuration;
            set => SetField(ref _mediaDuration, value);
        }

        public string ThumbnailUrl
        {
            get => _thumbnailUrl;
            set => SetField(ref _thumbnailUrl, value);
        }

        public bool IsPlaylistDetected
        {
            get => _isPlaylistDetected;
            set => SetField(ref _isPlaylistDetected, value);
        }

        public string PlaylistCountText
        {
            get => _playlistCountText;
            set => SetField(ref _playlistCountText, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetField(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsNotBusy));
                    OnPropertyChanged(nameof(CanFetchInfo));
                    OnPropertyChanged(nameof(CanStartDownload));
                }
            }
        }

        public bool IsNotBusy => !IsBusy;

        public bool IsDownloading
        {
            get => _isDownloading;
            set => SetField(ref _isDownloading, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public double DownloadProgress
        {
            get => _downloadProgress;
            set => SetField(ref _downloadProgress, value);
        }

        public string DownloadDetail
        {
            get => _downloadDetail;
            set => SetField(ref _downloadDetail, value);
        }

        public bool IsLauncherActive
        {
            get => _isLauncherActive;
            set => SetField(ref _isLauncherActive, value);
        }

        public double LauncherProgress
        {
            get => _launcherProgress;
            set => SetField(ref _launcherProgress, value);
        }

        public string LauncherStatus
        {
            get => _launcherStatus;
            set => SetField(ref _launcherStatus, value);
        }

        public bool IsDependenciesReady
        {
            get => _isDependenciesReady;
            set
            {
                if (SetField(ref _isDependenciesReady, value))
                {
                    OnPropertyChanged(nameof(CanFetchInfo));
                    OnPropertyChanged(nameof(CanStartDownload));
                }
            }
        }

        public string LogText
        {
            get => _logText;
            set => SetField(ref _logText, value);
        }

        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set => SetField(ref _isUpdateAvailable, value);
        }

        public string LatestVersion
        {
            get => _latestVersion;
            set => SetField(ref _latestVersion, value);
        }

        public string ReleaseUrl
        {
            get => _releaseUrl;
            set => SetField(ref _releaseUrl, value);
        }

        public string ReleaseNotes
        {
            get => _releaseNotes;
            set => SetField(ref _releaseNotes, value);
        }

        public string CurrentAppVersion
        {
            get => _currentAppVersion;
            set => SetField(ref _currentAppVersion, value);
        }

        public ObservableCollection<string> AvailableVideoQualities { get; }
        public ObservableCollection<string> AvailableVideoFormats { get; }
        public ObservableCollection<string> AvailableAudioQualities { get; }
        public ObservableCollection<string> AvailableAudioFormats { get; }

        public bool CanFetchInfo => !string.IsNullOrWhiteSpace(Url) && !IsBusy && IsDependenciesReady;
        public bool CanStartDownload => !string.IsNullOrWhiteSpace(Url) && !IsBusy && IsDependenciesReady;

        #endregion

        #region Commands

        public ICommand FetchInfoCommand { get; }
        public ICommand StartDownloadCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand PasteUrlCommand { get; }
        public ICommand CheckDependenciesCommand { get; }
        public ICommand UpdateYtDlpCommand { get; }
        public ICommand CheckAppUpdateCommand { get; }
        public ICommand OpenReleaseUrlCommand { get; }
        public ICommand DismissUpdateBannerCommand { get; }

        #endregion

        #region Actions

        public async Task EnsureDependenciesAsync()
        {
            App.Log("EnsureDependenciesAsync iniciado.");
            if (DependencyManager.AreDependenciesReady())
            {
                IsDependenciesReady = true;
                IsLauncherActive = false;
                StatusMessage = "Pronto para utilização";
                AppendLog("[Sistema] Todas as dependências (yt-dlp e FFmpeg) estão instaladas.");
                App.Log("EnsureDependenciesAsync: dependências já estavam prontas.");
                _ = Task.Run(() => CheckAppUpdateAsync(manual: false));
                return;
            }

            IsLauncherActive = true;
            IsBusy = true;
            LauncherStatus = "Iniciando instalador automático de dependências...";
            LauncherProgress = 0;

            try
            {
                using var cts = new CancellationTokenSource();
                await DependencyManager.EnsureDependenciesAsync(
                    log: msg =>
                    {
                        App.Log($"[DepLog] {msg}");
                        Application.Current?.Dispatcher?.InvokeAsync(() => AppendLog(msg));
                    },
                    onProgress: prog =>
                    {
                        Application.Current?.Dispatcher?.InvokeAsync(() =>
                        {
                            LauncherStatus = $"{prog.ComponentName}: {prog.StatusText}";
                            LauncherProgress = prog.Percentage;
                        });
                    },
                    cancellationToken: cts.Token);

                IsDependenciesReady = true;
                IsLauncherActive = false;
                StatusMessage = "Pronto para utilização";
                AppendLog("[Sistema] Componentes verificados com sucesso!");
                App.Log("EnsureDependenciesAsync: dependências instaladas com sucesso.");
                _ = Task.Run(() => CheckAppUpdateAsync(manual: false));
            }
            catch (Exception ex)
            {
                App.Log($"EnsureDependenciesAsync EXCEÇÃO: {ex}");
                LauncherStatus = $"Erro ao configurar: {ex.Message}";
                AppendLog($"[ERRO CRÍTICO] Falha ao configurar dependências: {ex.Message}");
                StatusMessage = "Falha na inicialização dos componentes.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task UpdateYtDlpAsync()
        {
            if (!DependencyManager.AreDependenciesReady() || IsBusy) return;

            IsBusy = true;
            StatusMessage = "Verificando atualizações do yt-dlp...";
            AppendLog("[Sistema] Executando comando de atualização automática (yt-dlp -U)...");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = DependencyManager.YtDlpExe,
                    Arguments = "-U",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                using var process = new Process { StartInfo = psi };
                process.OutputDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) AppendLog($"[yt-dlp] {e.Data.Trim()}"); };
                process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) AppendLog($"[yt-dlp] {e.Data.Trim()}"); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                StatusMessage = "yt-dlp verificado com sucesso.";
                AppendLog("[Sistema] Verificação de atualização finalizada.");
            }
            catch (Exception ex)
            {
                AppendLog($"[Erro ao atualizar] {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task CheckAppUpdateAsync(bool manual = false)
        {
            if (manual)
            {
                AppendLog("[Atualização] Consultando releases no GitHub...");
                StatusMessage = "Verificando atualizações do aplicativo...";
            }

            try
            {
                var update = await UpdateManager.CheckForUpdateAsync();
                if (update.IsUpdateAvailable)
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        LatestVersion = update.LatestVersion;
                        ReleaseUrl = update.HtmlUrl;
                        ReleaseNotes = update.ReleaseNotes;
                        IsUpdateAvailable = true;
                    });

                    AppendLog($"[Atualização] Nova versão encontrada: v{update.LatestVersion}! ({update.ReleaseTitle})");
                    StatusMessage = $"Nova versão v{update.LatestVersion} disponível no GitHub!";
                }
                else
                {
                    if (manual)
                    {
                        string msg = string.IsNullOrEmpty(update.ErrorMessage)
                            ? $"Você já está usando a versão mais recente (v{CurrentAppVersion})."
                            : $"Não foi possível verificar: {update.ErrorMessage}";

                        AppendLog($"[Atualização] {msg}");
                        StatusMessage = $"Aplicativo na versão v{CurrentAppVersion}";

                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            MessageBox.Show(msg, "Atualização do MediaDownloader", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                if (manual)
                {
                    AppendLog($"[Atualização] Erro ao verificar atualizações: {ex.Message}");
                    StatusMessage = "Falha ao verificar atualizações.";
                }
            }
        }

        public void OpenReleaseUrl()
        {
            try
            {
                string target = string.IsNullOrWhiteSpace(ReleaseUrl)
                    ? $"https://github.com/{UpdateManager.GitHubOwner}/{UpdateManager.GitHubRepo}/releases"
                    : ReleaseUrl;

                Process.Start(new ProcessStartInfo
                {
                    FileName = target,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppendLog($"[Erro] Falha ao abrir página de atualizações: {ex.Message}");
            }
        }

        public async Task FetchInfoAsync()
        {
            if (string.IsNullOrWhiteSpace(Url)) return;

            IsBusy = true;
            StatusMessage = "Consultando informações do link...";
            AppendLog($"[Sistema] Buscando informações para: {Url}");

            _activeCts = new CancellationTokenSource();

            try
            {
                var info = await _ytDlpService.FetchInfoAsync(
                    Url,
                    log: msg => Application.Current.Dispatcher.Invoke(() => AppendLog(msg)),
                    ct: _activeCts.Token);

                if (info != null)
                {
                    MediaTitle = info.Title;
                    MediaUploader = string.IsNullOrWhiteSpace(info.Uploader) ? "Desconhecido" : info.Uploader;
                    MediaDuration = string.IsNullOrWhiteSpace(info.DurationString) ? "N/A" : info.DurationString;
                    ThumbnailUrl = info.ThumbnailUrl;
                    IsPlaylistDetected = info.IsPlaylist;
                    PlaylistCountText = info.IsPlaylist ? $"{info.PlaylistCount} itens" : string.Empty;
                    DownloadPlaylist = info.IsPlaylist;
                    IsMediaLoaded = true;

                    StatusMessage = $"Informações carregadas: {info.Title}";
                }
                else
                {
                    StatusMessage = "Não foi possível carregar os dados do vídeo/playlist.";
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Busca cancelada.";
                AppendLog("[Sistema] Operação cancelada pelo usuário.");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro: {ex.Message}";
                AppendLog($"[Erro] {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                _activeCts?.Dispose();
                _activeCts = null;
            }
        }

        public async Task StartDownloadAsync()
        {
            if (string.IsNullOrWhiteSpace(Url)) return;

            IsBusy = true;
            IsDownloading = true;
            DownloadProgress = 0;
            DownloadDetail = "Iniciando download...";
            StatusMessage = "Baixando mídia...";

            _activeCts = new CancellationTokenSource();

            var req = new DownloadRequest
            {
                Url = Url,
                DestinationFolder = DestinationFolder,
                IsAudioOnly = IsAudioOnly,
                VideoQuality = SelectedVideoQuality,
                VideoFormat = SelectedVideoFormat,
                AudioQuality = SelectedAudioQuality,
                AudioFormat = SelectedAudioFormat,
                DownloadPlaylist = DownloadPlaylist,
                EmbedMetadata = EmbedMetadata,
                EmbedSubtitles = EmbedSubtitles
            };

            AppendLog($"[Sistema] Iniciando download: {Url}");
            AppendLog($"[Sistema] Formato: {(req.IsAudioOnly ? $"Áudio ({req.AudioFormat}) - {req.AudioQuality}" : $"Vídeo ({req.VideoFormat}) - {req.VideoQuality}")}");
            AppendLog($"[Sistema] Destino: {req.DestinationFolder}");

            try
            {
                bool success = await _ytDlpService.DownloadAsync(
                    req,
                    onProgress: (pct, detail) => Application.Current.Dispatcher.Invoke(() =>
                    {
                        DownloadProgress = pct;
                        DownloadDetail = detail;
                        StatusMessage = detail;
                    }),
                    log: msg => Application.Current.Dispatcher.Invoke(() => AppendLog(msg)),
                    ct: _activeCts.Token);

                if (success)
                {
                    StatusMessage = "Download concluído com sucesso!";
                    DownloadDetail = "100% - Arquivo salvo na pasta selecionada.";
                    DownloadProgress = 100;
                }
                else
                {
                    StatusMessage = "Download finalizado com aviso ou erro.";
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Download cancelado.";
                DownloadDetail = "Operação interrompida.";
                AppendLog("[Sistema] Download interrompido pelo usuário.");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro durante o download: {ex.Message}";
                AppendLog($"[ERRO] {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                IsDownloading = false;
                _activeCts?.Dispose();
                _activeCts = null;
            }
        }

        public void CancelOperation()
        {
            if (_activeCts != null && !_activeCts.IsCancellationRequested)
            {
                _activeCts.Cancel();
                StatusMessage = "Cancelando...";
                AppendLog("[Sistema] Enviando sinal de cancelamento...");
            }
        }

        public void BrowseFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Selecione a pasta para salvar os downloads",
                InitialDirectory = DestinationFolder
            };

            if (dialog.ShowDialog() == true)
            {
                DestinationFolder = dialog.FolderName;
                AppendLog($"[Sistema] Pasta de destino alterada para: {DestinationFolder}");
            }
        }

        public void OpenFolder()
        {
            try
            {
                if (Directory.Exists(DestinationFolder))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = DestinationFolder,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("A pasta selecionada não existe ainda.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[Erro ao abrir pasta] {ex.Message}");
            }
        }

        public void PasteUrl()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText().Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        Url = text;
                        AppendLog($"[Sistema] Link colado da área de transferência: {Url}");
                    }
                }
            }
            catch { }
        }

        public void AppendLog(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{time}] {message}\n";
            RequestScrollLogToEnd?.Invoke();
        }

        #endregion

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

