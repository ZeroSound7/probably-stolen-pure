# 构建并部署「可能是纯的」到游戏的 Mods 目录。
# 用法：
#   pwsh -File .\deploy.ps1                                  # 自动找游戏目录（环境变量 / 仓库在游戏目录内）
#   pwsh -File .\deploy.ps1 -GameDir "D:\...\Probably Stolen" # 或显式指定
[CmdletBinding()]
param([string]$GameDir)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = $here

if (-not $GameDir) { $GameDir = $env:PROBABLY_STOLEN_DIR }
if (-not $GameDir -and (Test-Path (Join-Path $here '..\..\Probably Stolen_Data\Managed\Assembly-CSharp.dll'))) {
    $GameDir = Join-Path $here '..\..'
}
if (-not $GameDir) {
    throw '找不到游戏目录：请传 -GameDir "D:\...\Probably Stolen"，或设置环境变量 PROBABLY_STOLEN_DIR。'
}
$gameRoot = (Resolve-Path $GameDir).Path
$target = Join-Path $gameRoot 'Mods\PureAcidAlkali'

Write-Host "构建: $here"
dotnet build (Join-Path $source 'PureAcidAlkali.csproj') -c Release -nologo "-p:GameDir=$gameRoot" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'dotnet build 失败' }

New-Item -ItemType Directory -Force -Path $target | Out-Null
# 结构变化时清掉旧版本的残留文件（旧机器贴图/旧本地化条目等）。
Get-ChildItem $target -Force | Remove-Item -Recurse -Force
New-Item -ItemType Directory -Force -Path (Join-Path $target 'Localization') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $target 'Textures\Items') | Out-Null

Copy-Item (Join-Path $source 'bin\Release\PureAcidAlkali.dll') $target -Force
Copy-Item (Join-Path $source 'manifest.xml') $target -Force
Copy-Item (Join-Path $source 'Localization\*.csv') (Join-Path $target 'Localization') -Force
Copy-Item (Join-Path $source 'Textures\Items\*.png') (Join-Path $target 'Textures\Items') -Force

Write-Host "已安装到: $target"
Get-ChildItem $target -Recurse -File | ForEach-Object { Write-Host "  $($_.FullName.Substring($target.Length + 1))  $($_.Length) bytes" }
