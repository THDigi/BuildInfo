using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Utils;

namespace Digi.BuildInfo.Features.Tooltips
{
    public class TooltipHandler : ModComponent
    {
        public readonly Dictionary<MyDefinitionId, string> Tooltips = new Dictionary<MyDefinitionId, string>(MyDefinitionId.Comparer);

        public readonly HashSet<MyDefinitionId> IgnoreModItems = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        public delegate void SetupDel(bool generate);
        public event SetupDel Setup;

        public HashSet<MyProductionBlockDefinition> BlueprintPlannerBlocks = new HashSet<MyProductionBlockDefinition>();

        public Dictionary<MyDefinitionId, HashSet<MyProductionBlockDefinition>> TmpBpUsedIn = new Dictionary<MyDefinitionId, HashSet<MyProductionBlockDefinition>>(MyDefinitionId.Comparer);
        public Dictionary<MyDefinitionId, HashSet<MyProductionBlockDefinition>> TmpItemRefillIn = new Dictionary<MyDefinitionId, HashSet<MyProductionBlockDefinition>>(MyDefinitionId.Comparer);
        public Dictionary<MyDefinitionId, List<MyTuple<MyDefinitionBase, MyWeaponDefinition>>> TmpMagUsedIn = new Dictionary<MyDefinitionId, List<MyTuple<MyDefinitionBase, MyWeaponDefinition>>>(MyDefinitionId.Comparer);
        public HashSet<MyDefinitionId> TmpHasBP = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
        public Dictionary<MyDefinitionId, HashSet<MyCubeBlockDefinition>> TmpBlockFuel = new Dictionary<MyDefinitionId, HashSet<MyCubeBlockDefinition>>(MyDefinitionId.Comparer);
        public Dictionary<MyDefinitionId, HashSet<MyVendingMachineDefinition>> TmpVendingBuy = new Dictionary<MyDefinitionId, HashSet<MyVendingMachineDefinition>>(MyDefinitionId.Comparer);
        public Dictionary<MyDefinitionId, HashSet<MyVendingMachineDefinition>> TmpVendingSell = new Dictionary<MyDefinitionId, HashSet<MyVendingMachineDefinition>>(MyDefinitionId.Comparer);
        public Dictionary<MyDefinitionId, HashSet<MyCubeBlockDefinition>> TmpComponentInBlocks = new Dictionary<MyDefinitionId, HashSet<MyCubeBlockDefinition>>(MyDefinitionId.Comparer);
        public Dictionary<MyDefinitionId, HashSet<MyCubeBlockDefinition>> TmpComponentFromGrindingBlocks = new Dictionary<MyDefinitionId, HashSet<MyCubeBlockDefinition>>(MyDefinitionId.Comparer);
        public Dictionary<string, string> TmpStatDisplayNames = new Dictionary<string, string>()
        {
            ["BatteryCharge"] = "Battery",
        };

        void DisposeTempObjects()
        {
            //TmpComponentFromGrindingBlocks = null;
            //TmpBpUsedIn = null;

            TmpItemRefillIn = null;
            TmpMagUsedIn = null;
            TmpHasBP = null;
            TmpBlockFuel = null;
            TmpVendingBuy = null;
            TmpVendingSell = null;
            TmpComponentInBlocks = null;
            TmpStatDisplayNames = null;
        }

        bool init = false;

        public TooltipHandler(BuildInfoMod main) : base(main)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public override void RegisterComponent()
        {
        }

        public override void UpdateAfterSim(int tick)
        {
            if(!init && tick >= 30)
            {
                init = true;
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                DelayedRegister();
            }
        }

        void DelayedRegister()
        {
            SetupItems(generate: true);

            Main.Config.InternalInfo.ValueAssigned += ConfigValueChanged;
            Main.Config.ItemTooltipAdditions.ValueAssigned += ConfigValueChanged;
            Main.Config.ItemSymbolAdditions.ValueAssigned += ConfigValueChanged;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.Config.InternalInfo.ValueAssigned -= ConfigValueChanged;
            Main.Config.ItemTooltipAdditions.ValueAssigned -= ConfigValueChanged;
            Main.Config.ItemSymbolAdditions.ValueAssigned -= ConfigValueChanged;
        }

