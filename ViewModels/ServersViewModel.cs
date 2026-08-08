using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraftStation.Core.Models;
using CraftStation.Core.Services;

namespace CraftStation.ViewModels;

public partial class ServersViewModel : ObservableObject
{
    private readonly IServerService _servers;
    private readonly IInstanceManager _instances;
    private readonly IAccountService _accounts;
    private readonly ILauncherService _launcher;

    public ServersViewModel(
        IServerService servers,
        IInstanceManager instances,
        IAccountService accounts,
        ILauncherService launcher)
    {
        _servers = servers;
        _instances = instances;
        _accounts = accounts;
        _launcher = launcher;
    }

    public ObservableCollection<ServerEntry> ServerList { get; } = new();

    [ObservableProperty]
    private ServerEntry? _selectedServer;

    [ObservableProperty]
    private ServerStatus? _status;

    [ObservableProperty]
    private string _newName = "";

    [ObservableProperty]
    private string _newAddress = "";

    [ObservableProperty]
    private string _newPort = "25565";

    [ObservableProperty]
    private string _statusText = "";

    public void Refresh()
    {
        ServerList.Clear();
        foreach (var server in _servers.Servers)
            ServerList.Add(server);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var port = int.TryParse(NewPort, out var p) ? p : 25565;
        var server = await _servers.AddAsync(
            string.IsNullOrWhiteSpace(NewName) ? NewAddress : NewName.Trim(),
            NewAddress.Trim(),
            port);
        Refresh();
        SelectedServer = server;
        StatusText = "服务器已添加";
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedServer == null)
            return;
        await _servers.DeleteAsync(SelectedServer.Id);
        Refresh();
        StatusText = "服务器已删除";
    }

    [RelayCommand]
    private async Task PingAsync()
    {
        if (SelectedServer == null)
            return;
        Status = await _servers.PingAsync(SelectedServer);
        StatusText = Status.Online
            ? $"在线 · {Status.PlayersOnline}/{Status.PlayersMax} 人 · {Status.LatencyMs} ms"
            : $"离线：{Status.Error}";
        SelectedServer.LastPingUtc = DateTime.UtcNow;
        await _servers.UpdateAsync(SelectedServer);
    }

    [RelayCommand]
    private async Task LaunchAsync()
    {
        var instance = _instances.Current;
        var account = _accounts.CurrentAccount;
        if (instance == null || account == null || SelectedServer == null)
        {
            StatusText = "需要实例、账户和服务器";
            return;
        }
        instance.ServerId = SelectedServer.Id;
        await _instances.UpdateAsync(instance);
        await _launcher.LaunchAsync(instance, account, SelectedServer, new Progress<string>(s => StatusText = s));
        StatusText = "已直连启动";
    }
}
