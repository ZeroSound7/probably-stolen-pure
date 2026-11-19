using System.Collections.Generic;
using UnityEngine;

namespace PureAcidAlkali
{
    public sealed class ModEntry : IMod
    {
        public const string ModId = "pure_acid_alkali";

        /// <summary>
        /// 发明家货单里“已持有后补货”的机器概率（25，和净水器同档）。
        /// 芯片未拥有时仍然必出；只改概率不影响存档（物品 ID/行为/已有物品都不变）。
        /// </summary>
        private const int MachineRestockChancePercent = 25;

        private ModLog log;

        public void Init(ModManifest manifest)
        {
            log = new ModLog(manifest);
        }

        public void OnEnable()
        {
            ModHook.OnModItemDirectoryInit += RegisterItems;
            ModHook.OnPlaceInventorInventoryItemLate += OnInventorStock;
            ModHook.OnCreateTooltipLate += OnTooltip;
            ModHook.OnGameLoadedLate += RepairMissingNotes;
#if PURE_FACTORY_SMOKE
            if (ModLoader.Instance != null)
            {
                ModLoader.Instance.StartCoroutine(AutoLoadThenSmoke());
            }
            ModHook.OnGameLoadedNormal += FactorySmokeTestOnce;
#endif
        }

        public void OnDisable()
        {
            ModHook.OnModItemDirectoryInit -= RegisterItems;
            ModHook.OnPlaceInventorInventoryItemLate -= OnInventorStock;
            ModHook.OnCreateTooltipLate -= OnTooltip;
            ModHook.OnGameLoadedLate -= RepairMissingNotes;
#if PURE_FACTORY_SMOKE
            ModHook.OnGameLoadedNormal -= FactorySmokeTestOnce;
#endif
        }

        /// <summary>
        /// 读档后给旧版本购买的机器补上说明书便签（只补空槽，不改其他状态）。
        /// 这样已经买过的机器不用重买/重刷。
        /// </summary>
        private void RepairMissingNotes()
        {
            if (PlayerStore.Instance == null)
            {
                return;
            }
            int repaired = 0;
            foreach (GameItem item in PlayerStore.Instance.FindAllItem())
            {
                if (item == null || item.identifier != SolutionPurifier.MachineItemId)
                {
                    continue;
                }
                if (SolutionPurifier.AttachNote(item))
                {
                    repaired++;
                }
            }
            if (repaired > 0)
            {
                log.Log("NOTE REPAIR: added manual note to " + repaired + " old machine(s)");
            }
        }

        private void RegisterItems(ModItemDirectory directory)
        {
            directory.Add(SolutionPurifier.MachineItemId, SolutionPurifier.CreateMachine);
            foreach (SolutionPurifier.ChipInfo chip in SolutionPurifier.Chips)
            {
                SolutionPurifier.ChipInfo info = chip;
                directory.Add(info.Id, () => SolutionPurifier.CreateChip(info));
            }
        }

        /// <summary>
        /// 杰克逊博士（发明家）准备货单时：机器按概率补货；芯片只在玩家还没拥有时上架
        /// （芯片可重复使用，拥有后不需要再刷）。
        /// </summary>
        private void OnInventorStock(List<GameItem> items)
        {
            if (PlayerStore.Instance == null)
            {
                return;
            }
            bool machineOwned = PlayerStore.Instance.IsPlayerOwnThisItem(SolutionPurifier.MachineItemId);
            if (!machineOwned || RNG.Roll(MachineRestockChancePercent))
            {
                // 先按目录工厂造机器，再挂说明书便签，最后作为商店货上架（原版机器同款流程）。
                GameItem machine = SolutionPurifier.CreateMachineWithNote();
                PlayerStore.Instance.AddDirectSellingItemToTable(machine, isOwend: false);
            }

            foreach (SolutionPurifier.ChipInfo chip in SolutionPurifier.Chips)
            {
                if (!PlayerStore.Instance.IsPlayerOwnThisItem(chip.Id))
                {
                    PlayerStore.Instance.AddDirectSellingItemToTable(chip.Id);
                }
            }
        }

