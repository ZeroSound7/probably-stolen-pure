using System;
using System.Collections.Generic;
using UnityEngine;

namespace PureAcidAlkali
{
    /// <summary>
    /// 一台通用「纯溶液净化机」+ 4 张可重复使用的工艺芯片：
    /// 芯片决定“保留哪种液体”，其余液体（杂质）按额度去除并等量转成目标液体。
    /// N-羟基甲碱芯片额外保留 stabilizing_agent（稳定剂是正常流程的一部分，不能当杂质去掉）。
    /// </summary>
    public static class SolutionPurifier
    {
        public const string MachineItemId = "pure_solution_purifier";

        public const string ChipTag = "PURE_PURIFIER_CHIP_TAG";
        private const string ChipTargetTag = "PURE_PURIFIER_CHIP_TARGET";
        private const string ChipExtraKeepTag = "PURE_PURIFIER_CHIP_EXTRA_KEEP";

        private const string MachineTag = "PURE_SOLUTION_PURIFIER_TAG";
        private const string LastRemovedTag = "PURE_PURIFIER_LAST_REMOVED_UL";

        /// <summary>基础耗电 8；效率芯片按游戏公式降低（+16% → 8→7）。</summary>
        private const int BasePowerUsage = 8;

        private const long MachineUnitValue = 300;
        private const long ChipUnitValue = 100;

        public sealed class ChipInfo
        {
            public string Id;
            public string TargetLiquid;
            public string KeepExtraLiquid;
            public string Sprite;
            public string NameKey;
            public string DescKey;
        }

        public static readonly ChipInfo[] Chips =
        {
            new ChipInfo
            {
                Id = "pure_chip_acid",
                TargetLiquid = Liquid.LIQUID_ACID,
                Sprite = "printer_chip2",
                NameKey = "chip_acid_name",
                DescKey = "chip_acid_desc",
            },
            new ChipInfo
            {
                Id = "pure_chip_base",
                TargetLiquid = Liquid.LIQUID_BASE,
                Sprite = "printer_chip3",
                NameKey = "chip_base_name",
                DescKey = "chip_base_desc",
            },
            new ChipInfo
            {
                Id = "pure_chip_lipobond",
                TargetLiquid = Liquid.LIQUID_LIPOBOND_AGENT,
                Sprite = "printer_chip4",
                NameKey = "chip_lipobond_name",
                DescKey = "chip_lipobond_desc",
            },
            new ChipInfo
            {
                Id = "pure_chip_methylol",
                TargetLiquid = Liquid.LIQUID_METHYLOL,
                KeepExtraLiquid = Liquid.LIQUID_STABILIZING_AGENT,
                Sprite = "printer_chip6",
                NameKey = "chip_methylol_name",
                DescKey = "chip_methylol_desc",
            },
            new ChipInfo
            {
                Id = "pure_chip_reducing",
                TargetLiquid = Liquid.LIQUID_REDUCING_AGENT,
                Sprite = "printer_chip_pistol",
                NameKey = "chip_reducing_name",
                DescKey = "chip_reducing_desc",
            },
        };

