using CraftStation.Core;
using CraftStation.Core.Models;
using CraftStation.Core.Services;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Desktop;
using XboxAuthNet.Game.Msal;

namespace CraftStation.Services;

/// <summary>
/// 在 WPF 壳层构建 MSAL 公开客户端应用，注册 Windows 内嵌 WebView（WebView2）登录支持。
/// </summary>
public sealed class WindowsMsalAppFactory : IMsalAppFactory
{
    public async Task<IPublicClientApplication> CreateAsync(string clientId, string redirectUri)
    {
        var app = PublicClientApplicationBuilder.Create(clientId)
            .WithWindowsEmbeddedBrowserSupport()
            .WithTenantId(Config.MicrosoftTenant)
            .WithRedirectUri(redirectUri)
            .Build();
        await MsalClientHelper.RegisterCache(app, new MsalCacheSettings());
        return app;
    }
}
