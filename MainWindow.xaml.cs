using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using MediaDownloader.ViewModels;

namespace MediaDownloader
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            App.Log("MainWindow construtor: iniciando InitializeComponent.");
            InitializeComponent();
            App.Log("MainWindow construtor: InitializeComponent concluído. Criando ViewModel.");

            ViewModel = new MainViewModel();
            DataContext = ViewModel;
            App.Log("MainWindow construtor: ViewModel vinculado ao DataContext.");

            ViewModel.RequestScrollLogToEnd = () =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        LogScrollViewer?.ScrollToEnd();
                        TxtLogConsole?.ScrollToEnd();
                    }
                    catch { }
                });
            };
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            App.Log("Window_Loaded disparado.");
            try
            {
                await ViewModel.EnsureDependenciesAsync();
                App.Log("Window_Loaded: EnsureDependenciesAsync concluído.");
            }
            catch (Exception ex)
            {
                App.Log($"Window_Loaded ERRO: {ex}");
            }
        }

        private void TxtUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ViewModel.CanFetchInfo)
            {
                ViewModel.FetchInfoCommand.Execute(null);
            }
        }

        private void BtnCopyLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(ViewModel.LogText))
                {
                    Clipboard.SetText(ViewModel.LogText);
                    MessageBox.Show("Logs copiados para a área de transferência!", "Informação", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Falha ao copiar logs: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                ViewModel.AppendLog($"[Aviso] Falha ao abrir link: {ex.Message}");
            }
        }
    }
}
