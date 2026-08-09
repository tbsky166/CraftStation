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
    $ext = [System.IO.Path]::GetExtension($ZipPath).ToLowerInvariant()
    if ($ext -eq ".cab") {
        # 微软官方固定版运行时是 .cab，用系统自带 expand.exe 解压
        & expand -F:* $ZipPath $OutDir | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "expand 解压失败: $ZipPath"
        }
    }
    elseif ($ext -eq ".zip") {
        Expand-Archive -LiteralPath $ZipPath -DestinationPath $OutDir -Force
    }
    else {
        throw "不支持的格式（支持 .zip / .cab）: $ZipPath"
    }

    # 压缩包内可能带一层子目录，把 msedgewebview2.exe 所在目录内容提升到根
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
    Write-Host "请到微软官方页面下载 WebView2 Runtime 固定版（Fixed Version）x64："
    Write-Host "https://developer.microsoft.com/microsoft-edge/webview2/"
    Write-Host "注意：官方固定版下载下来是 .cab 文件，脚本已支持直接解压。"
    Start-Process "https://developer.microsoft.com/microsoft-edge/webview2/"
    Write-Host ""
    Write-Host "下载后执行："
    Write-Host "  .\tools\Get-WebView2Runtime.ps1 -ZipPath <下载的.cab路径>"
    Write-Host "随后正常 dotnet publish，运行时会被编进单文件 exe。"
}
