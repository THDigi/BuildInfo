using System.Collections.Generic;
using System.Diagnostics;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Utils;

namespace Digi.BuildInfo.Features.Tooltips
{
    public class TooltipHandler : ModComponent
    {
        public readonly Dictionary<MyDefinitionId, string> Tooltips = new Dictionary<MyDefinitionId, string>(MyDefinitionId.Comparer);

        public readonly HashSet<MyDefinitionId> IgnoreModItems = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        public delegate void SetupDel(bool generate);
        public event SetupDel Setup;

        public Dictionary<MyDefinitionId, List<MyProductionBlockDefinition>> TmpBpUsedIn = new Dictionary<MyDefinitionId, List<MyProductionBlockDefinition>>(MyDefinitionId.Comparer);
        public Dictionary<MyDefinitionId, List<MyTuple<MyDefinitionBase, MyWeaponDefinition>>> TmpMagUsedIn = new Dictionary<MyDefinitionId, List<MyTuple<MyDefinitionBase, MyWeaponDefinition>>>(MyDefinitionId.Comparer);
        public HashSet<MyDefinitionId> TmpHasBP = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
        public Dictionary<MyDefinitionId, List<MyCubeBlockDefinition>> TmpBlockFuel = new Dictionary<MyDefinitionId, List<MyCubeBlockDefinition>>(MyDefinitionId.Comparer);
        public Dictionary<string, string> TmpStatDisplayNames = new Dictionary<string, string>()
        {
            ["BatteryCharge"] = "Battery",
        };

        void DisposeTempObjects()
        {
            TmpBpUsedIn = null;
            TmpMagUsedIn = null;
            TmpHasBP = null;
            TmpBlockFuel = null;
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

            foreach(var blockDef in Main.Caches.BlockDefs)
            {
                if(!blockDef.Public || (!survival && blockDef.AvailableInSurvival))
                    continue;

                {
                    var weaponBlockDef = blockDef as MyWeaponBlockDefinition;
                    if(weaponBlockDef != null)
                    {
                        // TODO: weaponcore?

                        MyWeaponDefinition wpDef;
                        if(!MyDefinitionManager.Static.TryGetWeaponDefinition(weaponBlockDef.WeaponDefinitionId, out wpDef))
                            continue;

                        if(wpDef.AmmoMagazinesId != null && wpDef.AmmoMagazinesId.Length > 0)
                        {
                            foreach(var magId in wpDef.AmmoMagazinesId)
                            {
                                TmpMagUsedIn.GetOrAdd(magId).Add(new MyTuple<MyDefinitionBase, MyWeaponDefinition>(weaponBlockDef, wpDef));
                            }
                        }
                    }
                }
                {
                    var prodDef = blockDef as MyProductionBlockDefinition;
                    if(prodDef != null)
                    {
                        bool isGasGenOrTank = blockDef is MyGasTankDefinition || blockDef is MyOxygenGeneratorDefinition;

                        foreach(var bpClass in prodDef.BlueprintClasses)
                        {
                            foreach(var bp in bpClass)
                            {
                                TmpBpUsedIn.GetOrAdd(bp.Id).Add(prodDef);

                                if(!isGasGenOrTank) // bp results of gas generators or gas tanks are not used, skip
                                {
                                    foreach(var result in bp.Results)
                                    {
                                        TmpHasBP.Add(result.Id);
                                    }
                                }
                            }
                        }
                    }
                }
                {
                    var reactorDef = blockDef as MyReactorDefinition; // only one that has FuelInfos it seems
                    if(reactorDef != null)
                    {
                        foreach(var fuelInfo in reactorDef.FuelInfos)
                        {
                            TmpBlockFuel.GetOrAdd(fuelInfo.FuelId).Add(reactorDef);
                        }
                    }
                }
                {
                    var parachuteDef = blockDef as MyParachuteDefinition;
                    if(parachuteDef != null)
                    {
                        TmpBlockFuel.GetOrAdd(parachuteDef.MaterialDefinitionId).Add(parachuteDef);
                    }
                }
            }

            foreach(var physItemDef in Main.Caches.ItemDefs)
            {
                var weaponItemDef = physItemDef as MyWeaponItemDefinition;
                if(weaponItemDef != null)
                {
                    // TODO: weaponcore?

                    MyWeaponDefinition wpDef;
                    if(!MyDefinitionManager.Static.TryGetWeaponDefinition(weaponItemDef.WeaponDefinitionId, out wpDef))
                        continue;

                    if(wpDef.AmmoMagazinesId != null && wpDef.AmmoMagazinesId.Length > 0)
                    {
                        foreach(var magId in wpDef.AmmoMagazinesId)
                        {
                            TmpMagUsedIn.GetOrAdd(magId).Add(new MyTuple<MyDefinitionBase, MyWeaponDefinition>(weaponItemDef, wpDef));
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
                MyLog.Default.WriteLine("BuildInfo mod: Starting to generate tooltips...");

            var timer = (generate ? Stopwatch.StartNew() : null);

            if(generate)
            {
                PreTooltipGeneration();
            }

            Setup?.Invoke(generate);

            if(generate)
            {
                DisposeTempObjects();
            }

            if(generate)
            {
                timer.Stop();

                string msg = $"Finished generating {Tooltips.Count.ToString()} tooltips in {timer.Elapsed.TotalMilliseconds.ToString("0.##########")} ms";
                MyLog.Default.WriteLine($"BuildInfo mod: {msg}");
                Log.Info(msg);
            }
        }
    }
}
