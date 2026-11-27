<#
.SYNOPSIS
    下载 Microsoft.Web.WebView2 的 NuGet 包，并把这套构建需要的两个文件放到本目录。

.DESCRIPTION
    仓库不提交第三方二进制；本地构建与 CI 都走这个脚本。
    产物：Microsoft.Web.WebView2.Core.dll + WebView2Loader.dll

    包内路径会随版本变化（lib/net45、lib/netstandard2.0、lib/netcoreapp3.0……），
    所以这里不写死路径，直接递归找文件，并优先 netstandard2.0。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File get-webview2.ps1
    powershell -ExecutionPolicy Bypass -File get-webview2.ps1 -Version 1.0.2700.33
#>
param(
    [string]$Version = '1.0.864.35'
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$tmp = Join-Path $env:TEMP ('wv2-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null

function Get-Package([string]$ver) {
    $pkg = Join-Path $tmp 'wv2.zip'
    $url = if ($ver) {
        "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/$ver"
    } else {
        "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2"
    }
    Write-Host "download $url"
    Invoke-WebRequest -Uri $url -OutFile $pkg -UseBasicParsing
    Expand-Archive -Path $pkg -DestinationPath $tmp -Force
}

try {
    try {
        Get-Package $Version
    }
    catch {
        Write-Warning "指定版本 $Version 拉取失败，改用最新版：$($_.Exception.Message)"
        Get-Package $null
    }

    $core = Get-ChildItem -Path $tmp -Recurse -Filter 'Microsoft.Web.WebView2.Core.dll' |
        Sort-Object { if ($_.FullName -match 'netstandard2\.0') { 0 } elseif ($_.FullName -match 'net45') { 1 } else { 2 } } |
        Select-Object -First 1

    $loader = Get-ChildItem -Path $tmp -Recurse -Filter 'WebView2Loader.dll' |
        Where-Object { $_.FullName -match 'win-x64|x64' } |
        Select-Object -First 1

    if (-not $core) {
        Write-Host '包目录结构（前 30 个 dll）：' -ForegroundColor Yellow
        Get-ChildItem -Path $tmp -Recurse -Filter '*.dll' | Select-Object -First 30 FullName | Format-Table -AutoSize
        throw '包内没找到 Microsoft.Web.WebView2.Core.dll'
    }
    if (-not $loader) { throw '包内没找到 WebView2Loader.dll（win-x64）' }

    Copy-Item -LiteralPath $core.FullName   (Join-Path $here 'Microsoft.Web.WebView2.Core.dll') -Force
    Copy-Item -LiteralPath $loader.FullName (Join-Path $here 'WebView2Loader.dll') -Force

    Write-Host "OK  core  <- $($core.FullName)"
    Write-Host "OK  loader<- $($loader.FullName)"
    Get-ChildItem $here -Filter '*.dll' | Select-Object Name, Length | Format-Table
}
finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
