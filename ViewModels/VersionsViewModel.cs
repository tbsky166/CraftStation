using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraftStation.Core.Models;
using CraftStation.Core.Services;
using Serilog;

namespace CraftStation.ViewModels;

public partial class VersionsViewModel : ObservableObject
{
    private readonly ILauncherService _launcher;
    private readonly IModLoaderInstaller _loaders;
    private readonly IInstanceManager _instances;

    public VersionsViewModel(
        ILauncherService launcher,
        IModLoaderInstaller loaders,
        IInstanceManager instances)
    {
        _launcher = launcher;
        _loaders = loaders;
        _instances = instances;
    }

    public ObservableCollection<VersionInfo> Versions { get; } = new();
    public ObservableCollection<string> LoaderKinds { get; } = new()
    {
        "Fabric", "Forge", "Quilt", "NeoForge", "OptiFine", "LiteLoader"
    };

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private VersionInfo? _selectedVersion;

    [ObservableProperty]
    private string _selectedLoader = "Fabric";

    [ObservableProperty]
    private string? _selectedLoaderVersion;

    [ObservableProperty]
    private ObservableCollection<string> _loaderVersions = new();

    [ObservableProperty]
    private string _statusText = "点击刷新加载版本列表";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progress;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusText = "正在获取版本列表…";
        try
        {
            var list = await _launcher.GetVersionsAsync(refresh: true);
            Versions.Clear();
            foreach (var v in list)
                Versions.Add(v);
            StatusText = $"共 {Versions.Count} 个版本";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取版本列表失败");
            StatusText = $"获取失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (SelectedVersion == null)
            return;

        // 下载期间版本列表可能被并发刷新，选中项可能短暂变 null；
        // 先把目标版本名固定下来，进度回调不再读取可变的 SelectedVersion。
        var targetName = SelectedVersion.Name;
        IsBusy = true;
        StatusText = $"正在安装 {targetName}…";
        try
        {
            await _launcher.InstallAsync(targetName,
                new Progress<DownloadProgress>(p =>
                {
                    Progress = p.Percent;
                    StatusText = $"正在下载 {p.CurrentFile ?? targetName} ({p.CompletedFiles}/{p.TotalFiles})";
                }));

            await RefreshAsync();

            // 参考 PCL-CE：安装完成后版本立即出现在实例列表，无需手动再建实例
            var exists = _instances.Instances.Any(i =>
                string.Equals(i.VersionId, targetName, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                var created = await _instances.CreateAsync(targetName, targetName);
                StatusText = $"{targetName} 安装完成，已创建实例「{created.Name}」";
            }
            else
            {
                StatusText = $"{targetName} 安装完成";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "安装版本失败: {Version}", targetName);
            StatusText = $"安装失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RepairAsync()
    {
        if (SelectedVersion == null)
            return;

        var targetName = SelectedVersion.Name;
        IsBusy = true;
        StatusText = $"正在修复 {targetName}…";
        try
        {
            await _launcher.RepairAsync(targetName,
                new Progress<DownloadProgress>(p =>
                {
                    Progress = p.Percent;
                    StatusText = $"正在修复 {p.CurrentFile ?? targetName}";
                }));
            StatusText = "修复完成";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "修复版本失败: {Version}", targetName);
            StatusText = $"修复失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedVersion == null)
            return;
        var targetName = SelectedVersion.Name;
        try
        {
            await _launcher.DeleteVersionAsync(targetName);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除版本失败: {Version}", targetName);
            StatusText = $"删除失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadLoaderVersionsAsync()
    {
        if (SelectedVersion == null)
            return;
        var mcVersion = SelectedVersion.Name;
        IsBusy = true;
        try
        {
            var kind = ParseLoader(SelectedLoader);
            var versions = await _loaders.GetVersionsAsync(mcVersion, kind);
            LoaderVersions.Clear();
            foreach (var v in versions)
                LoaderVersions.Add(v);
            SelectedLoaderVersion = LoaderVersions.FirstOrDefault();
            StatusText = $"{SelectedLoader} 可用版本：{LoaderVersions.Count}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取加载器版本失败: {Version} {Loader}", mcVersion, SelectedLoader);
            StatusText = $"获取加载器版本失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task InstallLoaderAsync()
    {
        if (SelectedVersion == null)
            return;

        var mcVersion = SelectedVersion.Name;
        IsBusy = true;
        var kind = ParseLoader(SelectedLoader);
        StatusText = $"正在安装 {SelectedLoader} {SelectedLoaderVersion ?? "最新"}…";
        try
        {
            var versionName = await _loaders.InstallAsync(
                mcVersion,
                kind,
                SelectedLoaderVersion,
                new Progress<DownloadProgress>(p =>
                {
                    Progress = p.Percent;
                    StatusText = $"正在安装加载器：{p.CurrentFile}";
                }),
                new Progress<string>(s => StatusText = s));
            StatusText = $"安装完成：{versionName}";
            var current = _instances.Current;
            if (current != null && current.VersionId == mcVersion)
            {
                current.Loader = kind;
                current.LoaderVersion = SelectedLoaderVersion ?? "latest";
                await _instances.UpdateAsync(current);
            }
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载器安装失败: {Version} {Loader}", mcVersion, SelectedLoader);
            StatusText = $"加载器安装失败：{ex.Message}";
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
        "LiteLoader" => LoaderKind.LiteLoader,
        _ => LoaderKind.Vanilla
    };
}