        public static GameItem CreateMachine()
        {
            GameSlotInventory batterySlot = new GameSlotInventory();
            GameGridInventory moduleGrid = new GameGridInventory(MachineHelper.DEFAULT_MODULE_BAY_CONFIG, MachineHelper.DEFAULT_MODULE_BAY_WIDTH);
            GameSlotInventory liquidSlot = new GameSlotInventory();
            GameSlotInventory chipSlot = new GameSlotInventory();
            GameSlotInventory noteSlot = new GameSlotInventory();

            // 原版机器的窗口：电源 / 模组 / 溶液容器 / 工艺芯片 / 右侧小纸条。
            PixelWindow window = MachineHelper.CreateMachineryWindow(
                true,
                MachineHelper.PowerPanel(batterySlot),
                MachineHelper.Panel(L("label_module"), moduleGrid, "module"),
                MachineHelper.Panel(L("label_liquid"), liquidSlot, "liquid"),
                MachineHelper.Panel(L("label_chip"), chipSlot, "chip"),
                MachineHelper.NotePanel(noteSlot));

            GameItem machine = ItemDirectory.CreateEmptyItem()
                .SetContentWindow(window)
                .SetSpriteAndShapeFromMod(ModEntry.ModId, "pure_solution_purifier");

            machine.name = L("machine_name");
            machine.shortDescription = L("machine_desc");
            machine.flavorText = L("machine_flavor");
            machine.unitValue = MachineUnitValue;
            machine.SetGameItemType(TypeHelper.MACHINE);
            machine.EnableTag(ContainerHelper.CONTAINER_TAG);
            machine.EnableTag("manufacture");
            machine.EnableTag(MachineHelper.STANDARD_MACHINE_TAG);
            machine.EnableTag(MachineTag);

            MachineHelper.SetupBatterySlot(batterySlot, machine);
            MachineryHelper.InitMachinery(machine, BasePowerUsage);

            ContainerHelper.AllowOnlyTaggedItemsOr(moduleGrid, new string[1] { ModuleHelper.MODULE_TYPE_UNIVERSAL }, true);
            moduleGrid.onSlotAddItemEndFunc = delegate(GameItem addedItem, GameInventory parentInventory, SlotMarker slot)
            {
                ModuleEffectHelper.OnAddedModuleToGrid(addedItem, machine, moduleGrid.childItems);
                if (addedItem.IsTag(ModuleHelper.MODULE_TAG))
                {
                    ModuleHelper.ComputeModuleEffect(moduleGrid, addedItem, machine);
                    ModuleHelper.ApplyBasicModuleEffect(moduleGrid, addedItem, machine);
                }
                MachineryHelper.UpdatePowerUsageProcessTypeMachine(machine);
            };
            moduleGrid.onSlotRemoveItemEndFunc = delegate(GameItem removedItem, GameInventory parentInventory, SlotMarker slot)
            {
                ModuleEffectHelper.OnRemovedModuleFromGrid(removedItem, machine, moduleGrid.childItems);
                if (removedItem.IsTag(ModuleHelper.MODULE_TAG))
                {
                    ModuleHelper.ComputeModuleEffect(moduleGrid, removedItem, machine);
                    ModuleHelper.ApplyBasicModuleEffect(moduleGrid, removedItem, machine);
                }
                MachineryHelper.UpdatePowerUsageProcessTypeMachine(machine);
            };

            liquidSlot.mayInventoryAddItemFunc = delegate(GameItem item, GameInventory inventory)
            {
                return GeneralHelper.IsItemOwned(item) && item.IsTag(LiquidContainerHelper.LIQUID_CONTAINER_TAG);
            };
            liquidSlot.SetBackgroundFadeSprite("Items/water_container4", "xl");

            ContainerHelper.AllowOnlyTaggedItem(chipSlot, ChipTag, ownedOnly: true);
            chipSlot.SetBackgroundFadeSprite("Items/items_tool", "printer_chip_pistol");

            noteSlot.mayInventoryAddItemFunc = delegate(GameItem item, GameInventory inventory)
            {
                return item.identifier.Equals("postit");
            };
            noteSlot.SetBackgroundFadeSprite("Items/items_tool", "postit");

            machine.onCycleEndSlotItemFunc = delegate(GameItem item, GameInventory parentInventory, SlotMarker slot)
            {
                int removed = PurifyTonight(machine, batterySlot, moduleGrid, liquidSlot, chipSlot);
                item.ModifyTag(LastRemovedTag, delegate(TagState state)
                {
                    state.Enable().SetInt(removed);
                });
            };

            return machine;
        }

        /// <summary>
        /// 供商店上架用：机器先按目录工厂造好，再挂上说明书便签。
        /// 不能把便签放在工厂里——DirectoryMaster.Item 造完物品会清掉所有子物品
        /// （"Removing all child items as a fix..."），原版机器也是在上架前才挂便签的。
        /// </summary>
        public static GameItem CreateMachineWithNote()
        {
            // 必须走 DirectoryMaster.Item：它会设置 identifier / uniqueId / 归属，
            // 工厂直出的物品 identifier 为 null，上架后会导致遍历物品时报 NullReference。
            GameItem machine = DirectoryMaster.Item(MachineItemId);
            AttachNote(machine);
            return machine;
        }

        /// <summary>机器是否已经有说明书便签（便签槽是第 4 个存储）。</summary>
        public static bool HasNote(GameItem machine)
        {
            GameSlotInventory noteSlot = MachineHelper.GetBodyStorage(machine, 4) as GameSlotInventory;
            return noteSlot != null && noteSlot.childItem != null;
        }

