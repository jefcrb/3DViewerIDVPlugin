using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Extensions.Registry;
using neo_bpsys_wpf._3DViewerIDV.ViewModels;
using neo_bpsys_wpf._3DViewerIDV.Views;

namespace neo_bpsys_wpf._3DViewerIDV;

public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddFrontedWindow<StatsViewerWindow, StatsViewerWindowViewModel>();
    }
}
