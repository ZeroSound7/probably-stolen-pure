# 可能是纯的（PureAcidAlkali）· 开发说明

> **作用域**：只适用于 `ModDev/PureAcidAlkali/` 里的代码。装/用说明看同目录 `README.md`；跨会话进度看 `../PROGRESS.md`；游戏 API 与生态资料看 `../Research/RESEARCH.md`。
> 本文件是"改这个 Mod 之前必须知道的东西"，不是用户文档；不要在这里重复 README 的安装步骤。

## 1. 代码地图（谁负责什么）

| 文件 | 负责 | 动它之前注意 |
|---|---|---|
| `src/ModEntry.cs` | `IMod` 入口：注册物品、发明家货单补货、提示框钩子、旧机器补字条；`#if PURE_FACTORY_SMOKE` 测试代码 | 改货单概率只动 `MachineRestockChancePercent`；**不要改物品 ID**（存档兼容） |
| `src/SolutionPurifier.cs` | 机器窗口/槽位、5 张芯片、夜间提纯逻辑、便签生成 | 槽位顺序 = 存档顺序，面板顺序不能乱；字条不能放在工厂里（见坑） |
| `src/PurifierMath.cs` | 纯数值模型：质量→纯度上限、性能→去除量 | 调平衡只改这个文件；改公式后跑一次测试构建 |
| `Localization/zh.csv`、`en.csv` | 所有显示文本（机器/芯片/便签/提示框） | UTF-8 无表头；`key,text` 只按第一个逗号切；`\n` 会转成换行；值不要以英文引号开头 |
| `Textures/Items/pure_solution_purifier.png` | 32×48 机器贴图（16px 网格 → 2×3） | 形状由不透明像素推导；改尺寸必须仍是 16 的倍数 |
| `manifest.xml` | Mod ID / 显示名 / 版本 / 目标游戏版本 / 能力 | `<ID>` 永远不要改；`<GameVersions>` 是 `0.46D`（不是主菜单的 100）；`<Capabilities />` 保持空 |
| `deploy.ps1` | 构建 + **清空** `Mods/PureAcidAlkali` + 复制 | 会删目标目录，别把手工文件放进去；部署后会带 PDB，正式测试前删掉 |
| `../Tools/make_sprites.py` | 生成机器贴图 | 输出到 `Textures/Items/`，改完重新构建部署 |

## 2. 不变量（改代码前先确认不会破坏）

- **物品 ID 固定**：机器 `pure_solution_purifier`，芯片 `pure_chip_acid/base/lipobond/methylol/reducing`。改 ID = 旧存档物品变未知物品。
- **机器/芯片都是可重复使用**：芯片不消耗，不要加"用一次销毁"的逻辑。
- **保留集合**：酸/碱/脂合剂/还原剂只保留目标；N-羟基甲碱保留 `methylol`+`stabilizing_agent`（稳定剂是正常流程的一部分，不能自动去掉）。
- **纯度 = 保留集合总量 / 液体总量**；机器只往上提纯，输入高于上限时不降纯、不工作、不扣电。
- **去掉的杂质等量转成主目标液体**，总体积不变（净水器"杂质→水"的同款原理）。
- **没有状态栏**：机器窗口固定 5 个面板（电源 / 模组 / 溶液容器 / 工艺芯片 / 小纸条），顺序不要改。
- **商店上架的机器必须**：`DirectoryMaster.Item(机器ID)` 造 → 挂便签 → `AddDirectSellingItemToTable(item, isOwend:false)`。
- **能力保持空**：不引入 `System.IO` / 网络 / `AccessTools` 字符串反射 / 自带库；否则扫描器要求声明或被拒。

## 3. 常见任务

