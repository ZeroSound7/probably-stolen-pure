# 交互式上传到 Steam 创意工坊（会提示输入 Steam 账号，然后由 steamcmd 询问密码 / Steam Guard）。
# 用法（在 PowerShell 里，任意目录）：
#   pwsh -File .\upload.ps1
#   pwsh -File .\upload.ps1 -SteamAccount <账号>
# 上传成功后，把 steamcmd 打印的物品 ID 写进 publishedfileid.txt；以后更新用同一个 VDF。
[CmdletBinding()]
param([string]$SteamAccount = '')

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$vdf = Join-Path $here 'publish.generated.vdf'
if (-not (Test-Path $vdf)) {
    throw '缺 publish.generated.vdf：先运行 publish.ps1 生成发布包（它会先构建）'
}

# 找最近更新过的 steamcmd（自更新过的优先，避免旧副本重下几十 MB）
$wingetSteamCmd = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Directory -Filter "Valve.SteamCMD*" -ErrorAction SilentlyContinue |
    ForEach-Object { Get-ChildItem $_.FullName -Recurse -Filter "steamcmd.exe" -ErrorAction SilentlyContinue } |
    Select-Object -First 1 -ExpandProperty FullName
$candidates = @(
    $env:STEAMCMD,
    'D:\UseTools\steamcmd\steamcmd.exe',
    'C:\steamcmd\steamcmd.exe',
    $wingetSteamCmd,
    (Get-Command steamcmd -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
) | Where-Object { $_ -and (Test-Path $_) }
$steamcmd = $candidates | Sort-Object { (Get-Item $_).LastWriteTime } -Descending | Select-Object -First 1
if (-not $steamcmd) {
    throw '找不到 steamcmd.exe：https://developer.valvesoftware.com/wiki/SteamCMD'
}

if (-not $SteamAccount) {
    $SteamAccount = Read-Host 'Steam 账号名（登录名，不是昵称）'
}
Write-Host "steamcmd : $steamcmd"
Write-Host "VDF      : $vdf"
Write-Host '接下来 steamcmd 会询问密码 / Steam Guard（不会保存密码）。' -ForegroundColor Yellow
& $steamcmd '+login' $SteamAccount '+workshop_build_item' $vdf '+quit'
Write-Host ''
Write-Host '上传成功的话：把 steamcmd 打印的「物品 ID」写进 publish\publishedfileid.txt，' -ForegroundColor Green
Write-Host '以后更新仍是同一个 VDF / 同一个 ID。' -ForegroundColor Green
