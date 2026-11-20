# 生成 Steam 创意工坊发布包（content + VDF），默认不上传。
# 用法（在游戏根目录）：
#   pwsh -File ModDev\PureAcidAlkali\publish\publish.ps1
#   pwsh -File ModDev\PureAcidAlkali\publish\publish.ps1 -Visibility 0
#   pwsh -File ModDev\PureAcidAlkali\publish\publish.ps1 -Run -SteamAccount <账号>
# 说明：-Run 会调用 steamcmd（需要你现场输入密码 / Steam Guard）；不加 -Run 只生成文件并打印命令。
param(
    [ValidateSet(0, 1, 2)][int]$Visibility = 2,
    [switch]$Run,
    [string]$SteamAccount = '',
    [string]$SteamCmd = '',
    [string]$GameDir = ''
)

$ErrorActionPreference = 'Stop'
$here    = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Split-Path -Parent $here
$content  = Join-Path $here 'content'
$projectFile = Join-Path $project 'PureAcidAlkali.csproj'

# 游戏目录：-GameDir / 环境变量 PROBABLY_STOLEN_DIR / 仓库仍在游戏目录内的开发布局。
if (-not $GameDir) { $GameDir = $env:PROBABLY_STOLEN_DIR }
if (-not $GameDir -and (Test-Path (Join-Path $project '..\..\Probably Stolen_Data\Managed\Assembly-CSharp.dll'))) {
    $GameDir = Join-Path $project '..\..'
}
if (-not $GameDir) {
    throw '找不到游戏目录：请传 -GameDir "D:\...\Probably Stolen"，或设置环境变量 PROBABLY_STOLEN_DIR。'
}
$gameRoot = (Resolve-Path $GameDir).Path

function Quote([string]$s) { return '"' + $s + '"' }

function Esc([string]$s) {
    $s = $s -replace '\\', '\\'
    $s = $s -replace '"', '\"'
    $s = $s -replace "`r`n", "`n"
    $s = $s -replace "`n", '\n'
    return $s
}

# 1. Release 构建
Write-Host '构建 Release ...'
dotnet build $projectFile -c Release -nologo "-p:GameDir=$gameRoot" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'dotnet build 失败' }

# 2. 重同步 content（只动 publish\content）
Write-Host '同步 content ...'
if (Test-Path $content) { Get-ChildItem $content -Force | Remove-Item -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $content 'Localization') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $content 'Textures\Items') | Out-Null
Copy-Item (Join-Path $project 'bin\Release\PureAcidAlkali.dll') $content -Force
Copy-Item (Join-Path $project 'manifest.xml') $content -Force
Copy-Item (Join-Path $project 'Localization\*.csv') (Join-Path $content 'Localization') -Force
Copy-Item (Join-Path $project 'Textures\Items\*.png') (Join-Path $content 'Textures\Items') -Force
$preview = Join-Path $here 'preview.jpg'
if (-not (Test-Path $preview)) { $preview = Join-Path $here 'preview.png' }
$fallback = Join-Path $here 'preview.png'
if (Test-Path $fallback) { Copy-Item $fallback $content -Force }  # 模组菜单兜底图，必须叫 preview.png

# 3. 校验
if (-not (Test-Path (Join-Path $content 'manifest.xml'))) { throw 'content 缺 manifest.xml' }
$badFiles = Get-ChildItem $content -Recurse -File | Where-Object { $_.Extension -in '.pdb', '.cs', '.json' }
if ($badFiles) { throw ('content 里混入了不该上传的文件: ' + (($badFiles | ForEach-Object FullName) -join ', ')) }
$dlls = Get-ChildItem $content -Recurse -File -Filter *.dll
if ($dlls.Count -ne 1) { throw "content 里应只有 1 个 DLL，实际 $($dlls.Count) 个" }
if (-not (Test-Path $preview)) { throw '缺 preview.jpg / preview.png（< 1 MB 的工坊预览图）' }
if ((Get-Item $preview).Length -ge 1MB) { throw ('预览图超过 1 MB: ' + (Split-Path -Leaf $preview)) }
$manifestXml = [xml](Get-Content (Join-Path $content 'manifest.xml') -Raw)
$title = $manifestXml.Manifest.Name
if (-not $title) { throw 'manifest.xml 缺 <Name>' }

$fidFile = Join-Path $here 'publishedfileid.txt'
$fid = if (Test-Path $fidFile) { (Get-Content $fidFile -Raw).Trim() } else { '0' }
if ($fid -notmatch '^\d+$') { throw "publishedfileid.txt 内容不是数字: $fid" }

$changenoteFile = Join-Path $here 'changenote.txt'
$changenote = if (Test-Path $changenoteFile) { (Get-Content $changenoteFile -Raw).Trim() } else { '首次发布：净化机 + 5 张可复用工艺芯片。' }
$descriptionFile = Join-Path $here 'description.txt'
if (-not (Test-Path $descriptionFile)) { throw '缺 description.txt' }
$description = (Get-Content $descriptionFile -Raw).Trim()

# 4. 生成 VDF（绝对路径、反斜杠转义）
$vdfPath = Join-Path $here 'publish.generated.vdf'
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('"workshopitem"')
$lines.Add('{')
$lines.Add(('    "appid"            {0}' -f (Quote '4348910')))
$lines.Add(('    "publishedfileid"  {0}' -f (Quote $fid)))
$lines.Add(('    "contentfolder"    {0}' -f (Quote (Esc $content))))
$lines.Add(('    "previewfile"      {0}' -f (Quote (Esc $preview))))
$lines.Add(('    "visibility"       {0}' -f (Quote "$Visibility")))
$lines.Add(('    "title"            {0}' -f (Quote (Esc $title))))
$lines.Add(('    "description"      {0}' -f (Quote (Esc $description))))
$lines.Add(('    "changenote"       {0}' -f (Quote (Esc $changenote))))
$lines.Add('}')
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($vdfPath, ($lines -join "`r`n") + "`r`n", $utf8)

Write-Host ''
Write-Host "content : $content" -ForegroundColor Green
Write-Host "VDF     : $vdfPath" -ForegroundColor Green
Write-Host ("visibility = {0}  (0 公开 / 1 仅好友 / 2 隐藏)" -f $Visibility)
Write-Host ("publishedfileid = {0}" -f $fid)
Write-Host ''
Write-Host '上传命令（自己执行；会要求 Steam 密码 / Steam Guard）：' -ForegroundColor Yellow
Write-Host ('  steamcmd +login <你的Steam账号> +workshop_build_item "' + $vdfPath + '" +quit')
Write-Host ''

if (-not $Run) {
    Write-Host '当前为“只生成”模式，没有上传。' -ForegroundColor DarkGray
    return
}

if (-not $SteamAccount) { throw '-Run 需要 -SteamAccount <账号>（密码由 steamcmd 现场询问）' }
if (-not $SteamCmd) {
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
    $SteamCmd = $candidates | Select-Object -First 1
}
if (-not $SteamCmd) {
    throw '找不到 steamcmd.exe：请先安装（https://developer.valvesoftware.com/wiki/SteamCMD），或用 -SteamCmd 指定路径'
}

Write-Host "调用: $SteamCmd +login $SteamAccount +workshop_build_item ..." -ForegroundColor Cyan
& $SteamCmd '+login' $SteamAccount '+workshop_build_item' $vdfPath '+quit'
if ($LASTEXITCODE -ne 0) { throw "steamcmd 退出码 $LASTEXITCODE" }
Write-Host '如果上传成功：把 steamcmd 打印的物品 ID 写进 publishedfileid.txt，以后更新用同一个 VDF。' -ForegroundColor Green
