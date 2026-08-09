#Requires -Version 5.1
<#
.SYNOPSIS
  对 CraftStation 发布产物进行本地自签名。

.DESCRIPTION
  流程：
    1. 在 CurrentUser\My 中查找代码签名证书（按主题或指纹）；
       找不到时自动创建一张自签名代码签名证书（RSA 3072 / SHA256 / 5 年）。
    2. 定位 Windows SDK 的 signtool.exe。
    3. 用 SHA256 + RFC3161 时间戳签名目标 exe；时间戳失败时回退为无时间戳签名。
    4. 用 Get-AuthenticodeSignature 校验结果。

  注意：
  - 自签名证书不是受信任 CA 签发，公开分发时 Windows 仍会提示
    “未知发布者 / SmartScreen”。
  - 正式对外发布请改用 SignPath（开源项目免费）或商业 OV 证书，
    并在 CI 中用 GitHub Secrets 保管 PFX。

.PARAMETER ExePath
  要签名的 exe 完整路径。不填则自动查找 win-x64 publish 输出。

.PARAMETER CertSubject
  证书主题，默认 "CN=CraftStation Development"。

.PARAMETER Thumbprint
  指定已有证书指纹（SHA1）。不填则按 CertSubject 查找，找不到就新建。

.PARAMETER CreateCert
  找不到证书时是否自动创建，默认 $true。

.PARAMETER SigntoolPath
  signtool.exe 路径，不填则从 Windows Kits 自动查找。

.PARAMETER TimestampServer
  RFC3161 时间戳服务器地址，默认 http://timestamp.digicert.com。
  传空字符串可禁用时间戳。

.PARAMETER TrustLocal
  签名后把自签名证书导入 CurrentUser\Root，让本机信任（仅本用户）。
  仅用于本机测试，不要把这个证书分发给用户。

.EXAMPLE
  .\Sign-Release.ps1

.EXAMPLE
  .\Sign-Release.ps1 -ExePath .\publish\CraftStation.exe -TrustLocal
#>
param(
    [string]$ExePath = '',
    [string]$CertSubject = 'CN=CraftStation Development',
    [string]$Thumbprint = '',
    [bool]$CreateCert = $true,
    [string]$SigntoolPath = '',
    [string]$TimestampServer = 'http://timestamp.digicert.com',
    [switch]$TrustLocal
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step([string]$Text) {
    Write-Host "==> $Text" -ForegroundColor Cyan
}

# ---------- 1. 定位要签名的 exe ----------
if (-not $ExePath) {
    $defaultExe = Join-Path $PSScriptRoot '..\bin\Release\net10.0-windows\win-x64\publish\CraftStation.exe'
    if (Test-Path $defaultExe) { $ExePath = $defaultExe }
}
if (-not $ExePath -or -not (Test-Path $ExePath)) {
    throw '找不到要签名的 CraftStation.exe，请用 -ExePath 指定，或先执行：dotnet publish CraftStation\CraftStation.csproj -p:PublishProfile=win-x64'
}
$ExePath = (Resolve-Path $ExePath).Path

# ---------- 2. 定位 signtool ----------
if (-not $SigntoolPath) {
    $signtoolCandidates = @()
    foreach ($root in @('C:\Program Files (x86)\Windows Kits\10\bin', 'C:\Program Files\Windows Kits\10\bin')) {
        if (Test-Path $root) {
            $signtoolCandidates += Get-ChildItem $root -Directory -ErrorAction SilentlyContinue |
                Sort-Object Name -Descending |
                ForEach-Object {
                    $candidate = Join-Path $_.FullName 'x64\signtool.exe'
                    if (Test-Path $candidate) { $candidate }
                }
        }
    }
    $SigntoolPath = $signtoolCandidates | Select-Object -First 1
}
if (-not $SigntoolPath) {
    $cmd = Get-Command signtool -ErrorAction SilentlyContinue
    if ($cmd) { $SigntoolPath = $cmd.Source }
}
if (-not $SigntoolPath -or -not (Test-Path $SigntoolPath)) {
    throw '找不到 signtool.exe，请用 -SigntoolPath 指定（需要安装 Windows SDK 或 Visual Studio 的 Windows 组件）'
}
$SigntoolPath = (Resolve-Path $SigntoolPath).Path

# ---------- 3. 查找 / 创建证书 ----------
$storePath = 'Cert:\CurrentUser\My'
$cert = $null
if ($Thumbprint) {
    $cert = Get-ChildItem $storePath -CodeSigningCert -ErrorAction SilentlyContinue |
        Where-Object { $_.Thumbprint -eq $Thumbprint } |
        Select-Object -First 1
    if (-not $cert) { throw "在 $storePath 中找不到指纹为 $Thumbprint 的代码签名证书" }
}
else {
    $cert = Get-ChildItem $storePath -CodeSigningCert -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -like "*$CertSubject*" } |
        Select-Object -First 1
}

