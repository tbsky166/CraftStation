using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using CraftStation.Core;
using CraftStation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Serilog;

namespace CraftStation;

public partial class WebPreviewWindow : Window
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim RuntimeExtractLock = new(1, 1);
    private HtmlBridge? _bridge;

    public WebPreviewWindow()
    {
        InitializeComponent();
        Title = Config.AppName;
        Width = Config.WindowDefaultWidth;
        Height = Config.WindowDefaultHeight;
        MinWidth = Config.WindowMinWidth;
        MinHeight = Config.WindowMinHeight;
        // 加载完成前保持深色，避免白屏闪烁
        var bg = System.Drawing.ColorTranslator.FromHtml(Config.WebViewBackgroundColorHex);
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromArgb(bg.A, bg.R, bg.G, bg.B));
        WebView.DefaultBackgroundColor = bg;
        Loaded += async (_, _) =>
        {
            Log.Information("Web 预览窗口 Loaded");
            await InitializeWebAsync();
        };
    }

    private async Task InitializeWebAsync()
    {
        try
        {
            Log.Information("WebView2 初始化开始");
            var fixedRuntime = await ResolveFixedRuntimeFolderAsync();
            if (fixedRuntime != null)
            {
                // 使用随包分发的固定版运行时，目标机无需安装 WebView2
                WebView.CreationProperties = new CoreWebView2CreationProperties
                {
                    BrowserExecutableFolder = fixedRuntime,
                    UserDataFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        Config.AppName,
                        Config.WebView2UserDataDirectoryName)
                };
                Log.Information("使用随包 WebView2 固定版运行时：{Runtime}", fixedRuntime);
            }
            else
            {
                Log.Information("未找到随包/内嵌 WebView2 运行时，回退系统 Evergreen Runtime");
            }
            await WebView.EnsureCoreWebView2Async();
            var settings = WebView.CoreWebView2.Settings;
            settings.AreDefaultContextMenusEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.IsZoomControlEnabled = false;
            settings.IsPinchZoomEnabled = false;
#if !DEBUG
            settings.AreDevToolsEnabled = false;
#endif
            // 所有 Web 资源内嵌在程序集里，通过 WebResourceRequested 按虚拟域名提供
            WebView.CoreWebView2.AddWebResourceRequestedFilter(
                $"https://{Config.WebViewVirtualHostName}/*",
                CoreWebView2WebResourceContext.All);
            WebView.CoreWebView2.WebResourceRequested += OnWebResourceRequested;
            WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _bridge = App.Services.GetRequiredService<HtmlBridge>();
            _bridge.Notify = (eventName, json) =>
            {
                try
                {
                    var script = $"window.__csEvent?.({JsonSerializer.Serialize(eventName, JsonOpts)}, {json});";
                    _ = WebView.CoreWebView2.ExecuteScriptAsync(script);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Web 事件发送失败");
                }
            };
            WebView.CoreWebView2.Navigate(Config.WebViewStartUrl);
            Log.Information("WebView2 已导航");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "WebView2 初始化失败");
            MessageBox.Show(
                "WebView2 运行时初始化失败，界面无法加载。\n\n" +
                "可能原因：未安装 WebView2 Runtime，或版本过旧。\n" +
                "请安装 Microsoft Edge WebView2 Runtime（Evergreen）：\n" +
                "https://developer.microsoft.com/microsoft-edge/webview2/\n\n" +
                $"详细信息：{ex.Message}",
                "CraftStation",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static async Task<string?> ResolveFixedRuntimeFolderAsync()
    {
        var localRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Config.AppName);
        foreach (var root in new[]
                 {
                     AppContext.BaseDirectory,
                     localRoot
                 })
        {
            var dir = Path.Combine(root, Config.WebView2RuntimeDirectoryName);
            if (File.Exists(Path.Combine(dir, "msedgewebview2.exe")))
                return dir;
        }

        // 内嵌固定版运行时：首次运行解压到 LocalAppData
        return await ExtractEmbeddedRuntimeAsync(localRoot);
    }

    private static async Task<string?> ExtractEmbeddedRuntimeAsync(string localRoot)
    {
        const string prefix = "wruntime/";
        var assembly = typeof(WebPreviewWindow).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();
        if (names.Count == 0)
            return null;

        var targetRoot = Path.Combine(localRoot, Config.WebView2RuntimeDirectoryName);
        if (File.Exists(Path.Combine(targetRoot, "msedgewebview2.exe")))
            return targetRoot;

        await RuntimeExtractLock.WaitAsync();
        try
        {
            if (File.Exists(Path.Combine(targetRoot, "msedgewebview2.exe")))
                return targetRoot;

            var tmp = Path.Combine(localRoot, Config.WebView2RuntimeDirectoryName + ".tmp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            var tmpFull = Path.GetFullPath(tmp);
            foreach (var name in names)
            {
                var rel = name[prefix.Length..]
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);
                var target = Path.GetFullPath(Path.Combine(tmp, rel));
                if (!target.StartsWith(tmpFull, StringComparison.OrdinalIgnoreCase))
                    continue; // 防御路径逃逸
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var src = assembly.GetManifestResourceStream(name);
                if (src == null)
                    continue;
                await using var dst = File.Create(target);
                await src.CopyToAsync(dst);
            }

            if (!File.Exists(Path.Combine(tmp, "msedgewebview2.exe")))
            {
                Directory.Delete(tmp, true);
                return null;
            }

            if (Directory.Exists(targetRoot))
                Directory.Delete(targetRoot, true); // 覆盖旧的不完整解压
            Directory.Move(tmp, targetRoot);
            return targetRoot;
        }
        catch
        {
            try
            {
                foreach (var d in Directory.EnumerateDirectories(localRoot, Config.WebView2RuntimeDirectoryName + ".tmp-*"))
                    Directory.Delete(d, true);
            }
            catch
            {
                // 清理失败不影响回退系统运行时
            }
            return null;
        }
        finally
        {
            RuntimeExtractLock.Release();
        }
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            var id = root.GetProperty("id").GetInt32();
            var type = root.GetProperty("type").GetString() ?? "";
            var payload = root.TryGetProperty("payload", out var p) ? p : default;

            var result = await _bridge!.HandleAsync(type, payload);
            var resultJson = JsonSerializer.Serialize(result, JsonOpts);
            var script = $"window.__csCallback({id}, true, {resultJson});";
            await WebView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Web 桥接处理失败");
        }
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            var uri = new Uri(e.Request.Uri);
            if (!uri.Host.Equals(Config.WebViewVirtualHostName, StringComparison.OrdinalIgnoreCase))
                return;

            var assembly = typeof(WebPreviewWindow).Assembly;
            var rel = uri.AbsolutePath.TrimStart('/').Replace('/', '.');
            var resourceName = $"{assembly.GetName().Name}.{Config.WebAssetsDirectoryName}.{Config.WebRootDirectoryName}.{rel}";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                e.Response = WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                    null, 404, "Not Found", "Content-Type: text/plain");
                return;
            }

            var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;
            var mime = GetMimeType(Path.GetExtension(uri.AbsolutePath));
            e.Response = WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                buffer, 200, "OK", $"Content-Type: {mime}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Web 资源加载失败");
        }
    }

    private static string GetMimeType(string extension) => extension.ToLowerInvariant() switch
    {
        ".html" or ".htm" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" or ".mjs" => "application/javascript; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".ttf" => "font/ttf",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        ".ico" => "image/x-icon",
        ".mp3" => "audio/mpeg",
        ".ogg" => "audio/ogg",
        ".wav" => "audio/wav",
        _ => "application/octet-stream"
    };

    internal void MinimizeWindow() => WindowState = WindowState.Minimized;

    internal void ToggleMaximizeWindow() =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    internal void CloseWindow() => Close();

    internal void BeginDrag()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                // 模拟标题栏按下：对无边框 + WebView2 窗口最可靠，也支持双击最大化
                SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                return;
            }
        }
        catch
        {
            // 回退到 WPF 原生拖动
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // 鼠标已释放等情况下忽略，保持稳定
        }
    }

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
