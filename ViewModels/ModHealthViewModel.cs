using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraftStation.Core.Models;
using CraftStation.Core.Services;
using Microsoft.Win32;

namespace CraftStation.ViewModels;

public partial class ModHealthViewModel : ObservableObject
{
    private readonly IInstanceManager _instances;
    private readonly IModHealthService _health;

    public ModHealthViewModel(IInstanceManager instances, IModHealthService health)
    {
        _instances = instances;
        _health = health;
    }

    public ObservableCollection<HealthIssue> Issues { get; } = new();
    public ObservableCollection<ModEntry> Mods { get; } = new();
    public ObservableCollection<ModEntry> DependencyTree { get; } = new();

    [ObservableProperty]
    private ModEntry? _selectedMod;

    [ObservableProperty]
    private HealthIssue? _selectedIssue;

    [ObservableProperty]
    private string _statusText = "尚未扫描";

    [ObservableProperty]
    private bool _isBusy;

    public void Refresh() { }

    [RelayCommand]
    private async Task ScanAsync()
    {
        var instance = _instances.Current;
        if (instance == null)
            return;
        IsBusy = true;
        StatusText = "正在扫描模组…";
        try
        {
            var report = await _health.ScanAsync(instance);
            Issues.Clear();
            foreach (var issue in report.Issues)
                Issues.Add(issue);
            Mods.Clear();
            foreach (var mod in report.Mods)
                Mods.Add(mod);
            StatusText = report.Issues.Count == 0
                ? $"扫描完成：{report.Mods.Count} 个模组，未发现问题"
                : $"扫描完成：发现 {report.Issues.Count} 个问题";
        }
        catch (Exception ex)
        {
            StatusText = $"扫描失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ShowDependenciesAsync(ModEntry mod)
    {
        SelectedMod = mod;
        var instance = _instances.Current;
        if (instance == null)
            return;
        var report = await _health.ScanAsync(instance);
        DependencyTree.Clear();
        foreach (var m in _health.GetDependencyTree(report, mod.ModId ?? ""))
            DependencyTree.Add(m);
    }

    [RelayCommand]
    private async Task DisableAsync(ModEntry mod)
    {
        await _health.DisableAsync(mod);
        await ScanAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(ModEntry mod)
    {
        await _health.DeleteAsync(mod);
        await ScanAsync();
    }

    [RelayCommand]
    private void ExportReport()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Markdown (*.md)|*.md|文本文件 (*.txt)|*.txt",
            FileName = "CraftStation-模组体检报告.md"
        };
        if (dialog.ShowDialog() != true)
            return;
        var instance = _instances.Current;
        if (instance == null)
            return;
        var report = _health.ScanAsync(instance).Result;
        File.WriteAllText(dialog.FileName, _health.ExportReport(report));
        StatusText = $"报告已导出：{dialog.FileName}";
    }
}