if (-not $cert) {
    if (-not $CreateCert) { throw "未找到代码签名证书（$CertSubject），且已禁用自动创建" }
    Write-Step "创建自签名代码签名证书：$CertSubject（RSA 3072 / SHA256 / 5 年）"
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $CertSubject `
        -FriendlyName 'CraftStation Development Signing' `
        -CertStoreLocation $storePath `
        -KeyExportPolicy Exportable `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -HashAlgorithm SHA256 `
        -NotBefore (Get-Date).AddDays(-1) `
        -NotAfter (Get-Date).AddYears(5)
}
Write-Step "使用证书：$($cert.Subject)  指纹 $($cert.Thumbprint)  有效期至 $($cert.NotAfter)"

# ---------- 4. 签名 ----------
Write-Step "签名：$ExePath"
$commonArgs = @(
    'sign', '/fd', 'SHA256', '/sha1', $cert.Thumbprint,
    '/d', 'CraftStation',
    '/du', 'https://github.com/tbsky166/CraftStation'
)
$tsArgs = @('/tr', $TimestampServer, '/td', 'SHA256')
$fileArgs = @('/v', $ExePath)
if ($TimestampServer) {
    & $SigntoolPath @($commonArgs + $tsArgs + $fileArgs)
    if ($LASTEXITCODE -ne 0) {
        Write-Warning '带时间戳签名失败（可能连不上时间戳服务器），回退为无时间戳签名…'
        & $SigntoolPath @($commonArgs + $fileArgs)
    }
}
else {
    & $SigntoolPath @($commonArgs + $fileArgs)
}
if ($LASTEXITCODE -ne 0) {
    throw "signtool 签名失败，退出码：$LASTEXITCODE"
}

# ---------- 5. 可选：本机信任 ----------
if ($TrustLocal) {
    Write-Step '将证书导入 CurrentUser\Root（仅本机本用户信任）'
    $cerPath = Join-Path $env:TEMP "craftstation-$($cert.Thumbprint).cer"
    Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT | Out-Null
    Import-Certificate -FilePath $cerPath -CertStoreLocation 'Cert:\CurrentUser\Root' | Out-Null
    Remove-Item -LiteralPath $cerPath -Force
}

# ---------- 6. 校验 ----------
Write-Step '校验签名'
$sig = Get-AuthenticodeSignature $ExePath
$sig | Format-List Status, StatusMessage, @{L = '签名者'; E = { $_.SignerCertificate.Subject } }, TimeStamperCertificate
if ($sig.Status -eq 'Valid') {
    Write-Host '签名有效（本机受信任）。' -ForegroundColor Green
}
elseif ($sig.Status -eq 'NotTrusted') {
    Write-Host '文件已签名，但证书链不受信任（自签名正常现象）。' -ForegroundColor Yellow
    Write-Host '如需本机测试时消除该提示，可执行：' -ForegroundColor Yellow
    Write-Host "    .\Sign-Release.ps1 -TrustLocal" -ForegroundColor Gray
}
elseif ($sig.Status -eq 'UnknownError' -and $sig.SignerCertificate) {
    Write-Host '文件已签名，但证书链不受信任（自签名正常现象）。' -ForegroundColor Yellow
    Write-Host '如需本机测试时消除该提示，可执行：' -ForegroundColor Yellow
    Write-Host "    .\Sign-Release.ps1 -TrustLocal" -ForegroundColor Gray
}
else {
    throw "签名校验失败：$($sig.Status) $($sig.StatusMessage)"
}
Write-Host ''
Write-Host '完成。公开分发前请记住：自签名不等于可信发布者。' -ForegroundColor Cyan
