# 下载并解包 Microsoft.Web.WebView2，把构建需要的两个文件放到脚本所在目录。
# 用法（PowerShell）：  ./get-webview2.ps1
# 产物：Microsoft.Web.WebView2.Core.dll + WebView2Loader.dll
$ErrorActionPreference = 'Stop'

$version = '1.0.864.35'          # 需要换版本改这里
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$tmp  = Join-Path $env:TEMP ('wv2-' + [guid]::NewGuid().ToString('N'))

New-Item -ItemType Directory -Path $tmp -Force | Out-Null
try {
    $pkg = Join-Path $tmp 'wv2.zip'
    $url = "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/$version"
    Write-Host "download $url"
    Invoke-WebRequest -Uri $url -OutFile $pkg -UseBasicParsing
    Expand-Archive -Path $pkg -DestinationPath $tmp -Force

    $core   = Join-Path $tmp 'lib\netstandard2.0\Microsoft.Web.WebView2.Core.dll'
    $loader = Join-Path $tmp 'runtimes\win-x64\native\WebView2Loader.dll'

    if (-not (Test-Path $core))   { throw "not found: $core" }
    if (-not (Test-Path $loader)) { throw "not found: $loader" }

    Copy-Item $core   (Join-Path $here 'Microsoft.Web.WebView2.Core.dll') -Force
    Copy-Item $loader (Join-Path $here 'WebView2Loader.dll') -Force
    Write-Host "OK -> $here"
}
finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
