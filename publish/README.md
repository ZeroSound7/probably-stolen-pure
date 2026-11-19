# Steam 创意工坊发布包 · 生成与上传说明（不上传）

> 本目录只负责“把 Mod 变成可上传工坊的东西”。游戏内没有上传按钮，官方社区指南给的通道是
> **steamcmd + VDF**（需要登录一个拥有本游戏的 Steam 账号）。
> 实际执行上传的人是你；脚本默认只生成文件并打印命令。

## 目录

| 路径 | 作用 |
|---|---|
| `content/` | 上传内容根目录（根目录必须有 `manifest.xml`），由 `publish.ps1` 从 Release 构建同步 |
| `description.txt` | 工坊描述（Steam BBCode，中英双语）——改文案改这里 |
| `preview.png` | 工坊预览图（< 1 MB）；有它就自动复制进 `content/` 作为模组菜单兜底图 |
| `publish.generated.vdf` | `publish.ps1` 生成的物品描述文件（绝对路径已填好） |
| `publishedfileid.txt` | 首次上传前是 `0`；上传成功后把 steamcmd 打印的物品 ID 写进来，以后更新用它 |
| `changenote.txt` | 可选，更新说明；不写就用“首次发布” |

## 当前状态

- 已用 steamcmd 首次上传（隐藏）：物品 ID **3830306774**，`https://steamcommunity.com/sharedfiles/filedetails/?id=3830306774`
- `publishedfileid.txt` 已记录该 ID；以后更新都用同一个 VDF（`publish.ps1` 会自动带上）
- 待办：本机订阅验证 → `publish.ps1 -Visibility 0` 转公开

## 首次发布（建议先隐藏再公开）

1. **装 steamcmd**：<https://developer.valvesoftware.com/wiki/SteamCMD>（Windows 下载 zip 解压即可）。
2. **生成发布包**（默认隐藏 `visibility=2`）：

   ```powershell
   pwsh -File ModDev\PureAcidAlkali\publish\publish.ps1
   ```

   脚本会：Release 构建 → 重同步 `content/` → 校验 DLL/PDB/预览图 → 生成 `publish.generated.vdf` → 打印上传命令。
3. **先自己登录一次**（会要求密码 + Steam Guard，不要把密码写进任何文件）：

   ```text
   steamcmd +login <你的Steam账号>
   ```

   首次发布还要按提示在 Steam 网页上接受一次**创意工坊法律协议**。
4. **上传隐藏物品**：

   ```text
   steamcmd +login <你的Steam账号> +workshop_build_item "D:\...\publish\publish.generated.vdf" +quit
   ```

   成功后会打印新物品 ID，并写回 VDF。把 ID 写进 `publishedfileid.txt`，**这个文件要保留**。
5. **本机验证工坊加载路径**：
   - 把 `Mods\PureAcidAlkali` 暂时挪出 `Mods`（同 ID 的本地副本会覆盖工坊副本）；
   - 用作者账号打开 `https://steamcommunity.com/sharedfiles/filedetails/?id=<ID>` 订阅隐藏物品；
   - 重启游戏 → 模组菜单应出现带 Steam 图标的条目 + 预览图 → 审核并启用 → 重启 → 进游戏验证机器/芯片/便签/商店。
6. **转公开**：

   ```powershell
   pwsh -File ModDev\PureAcidAlkali\publish\publish.ps1 -Visibility 0
   ```

   再跑一次第 4 步的 `workshop_build_item`（`publishedfileid` 已不是 0，即更新）。

## 以后更新

1. 改 `manifest.xml` 的 `<ModVersion>`（以及需要时的 `description.txt` / `changenote.txt`）；
2. `pwsh -File ModDev\PureAcidAlkali\publish\publish.ps1`（默认隐藏不合适，更新用 `-Visibility 0`）；
3. 跑一次 `workshop_build_item` 同一个 VDF。订阅者会自动收到新版本。

> 信任机制：同一工坊物品、同一发布者、文件哈希变化 → 游戏判为 `Updated` 并**自动加载**，玩家不需要重新审核；
> 只有新增能力声明（`System.IO`/网络等）才会要求重新审核。本 Mod 的 `<Capabilities />` 保持空。

## 预览图

- 底图：`ModDev/Tools/workshop_art.py` 调本地 ComfyUI（AIpic 的 Anima 模型，端口 8288）生成，输出在 `publish/art/`；候选排版在 `publish/candidates/`。
- 合成：`python ModDev/Tools/compose_preview.py --final publish/art/<底图>.png` → `publish/preview.jpg`（上传用，<1MB）+ `publish/preview.png`（模组菜单兜底图，会被复制进 `content/`）。
- 当前默认用的是 `d_scientist.png`（实验室少女 + 像素机器贴图）。换图：重跑 `--final` 指定另一张底图，再跑一次 `publish.ps1`。
- ComfyUI 手动启动：`cd D:\UseTools\AIpic\ComfyUI-aki-v3\ComfyUI` 后 `..\python\python.exe main.py --port 8288`。

## 上传前检查清单

- [ ] `content/` 里只有 `manifest.xml`、`PureAcidAlkali.dll`、`Localization/`、`Textures/`（以及可选的 `preview.png`）
- [ ] 没有 `.pdb`、`.cs`、`bin/`、`obj/`
- [ ] 构建是 Release，不是 `PURE_FACTORY_SMOKE` 测试构建
- [ ] `manifest.xml` 的 `<ID>` 没变、`<GameVersions>` 是 `0.46D`、`<Capabilities />` 为空
- [ ] `preview.png` < 1 MB，且标题/描述与 Mod 实际内容一致
- [ ] 上传账号拥有本游戏；密码/Steam Guard 不落盘、不发给别人
- [ ] 隐藏物品测过：订阅 → 模组菜单能看到预览图 → 审核并启用 → 重启后机器可用

## 注意

- 工坊物品归上传账号所有，以后更新也必须用同一账号；公开后作者 Steam 资料可见。
- 下架/删除物品会让订阅者的文件消失，描述里已提醒玩家先清理存档里的机器和芯片。
- 上传时关闭游戏，避免文件占用。
- SteamCMD 走 Steam CDN，国内网络可能较慢；失败重试即可。
