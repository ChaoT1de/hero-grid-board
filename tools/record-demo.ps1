<#
.SYNOPSIS
    录一小段演示（MP4 + GIF），用于 README。

.DESCRIPTION
    需要 ffmpeg：  winget install --id Gyan.FFmpeg
    录制前先把「拼图板」窗口摆好（建议 1280x800 以内），然后运行：

        ./tools/record-demo.ps1 -X 100 -Y 80 -W 1200 -H 760 -Seconds 12

    会生成 docs/demo.mp4 和 docs/demo.gif。
    X/Y 是窗口左上角在屏幕上的坐标；不确定的话用 Win+Shift+S 的截图框看一下。

.NOTES
    只录你指定的矩形区域，不会录到别的内容。录完记得看一眼 GIF 里有没有隐私信息。
#>
param(
    [int]$X = 100,
    [int]$Y = 80,
    [int]$W = 1200,
    [int]$H = 760,
    [int]$Seconds = 12,
    [string]$OutDir = "docs"
)

$ErrorActionPreference = 'Stop'
$ff = Get-Command ffmpeg -ErrorAction SilentlyContinue
if (-not $ff) {
    Write-Host "没找到 ffmpeg。先装：" -ForegroundColor Yellow
    Write-Host "  winget install --id Gyan.FFmpeg" -ForegroundColor Yellow
    exit 1
}

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $here
$outDir = Join-Path $root $OutDir
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$mp4 = Join-Path $outDir 'demo.mp4'
$gif = Join-Path $outDir 'demo.gif'

Write-Host "3 秒后开始录（$W x $H，共 $Seconds 秒）……" -ForegroundColor Cyan
Start-Sleep -Seconds 3

& ffmpeg -y -hide_banner -loglevel warning `
    -f gdigrab -framerate 15 `
    -offset_x $X -offset_y $Y -video_size "${W}x${H}" `
    -i desktop -t $Seconds -pix_fmt yuv420p $mp4
if ($LASTEXITCODE -ne 0) { throw "录制失败" }

& ffmpeg -y -hide_banner -loglevel warning -i $mp4 `
    -vf "fps=12,scale=960:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse" `
    -loop 0 $gif
if ($LASTEXITCODE -ne 0) { throw "转 GIF 失败" }

Write-Host "OK：" $mp4 -ForegroundColor Green
Write-Host "OK：" $gif -ForegroundColor Green
Write-Host "把 docs/demo.gif 贴进 README 即可。" -ForegroundColor Green
