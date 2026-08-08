using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraftStation.Core.Models;
using CraftStation.Core.Services;
using Microsoft.Win32;

namespace CraftStation.ViewModels;

public partial class ResourcesViewModel : ObservableObject
{
    private readonly IInstanceManager _instances;
    private readonly IResourceManager _resources;

    public ResourcesViewModel(IInstanceManager instances, IResourceManager resources)
    {
        _instances = instances;
        _resources = resources;
    }

    public ObservableCollection<ResourceEntry> Mods { get; } = new();
    public ObservableCollection<ResourceEntry> ResourcePacks { get; } = new();
    public ObservableCollection<ResourceEntry> ShaderPacks { get; } = new();
    public ObservableCollection<SaveEntry> Saves { get; } = new();

    [ObservableProperty]
    private ResourceEntry? _selectedResource;

    [ObservableProperty]
    private SaveEntry? _selectedSave;

    [ObservableProperty]
    private string _statusText = "";

    public void Refresh()
    {
        var instance = _instances.Current;
        if (instance == null)
            return;
        Mods.Clear();
        foreach (var m in _resources.ListModsAsync(instance).Result)
            Mods.Add(m);
        ResourcePacks.Clear();
        foreach (var r in _resources.ListResourcePacksAsync(instance).Result)
            ResourcePacks.Add(r);
        ShaderPacks.Clear();
        foreach (var s in _resources.ListShaderPacksAsync(instance).Result)
            ShaderPacks.Add(s);
        Saves.Clear();
        foreach (var s in _resources.ListSavesAsync(instance).Result)
            Saves.Add(s);
    }

    [RelayCommand]
    private async Task ImportModAsync()
    {
        await ImportAsync(ResourceKind.Mod);
    }

    [RelayCommand]
    private async Task ImportResourcePackAsync()
    {
        await ImportAsync(ResourceKind.ResourcePack);
    }

    [RelayCommand]
    private async Task ImportShaderPackAsync()
    {
        await ImportAsync(ResourceKind.ShaderPack);
    }

    [RelayCommand]
    private async Task ToggleEnabledAsync(ResourceEntry entry)
    {
        await _resources.SetEnabledAsync(entry, entry.IsDisabled);
        Refresh();
    }

    [RelayCommand]
    private async Task DeleteResourceAsync(ResourceEntry entry)
    {
        await _resources.DeleteAsync(entry);
        Refresh();
    }

    [RelayCommand]
    private void OpenFolder(string kind)
    {
        var instance = _instances.Current;
        if (instance == null)
            return;
        var folder = _resources.GetFolder(instance, kind switch
        {
            "mods" => ResourceKind.Mod,
            "resourcepacks" => ResourceKind.ResourcePack,
            _ => ResourceKind.ShaderPack
        });
        Directory.CreateDirectory(folder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenSaveFolder(SaveEntry save)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = save.FolderPath,
            UseShellExecute = true
        });
    }

    private async Task ImportAsync(ResourceKind kind)
    {
        var dialog = new OpenFileDialog
        {
            Filter = kind == ResourceKind.Mod
                ? "模组文件 (*.jar)|*.jar|所有文件 (*.*)|*.*"
                : "压缩包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true)
            return;
        var instance = _instances.Current;
        if (instance == null)
            return;
        foreach (var file in dialog.FileNames)
        {
            await _resources.ImportFileAsync(instance, file, kind);
        }
        StatusText = $"已导入 {dialog.FileNames.Length} 个文件";
        Refresh();
    }
}
