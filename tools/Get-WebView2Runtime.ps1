param(
    [string]$ZipPath = "",
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"

if (-not $OutDir) {
    $OutDir = Join-Path $PSScriptRoot "..\WebView2Runtime"
}

if ($ZipPath) {
    if (-not (Test-Path -LiteralPath $ZipPath)) {
        throw "找不到压缩包: $ZipPath"
    }
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
    Expand-Archive -LiteralPath $ZipPath -DestinationPath $OutDir -Force

    # zip 内可能带一层子目录，把 msedgewebview2.exe 所在目录内容提升到根
    $exe = Get-ChildItem -Path $OutDir -Recurse -Filter "msedgewebview2.exe" | Select-Object -First 1
    if ($exe) {
        $srcRoot = $exe.Directory
        $outRoot = (Resolve-Path $OutDir).Path
        if ($srcRoot.FullName -ne $outRoot) {
            Get-ChildItem -LiteralPath $srcRoot.FullName -Force | Move-Item -Destination $OutDir -Force
        }
        Write-Host "WebView2 固定版运行时已就绪: $OutDir"
        Write-Host "提示：该文件夹会被编译进单文件 exe（首次运行自动解压），发布前请保留此目录。"
    }
    else {
        Write-Host "解压完成，但未找到 msedgewebview2.exe，请检查 zip 内容。"
    }
}
else {
    Write-Host "请到微软官方页面下载 WebView2 Runtime 固定版（Fixed Version）x64 zip："
    Write-Host "https://developer.microsoft.com/microsoft-edge/webview2/"
    Start-Process "https://developer.microsoft.com/microsoft-edge/webview2/"
    Write-Host ""
    Write-Host "下载后执行："
    Write-Host "  .\tools\Get-WebView2Runtime.ps1 -ZipPath <下载的zip路径>"
    Write-Host "然后把 WebView2Runtime 文件夹放到 exe 同级目录一起分发。"
}