        void PreTooltipGeneration()
        {
            bool survival = MyAPIGateway.Session.SurvivalMode;

            foreach(MyCubeBlockDefinition blockDef in Main.Caches.BlockDefs)
            {
                if(!blockDef.Public || (survival && !blockDef.AvailableInSurvival))
                    continue;

                {
                    foreach(MyCubeBlockDefinition.Component comp in blockDef.Components)
                    {
                        if(comp.Definition == null)
                            continue;

                        TmpComponentInBlocks.GetValueOrNew(comp.Definition.Id).Add(blockDef);

                        if(comp.Definition != comp.DeconstructItem)
                        {
                            TmpComponentFromGrindingBlocks.GetValueOrNew(comp.DeconstructItem.Id).Add(blockDef);
                        }
                    }
                }
                {
                    MyWeaponBlockDefinition weaponBlockDef = blockDef as MyWeaponBlockDefinition;
                    if(weaponBlockDef != null)
                    {
                        if(Main.CoreSystemsAPIHandler.Weapons.ContainsKey(weaponBlockDef.Id))
                            continue;

                        MyWeaponDefinition wpDef;
                        if(!MyDefinitionManager.Static.TryGetWeaponDefinition(weaponBlockDef.WeaponDefinitionId, out wpDef))
                            continue;

                        if(wpDef.AmmoMagazinesId != null && wpDef.AmmoMagazinesId.Length > 0)
                        {
                            foreach(MyDefinitionId magId in wpDef.AmmoMagazinesId)
                            {
                                TmpMagUsedIn.GetValueOrNew(magId).Add(new MyTuple<MyDefinitionBase, MyWeaponDefinition>(weaponBlockDef, wpDef));
                            }
                        }
                    }
                }
                {
                    MyProductionBlockDefinition prodDef = blockDef as MyProductionBlockDefinition;
                    if(prodDef != null)
                    {
                        bool isGasGenOrTank = blockDef is MyGasTankDefinition || blockDef is MyOxygenGeneratorDefinition;

                        foreach(MyBlueprintClassDefinition bpClass in prodDef.BlueprintClasses)
                        {
                            if(bpClass.Id.SubtypeName == Hardcoded.BuildPlanner_BPClassSubtype)
                            {
                                BlueprintPlannerBlocks.Add(prodDef);
                                continue;
                            }

                            foreach(MyBlueprintDefinitionBase bp in bpClass)
                            {
                                TmpBpUsedIn.GetValueOrNew(bp.Id).Add(prodDef);

                                if(isGasGenOrTank)
                                {
                                    foreach(MyBlueprintDefinitionBase.Item preReq in bp.Prerequisites)
                                    {
                                        // is it a bottle item?
                                        if(MyAPIGateway.Reflection.IsAssignableFrom(typeof(MyObjectBuilder_GasContainerObject), preReq.Id.TypeId))
                                        {
                                            TmpItemRefillIn.GetValueOrNew(preReq.Id).Add(prodDef);
                                        }
                                    }
                                }

                                if(!isGasGenOrTank) // HACK: bp results of gas generators or gas tanks are not used, skip
                                {
                                    foreach(MyBlueprintDefinitionBase.Item result in bp.Results)
                                    {
                                        TmpHasBP.Add(result.Id);
                                    }
                                }
                            }
                        }
                    }
                }
                {
                    MyReactorDefinition reactorDef = blockDef as MyReactorDefinition; // only one that has FuelInfos it seems
                    if(reactorDef != null)
                    {
                        foreach(MyReactorDefinition.FuelInfo fuelInfo in reactorDef.FuelInfos)
                        {
                            TmpBlockFuel.GetValueOrNew(fuelInfo.FuelId).Add(reactorDef);
                        }
                    }
                }
                {
                    MyParachuteDefinition parachuteDef = blockDef as MyParachuteDefinition;
                    if(parachuteDef != null)
                    {
                        TmpBlockFuel.GetValueOrNew(parachuteDef.MaterialDefinitionId).Add(parachuteDef);
                    }
                }
                {
                    MyVendingMachineDefinition vendingDef = blockDef as MyVendingMachineDefinition;
                    if(vendingDef != null && vendingDef.DefaultItems != null && vendingDef.DefaultItems.Count > 0)
                    {
                        foreach(MyObjectBuilder_StoreItem offer in vendingDef.DefaultItems)
                        {
                            if(offer.ItemType != ItemTypes.PhysicalItem || !offer.Item.HasValue)
                                continue;

                            if(offer.StoreItemType == StoreItemTypes.Offer)
                                TmpVendingBuy.GetValueOrNew(offer.Item.Value).Add(vendingDef);
                            else
                                TmpVendingSell.GetValueOrNew(offer.Item.Value).Add(vendingDef);
                        }
                    }
                }
            }

            foreach(MyPhysicalItemDefinition physItemDef in Main.Caches.ItemDefs)
            {
                MyWeaponItemDefinition weaponItemDef = physItemDef as MyWeaponItemDefinition;
                if(weaponItemDef != null)
                {
                    // TODO: weaponcore?

                    MyWeaponDefinition wpDef;
                    if(!MyDefinitionManager.Static.TryGetWeaponDefinition(weaponItemDef.WeaponDefinitionId, out wpDef))
                        continue;

                    if(wpDef.AmmoMagazinesId != null && wpDef.AmmoMagazinesId.Length > 0)
                    {
                        foreach(MyDefinitionId magId in wpDef.AmmoMagazinesId)
                        {
                            TmpMagUsedIn.GetValueOrNew(magId).Add(new MyTuple<MyDefinitionBase, MyWeaponDefinition>(weaponItemDef, wpDef));
                        }
                    }
                }
            }
        }

        void ConfigValueChanged(bool oldValue, bool newValue, SettingBase<bool> setting)
        {
            if(oldValue != newValue)
            {
                SetupItems();
            }
        }

        void SetupItems(bool generate = false)
        {
            if(generate)
                MyLog.Default.WriteLine($"{BuildInfoMod.ModName} mod: Starting to generate tooltips...");

            Stopwatch timer = (generate ? Stopwatch.StartNew() : null);

            if(generate)
            {
                PreTooltipGeneration();
            }

            try
            {
                Setup?.Invoke(generate);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            if(generate)
            {
                DisposeTempObjects();
                timer.Stop();

                string msg = $"Finished generating {Tooltips.Count.ToString()} tooltips in {timer.Elapsed.TotalMilliseconds.ToString("###,###,###,##0.##")} ms";
                MyLog.Default.WriteLine($"{BuildInfoMod.ModName} mod: {msg}");
                Log.Info(msg);
            }
        }

        public static void AppendModInfo(StringBuilder s, MyDefinitionBase def, int modNameMaxLen = 64)
        {
            if(!def.Context.IsBaseGame)
            {
                s.Append("Mod: ").AppendMaxLength(def.Context.ModName, modNameMaxLen).Append('\n');

                MyObjectBuilder_Checkpoint.ModItem modItem = def.Context.ModItem;
                if(modItem.Name != null && modItem.PublishedFileId > 0)
                {
                    s.Append("ModId: ").Append(modItem.PublishedServiceName).Append(':').Append(modItem.PublishedFileId).Append('\n');
                }
            }
        }
    }
}
