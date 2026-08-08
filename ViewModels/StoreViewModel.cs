using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraftStation.Core.Models;
using CraftStation.Core.Services;
using Serilog;

namespace CraftStation.ViewModels;

public partial class StoreViewModel : ObservableObject
{
    private readonly IModrinthService _modrinth;
    private readonly IInstanceManager _instances;
    private readonly IResourceManager _resources;

    public StoreViewModel(IModrinthService modrinth, IInstanceManager instances, IResourceManager resources)
    {
        _modrinth = modrinth;
        _instances = instances;
        _resources = resources;
    }

    public ObservableCollection<ModrinthProject> Projects { get; } = new();
    public ObservableCollection<ModrinthVersion> Versions { get; } = new();
    public string[] ProjectTypes { get; } = { "mod", "resourcepack", "shader", "modpack" };

    [ObservableProperty]
    private string _query = "";

    [ObservableProperty]
    private string _projectType = "mod";

    [ObservableProperty]
    private string _gameVersion = "";

    [ObservableProperty]
    private string _loader = "";

    [ObservableProperty]
    private ModrinthProject? _selectedProject;

    [ObservableProperty]
    private ModrinthVersion? _selectedVersion;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progress;

    public void Refresh() { }

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsBusy = true;
        StatusText = "正在搜索…";
        try
        {
            var results = await _modrinth.SearchAsync(
                Query,
                ProjectType,
                string.IsNullOrWhiteSpace(GameVersion) ? null : GameVersion.Trim(),
                string.IsNullOrWhiteSpace(Loader) ? null : Loader.Trim());
            Projects.Clear();
            foreach (var p in results)
                Projects.Add(p);
            StatusText = $"找到 {Projects.Count} 个结果";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Modrinth 搜索失败");
            StatusText = $"搜索失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadVersionsAsync(ModrinthProject project)
    {
        SelectedProject = project;
        Versions.Clear();
        try
        {
            var versions = await _modrinth.GetVersionsAsync(
                project.Id,
                string.IsNullOrWhiteSpace(GameVersion) ? null : GameVersion.Trim(),
                string.IsNullOrWhiteSpace(Loader) ? null : Loader.Trim());
            foreach (var v in versions)
                Versions.Add(v);
            SelectedVersion = Versions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取 Modrinth 版本失败: {Project}", project.Id);
            StatusText = $"获取版本失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        var instance = _instances.Current;
        if (instance == null || SelectedVersion == null)
        {
            StatusText = "请先选择实例和版本";
            return;
        }

        // 下载期间选中项可能被刷新清空，先固定版本与文件引用
        var version = SelectedVersion;
        var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault();
        if (file == null)
        {
            StatusText = "该版本没有可下载文件";
            return;
        }

        IsBusy = true;
        try
        {
            var kind = SelectedProject?.ProjectType switch
            {
                "resourcepack" => ResourceKind.ResourcePack,
                "shader" => ResourceKind.ShaderPack,
                _ => ResourceKind.Mod
            };
            var folder = _resources.GetFolder(instance, kind);
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, file.Filename);
            await _modrinth.DownloadFileAsync(version, file, target,
                new Progress<DownloadProgress>(p =>
                {
                    Progress = p.TotalBytes == 0 ? 0 : p.CompletedBytes * 100d / p.TotalBytes;
                    StatusText = $"正在下载 {p.CurrentFile}";
                }));
            StatusText = $"已下载 {file.Filename}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "下载资源失败: {File}", file.Filename);
            StatusText = $"下载失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
