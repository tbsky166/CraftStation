using System.Windows;
using System.IO;
using CraftStation.Core;
using CraftStation.Core.Services;
using CraftStation.Core.Utils;
using CraftStation.Services;
using CraftStation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace CraftStation;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "UI 未处理异常");
            MessageBox.Show(
                $"CraftStation 遇到未处理异常：\n\n{args.Exception.Message}\n\n详情已写入日志。",
                "CraftStation",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Log.Error(ex, "AppDomain 未处理异常");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "未观察的任务异常");
            args.SetObserved();
        };

        var services = new ServiceCollection();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDownloadMirror>(sp =>
            new DownloadMirror(sp.GetRequiredService<ISettingsService>().Settings));
        services.AddSingleton<ILauncherService, LauncherService>();
        services.AddSingleton<IInstanceManager, InstanceManager>();
        services.AddSingleton<IAccountService, AccountService>();
        services.AddSingleton<IMsalAppFactory, WindowsMsalAppFactory>();
        services.AddSingleton<IJavaService, JavaService>();
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<IModLoaderInstaller, ModLoaderInstaller>();
        services.AddSingleton<IResourceManager, ResourceManager>();
        services.AddSingleton<IModrinthService, ModrinthService>();
        services.AddSingleton<IModpackService, ModpackService>();
        services.AddSingleton<IModHealthService, ModHealthService>();
        services.AddSingleton<IServerService, ServerService>();
        services.AddSingleton<ISkinService, SkinService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<HtmlBridge>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<VersionsViewModel>();
        services.AddSingleton<InstancesViewModel>();
        services.AddSingleton<ResourcesViewModel>();
        services.AddSingleton<ModHealthViewModel>();
        services.AddSingleton<ServersViewModel>();
        services.AddSingleton<AccountsViewModel>();
        services.AddSingleton<StoreViewModel>();
        services.AddSingleton<SettingsViewModel>();
        Services = services.BuildServiceProvider();

        try
        {
            var settings = Services.GetRequiredService<ISettingsService>();
            await settings.LoadAsync();
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(settings.LogsDirectory, Config.LauncherLogFilePattern),
                    rollingInterval: RollingInterval.Day)
                .CreateLogger();
            Log.Information("CraftStation 启动，数据目录：{DataDirectory}", settings.DataDirectory);

            await Services.GetRequiredService<IInstanceManager>().LoadAsync();
            // 已有实例也统一预置中文语言
            var instanceManager = Services.GetRequiredService<IInstanceManager>();
            foreach (var instance in instanceManager.Instances)
                GameOptionsHelper.EnsureChineseLanguage(instanceManager.GetGameDirectory(instance));
            await Services.GetRequiredService<IAccountService>().InitializeAsync();
            await Services.GetRequiredService<IServerService>().LoadAsync();

            // 默认启动入口：WebView2 内嵌 HTML 界面（fz.wiki 完整复刻）
            var window = new WebPreviewWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            try
            {
                Log.Error(ex, "CraftStation 启动失败");
            }
            catch
            {
                // 日志不可用时忽略
            }
            MessageBox.Show(
                $"CraftStation 启动失败：\n\n{ex.Message}\n\n" +
                "请确认程序所在目录可写（不要在 Program Files 等受保护目录运行），" +
                "并已安装 WebView2 Runtime。",
                "CraftStation",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
