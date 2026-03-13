using System;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf._3DViewerIDV.ViewModels;

namespace neo_bpsys_wpf._3DViewerIDV.Views;

[FrontedWindowInfo(id: "StatsViewerWindow", name: "3D View", canvas: ["MainCanvas"])]
public partial class StatsViewerWindow : Window
{
    private readonly StatsViewerWindowViewModel _viewModel;
    private bool _isInitialized = false;

    public StatsViewerWindow(StatsViewerWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += StatsViewerWindow_Loaded;
    }

    private async void StatsViewerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var userDataFolder = Path.Combine(AppConstants.AppDataPath, "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);

            await WebView.EnsureCoreWebView2Async(environment);

            var pluginFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "neo-bpsys-wpf", "Plugins", "3DViewerIDV"
            );

            var wwwrootFolder = Path.Combine(pluginFolder, "wwwroot");

            if (!Directory.Exists(wwwrootFolder))
            {
                MessageBox.Show($"wwwroot folder not found: {wwwrootFolder}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Map virtual host to wwwroot folder - this allows loading local files without CORS issues
            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "app.local",
                wwwrootFolder,
                CoreWebView2HostResourceAccessKind.Allow
            );

            // Navigate to the scene.html using virtual host
            WebView.CoreWebView2.Navigate("https://app.local/scene.html");

            // Wait for navigation to complete, then inject hunter data
            WebView.CoreWebView2.NavigationCompleted += async (s, args) =>
            {
                if (args.IsSuccess && !_isInitialized)
                {
                    _isInitialized = true;
                    await UpdateHunterData();
                }
            };

            // Subscribe to ViewModel property changes
            _viewModel.PropertyChanged += async (s, args) =>
            {
                if (args.PropertyName == nameof(StatsViewerWindowViewModel.HunterDataJson) && _isInitialized)
                {
                    await UpdateHunterData();
                }
            };
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Error initializing WebView2: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async System.Threading.Tasks.Task UpdateHunterData()
    {
        try
        {
            var hunterJson = _viewModel.HunterDataJson;
            var script = $@"
                if (typeof loadHunterFromJson === 'function') {{
                    loadHunterFromJson({hunterJson});
                }}
            ";
            await WebView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            // Silent fail - page might not be ready yet
            System.Diagnostics.Debug.WriteLine($"Failed to update hunter data: {ex.Message}");
        }
    }
}
