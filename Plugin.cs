using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Extensions.Registry;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf._3DViewerIDV.Models;
using neo_bpsys_wpf._3DViewerIDV.ViewModels;
using neo_bpsys_wpf._3DViewerIDV.Views;
using System;
using System.IO;

namespace neo_bpsys_wpf._3DViewerIDV;

public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // Load settings from JSON
        var configFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "neo-bpsys-wpf", "Plugins", "3DViewerIDV"
        );
        Directory.CreateDirectory(configFolder);

        // Initialize user files from defaults
        InitializeUserFiles(configFolder);

        var settingsFilePath = Path.Combine(configFolder, "Settings.json");
        var settings = ConfigureFileHelper.LoadConfig<PluginSettings>(settingsFilePath);

        // Save settings when properties change
        settings.PropertyChanged += (sender, args) =>
        {
            ConfigureFileHelper.SaveConfig(settingsFilePath, settings);
        };

        // Register settings as singleton
        services.AddSingleton(settings);

        // Register windows and pages
        services.AddFrontedWindow<StatsViewerWindow, StatsViewerWindowViewModel>();
        services.AddBackendPage<SettingsPage, SettingsPageViewModel>();
    }

    private void InitializeUserFiles(string configFolder)
    {
        var wwwrootPath = Path.Combine(configFolder, "wwwroot");

        // Copy viewer_settings.json from default if it doesn't exist
        var settingsDefaultPath = Path.Combine(wwwrootPath, "viewer_settings.default.json");
        var settingsPath = Path.Combine(wwwrootPath, "viewer_settings.json");
        if (!File.Exists(settingsPath) && File.Exists(settingsDefaultPath))
        {
            File.Copy(settingsDefaultPath, settingsPath);
            System.Diagnostics.Debug.WriteLine($"[Plugin] Copied default settings to {settingsPath}");
        }

        // Copy scene.glb from default if it doesn't exist
        var sceneDefaultPath = Path.Combine(wwwrootPath, "assets", "scene.default.glb");
        var scenePath = Path.Combine(wwwrootPath, "assets", "scene.glb");
        if (!File.Exists(scenePath) && File.Exists(sceneDefaultPath))
        {
            File.Copy(sceneDefaultPath, scenePath);
            System.Diagnostics.Debug.WriteLine($"[Plugin] Copied default scene to {scenePath}");
        }

        // Copy theatre_state.json from default if it doesn't exist
        var theatreStateDefaultPath = Path.Combine(wwwrootPath, "theatre_state.default.json");
        var theatreStatePath = Path.Combine(wwwrootPath, "theatre_state.json");
        if (!File.Exists(theatreStatePath) && File.Exists(theatreStateDefaultPath))
        {
            File.Copy(theatreStateDefaultPath, theatreStatePath);
            System.Diagnostics.Debug.WriteLine($"[Plugin] Copied default theatre state to {theatreStatePath}");
        }
    }
}
