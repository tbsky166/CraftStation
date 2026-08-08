using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraftStation.Core.Models;
using CraftStation.Core.Services;
using Microsoft.Win32;
using Serilog;

namespace CraftStation.ViewModels;

public partial class InstancesViewModel : ObservableObject
{
    private static readonly string[] LoaderMarkers =
        { "forge", "fabric", "quilt", "neoforge", "optifine", "liteloader" };

    private readonly IInstanceManager _instances;
    private readonly ILauncherService _launcher;
    private readonly IAccountService _accounts;
    private readonly IServerService _servers;
    private readonly IModpackService _modpacks;
    private readonly IJavaService _java;

    public InstancesViewModel(
        IInstanceManager instances,
        ILauncherService launcher,
        IAccountService accounts,
        IServerService servers,
        IModpackService modpacks,
        IJavaService java)
    {
        _instances = instances;
        _launcher = launcher;
        _accounts = accounts;
        _servers = servers;
        _modpacks = modpacks;
        _java = java;
    }

    public ObservableCollection<Instance> InstanceList { get; } = new();
    public ObservableCollection<string> InstalledVersions { get; } = new();
    public ObservableCollection<string> LoaderKinds { get; } = new()
    {
        "Vanilla", "Fabric", "Forge", "Quilt", "NeoForge", "OptiFine"
    };

    [ObservableProperty]
    private Instance? _selectedInstance;

    [ObservableProperty]
    private string _newName = "";

    [ObservableProperty]
    private string _newVersion = "1.20.1";

    [ObservableProperty]
    private string _newLoader = "Vanilla";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string _installedVersionLabel = "已安装版本：-";

    [ObservableProperty]
    private bool _isBusy;

    public void Refresh()
    {
        RefreshInstanceList();
        _ = RefreshInstalledVersionsAsync();
    }

    private void RefreshInstanceList()
    {
        InstanceList.Clear();
        foreach (var instance in _instances.Instances)
            InstanceList.Add(instance);
        SelectedInstance = _instances.Current;
    }

