using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraftStation.Core.Services;

namespace CraftStation.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IInstanceManager _instances;
    private readonly IAccountService _accounts;

    public MainViewModel(
        IInstanceManager instances,
        IAccountService accounts,
        DashboardViewModel dashboard,
        VersionsViewModel versions,
        InstancesViewModel instancePage,
        ResourcesViewModel resources,
        ModHealthViewModel modHealth,
        ServersViewModel servers,
        AccountsViewModel accountPage,
        StoreViewModel store,
        SettingsViewModel settings)
    {
        _instances = instances;
        _accounts = accounts;
        Dashboard = dashboard;
        Versions = versions;
        Instances = instancePage;
        Resources = resources;
        ModHealth = modHealth;
        Servers = servers;
        Accounts = accountPage;
        Store = store;
        Settings = settings;

        NavItems = new ObservableCollection<NavItem>
        {
            new() { Key = "dashboard", Label = "主页", Icon = "\uE80F" },
            new() { Key = "versions", Label = "版本库", Icon = "\uE7FC" },
            new() { Key = "instances", Label = "实例", Icon = "\uE8F1" },
            new() { Key = "resources", Label = "资源管理", Icon = "\uE8D7" },
            new() { Key = "store", Label = "资源市场", Icon = "\uE8D4" },
            new() { Key = "modhealth", Label = "模组体检", Icon = "\uE9D9" },
            new() { Key = "servers", Label = "服务器", Icon = "\uE774" },
            new() { Key = "accounts", Label = "账户", Icon = "\uE77B" },
            new() { Key = "settings", Label = "设置", Icon = "\uE713" }
        };
        CurrentPage = dashboard;
    }

    public ObservableCollection<NavItem> NavItems { get; }
    public DashboardViewModel Dashboard { get; }
    public VersionsViewModel Versions { get; }
    public InstancesViewModel Instances { get; }
    public ResourcesViewModel Resources { get; }
    public ModHealthViewModel ModHealth { get; }
    public ServersViewModel Servers { get; }
    public AccountsViewModel Accounts { get; }
    public StoreViewModel Store { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    private object _currentPage;

    [ObservableProperty]
    private string _pageTitle = "主页";

    [ObservableProperty]
    private string _accountLabel = "未登录";

    [ObservableProperty]
    private string _instanceLabel = "无实例";

    public void RefreshHeader()
    {
        var account = _accounts.CurrentAccount;
        AccountLabel = account == null ? "未登录" : $"{account.DisplayName} · {account.KindLabel}";
        var instance = _instances.Current;
        InstanceLabel = instance == null ? "无实例" : instance.Name;
    }

    [RelayCommand]
    private void Navigate(string key)
    {
        CurrentPage = key switch
        {
            "versions" => Versions,
            "instances" => Instances,
            "resources" => Resources,
            "store" => Store,
            "modhealth" => ModHealth,
            "servers" => Servers,
            "accounts" => Accounts,
            "settings" => Settings,
            _ => Dashboard
        };
        PageTitle = NavItems.FirstOrDefault(n => n.Key == key)?.Label ?? "主页";
        if (CurrentPage is VersionsViewModel v) v.RefreshCommand.Execute(null);
        else if (CurrentPage is InstancesViewModel i) i.Refresh();
        else if (CurrentPage is ResourcesViewModel r) r.Refresh();
        else if (CurrentPage is ModHealthViewModel m) m.Refresh();
        else if (CurrentPage is ServersViewModel s) s.Refresh();
        else if (CurrentPage is AccountsViewModel a) a.Refresh();
        else if (CurrentPage is StoreViewModel st) st.Refresh();
        else if (CurrentPage is DashboardViewModel d) { d.Refresh(); _ = d.RefreshStatsAsync(); }
        RefreshHeader();
    }
}
