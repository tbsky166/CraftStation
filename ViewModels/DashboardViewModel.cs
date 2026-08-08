using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraftStation.Core.Models;
using CraftStation.Core.Services;

namespace CraftStation.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IInstanceManager _instances;
    private readonly IAccountService _accounts;
    private readonly ILauncherService _launcher;
    private readonly IServerService _servers;

    public DashboardViewModel(
        IInstanceManager instances,
        IAccountService accounts,
        ILauncherService launcher,
        IServerService servers)
    {
        _instances = instances;
        _accounts = accounts;
        _launcher = launcher;
        _servers = servers;
    }

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private int _totalVersions;

    [ObservableProperty]
    private int _installedVersions;

    [ObservableProperty]
    private int _instanceCount;

    [ObservableProperty]
    private string _instanceName = "默认实例";

    [ObservableProperty]
    private string _instanceVersion = "1.20.1";

    [ObservableProperty]
    private string _accountName = "未登录";

    public void Refresh()
    {
        var instance = _instances.Current;
        InstanceName = instance?.Name ?? "无实例";
        InstanceVersion = instance?.ResolvedVersionName ?? "-";
        AccountName = _accounts.CurrentAccount?.DisplayName ?? "未登录";
        InstanceCount = _instances.Instances.Count;
    }

    public async Task RefreshStatsAsync()
    {
        try
        {
            var versions = await _launcher.GetVersionsAsync(refresh: false);
            TotalVersions = versions.Count;
            InstalledVersions = versions.Count(v => v.IsInstalled);
        }
        catch
        {
            TotalVersions = 0;
            InstalledVersions = 0;
        }
    }

    [RelayCommand]
    private async Task LaunchAsync()
    {
        var instance = _instances.Current;
        var account = _accounts.CurrentAccount;
        if (instance == null)
        {
            StatusText = "请先在实例页创建实例";
            return;
        }
        if (account == null)
        {
            StatusText = "请先在账户页添加账户";
            return;
        }
        IsRunning = true;
        StatusText = "正在启动…";
        try
        {
            var session = await _accounts.GetLaunchSessionAsync(account);
            var server = instance.ServerId == null
                ? null
                : _servers.Servers.FirstOrDefault(s => s.Id == instance.ServerId);
            await _launcher.LaunchAsync(instance, account, server, new Progress<string>(s => StatusText = s));
            StatusText = "游戏已启动";
        }
        catch (Exception ex)
        {
            StatusText = $"启动失败：{ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }
}
