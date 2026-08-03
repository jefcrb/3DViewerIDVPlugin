using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf._3DViewerIDV.Models;
using neo_bpsys_wpf._3DViewerIDV.Services;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace neo_bpsys_wpf._3DViewerIDV.ViewModels;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly PluginSettings _settings;
    private readonly StatsViewerWindowViewModel _statsViewModel;
    private WebServer? _webServer;
    private CharacterDownloadService? _downloadService;

    [ObservableProperty]
    private bool _isServerRunning;

    [ObservableProperty]
    private string _serverUrl = "Server stopped";

    [ObservableProperty]
    private string _devModeUrl = "Server stopped";

    [ObservableProperty]
    private int _portInput;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private int _downloadedHunters;

    [ObservableProperty]
    private int _downloadedSurvivors;

    [ObservableProperty]
    private int _totalHunters;

    [ObservableProperty]
    private int _totalSurvivors;

    [ObservableProperty]
    private string _downloadProgress = "0/0";

    [ObservableProperty]
    private string _currentDownloadCharacter = "";

    private readonly string _settingsFilePath;

    public SettingsPageViewModel(PluginSettings settings, StatsViewerWindowViewModel statsViewModel)
    {
        _settings = settings;
        _statsViewModel = statsViewModel;
        PortInput = _settings.WebServerPort;

        var configFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "neo-bpsys-wpf", "Plugins", "3DViewerIDV"
        );
        _settingsFilePath = Path.Combine(configFolder, "Settings.json");

        // Initialize download service
        _downloadService = new CharacterDownloadService();
        _downloadService.ProgressChanged += OnDownloadProgressChanged;
        _downloadService.DownloadCompleted += OnDownloadCompleted;

        // Load downloaded counts
        _ = RefreshDownloadedCountsAsync();
    }

    [RelayCommand]
    private void StartServer()
    {
        if (_webServer != null)
        {
            return;
        }

        try
        {
            // Update port from input
            _settings.WebServerPort = PortInput;
            SaveSettings();

            _webServer = new WebServer(() => _statsViewModel.HunterDataJson, _settings.WebServerPort);
            _webServer.Start();

            IsServerRunning = true;
            ServerUrl = $"http://localhost:{_settings.WebServerPort}";
            DevModeUrl = $"http://localhost:{_settings.WebServerPort}?dev=true";
        }
        catch (Exception ex)
        {
            ServerUrl = $"Failed to start: {ex.Message}";
            DevModeUrl = "Server failed to start";
            _webServer = null;
            IsServerRunning = false;
        }
    }

    [RelayCommand]
    private void StopServer()
    {
        if (_webServer == null)
        {
            return;
        }

        try
        {
            _webServer.Stop();
            _webServer.Dispose();
            _webServer = null;

            IsServerRunning = false;
            ServerUrl = "Server stopped";
            DevModeUrl = "Server stopped";
        }
        catch (Exception ex)
        {
            ServerUrl = $"Error: {ex.Message}";
            DevModeUrl = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenServerUrl()
    {
        if (IsServerRunning)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ServerUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open URL: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void OpenDevModeUrl()
    {
        if (IsServerRunning)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = DevModeUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open dev URL: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void CopyServerUrl()
    {
        if (!IsServerRunning) return;
        try
        {
            System.Windows.Clipboard.SetText(ServerUrl);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to copy URL: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        try
        {
            ConfigureFileHelper.SaveConfig(_settingsFilePath, _settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    [RelayCommand]
    private void UploadScene()
    {
        try
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Scene File (.glb)",
                Filter = "GLB Files (*.glb)|*.glb|All Files (*.*)|*.*",
                DefaultExt = ".glb"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var scenePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "neo-bpsys-wpf", "Plugins", "3DViewerIDV", "wwwroot", "assets", "scene.glb"
                );

                File.Copy(openFileDialog.FileName, scenePath, overwrite: true);
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Scene uploaded from {openFileDialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to upload scene: {ex.Message}");
        }
    }

    [RelayCommand]
    private void DownloadScene()
    {
        try
        {
            var scenePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "neo-bpsys-wpf", "Plugins", "3DViewerIDV", "wwwroot", "assets", "scene.glb"
            );

            if (!File.Exists(scenePath))
            {
                System.Diagnostics.Debug.WriteLine("[SettingsPage] Scene file does not exist");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Title = "Save Scene File",
                Filter = "GLB Files (*.glb)|*.glb|All Files (*.*)|*.*",
                DefaultExt = ".glb",
                FileName = "scene.glb"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                File.Copy(scenePath, saveFileDialog.FileName, overwrite: true);
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Scene downloaded to {saveFileDialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to download scene: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ImportViewerSettings()
    {
        try
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select viewer_settings.json",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".json"
            };

            if (openFileDialog.ShowDialog() != true) return;

            // Validate it's parseable JSON before overwriting (same shape check as the
            // dev page's import flow).
            string text;
            try
            {
                text = File.ReadAllText(openFileDialog.FileName);
                using var _ = System.Text.Json.JsonDocument.Parse(text);
            }
            catch (Exception parseEx)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Import rejected — invalid JSON: {parseEx.Message}");
                return;
            }

            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "neo-bpsys-wpf", "Plugins", "3DViewerIDV", "wwwroot", "viewer_settings.json"
            );

            File.WriteAllText(settingsPath, text);
            System.Diagnostics.Debug.WriteLine($"[SettingsPage] Imported viewer_settings.json from {openFileDialog.FileName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to import viewer settings: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ExportViewerSettings()
    {
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "neo-bpsys-wpf", "Plugins", "3DViewerIDV", "wwwroot", "viewer_settings.json"
            );

            if (!File.Exists(settingsPath))
            {
                System.Diagnostics.Debug.WriteLine("[SettingsPage] viewer_settings.json does not exist");
                return;
            }

            // Default to a timestamped name so users can keep multiple snapshots,
            // matching the dev page's exportSettings() behaviour.
            var stamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            var saveFileDialog = new SaveFileDialog
            {
                Title = "Save viewer_settings.json",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".json",
                FileName = $"viewer_settings_{stamp}.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                File.Copy(settingsPath, saveFileDialog.FileName, overwrite: true);
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Exported viewer_settings.json to {saveFileDialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to export viewer settings: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ShowInExplorer()
    {
        try
        {
            var scenePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "neo-bpsys-wpf", "Plugins", "3DViewerIDV", "wwwroot", "assets", "scene.glb"
            );

            if (File.Exists(scenePath))
            {
                Process.Start("explorer.exe", $"/select,\"{scenePath}\"");
            }
            else
            {
                // If scene.glb doesn't exist, just open the assets folder
                var assetsPath = Path.GetDirectoryName(scenePath);
                if (Directory.Exists(assetsPath))
                {
                    Process.Start("explorer.exe", assetsPath);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to show in explorer: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DownloadCharactersAsync()
    {
        if (_downloadService == null || IsDownloading) return;

        try
        {
            IsDownloading = true;
            await _downloadService.DownloadAllCharactersAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to download characters: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CancelDownload()
    {
        if (_downloadService != null && IsDownloading)
        {
            _downloadService.CancelDownload();
            IsDownloading = false;
            CurrentDownloadCharacter = "Download cancelled";
        }
    }

    [RelayCommand]
    private async Task RefreshDownloadedCountsAsync()
    {
        if (_downloadService == null) return;

        try
        {
            var (totalHunters, totalSurvivors) = await _downloadService.GetCatalogTotalsAsync();
            TotalHunters = totalHunters;
            TotalSurvivors = totalSurvivors;

            var (hunters, survivors) = await _downloadService.GetDownloadedCountAsync();
            DownloadedHunters = hunters;
            DownloadedSurvivors = survivors;
            DownloadProgress = $"{hunters + survivors}/{totalHunters + totalSurvivors}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to refresh counts: {ex.Message}");
        }
    }

    private void OnDownloadProgressChanged(object? sender, DownloadProgressEventArgs e)
    {
        DownloadedHunters = e.DownloadedCount - (e.Type == "Survivor" ? (e.DownloadedCount - TotalHunters) : 0);
        DownloadedSurvivors = e.Type == "Survivor" ? (e.DownloadedCount - TotalHunters) : 0;
        DownloadProgress = $"{e.DownloadedCount}/{e.TotalCount}";
        CurrentDownloadCharacter = $"Downloading {e.Type}: {e.CharacterName}";
    }

    private void OnDownloadCompleted(object? sender, DownloadCompletedEventArgs e)
    {
        IsDownloading = false;

        if (e.Success)
        {
            CurrentDownloadCharacter = $"Download completed! {e.TotalDownloaded}/{e.TotalCharacters} characters";
            _ = RefreshDownloadedCountsAsync();
        }
        else
        {
            CurrentDownloadCharacter = string.IsNullOrEmpty(e.ErrorMessage)
                ? "Download failed"
                : $"Download failed: {e.ErrorMessage}";
        }
    }

    public void Dispose()
    {
        StopServer();

        if (_downloadService != null)
        {
            _downloadService.ProgressChanged -= OnDownloadProgressChanged;
            _downloadService.DownloadCompleted -= OnDownloadCompleted;
            _downloadService.Dispose();
        }
    }
}
