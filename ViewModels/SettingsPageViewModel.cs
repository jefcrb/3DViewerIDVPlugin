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

namespace neo_bpsys_wpf._3DViewerIDV.ViewModels;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly PluginSettings _settings;
    private readonly StatsViewerWindowViewModel _statsViewModel;
    private WebServer? _webServer;

    [ObservableProperty]
    private bool _isServerRunning;

    [ObservableProperty]
    private string _serverUrl = "Server stopped";

    [ObservableProperty]
    private int _portInput;

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
        }
        catch (Exception ex)
        {
            ServerUrl = $"Failed to start: {ex.Message}";
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
        }
        catch (Exception ex)
        {
            ServerUrl = $"Error: {ex.Message}";
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

    public void Dispose()
    {
        StopServer();
    }
}