        private void OnTooltip(RichTextBuilder builder, GameItem item)
        {
            SolutionPurifier.AppendTooltip(builder, item);
        }

#if PURE_FACTORY_SMOKE
        private System.Collections.IEnumerator AutoLoadThenSmoke()
        {
            yield return new UnityEngine.WaitForSeconds(8f);
            if (MainMenuUIController.Instance == null || NewGameData.Instance == null)
            {
                log.Log("SMOKE: no main menu controller, skipped");
                yield break;
            }
            log.Log("SMOKE: auto-loading save slot 4");
            NewGameData.Instance.isLoadFromMainMenu = true;
            NewGameData.Instance.saveSlot = 4;
            NewGameData.Instance.saveFile = "save_4.es3";
            MainMenuUIController.Instance.StartNewGameClick();
        }

        private bool smokeDone;

        private void FactorySmokeTestOnce()
        {
            if (smokeDone)
            {
                return;
            }
            smokeDone = true;
            if (ModLoader.Instance != null)
            {
                ModLoader.Instance.StartCoroutine(FactorySmokeAfterLoad());
            }
        }

        private System.Collections.IEnumerator FactorySmokeAfterLoad()
        {
            yield return new UnityEngine.WaitForSeconds(4f);
            try
            {
                FactorySmokeTest();
            }
            catch (System.Exception e)
            {
                log.Error("FACTORY SMOKE FAIL: " + e);
            }
            yield return RunPurifyCase("ACID", "pure_chip_acid", () => PreBuildChemHelper.CreateAcidBottle(), false);
            yield return RunPurifyCase("METHYLOL", "pure_chip_methylol", () => PreBuildChemHelper.CreateFantaPrecusorBottle(), true);
            yield return RunPurifyCase("REDUCING", "pure_chip_reducing", () => PreBuildChemHelper.CreateReducingAgentBottle(), false);
        }

        private void FactorySmokeTest()
        {
            // 商店上架用的构造方式：机器 + 自动附带的说明书便签。
            GameItem machine = SolutionPurifier.CreateMachineWithNote();
            log.Log("FACTORY SMOKE OK: " + machine.name + " value=" + machine.unitValue
                + " window=" + (machine.contentWindow != null));

            RichTextBuilder builder = new RichTextBuilder();
            SolutionPurifier.AppendTooltip(builder, machine);
            builder.Build();
            log.Log("TOOLTIP SMOKE OK");

            GameSlotInventory noteSlot = MachineHelper.GetBodyStorage(machine, 4) as GameSlotInventory;
            GameItem note = noteSlot != null ? noteSlot.childItem : null;
            log.Log("NOTE SMOKE: " + (note == null ? "MISSING" : note.name));

            // 目录工厂本身不应带子物品（DirectoryMaster.Item 会清理子物品），字条由商店上架时挂。
            GameItem fromDirectory = DirectoryMaster.Item(SolutionPurifier.MachineItemId);
            log.Log("DIRECTORY SMOKE OK: " + fromDirectory.name + " id=" + fromDirectory.identifier);

            foreach (SolutionPurifier.ChipInfo info in SolutionPurifier.Chips)
            {
                GameItem chip = DirectoryMaster.Item(info.Id);
                log.Log("CHIP SMOKE: " + chip.identifier + " -> " + chip.name);
                chip.Destroy();
            }

            log.Log("LOCALE SMOKE: " + ModHelper.GetLocalized(ModId, "machine_name"));

            try
            {
                log.Log("SHOP PRE-COUNT: " + EmporiumEntry.Instance.GetAllItems().Count);
            }
            catch (System.Exception e)
            {
                log.Error("SHOP PRE-COUNT FAIL: " + e);
            }

            bool machineAdded = PlayerStore.Instance.TryAddDirectSellingItemToTable(machine, isOwend: false);
            int machineCount = -1;
            try
            {
                machineCount = EmporiumEntry.Instance.GetItemCountById(SolutionPurifier.MachineItemId);
            }
            catch (System.Exception e)
            {
                log.Error("SHOP SMOKE count check failed: " + e);
            }
            log.Log("SHOP SMOKE: machine added=" + machineAdded + " count=" + machineCount);
            fromDirectory.Destroy();
            foreach (SolutionPurifier.ChipInfo info in SolutionPurifier.Chips)
            {
                bool chipAdded = PlayerStore.Instance.TryAddDirectSellingItemToTable(info.Id);
                int chipCount = EmporiumEntry.Instance.GetItemCountById(info.Id);
                log.Log("SHOP CHIP: " + info.Id + " added=" + chipAdded + " count=" + chipCount);
            }
        }

