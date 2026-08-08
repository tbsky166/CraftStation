using System.Windows;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraftStation;
using CraftStation.Core.Models;
using CraftStation.Core.Services;
using Serilog;

namespace CraftStation.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IUpdateService _updater;
    private readonly IInstanceManager _instances;

    public SettingsViewModel(ISettingsService settings, IUpdateService updater, IInstanceManager instances)
    {
        _settings = settings;
        _updater = updater;
        _instances = instances;
        GameDirectory = settings.ResolveGameDirectory();
        DownloadSource = settings.Settings.DownloadSource.ToString();
        FallbackEnabled = settings.Settings.FallbackToOfficial;
        Proxy = settings.Settings.Proxy ?? "";
        MaxConcurrency = settings.Settings.MaxConcurrency;
        UpdateEndpoint = settings.Settings.UpdateEndpoint;
        CurseForgeApiKey = settings.Settings.CurseForgeApiKey;
    }

    public string[] DownloadSources { get; } = { "Bmclapi", "Mojang", "Custom" };

    [ObservableProperty]
    private string _gameDirectory;

    [ObservableProperty]
    private string _downloadSource;

    [ObservableProperty]
    private bool _fallbackEnabled;

    [ObservableProperty]
    private string _proxy;

    [ObservableProperty]
    private int _maxConcurrency;

    [ObservableProperty]
    private string _updateEndpoint;

    [ObservableProperty]
    private string _curseForgeApiKey;

    [ObservableProperty]
    private string _statusText = "";

    [RelayCommand]
    private async Task SaveAsync()
    {
        var s = _settings.Settings;
        s.GameDirectory = GameDirectory;
        s.DownloadSource = Enum.TryParse<DownloadSourceKind>(DownloadSource, out var source) ? source : DownloadSourceKind.Bmclapi;
        s.FallbackToOfficial = FallbackEnabled;
        s.Proxy = string.IsNullOrWhiteSpace(Proxy) ? null : Proxy.Trim();
        s.MaxConcurrency = Math.Clamp(MaxConcurrency, 1, 64);
        s.UpdateEndpoint = UpdateEndpoint.Trim();
        s.CurseForgeApiKey = CurseForgeApiKey.Trim();
        await _settings.SaveAsync();
        StatusText = "设置已保存";
    }

    [RelayCommand]
    private void BrowseGameDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            InitialDirectory = GameDirectory
        };
        if (dialog.ShowDialog() == true)
            GameDirectory = dialog.FolderName;
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        Directory.CreateDirectory(_settings.DataDirectory);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = _settings.DataDirectory,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenHtmlPreview()
    {
        try
        {
            Log.Information("打开 Web 预览窗口");
            var window = new WebPreviewWindow { Owner = Application.Current.MainWindow };
            window.Show();
            Log.Information("Web 预览窗口已显示 visible={Visible}", window.IsVisible);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开 Web 预览窗口失败");
        }
    }

    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        var info = await _updater.CheckAsync();
        StatusText = info == null
            ? "未配置更新源或检查失败"
            : info.IsNewer
                ? $"发现新版本 {info.Version}：{info.Url}"
                : $"当前已是最新版本（{info.Version}）";
    }

    [RelayCommand]
    private async Task ReloadInstancesAsync()
    {
        await _instances.LoadAsync();
        StatusText = "实例已重新加载";
    }
}