    public async Task RefreshInstalledVersionsAsync()
    {
        try
        {
            var versions = await _launcher.GetVersionsAsync(refresh: false);
            var installed = versions.Where(v => v.IsInstalled).Select(v => v.Name).ToList();
            InstalledVersions.Clear();
            foreach (var name in installed)
                InstalledVersions.Add(name);
            InstalledVersionLabel = $"已安装版本：{installed.Count}";

            // 参考 PCL-CE：已安装的原版版本自动补建实例，打开本页即可看到
            foreach (var name in installed.Where(IsVanillaLike))
            {
                if (_instances.Instances.All(i =>
                        !string.Equals(i.VersionId, name, StringComparison.OrdinalIgnoreCase)))
                {
                    await _instances.CreateAsync(name, name);
                }
            }
            RefreshInstanceList();

            if (installed.Count > 0 &&
                (string.IsNullOrWhiteSpace(NewVersion) || !installed.Contains(NewVersion)))
            {
                NewVersion = installed[0];
            }
            else if (installed.Count == 0)
            {
                NewVersion = "1.20.1";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载已安装版本列表失败");
            InstalledVersionLabel = "已安装版本：加载失败";
        }
    }

    private static bool IsVanillaLike(string versionName) =>
        !LoaderMarkers.Any(m => versionName.Contains(m, StringComparison.OrdinalIgnoreCase));

    [RelayCommand]
    private async Task CreateAsync()
    {
        var version = string.IsNullOrWhiteSpace(NewVersion)
            ? InstalledVersions.FirstOrDefault() ?? "1.20.1"
            : NewVersion.Trim();
        var name = string.IsNullOrWhiteSpace(NewName) ? version : NewName.Trim();
        var loader = ParseLoader(NewLoader);
        try
        {
            var instance = await _instances.CreateAsync(name, version, loader);
            Refresh();
            StatusText = $"已创建实例 {instance.Name}（{version}）";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "创建实例失败: {Name} {Version}", name, version);
            StatusText = $"创建失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SelectAsync(Instance instance)
    {
        await _instances.SetCurrentAsync(instance.Id);
        SelectedInstance = instance;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedInstance == null)
            return;
        try
        {
            await _instances.UpdateAsync(SelectedInstance);
            Refresh();
            StatusText = "实例设置已保存";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存实例设置失败");
            StatusText = $"保存失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedInstance == null)
            return;
        try
        {
            await _instances.DeleteAsync(SelectedInstance.Id);
            Refresh();
            StatusText = "实例已删除";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除实例失败");
            StatusText = $"删除失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LaunchAsync()
    {
        var instance = SelectedInstance ?? _instances.Current;
        var account = _accounts.CurrentAccount;
        if (instance == null || account == null)
        {
            StatusText = "需要先选择实例并登录账户";
            return;
        }
        IsBusy = true;
        StatusText = "正在启动…";
        try
        {
            var server = instance.ServerId == null
                ? null
                : _servers.Servers.FirstOrDefault(s => s.Id == instance.ServerId);
            await _launcher.LaunchAsync(instance, account, server, new Progress<string>(s => StatusText = s));
            StatusText = "游戏已启动";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "启动实例失败: {Instance}", instance.Name);
            StatusText = $"启动失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenGameFolder()
    {
        var instance = SelectedInstance ?? _instances.Current;
        if (instance == null)
            return;
        var dir = _instances.GetGameDirectory(instance);
        Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = dir,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private async Task ImportPackAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "整合包 (*.zip;*.mrpack)|*.zip;*.mrpack|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true)
            return;
        IsBusy = true;
        StatusText = "正在导入整合包…";
        try
        {
            var name = string.IsNullOrWhiteSpace(NewName)
                ? Path.GetFileNameWithoutExtension(dialog.FileName)
                : NewName.Trim();
            var instance = await _modpacks.ImportAsync(dialog.FileName, name,
                new Progress<DownloadProgress>(p => StatusText = $"正在下载 {p.CurrentFile}"),
                new Progress<string>(s => StatusText = s));
            Refresh();
            SelectedInstance = instance;
            StatusText = $"整合包导入完成：{instance.Name}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "导入整合包失败: {File}", dialog.FileName);
            StatusText = $"导入失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportPackAsync()
    {
        var instance = SelectedInstance ?? _instances.Current;
        if (instance == null)
            return;
        var dialog = new SaveFileDialog
        {
            Filter = "Modrinth 整合包 (*.mrpack)|*.mrpack|CurseForge 整合包 (*.zip)|*.zip",
            FileName = instance.Name + ".mrpack"
        };
        if (dialog.ShowDialog() != true)
            return;
        IsBusy = true;
        StatusText = "正在导出整合包…";
        try
        {
            await _modpacks.ExportAsync(instance, dialog.FileName, dialog.FilterIndex == 1);
            StatusText = $"整合包已导出：{dialog.FileName}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "导出整合包失败");
            StatusText = $"导出失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ScanJavaAsync()
    {
        IsBusy = true;
        StatusText = "正在扫描 Java…";
        try
        {
            var list = await _java.ScanInstalledJavaAsync();
            if (list.Count == 0)
            {
                StatusText = "未找到已安装的 Java，启动时会自动下载 Mojang 运行时。";
                return;
            }
            StatusText = "找到 Java：" + string.Join(" | ",
                list.Take(5).Select(j => $"{Path.GetFileName(Path.GetDirectoryName(j.Path))} ({j.Version})"));
            if (list.Count == 1 && SelectedInstance != null)
            {
                SelectedInstance.JavaPath = list[0].Path;
                StatusText += "；已自动填入唯一 Java 路径。";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "扫描 Java 失败");
            StatusText = $"扫描失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static LoaderKind ParseLoader(string label) => label switch
    {
        "Fabric" => LoaderKind.Fabric,
        "Forge" => LoaderKind.Forge,
        "Quilt" => LoaderKind.Quilt,
        "NeoForge" => LoaderKind.NeoForge,
        "OptiFine" => LoaderKind.OptiFine,
        _ => LoaderKind.Vanilla
    };
}
