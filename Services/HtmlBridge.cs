using System.IO;
using System.Text.Json;
using System.Windows;
using CraftStation;
using CraftStation.Core;
using CraftStation.Core.Models;
using CraftStation.Core.Services;

namespace CraftStation.Services;

/// <summary>
/// WebView2 HTML 界面与 WPF 全部服务之间的完整桥接层。
/// </summary>
public sealed class HtmlBridge
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly ILauncherService _launcher;
    private readonly IInstanceManager _instances;
    private readonly IAccountService _accounts;
    private readonly IServerService _servers;
    private readonly IModrinthService _modrinth;
    private readonly IResourceManager _resources;
    private readonly IModHealthService _health;
    private readonly IModpackService _modpacks;
    private readonly IJavaService _java;
    private readonly IUpdateService _updater;
    private readonly ISkinService _skins;
    private readonly ISettingsService _settings;
    private readonly ILogService _logs;
    private readonly IModLoaderInstaller _loaders;

    public HtmlBridge(
        ILauncherService launcher,
        IInstanceManager instances,
        IAccountService accounts,
        IServerService servers,
        IModrinthService modrinth,
        IResourceManager resources,
        IModHealthService health,
        IModpackService modpacks,
        IJavaService java,
        IUpdateService updater,
        ISkinService skins,
        ISettingsService settings,
        ILogService logs,
        IModLoaderInstaller loaders)
    {
        _launcher = launcher;
        _instances = instances;
        _accounts = accounts;
        _servers = servers;
        _modrinth = modrinth;
        _resources = resources;
        _health = health;
        _modpacks = modpacks;
        _java = java;
        _updater = updater;
        _skins = skins;
        _settings = settings;
        _logs = logs;
        _loaders = loaders;
    }

    /// <summary>C# → JS 事件（例如设备码登录进度）。</summary>
    public Action<string, string>? Notify { get; set; }

    public async Task<object?> HandleAsync(string type, JsonElement payload)
    {
        return type switch
        {
            // 状态 / 版本
            "getState" => await GetStateAsync(),
            "getVersions" => await GetVersionsAsync(Bool(payload, "refresh", false)),
            "installVersion" => await InstallVersionAsync(Str(payload, "name")),
            "installVersionCustom" => await InstallVersionCustomAsync(payload),
            "repairVersion" => await RepairVersionAsync(Str(payload, "name")),
            "deleteVersion" => await DeleteVersionAsync(Str(payload, "name")),
            "getLoaderVersions" => await GetLoaderVersionsAsync(Str(payload, "version"), Str(payload, "loader")),
            "installLoader" => await InstallLoaderAsync(payload),

            // 实例
            "getInstances" => GetInstances(),
            "createInstance" => await CreateInstanceAsync(payload),
            "selectInstance" => await SelectInstanceAsync(Str(payload, "id")),
            "deleteInstance" => await DeleteInstanceAsync(Str(payload, "id")),
            "saveInstance" => await SaveInstanceAsync(payload),
            "launchInstance" => await LaunchInstanceAsync(Str(payload, "id")),
            "openGameFolder" => OpenGameFolder(Str(payload, "id")),
            "importPack" => await ImportPackAsync(payload),
            "exportPack" => await ExportPackAsync(payload),

            // 账户
            "getAccounts" => GetAccounts(),
            "addOfflineAccount" => AddOfflineAccount(Str(payload, "name")),
            "selectAccount" => await SelectAccountAsync(Str(payload, "id")),
            "removeAccount" => await RemoveAccountAsync(Str(payload, "id")),
            "loginMicrosoft" => await LoginMicrosoftAsync(),
            "loginDeviceCode" => await LoginDeviceCodeAsync(),
            "refreshAccount" => await RefreshAccountAsync(Str(payload, "id")),
            "downloadSkin" => await DownloadSkinAsync(Str(payload, "id")),

            // 资源管理
            "getResources" => await GetResourcesAsync(),
            "importResource" => await ImportResourceAsync(payload),
            "toggleResource" => await ToggleResourceAsync(payload),
            "deleteResource" => await DeleteResourceAsync(payload),
            "openResourceFolder" => OpenResourceFolder(Str(payload, "kind")),
            "openSaveFolder" => OpenSaveFolder(Str(payload, "folder")),

            // 资源市场
            "searchProjects" => await SearchProjectsAsync(payload),
            "getProjectVersions" => await GetProjectVersionsAsync(payload),
            "downloadProjectVersion" => await DownloadProjectVersionAsync(payload),

            // 模组体检
            "scanMods" => await ScanModsAsync(),
            "getDependencyTree" => await GetDependencyTreeAsync(Str(payload, "modId")),
            "disableMod" => await DisableModAsync(Str(payload, "filePath")),
            "deleteMod" => await DeleteModAsync(Str(payload, "filePath")),
            "exportModReport" => await ExportModReportAsync(),

            // 服务器
            "getServers" => GetServers(),
            "addServer" => await AddServerAsync(payload),
            "deleteServer" => await DeleteServerAsync(Str(payload, "id")),
            "pingServer" => await PingServerAsync(Str(payload, "id")),
            "launchServer" => await LaunchServerAsync(Str(payload, "id")),

            // 设置
            "getSettings" => GetSettings(),
            "saveSettings" => await SaveSettingsAsync(payload),
            "openDataFolder" => OpenFolder(_settings.DataDirectory),
            "openLogsFolder" => OpenFolder(_settings.LogsDirectory),
            "checkUpdate" => await CheckUpdateAsync(),
            "scanJava" => await ScanJavaAsync(),

            // 进程 / 日志
            "stopGame" => await StopGameAsync(),
            "getGameLog" => await GetGameLogAsync(Str(payload, "instanceId"), Int(payload, "maxLines", 500)),

            // 窗口（HTML 顶栏 → WPF 无边框窗口）
            "windowMinimize" => WindowMinimize(),
            "windowToggleMaximize" => WindowToggleMaximize(),
            "windowClose" => WindowClose(),
            "windowDrag" => WindowDrag(),

            _ => new { error = $"未知消息类型：{type}" }
        };
    }

    private static object WindowMinimize()
    {
        RunWindow(w => w.MinimizeWindow());
        return new { ok = true };
    }

    private static object WindowToggleMaximize()
    {
        RunWindow(w => w.ToggleMaximizeWindow());
        return new { ok = true };
    }

    private static object WindowClose()
    {
        RunWindow(w => w.CloseWindow());
        return new { ok = true };
    }

    private static object WindowDrag()
    {
        RunWindow(w => w.BeginDrag());
        return new { ok = true };
    }

    private static void RunWindow(Action<WebPreviewWindow> action)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (Application.Current.MainWindow is WebPreviewWindow window)
                action(window);
        });
    }

    // ---------- 状态 / 版本 ----------

    private async Task<object> GetStateAsync()
    {
        var instance = _instances.Current;
        var account = _accounts.CurrentAccount;
        // 版本清单未加载时绝不阻塞仪表盘：先返回计数，后台异步预热
        var versions = _launcher.IsVersionListLoaded
            ? await SafeVersionsAsync()
            : Array.Empty<VersionInfo>();
        if (!_launcher.IsVersionListLoaded)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await SafeVersionsAsync();
                }
                catch
                {
                    // 预热失败下次轮询会重试
                }
            });
        }
        return new
        {
            accountName = account?.DisplayName ?? "未登录",
            instanceName = instance?.Name ?? "无实例",
            instanceVersion = instance?.ResolvedVersionName ?? "-",
            totalVersions = versions.Count,
            installedVersions = versions.Count(v => v.IsInstalled),
            instanceCount = _instances.Instances.Count,
            gameRunning = _launcher.RunningProcess != null,
            statusText = "就绪"
        };
    }

    private async Task<IReadOnlyList<VersionInfo>> SafeVersionsAsync(bool refresh = false)
    {
        try
        {
            return await _launcher.GetVersionsAsync(refresh);
        }
        catch
        {
            return Array.Empty<VersionInfo>();
        }
    }

    private async Task<object> GetVersionsAsync(bool refresh = false)
    {
        var versions = await SafeVersionsAsync(refresh);
        return new
        {
            versions = versions.Select(v => new
            {
                v.Name,
                v.TypeLabel,
                v.Category,
                v.IsInstalled,
                ReleaseTimeUtc = v.ReleaseTimeUtc?.ToString("yyyy-MM-dd")
            })
        };
    }

    private async Task<object> InstallVersionAsync(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new { message = "缺少版本名" };
        try
        {
            await _launcher.InstallAsync(name);
            var exists = _instances.Instances.Any(i =>
                string.Equals(i.VersionId, name, StringComparison.OrdinalIgnoreCase));
            if (!exists)
                await _instances.CreateAsync(name, name);
            return new { message = $"安装完成：{name}" };
        }
        catch (Exception ex)
        {
            return new { message = $"安装失败：{ex.Message}" };
        }
    }

    private async Task<object> InstallVersionCustomAsync(JsonElement payload)
    {
        var name = Str(payload, "name");
        if (string.IsNullOrWhiteSpace(name))
            return new { message = "缺少版本名" };

        var loader = Str(payload, "loader") ?? "";
        var loaderVersion = Str(payload, "loaderVersion") ?? "";
        var createInstance = Bool(payload, "createInstance", true);

        try
        {
            PushInstallProgress(0, $"准备安装 {name}…");
            PushInstallLog($"[下载中心] 开始安装 {name}");

            // 先确保纯净版已安装
            var versionDir = Path.Combine(
                _settings.ResolveGameDirectory(), Config.MinecraftVersionsDirectoryName, name);
            if (!Directory.Exists(versionDir))
            {
                PushInstallLog($"[下载中心] 正在下载纯净版 {name}…");
                await _launcher.InstallAsync(name, new Progress<DownloadProgress>(p =>
                {
                    var percent = (int)Math.Clamp(p.Percent / 2, 0, 50);
                    PushInstallProgress(percent, $"正在下载 {p.CurrentFile ?? name}（{p.CompletedFiles}/{p.TotalFiles}）");
                    PushInstallLog($"[下载] {p.CurrentFile ?? name}（{p.CompletedFiles}/{p.TotalFiles}）");
                }));
                PushInstallLog($"[下载中心] 纯净版 {name} 下载完成");
            }
            else
            {
                PushInstallLog($"[下载中心] 纯净版 {name} 已存在，跳过下载");
                PushInstallProgress(50, "纯净版已就绪，准备加载器…");
            }

            var kind = ParseLoader(loader);
            var resolvedName = name;
            if (kind != LoaderKind.Vanilla)
            {
                PushInstallLog($"[下载中心] 开始安装加载器 {loader}…");
                resolvedName = await _loaders.InstallAsync(
                    name,
                    kind,
                    string.IsNullOrWhiteSpace(loaderVersion) ? null : loaderVersion,
                    new Progress<DownloadProgress>(p =>
                    {
                        var percent = (int)Math.Clamp(50 + p.Percent / 2, 50, 95);
                        PushInstallProgress(percent, $"加载器 {p.CurrentFile ?? loader}（{p.CompletedFiles}/{p.TotalFiles}）");
                        PushInstallLog($"[加载器] {p.CurrentFile ?? loader}（{p.CompletedFiles}/{p.TotalFiles}）");
                    }),
                    new Progress<string>(line => PushInstallLog($"[加载器] {line}")));
                PushInstallLog($"[下载中心] 加载器安装完成：{resolvedName}");
            }

            PushInstallProgress(96, "正在创建实例…");
            if (createInstance)
            {
                var existing = _instances.Instances.FirstOrDefault(i =>
                    string.Equals(i.VersionId, name, StringComparison.OrdinalIgnoreCase) &&
                    i.Loader == kind);
                if (existing != null)
                {
                    existing.Loader = kind;
                    existing.LoaderVersion = kind == LoaderKind.Vanilla ? null : resolvedName;
                    await _instances.UpdateAsync(existing);
                }
                else
                {
                    var instance = await _instances.CreateAsync(name, name, kind);
                    instance.LoaderVersion = kind == LoaderKind.Vanilla ? null : resolvedName;
                    await _instances.UpdateAsync(instance);
                }
            }

            PushInstallProgress(100, "安装完成");
            return new
            {
                message = kind == LoaderKind.Vanilla
                    ? $"安装完成：{name}"
                    : $"安装完成：{name} + {loader}（{resolvedName}）"
            };
        }
        catch (Exception ex)
        {
            return new { message = $"安装失败：{ex.Message}" };
        }
    }

    private void PushInstallProgress(int percent, string message)
    {
        Notify?.Invoke("installProgress", JsonSerializer.Serialize(new { percent, message }, JsonOpts));
    }

    private void PushInstallLog(string line)
    {
        Notify?.Invoke("installLog", JsonSerializer.Serialize(new { line }, JsonOpts));
    }

    private async Task<object> RepairVersionAsync(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new { message = "缺少版本名" };
        try
        {
            await _launcher.RepairAsync(name);
            return new { message = $"修复完成：{name}" };
        }
        catch (Exception ex)
        {
            return new { message = $"修复失败：{ex.Message}" };
        }
    }

    private async Task<object> DeleteVersionAsync(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new { message = "缺少版本名" };
        try
        {
            await _launcher.DeleteVersionAsync(name);
            return new { message = $"已删除：{name}" };
        }
        catch (Exception ex)
        {
            return new { message = $"删除失败：{ex.Message}" };
        }
    }

    private async Task<object> GetLoaderVersionsAsync(string? version, string? loader)
    {
        if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(loader))
            return new { versions = Array.Empty<string>() };
        try
        {
            var list = await _loaders.GetVersionsAsync(version, ParseLoader(loader));
            return new { versions = list };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message, versions = Array.Empty<string>() };
        }
    }

    private async Task<object> InstallLoaderAsync(JsonElement payload)
    {
        var version = Str(payload, "version");
        var loader = Str(payload, "loader");
        var loaderVersion = Str(payload, "loaderVersion");
        if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(loader))
            return new { message = "缺少版本或加载器" };
        try
        {
            var kind = ParseLoader(loader);
            var installedName = await _loaders.InstallAsync(version, kind, loaderVersion);
            var current = _instances.Current;
            if (current != null && current.VersionId == version)
            {
                current.Loader = kind;
                current.LoaderVersion = installedName;
                await _instances.UpdateAsync(current);
            }
            return new { message = $"加载器安装完成：{installedName}" };
        }
        catch (Exception ex)
        {
            return new { message = $"加载器安装失败：{ex.Message}" };
        }
    }

    private static LoaderKind ParseLoader(string label) => label.ToLowerInvariant() switch
    {
        "fabric" => LoaderKind.Fabric,
        "forge" => LoaderKind.Forge,
        "quilt" => LoaderKind.Quilt,
        "neoforge" => LoaderKind.NeoForge,
        "optifine" => LoaderKind.OptiFine,
        "liteloader" => LoaderKind.LiteLoader,
        _ => LoaderKind.Vanilla
    };

    // ---------- 实例 ----------

    private object GetInstances()
    {
        var currentId = _instances.Current?.Id;
        return new
        {
            instances = _instances.Instances.Select(i => InstanceDto(i, i.Id == currentId))
        };
    }

    private static object InstanceDto(Instance i, bool isCurrent) => new
    {
        i.Id,
        i.Name,
        i.Description,
        i.VersionId,
        Loader = i.Loader.ToString(),
        i.LoaderVersion,
        i.VersionIsolation,
        i.JavaPath,
        i.MinMemoryMb,
        i.MaxMemoryMb,
        i.JvmArgs,
        i.GameArgs,
        i.WindowWidth,
        i.WindowHeight,
        i.Fullscreen,
        i.ServerId,
        i.CloseLauncherAfterLaunch,
        i.IsFavorite,
        i.ResolvedVersionName,
        IsCurrent = isCurrent
    };

    private async Task<object> CreateInstanceAsync(JsonElement payload)
    {
        var name = Str(payload, "name");
        var version = Str(payload, "version");
        if (string.IsNullOrWhiteSpace(version))
            return new { message = "请填写游戏版本" };
        try
        {
            var instance = await _instances.CreateAsync(
                string.IsNullOrWhiteSpace(name) ? version : name!,
                version!);
            return new { message = $"已创建实例：{instance.Name}", instance = InstanceDto(instance, true) };
        }
        catch (Exception ex)
        {
            return new { message = $"创建失败：{ex.Message}" };
        }
    }

    private async Task<object> SelectInstanceAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return new { message = "缺少实例 ID" };
        try
        {
            await _instances.SetCurrentAsync(id);
            return new { message = "已切换实例" };
        }
        catch (Exception ex)
        {
            return new { message = ex.Message };
        }
    }

    private async Task<object> DeleteInstanceAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return new { message = "缺少实例 ID" };
        try
        {
            await _instances.DeleteAsync(id);
            return new { message = "实例已删除" };
        }
        catch (Exception ex)
        {
            return new { message = ex.Message };
        }
    }

    private async Task<object> SaveInstanceAsync(JsonElement payload)
    {
        var id = Str(payload, "id");
        var instance = _instances.Instances.FirstOrDefault(i => i.Id == id);
        if (instance == null)
            return new { message = "实例不存在" };
        try
        {
            instance.Name = Str(payload, "name") ?? instance.Name;
            instance.Description = Str(payload, "description");
            instance.VersionId = Str(payload, "versionId") ?? instance.VersionId;
            instance.Loader = ParseLoader(Str(payload, "loader") ?? instance.Loader.ToString());
            instance.LoaderVersion = Str(payload, "loaderVersion");
            instance.VersionIsolation = Bool(payload, "versionIsolation", instance.VersionIsolation);
            instance.JavaPath = Str(payload, "javaPath");
            instance.MinMemoryMb = Int(payload, "minMemoryMb", instance.MinMemoryMb);
            instance.MaxMemoryMb = Int(payload, "maxMemoryMb", instance.MaxMemoryMb);
            instance.JvmArgs = Str(payload, "jvmArgs") ?? "";
            instance.GameArgs = Str(payload, "gameArgs") ?? "";
            instance.WindowWidth = Int(payload, "windowWidth", instance.WindowWidth);
            instance.WindowHeight = Int(payload, "windowHeight", instance.WindowHeight);
            instance.Fullscreen = Bool(payload, "fullscreen", instance.Fullscreen);
            instance.ServerId = Str(payload, "serverId");
            instance.CloseLauncherAfterLaunch = Bool(payload, "closeLauncherAfterLaunch", instance.CloseLauncherAfterLaunch);
            instance.IsFavorite = Bool(payload, "isFavorite", instance.IsFavorite);
            await _instances.UpdateAsync(instance);
            return new { message = "实例设置已保存", instance = InstanceDto(instance, true) };
        }
        catch (Exception ex)
        {
            return new { message = $"保存失败：{ex.Message}" };
        }
    }

    private async Task<object> LaunchInstanceAsync(string? id)
    {
        var instance = _instances.Instances.FirstOrDefault(i => i.Id == id) ?? _instances.Current;
        var account = _accounts.CurrentAccount;
        if (instance == null || account == null)
            return new { message = "需要实例和账户" };
        try
        {
            var server = instance.ServerId == null
                ? null
                : _servers.Servers.FirstOrDefault(s => s.Id == instance.ServerId);
            await _launcher.LaunchAsync(instance, account, server);
            return new { message = "游戏已启动" };
        }
        catch (Exception ex)
        {
            return new { message = $"启动失败：{ex.Message}" };
        }
    }

    private object OpenGameFolder(string? id)
    {
        var instance = _instances.Instances.FirstOrDefault(i => i.Id == id) ?? _instances.Current;
        if (instance == null)
            return new { message = "无实例" };
        var dir = _instances.GetGameDirectory(instance);
        Directory.CreateDirectory(dir);
        return OpenFolder(dir) is { } r ? r : new { message = "已打开" };
    }

    private async Task<object> ImportPackAsync(JsonElement payload)
    {
        var fileName = Str(payload, "fileName");
        var data = Str(payload, "data");
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(data))
            return new { message = "缺少整合包文件" };
        var tmp = Path.Combine(Path.GetTempPath(), "craftstation", Guid.NewGuid().ToString("N") + Path.GetExtension(fileName));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tmp)!);
            await File.WriteAllBytesAsync(tmp, Convert.FromBase64String(data));
            var instanceName = Path.GetFileNameWithoutExtension(fileName);
            var instance = await _modpacks.ImportAsync(tmp, instanceName);
            return new { message = $"整合包导入完成：{instance.Name}", instance = InstanceDto(instance, true) };
        }
        catch (Exception ex)
        {
            return new { message = $"导入失败：{ex.Message}" };
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }

    private async Task<object> ExportPackAsync(JsonElement payload)
    {
        var id = Str(payload, "id");
        var format = Str(payload, "format") ?? "mrpack";
        var instance = _instances.Instances.FirstOrDefault(i => i.Id == id) ?? _instances.Current;
        if (instance == null)
            return new { message = "无实例" };
        try
        {
            var exportDir = Path.Combine(_settings.DataDirectory, "exports");
            Directory.CreateDirectory(exportDir);
            var safe = string.Concat(instance.Name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            var modrinth = string.Equals(format, "mrpack", StringComparison.OrdinalIgnoreCase);
            var path = Path.Combine(exportDir, safe + (modrinth ? ".mrpack" : ".zip"));
            await _modpacks.ExportAsync(instance, path, modrinth);
            return new { message = $"已导出：{path}", path };
        }
        catch (Exception ex)
        {
            return new { message = $"导出失败：{ex.Message}" };
        }
    }

    // ---------- 账户 ----------

    private object GetAccounts() => new
    {
        accounts = _accounts.Accounts.Select(a => new
        {
            a.Id,
            a.DisplayName,
            a.KindLabel,
            a.SkinUrl,
            a.CapeUrl,
            IsCurrent = a.Id == _accounts.CurrentAccount?.Id
        })
    };

    private object AddOfflineAccount(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new { message = "请输入用户名" };
        try
        {
            var entry = _accounts.AddOfflineAccount(name);
            return new { message = $"已添加离线账户：{entry.DisplayName}" };
        }
        catch (Exception ex)
        {
            return new { message = ex.Message };
        }
    }

    private async Task<object> SelectAccountAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return new { message = "缺少账户 ID" };
        await _accounts.SetCurrentAccountAsync(id);
        return new { message = "已切换账户" };
    }

    private async Task<object> RemoveAccountAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return new { message = "缺少账户 ID" };
        await _accounts.RemoveAccountAsync(id);
        return new { message = "账户已移除" };
    }

    private async Task<object> LoginMicrosoftAsync()
    {
        try
        {
            var entry = await _accounts.LoginMicrosoftAsync(new MicrosoftLoginOptions
            {
                ClientId = _settings.Settings.MicrosoftClientId,
                Mode = MicrosoftLoginMode.EmbeddedWebView
            });
            return new { message = $"登录成功：{entry.DisplayName}" };
        }
        catch (Exception ex)
        {
            return new { message = $"登录失败：{ex.Message}" };
        }
    }

    private async Task<object> LoginDeviceCodeAsync()
    {
        try
        {
            var entry = await _accounts.LoginMicrosoftAsync(new MicrosoftLoginOptions
            {
                ClientId = _settings.Settings.MicrosoftClientId,
                Mode = MicrosoftLoginMode.DeviceCode,
                DeviceCodeCallback = code =>
                {
                    var json = JsonSerializer.Serialize(new
                    {
                        code.UserCode,
                        code.VerificationUrl,
                        ExpiresOn = code.ExpiresOn.ToString("HH:mm:ss")
                    }, JsonOpts);
                    Notify?.Invoke("deviceCode", json);
                }
            });
            return new { message = $"登录成功：{entry.DisplayName}" };
        }
        catch (Exception ex)
        {
            return new { message = $"登录失败：{ex.Message}" };
        }
    }

    private async Task<object> RefreshAccountAsync(string? id)
    {
        var entry = _accounts.Accounts.FirstOrDefault(a => a.Id == id);
        if (entry == null)
            return new { message = "账户不存在" };
        try
        {
            await _accounts.RefreshMicrosoftAsync(entry);
            return new { message = "账户已刷新" };
        }
        catch (Exception ex)
        {
            return new { message = $"刷新失败：{ex.Message}" };
        }
    }

    private async Task<object> DownloadSkinAsync(string? id)
    {
        var entry = _accounts.Accounts.FirstOrDefault(a => a.Id == id);
        if (entry == null || entry.Kind != AccountKind.Microsoft)
            return new { message = "仅微软账户可下载皮肤" };
        try
        {
            var dir = Path.Combine(_settings.DataDirectory, "skins");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, entry.DisplayName + "-skin.png");
            await _skins.DownloadSkinAsync(entry, path);
            var bytes = await File.ReadAllBytesAsync(path);
            return new { message = "皮肤已下载", path, data = Convert.ToBase64String(bytes) };
        }
        catch (Exception ex)
        {
            return new { message = $"皮肤下载失败：{ex.Message}" };
        }
    }

    // ---------- 资源管理 ----------

    private async Task<object> GetResourcesAsync()
    {
        var instance = _instances.Current;
        if (instance == null)
            return new { mods = Array.Empty<object>(), resourcePacks = Array.Empty<object>(), shaderPacks = Array.Empty<object>(), saves = Array.Empty<object>() };

        var mods = (await _resources.ListModsAsync(instance)).Select(ResourceDto);
        var packs = (await _resources.ListResourcePacksAsync(instance)).Select(ResourceDto);
        var shaders = (await _resources.ListShaderPacksAsync(instance)).Select(ResourceDto);
        var saves = (await _resources.ListSavesAsync(instance)).Select(s => new
        {
            s.FolderName,
            s.FolderPath,
            s.DisplayName,
            s.GameMode,
            s.Difficulty,
            LastPlayedUtc = s.LastPlayedUtc?.ToString("yyyy-MM-dd HH:mm")
        });
        return new { mods, resourcePacks = packs, shaderPacks = shaders, saves };
    }

    private static object ResourceDto(ResourceEntry r) => new
    {
        r.FileName,
        r.FilePath,
        r.KindLabel,
        r.SizeLabel,
        r.IsDisabled,
        r.DisplayName,
        r.Version
    };

    private async Task<object> ImportResourceAsync(JsonElement payload)
    {
        var instance = _instances.Current;
        if (instance == null)
            return new { message = "请先选择实例" };
        var kind = Str(payload, "kind") switch
        {
            "resourcepack" => ResourceKind.ResourcePack,
            "shader" => ResourceKind.ShaderPack,
            _ => ResourceKind.Mod
        };
        var fileName = Str(payload, "fileName");
        var data = Str(payload, "data");
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(data))
            return new { message = "缺少文件" };
        var tmp = Path.Combine(Path.GetTempPath(), "craftstation", Guid.NewGuid().ToString("N") + Path.GetExtension(fileName));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tmp)!);
            await File.WriteAllBytesAsync(tmp, Convert.FromBase64String(data));
            await _resources.ImportFileAsync(instance, tmp, kind);
            return new { message = $"已导入：{fileName}" };
        }
        catch (Exception ex)
        {
            return new { message = $"导入失败：{ex.Message}" };
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }

    private async Task<object> ToggleResourceAsync(JsonElement payload)
    {
        var filePath = Str(payload, "filePath");
        var entry = await FindResourceAsync(filePath);
        if (entry == null)
            return new { message = "资源不存在" };
        await _resources.SetEnabledAsync(entry, entry.IsDisabled);
        return new { message = entry.IsDisabled ? "已启用" : "已禁用" };
    }

    private async Task<object> DeleteResourceAsync(JsonElement payload)
    {
        var filePath = Str(payload, "filePath");
        var entry = await FindResourceAsync(filePath);
        if (entry == null)
            return new { message = "资源不存在" };
        await _resources.DeleteAsync(entry);
        return new { message = "已删除" };
    }

    private async Task<ResourceEntry?> FindResourceAsync(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;
        var instance = _instances.Current;
        if (instance == null)
            return null;
        var all = new List<ResourceEntry>();
        all.AddRange(await _resources.ListModsAsync(instance));
        all.AddRange(await _resources.ListResourcePacksAsync(instance));
        all.AddRange(await _resources.ListShaderPacksAsync(instance));
        return all.FirstOrDefault(r => string.Equals(r.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
    }

    private object OpenResourceFolder(string? kind)
    {
        var instance = _instances.Current;
        if (instance == null)
            return new { message = "请先选择实例" };
        var resourceKind = kind switch
        {
            "resourcepack" => ResourceKind.ResourcePack,
            "shader" => ResourceKind.ShaderPack,
            _ => ResourceKind.Mod
        };
        var dir = _resources.GetFolder(instance, resourceKind);
        Directory.CreateDirectory(dir);
        return OpenFolder(dir);
    }

    private object OpenSaveFolder(string? folder) =>
        string.IsNullOrWhiteSpace(folder) ? new { message = "目录为空" } : OpenFolder(folder);

    // ---------- 资源市场 ----------

    private async Task<object> SearchProjectsAsync(JsonElement payload)
    {
        try
        {
            var results = await _modrinth.SearchAsync(
                Str(payload, "query") ?? "",
                Str(payload, "projectType") ?? "mod",
                NullIfEmpty(Str(payload, "gameVersion")),
                NullIfEmpty(Str(payload, "loader")),
                40);
            return new { projects = results.Select(ProjectDto) };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message, projects = Array.Empty<object>() };
        }
    }

    private static object ProjectDto(ModrinthProject p) => new
    {
        p.Id,
        p.Slug,
        p.Title,
        p.Description,
        p.IconUrl,
        p.Downloads,
        p.ProjectType,
        p.TypeLabel,
        p.Categories,
        p.GameVersions,
        p.Loaders,
        p.Followers
    };

    private async Task<object> GetProjectVersionsAsync(JsonElement payload)
    {
        var projectId = Str(payload, "projectId");
        if (string.IsNullOrWhiteSpace(projectId))
            return new { versions = Array.Empty<object>() };
        try
        {
            var list = await _modrinth.GetVersionsAsync(
                projectId,
                NullIfEmpty(Str(payload, "gameVersion")),
                NullIfEmpty(Str(payload, "loader")));
            return new { versions = list.Select(v => new
            {
                v.Id,
                v.Name,
                v.VersionNumber,
                v.Changelog,
                DatePublished = v.DatePublished.ToString("yyyy-MM-dd"),
                v.GameVersions,
                v.Loaders,
                Files = v.Files.Select(f => new { f.Filename, f.Size, f.Primary })
            }) };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message, versions = Array.Empty<object>() };
        }
    }

    private async Task<object> DownloadProjectVersionAsync(JsonElement payload)
    {
        var instance = _instances.Current;
        var projectId = Str(payload, "projectId");
        var versionId = Str(payload, "versionId");
        if (instance == null || string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(versionId))
            return new { message = "请选择实例、项目和版本" };
        try
        {
            var project = await _modrinth.GetProjectAsync(projectId);
            var versions = await _modrinth.GetVersionsAsync(projectId);
            var version = versions.FirstOrDefault(v => v.Id == versionId);
            if (version == null)
                return new { message = "版本不存在" };
            var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault();
            if (file == null)
                return new { message = "版本没有可下载文件" };
            var kind = project?.ProjectType switch
            {
                "resourcepack" => ResourceKind.ResourcePack,
                "shader" => ResourceKind.ShaderPack,
                _ => ResourceKind.Mod
            };
            var folder = _resources.GetFolder(instance, kind);
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, file.Filename);
            await _modrinth.DownloadFileAsync(version, file, target);
            return new { message = $"已下载：{file.Filename}" };
        }
        catch (Exception ex)
        {
            return new { message = $"下载失败：{ex.Message}" };
        }
    }

    // ---------- 模组体检 ----------

    private async Task<object> ScanModsAsync()
    {
        var instance = _instances.Current;
        if (instance == null)
            return new { issues = Array.Empty<object>(), mods = Array.Empty<object>() };
        try
        {
            var report = await _health.ScanAsync(instance);
            return new
            {
                issues = report.Issues.Select(i => new
                {
                    i.Title,
                    i.Detail,
                    i.Suggestion,
                    i.SeverityLabel,
                    Severity = i.Severity.ToString(),
                    i.ModId,
                    i.FilePath
                }),
                mods = report.Mods.Select(ModDto)
            };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message, issues = Array.Empty<object>(), mods = Array.Empty<object>() };
        }
    }

    private static object ModDto(ModEntry m) => new
    {
        m.FileName,
        m.FilePath,
        m.IsDisabled,
        m.ModId,
        m.DisplayName,
        m.Version,
        m.MinecraftVersionRange,
        Loader = m.Loader.ToString(),
        m.IsValidMetadata,
        m.Display,
        Dependencies = m.Dependencies.Select(d => new { d.ModId, Kind = d.Kind.ToString(), d.VersionRange, d.IsSatisfied }),
        m.Provides
    };

    private async Task<object> GetDependencyTreeAsync(string? modId)
    {
        var instance = _instances.Current;
        if (instance == null || string.IsNullOrWhiteSpace(modId))
            return new { mods = Array.Empty<object>() };
        try
        {
            var report = await _health.ScanAsync(instance);
            return new { mods = _health.GetDependencyTree(report, modId).Select(ModDto) };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message, mods = Array.Empty<object>() };
        }
    }

    private async Task<object> DisableModAsync(string? filePath)
    {
        var mod = await FindModAsync(filePath);
        if (mod == null)
            return new { message = "模组不存在" };
        if (mod.IsDisabled)
            await _health.EnableAsync(mod);
        else
            await _health.DisableAsync(mod);
        return new { message = mod.IsDisabled ? "已启用" : "已禁用" };
    }

    private async Task<object> DeleteModAsync(string? filePath)
    {
        var mod = await FindModAsync(filePath);
        if (mod == null)
            return new { message = "模组不存在" };
        await _health.DeleteAsync(mod);
        return new { message = "已删除" };
    }

    private async Task<ModEntry?> FindModAsync(string? filePath)
    {
        var instance = _instances.Current;
        if (instance == null || string.IsNullOrWhiteSpace(filePath))
            return null;
        var report = await _health.ScanAsync(instance);
        return report.Mods.FirstOrDefault(m =>
            string.Equals(m.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<object> ExportModReportAsync()
    {
        var instance = _instances.Current;
        if (instance == null)
            return new { message = "请先选择实例" };
        try
        {
            var report = await _health.ScanAsync(instance);
            var content = _health.ExportReport(report);
            var dir = Path.Combine(_settings.DataDirectory, "exports");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"modhealth-{DateTime.Now:yyyyMMdd-HHmmss}.md");
            await File.WriteAllTextAsync(path, content);
            return new { message = $"报告已导出：{path}", path, content };
        }
        catch (Exception ex)
        {
            return new { message = $"导出失败：{ex.Message}" };
        }
    }

    // ---------- 服务器 ----------

    private object GetServers() => new
    {
        servers = _servers.Servers.Select(ServerDto)
    };

    private static object ServerDto(ServerEntry s) => new
    {
        s.Id,
        s.Name,
        s.Address,
        s.Port,
        s.Notes,
        LastPingUtc = s.LastPingUtc?.ToString("yyyy-MM-dd HH:mm")
    };

    private async Task<object> AddServerAsync(JsonElement payload)
    {
        var name = Str(payload, "name");
        var address = Str(payload, "address");
        if (string.IsNullOrWhiteSpace(address))
            return new { message = "请输入服务器地址" };
        try
        {
            var server = await _servers.AddAsync(
                string.IsNullOrWhiteSpace(name) ? address : name!,
                address!,
                Int(payload, "port", 25565));
            return new { message = $"已添加服务器：{server.Name}", server = ServerDto(server) };
        }
        catch (Exception ex)
        {
            return new { message = ex.Message };
        }
    }

    private async Task<object> DeleteServerAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return new { message = "缺少服务器 ID" };
        await _servers.DeleteAsync(id);
        return new { message = "服务器已删除" };
    }

    private async Task<object> PingServerAsync(string? id)
    {
        var server = _servers.Servers.FirstOrDefault(s => s.Id == id);
        if (server == null)
            return new { message = "服务器不存在" };
        try
        {
            var status = await _servers.PingAsync(server);
            server.LastPingUtc = DateTime.UtcNow;
            await _servers.UpdateAsync(server);
            return new
            {
                status.Online,
                status.LatencyMs,
                status.PlayersOnline,
                status.PlayersMax,
                status.Motd,
                status.Version,
                status.IconBase64,
                status.Error
            };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    private async Task<object> LaunchServerAsync(string? id)
    {
        var instance = _instances.Current;
        var account = _accounts.CurrentAccount;
        var server = _servers.Servers.FirstOrDefault(s => s.Id == id);
        if (instance == null || account == null || server == null)
            return new { message = "需要实例、账户和服务器" };
        try
        {
            instance.ServerId = server.Id;
            await _instances.UpdateAsync(instance);
            await _launcher.LaunchAsync(instance, account, server);
            return new { message = "已直连启动" };
        }
        catch (Exception ex)
        {
            return new { message = $"启动失败：{ex.Message}" };
        }
    }

    // ---------- 设置 ----------

    private object GetSettings() => new
    {
        gameDirectory = _settings.ResolveGameDirectory(),
        downloadSource = _settings.Settings.DownloadSource.ToString(),
        fallbackToOfficial = _settings.Settings.FallbackToOfficial,
        customDownloadSource = _settings.Settings.CustomDownloadSource,
        useDeviceCodeFallback = _settings.Settings.UseDeviceCodeFallback,
        language = _settings.Settings.Language,
        maxConcurrency = _settings.Settings.MaxConcurrency,
        proxy = _settings.Settings.Proxy,
        updateEndpoint = _settings.Settings.UpdateEndpoint,
        curseForgeApiKey = _settings.Settings.CurseForgeApiKey,
        animationsEnabled = _settings.Settings.AnimationsEnabled
    };

    private async Task<object> SaveSettingsAsync(JsonElement payload)
    {
        try
        {
            var s = _settings.Settings;
            s.GameDirectory = Str(payload, "gameDirectory") ?? s.GameDirectory;
            if (Enum.TryParse<DownloadSourceKind>(Str(payload, "downloadSource"), true, out var source))
                s.DownloadSource = source;
            s.FallbackToOfficial = Bool(payload, "fallbackToOfficial", s.FallbackToOfficial);
            s.CustomDownloadSource = Str(payload, "customDownloadSource") ?? s.CustomDownloadSource;
            s.UseDeviceCodeFallback = Bool(payload, "useDeviceCodeFallback", s.UseDeviceCodeFallback);
            s.Language = Str(payload, "language") ?? s.Language;
            s.MaxConcurrency = Int(payload, "maxConcurrency", s.MaxConcurrency);
            s.Proxy = Str(payload, "proxy");
            s.UpdateEndpoint = Str(payload, "updateEndpoint") ?? s.UpdateEndpoint;
            s.CurseForgeApiKey = Str(payload, "curseForgeApiKey") ?? s.CurseForgeApiKey;
            s.AnimationsEnabled = Bool(payload, "animationsEnabled", s.AnimationsEnabled);
            await _settings.SaveAsync();
            _launcher.ResetLauncher();
            return new { message = "设置已保存" };
        }
        catch (Exception ex)
        {
            return new { message = $"保存失败：{ex.Message}" };
        }
    }

    private async Task<object> CheckUpdateAsync()
    {
        try
        {
            var info = await _updater.CheckAsync();
            return info == null
                ? new { message = "未配置更新源或检查失败" }
                : new
                {
                    message = info.IsNewer ? $"发现新版本 {info.Version}" : $"当前已是最新（{info.Version}）",
                    info.Version,
                    info.Url,
                    info.Notes,
                    info.IsNewer
                };
        }
        catch (Exception ex)
        {
            return new { message = $"检查失败：{ex.Message}" };
        }
    }

    private async Task<object> ScanJavaAsync()
    {
        try
        {
            var list = await _java.ScanInstalledJavaAsync(refresh: true);
            return new
            {
                javas = list.Select(j => new { j.Path, j.Version, j.Vendor, j.MajorVersion })
            };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message, javas = Array.Empty<object>() };
        }
    }

    private object OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return new { message = "已打开" };
        }
        catch (Exception ex)
        {
            return new { message = ex.Message };
        }
    }

    // ---------- 进程 / 日志 ----------

    private async Task<object> StopGameAsync()
    {
        await _launcher.StopAsync();
        return new { message = "已停止游戏" };
    }

    private async Task<object> GetGameLogAsync(string? instanceId, int maxLines)
    {
        var instance = _instances.Instances.FirstOrDefault(i => i.Id == instanceId) ?? _instances.Current;
        if (instance == null)
            return new { lines = Array.Empty<string>() };
        var lines = await _logs.ReadLatestAsync(instance, Math.Clamp(maxLines, 1, Config.BridgeMaxLogLines));
        return new { lines };
    }

    // ---------- 工具 ----------

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object &&
        e.TryGetProperty(name, out var p) &&
        p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static int Int(JsonElement e, string name, int def) =>
        e.ValueKind == JsonValueKind.Object &&
        e.TryGetProperty(name, out var p) &&
        p.ValueKind == JsonValueKind.Number
            ? p.GetInt32()
            : def;

    private static bool Bool(JsonElement e, string name, bool def) =>
        e.ValueKind == JsonValueKind.Object &&
        e.TryGetProperty(name, out var p) &&
        p.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? p.GetBoolean()
            : def;

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