        /// <summary>给机器挂说明书便签；已经有就跳过。读档修复旧机器也用它。</summary>
        public static bool AttachNote(GameItem machine)
        {
            if (machine == null || HasNote(machine))
            {
                return false;
            }
            GameItem note = CreateNote();
            if (GraphUtils.TryAcceptAll(machine, note) <= 0)
            {
                return true;
            }
            GameSlotInventory noteSlot = MachineHelper.GetBodyStorage(machine, 4) as GameSlotInventory;
            if (noteSlot != null)
            {
                noteSlot.UncheckedAccept(note);
                return true;
            }
            note.Destroy();
            return false;
        }

        public static GameItem CreateChip(ChipInfo info)
        {
            GameItem chip = ItemDirectory.CreateEmptyItem()
                .SetSpriteAndShape("Items/items_tool", info.Sprite);
            chip.name = L(info.NameKey);
            chip.shortDescription = L(info.DescKey);
            chip.flavorText = "";
            chip.unitValue = ChipUnitValue;
            chip.EnableTag(ChipTag);
            chip.ModifyTag(ChipTargetTag, delegate(TagState state)
            {
                state.Enable().SetString(info.TargetLiquid);
            });
            if (!string.IsNullOrEmpty(info.KeepExtraLiquid))
            {
                chip.ModifyTag(ChipExtraKeepTag, delegate(TagState state)
                {
                    state.Enable().SetString(info.KeepExtraLiquid);
                });
            }
            return chip;
        }

        private static GameItem CreateNote()
        {
            GameItem note = DirectoryMaster.Item("postit");
            note.name = L("note_name");
            note.shortDescription = L("note_body");
            note.flavorText = "";
            note.SetGameItemType("DOCUMENT");
            note.EnableTag(GeneralHelper.IMPORTANT_TAG);
            note.unitValue = 0;
            return note;
        }

        /// <summary>夜间净化一次，返回本次去除的杂质量（µL）。0 表示没工作（不扣电）。</summary>
        private static int PurifyTonight(GameItem machine, GameSlotInventory batterySlot, GameGridInventory moduleGrid, GameSlotInventory liquidSlot, GameSlotInventory chipSlot)
        {
            if (liquidSlot.childItem == null || chipSlot.childItem == null)
            {
                return 0;
            }
            GameItem chip = chipSlot.childItem;
            if (!chip.IsTag(ChipTag))
            {
                return 0;
            }
            GameItem liquid = liquidSlot.childItem;
            if (WaterHelper.IsEmpty(liquid))
            {
                return 0;
            }

            string targetLiquid = chip.GetTagReadonly(ChipTargetTag).GetString();
            string extraKeep = chip.IsTag(ChipExtraKeepTag) ? chip.GetTagReadonly(ChipExtraKeepTag).GetString() : null;
            int totalVolume = WaterHelper.GetTotalVolume(liquid);
            int targetAmount = liquid.GetTagReadonly(Liquid.GetCurrentPartFromId(targetLiquid)).GetInt();
            if (totalVolume <= 0 || targetAmount <= 0)
            {
                return 0;
            }

            // 纯度按“保留集合”计算：普通试剂只算目标液体；N-羟基甲碱还要算上稳定剂。
            int keptAmount = targetAmount;
            if (!string.IsNullOrEmpty(extraKeep))
            {
                keptAmount += liquid.GetTagReadonly(Liquid.GetCurrentPartFromId(extraKeep)).GetInt();
            }
            if (keptAmount <= 0)
            {
                return 0;
            }

            int qualityBonus = MachineryHelper.GetCurrentQualityBonus(machine);
            int purityCap = PurifierMath.PurityCapPermyriad(qualityBonus);
            long targetKeptAtEnd = ((long)purityCap * totalVolume + 9999L) / 10000L;
            long neededRemoval = targetKeptAtEnd - keptAmount;
            if (neededRemoval <= 0)
            {
                return 0;
            }
            long impurityAmount = totalVolume - keptAmount;
            if (impurityAmount <= 0)
            {
                return 0;
            }

            long budget = PurifierMath.RemovalBudgetUl(
                MachineryHelper.GetCurrentPerformanceBonus(machine));
            long remove = Math.Min(Math.Min(neededRemoval, impurityAmount), budget);
            if (remove <= 0)
            {
                return 0;
            }

            // 只有真的去除了杂质才扣整次耗电（和净水器一致）。
            int powerUsage = MachineryHelper.GetMachinePowerUsage(machine);
            if (batterySlot.childItem == null || !PowerHelper.CanDrawPowerSource(batterySlot.childItem, powerUsage))
            {
                return 0;
            }
            PowerHelper.DrawPowerSource(batterySlot.childItem, powerUsage);

            RemoveNonKept(liquid, targetLiquid, extraKeep, (int)remove);
            WaterHelper.AddLiquid(liquid, targetLiquid, (int)remove);
            MachineHelper.OnMachineActioned(machine, moduleGrid);
            return (int)remove;
        }

