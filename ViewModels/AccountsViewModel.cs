using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraftStation.Core.Models;
using CraftStation.Core.Services;
using Microsoft.Win32;

namespace CraftStation.ViewModels;

public partial class AccountsViewModel : ObservableObject
{
    private readonly IAccountService _accounts;
    private readonly ISkinService _skins;

    public AccountsViewModel(IAccountService accounts, ISkinService skins)
    {
        _accounts = accounts;
        _skins = skins;
    }

    public ObservableCollection<AccountEntry> AccountList { get; } = new();

    [ObservableProperty]
    private AccountEntry? _selectedAccount;

    [ObservableProperty]
    private string _offlineName = "";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string? _deviceCode;

    public void Refresh()
    {
        AccountList.Clear();
        foreach (var account in _accounts.Accounts)
            AccountList.Add(account);
        SelectedAccount = _accounts.CurrentAccount;
    }

    [RelayCommand]
    private void AddOffline()
    {
        var name = OfflineName.Trim();
        if (name.Length == 0)
            return;
        var entry = _accounts.AddOfflineAccount(name);
        Refresh();
        StatusText = $"已添加离线账户 {entry.DisplayName}";
    }

    [RelayCommand]
    private async Task LoginMicrosoftAsync()
    {
        DeviceCode = null;
        StatusText = "正在打开微软登录…";
        try
        {
            var entry = await _accounts.LoginMicrosoftAsync(new MicrosoftLoginOptions
            {
                Mode = MicrosoftLoginMode.EmbeddedWebView,
                DeviceCodeCallback = code => Application.Current.Dispatcher.Invoke(() =>
                    DeviceCode = $"请在浏览器打开 {code.VerificationUrl} 并输入代码：{code.UserCode}")
            });
            Refresh();
            StatusText = $"已登录 {entry.DisplayName}";
        }
        catch (Exception ex)
        {
            StatusText = $"登录失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoginDeviceCodeAsync()
    {
        DeviceCode = null;
        StatusText = "正在获取设备码…";
        try
        {
            var entry = await _accounts.LoginMicrosoftAsync(new MicrosoftLoginOptions
            {
                Mode = MicrosoftLoginMode.DeviceCode,
                DeviceCodeCallback = code => Application.Current.Dispatcher.Invoke(() =>
                    DeviceCode = $"请在浏览器打开 {code.VerificationUrl} 并输入代码：{code.UserCode}")
            });
            Refresh();
            StatusText = $"已登录 {entry.DisplayName}";
        }
        catch (Exception ex)
        {
            StatusText = $"登录失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SelectAsync(AccountEntry entry)
    {
        await _accounts.SetCurrentAccountAsync(entry.Id);
        SelectedAccount = entry;
        StatusText = $"当前账户：{entry.DisplayName}";
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (SelectedAccount == null)
            return;
        await _accounts.RemoveAccountAsync(SelectedAccount.Id);
        Refresh();
        StatusText = "账户已移除";
    }

    [RelayCommand]
    private async Task DownloadSkinAsync()
    {
        if (SelectedAccount == null)
            return;
        if (string.IsNullOrEmpty(SelectedAccount.SkinUrl))
        {
            StatusText = "该账户没有可用皮肤";
            return;
        }
        var dialog = new SaveFileDialog
        {
            Filter = "PNG 图片 (*.png)|*.png",
            FileName = SelectedAccount.DisplayName + "-skin.png"
        };
        if (dialog.ShowDialog() != true)
            return;
        try
        {
            await _skins.DownloadSkinAsync(SelectedAccount, dialog.FileName);
            StatusText = $"皮肤已保存：{dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"下载失败：{ex.Message}";
        }
    }
}
