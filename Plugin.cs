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
}