        private System.Collections.IEnumerator RunPurifyCase(string label, string chipId, System.Func<GameItem> bottleFactory, bool expectStabilizer)
        {
            GameItem machine = null;
            GameItem bottle = null;
            GameItem battery = null;
            int totalBefore = 0;
            int targetBefore = 0;
            int extraBefore = 0;
            int energyBefore = 0;
            string targetLiquid = null;
            string extraLiquid = null;
            try
            {
                foreach (SolutionPurifier.ChipInfo info in SolutionPurifier.Chips)
                {
                    if (info.Id == chipId)
                    {
                        targetLiquid = info.TargetLiquid;
                        extraLiquid = info.KeepExtraLiquid;
                    }
                }
                bottle = bottleFactory();
                // 保证这批样品一定需要净化（随机的原版杂质上限刚好踩线时测试会不稳定）。
                WaterHelper.AddLiquid(bottle, "chemical_contaminant", 8000);
                totalBefore = WaterHelper.GetTotalVolume(bottle);
                targetBefore = bottle.GetTagReadonly(Liquid.GetCurrentPartFromId(targetLiquid)).GetInt();
                extraBefore = string.IsNullOrEmpty(extraLiquid)
                    ? 0
                    : bottle.GetTagReadonly(Liquid.GetCurrentPartFromId(extraLiquid)).GetInt();

                machine = SolutionPurifier.CreateMachine();
                machine.contentWindow?.Show();
            }
            catch (System.Exception e)
            {
                log.Error("PURIFY[" + label + "] FAIL (setup): " + e);
                yield break;
            }

            yield return null;
            yield return null;

            try
            {
                GameSlotInventory chipSlot = MachineHelper.GetBodyStorage(machine, 3) as GameSlotInventory;
                GameSlotInventory liquidSlot = MachineHelper.GetBodyStorage(machine, 2) as GameSlotInventory;
                GameSlotInventory batterySlot = MachineHelper.GetBatterySlot(machine);
                bool chipOk = chipSlot != null && chipSlot.UncheckedAccept(DirectoryMaster.Item(chipId));
                bool liquidOk = liquidSlot != null && liquidSlot.UncheckedAccept(bottle);
                battery = PreBuiltItemHelper.EnergyCredit(50);
                energyBefore = battery.GetTagReadonly("power_source_item_energy").GetInt();
                bool batteryOk = batterySlot != null && batterySlot.UncheckedAccept(battery);
                log.Log("PURIFY[" + label + "] SLOTS: chip=" + chipOk + " liquid=" + liquidOk + " battery=" + batteryOk);
                machine.onCycleEndSlotItemFunc?.Invoke(machine, null, default);
            }
            catch (System.Exception e)
            {
                log.Error("PURIFY[" + label + "] FAIL (run): " + e);
                yield break;
            }

            int totalAfter = WaterHelper.GetTotalVolume(bottle);
            int targetAfter = bottle.GetTagReadonly(Liquid.GetCurrentPartFromId(targetLiquid)).GetInt();
            int extraAfter = string.IsNullOrEmpty(extraLiquid)
                ? 0
                : bottle.GetTagReadonly(Liquid.GetCurrentPartFromId(extraLiquid)).GetInt();
            int energyAfter = battery.GetTagReadonly("power_source_item_energy").GetInt();
            bool targetRaised = targetAfter > targetBefore;
            bool extraKept = !expectStabilizer || extraAfter == extraBefore;
            bool powerUsed = energyAfter < energyBefore;
            int purityAfter = (int)((long)(targetAfter + extraAfter) * 10000 / totalAfter);
            bool capOk = purityAfter >= PurifierMath.PurityCapPermyriad(0) - 1 || targetRaised;
            log.Log("PURIFY[" + label + "] BEFORE target=" + targetBefore + " extra=" + extraBefore
                + " purity=" + (100.0 * (targetBefore + extraBefore) / totalBefore).ToString("0.00") + "%");
            log.Log("PURIFY[" + label + "] AFTER  target=" + targetAfter + " extra=" + extraAfter
                + " purity=" + (100.0 * (targetAfter + extraAfter) / totalAfter).ToString("0.00") + "%"
                + " energy=" + energyBefore + "->" + energyAfter);
            log.Log("PURIFY[" + label + "] SMOKE " + ((targetRaised && extraKept && powerUsed && capOk) ? "OK" : "FAIL"));

            bottle?.Destroy();
            battery?.Destroy();
            machine?.Destroy();
        }
#endif
    }
}