        /// <summary>按比例扣掉保留集合之外的所有液体。</summary>
        private static void RemoveNonKept(GameItem liquid, string targetLiquid, string extraKeep, int amount)
        {
            List<Liquid> candidates = new List<Liquid>();
            long totalImpurity = 0L;
            foreach (Liquid candidate in Liquid.Liquids)
            {
                if (candidate.identifier == targetLiquid || candidate.identifier == extraKeep)
                {
                    continue;
                }
                int partAmount = liquid.GetTagReadonly(candidate.currentPart).GetInt();
                if (partAmount > 0)
                {
                    candidates.Add(candidate);
                    totalImpurity += partAmount;
                }
            }
            if (totalImpurity <= 0)
            {
                return;
            }

            int remaining = amount;
            for (int i = 0; i < candidates.Count && remaining > 0; i++)
            {
                Liquid candidate = candidates[i];
                int partAmount = liquid.GetTagReadonly(candidate.currentPart).GetInt();
                int take;
                if (i == candidates.Count - 1)
                {
                    take = Math.Min(remaining, partAmount);
                }
                else
                {
                    take = (int)Math.Min(partAmount, (long)partAmount * amount / totalImpurity);
                }
                if (take <= 0)
                {
                    continue;
                }
                remaining -= take;
                int left = partAmount - take;
                liquid.ModifyTag(candidate.currentPart, delegate(TagState state)
                {
                    state.Enable().SetInt(left);
                }, skipValidate: true);
            }
        }

        public static void AppendTooltip(RichTextBuilder builder, GameItem item)
        {
            if (item == null || builder == null)
            {
                return;
            }
            if (item.IsTag(ChipTag))
            {
                builder.AddLine(L("chip_tooltip_use"), true, RenderHandler.ColorPalette.LightCyan);
                return;
            }
            if (!item.IsTag(MachineTag))
            {
                return;
            }

            int qualityBonus = MachineryHelper.GetCurrentQualityBonus(item);
            int purityCap = PurifierMath.PurityCapPermyriad(qualityBonus);
            long budget = PurifierMath.RemovalBudgetUl(MachineryHelper.GetCurrentPerformanceBonus(item));

            GameItem chip = GetChip(item);
            builder.AddLine(
                chip == null ? L("tooltip_no_chip") : L("tooltip_chip", chip.name),
                true,
                RenderHandler.ColorPalette.LightCyan);
            builder.AddLine(L("tooltip_cap", PurifierMath.FormatPercent(purityCap)), true, RenderHandler.ColorPalette.LightCyan);
            builder.AddLine(L("tooltip_budget", (budget / 1000.0).ToString("0.0")), true, RenderHandler.ColorPalette.LightCyan);
            int lastRemoved = item.GetTagReadonly(LastRemovedTag).GetInt();
            if (lastRemoved > 0)
            {
                builder.AddLine(L("tooltip_removed", (lastRemoved / 1000.0).ToString("0.0")), true, RenderHandler.ColorPalette.LightGreen);
            }
            builder.AddLine(L("tooltip_hint"), true);
        }

        private static GameItem GetChip(GameItem machine)
        {
            GameSlotInventory chipSlot = MachineHelper.GetBodyStorage(machine, 3) as GameSlotInventory;
            return chipSlot != null ? chipSlot.childItem : null;
        }

        private static string L(string key, params object[] args)
        {
            return ModHelper.GetLocalized(ModEntry.ModId, key, args);
        }
    }
}
