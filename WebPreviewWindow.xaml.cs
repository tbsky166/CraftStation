using System.IO;
using System.Text.Json;
using System.Windows;
using CraftStation.Core;
using CraftStation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Serilog;

namespace CraftStation;

public partial class WebPreviewWindow : Window
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
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
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // 鼠标已释放等情况下忽略，保持稳定
        }
    }
}