| 想做什么 | 改哪里 | 之后做什么 |
|---|---|---|
| 调补货概率 | `ModEntry.MachineRestockChancePercent`（当前 25） | build + deploy；已买物品不受影响 |
| 调纯度上限/去除量 | `PurifierMath.cs` 的常量与两个公式 | 用测试构建跑 `PURIFY[...] SMOKE OK` |
| 加一种新芯片 | `SolutionPurifier.Chips` 加一条（ID / 目标液体 / 可选保留液体 / 贴图 / 文案 key）+ 两份 CSV + README 表格 | 测试构建里 `RunPurifyCase` 加一行，验证目标上升、保留液体不变 |
| 改机器/芯片文案 | 两份 CSV 对应 key | 测试构建看 `LOCALE SMOKE` 与 `CHIP SMOKE`；不要用英文引号开头 |
| 改机器外观 | `make_sprites.py` 重新生成 32×48 PNG | 重新 build + deploy |
| 改 Mod 显示名/版本 | `manifest.xml` 的 `<Name>` / `<ModVersion>` | 只影响 Mod 列表；ID 不动 |
| 调试运行时 | 游戏根目录放空文件 `dev.txt` → F8 控制台；日志在 `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\Player.log` | 用 `[可能是纯的]` / `[ModLoader]` 过滤 |

## 4. 游戏真相来源（别靠猜）

反编译源码在 `../GameSource/Assembly-CSharp/`，本 Mod 主要依赖：

- `ModHook.cs`：所有官方钩子（注册物品、货单、提示框、读档时机）。
- `MachineHelper.cs` / `MachineryHelper.cs` / `ModuleHelper.cs`：机器窗口、槽位、耗电、通用模组加成。
- `MachinePurifier.cs`：净水器参考实现；`MachineChemFinisher.cs`：成型损失与纯度分档。
- `Liquid.cs` / `WaterHelper.cs`：液体 id、体积/纯度、加液接口。
- `DirectoryMaster.cs` / `ItemDirectory.cs` / `GameItem.cs`：物品创建、identifier/uniqueId、贴图形状。
- `PreBuildChemHelper.cs`：酸/碱/脂合剂/甲基甲碱/还原剂瓶子的来源与杂质。

社区资料（URL 与结论在 `../Research/RESEARCH.md`）：`dragonpan2/probably-stolen-modding` 指南与 LearnerMod 示例、EZapi 文档、BWIKI、probablystolenguide。

## 5. 坑（都是踩过的）

- **字条（postit）不能放在目录工厂里**：`DirectoryMaster.Item` 造完物品会清掉所有子物品；正确流程是"造机器 → 挂便签 → 上架"。
- **工厂直出的物品 `identifier` 是 null**：商店上架的物品必须由 `DirectoryMaster.Item` 创建，否则其他系统遍历物品会 NRE。
- **旧机器补字条用 `OnGameLoadedLate`**：`OnGameLoadedNormal` 在存档解码之前，补物品会丢。
- **耗电是整数截断**：基础 4 时 +16% 效率芯片会被截成 0（净水器也这样）；所以基础耗电用 8，8→7 才看得见。
- **`deploy.ps1` 会清空目标目录**；结构变化（删旧贴图/词条）靠这个，不要手工往 `Mods/` 放东西。
- **部署会把 PDB 带过去**：正式版/发布前删掉 `Mods/PureAcidAlkali/*.pdb`（不删也能加载，只是多余）。
- **测试构建会启动游戏并自动载入 4 号存档**：`-p:DefineConstants=PURE_FACTORY_SMOKE` 才编进来，正常构建没有；测试后必须 `local_mod_toggle.py --restore` 恢复设置。
- **不要替用户改 Mod 列表**：除非用户明确要求测试，否则只部署文件、不注入启用记录、不改 `SaveFile.es3`。

## 6. 验证（改完至少做这些）

1. `dotnet build .\ModDev\PureAcidAlkali\PureAcidAlkali.csproj -c Release` — 0 warning 0 error。
2. `pwsh -File .\ModDev\PureAcidAlkali\deploy.ps1` + 删 PDB；确认 `Mods/PureAcidAlkali` 只有 manifest / DLL / Localization / 贴图。
3. 需要跑逻辑验证时：`dotnet build -c Debug -p:DefineConstants=PURE_FACTORY_SMOKE` → 用 `local_mod_toggle.py` 临时启用 → 启动游戏读日志里的 `FACTORY/NOTE/SHOP/PURIFY SMOKE` → 关闭游戏 → `--restore`，并核对 `SaveFile.es3`、`save_4.es3` 哈希与备份一致。
4. 只有 `PURIFY[...] SMOKE OK`、`SHOP CHIP ... count>=1`、`NOTE SMOKE` 都出现，才算这一轮验证通过。
