<#
.SYNOPSIS
    把仓库里占位的 OWNER/REPO 换成你自己的 GitHub 路径。
.EXAMPLE
    ./tools/set-repo.ps1 -Owner yourname -Repo hero-grid-board
#>
param(
    [Parameter(Mandatory = $true)][string]$Owner,
    [Parameter(Mandatory = $true)][string]$Repo
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$targets = @(
    (Join-Path $root 'README.md'),
    (Join-Path $root '.github\ISSUE_TEMPLATE\config.yml')
)

foreach ($f in $targets) {
    if (-not (Test-Path $f)) { continue }
    $t = [IO.File]::ReadAllText($f)
    $t = $t.Replace('OWNER/REPO', "$Owner/$Repo")
    [IO.File]::WriteAllText($f, $t, (New-Object Text.UTF8Encoding $false))
    Write-Host "updated $f"
}
Write-Host "done -> https://github.com/$Owner/$Repo" -ForegroundColor Green
