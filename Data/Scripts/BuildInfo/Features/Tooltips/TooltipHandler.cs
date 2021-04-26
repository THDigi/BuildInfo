using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Sandbox.Definitions;
using VRage.Game;

namespace Digi.BuildInfo.Features.Tooltips
{
    public class TooltipHandler : ModComponent
    {
        public readonly Dictionary<MyDefinitionId, string> Tooltips = new Dictionary<MyDefinitionId, string>(MyDefinitionId.Comparer);

        public readonly HashSet<MyDefinitionId> IgnoreModItems = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        public delegate void SetupDel(bool generate);
        public event SetupDel Setup;

        public HashSet<MyDefinitionId> TmpHasBP = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
        public Dictionary<MyDefinitionId, List<MyCubeBlockDefinition>> TmpBlockFuel = new Dictionary<MyDefinitionId, List<MyCubeBlockDefinition>>(MyDefinitionId.Comparer);
        public Dictionary<string, string> TmpStatDisplayNames = new Dictionary<string, string>()
        {
            ["BatteryCharge"] = "Battery",
        };

        void DisposeTempObjects()
        {
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
            foreach(var def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                {
                    var prodDef = def as MyProductionBlockDefinition;
                    if(prodDef != null)
                    {
                        foreach(var bpClass in prodDef.BlueprintClasses)
                        {
                            foreach(var bp in bpClass)
                            {
                                foreach(var result in bp.Results)
                                {
                                    TmpHasBP.Add(result.Id);
                                }
                            }
                        }
                        continue;
                    }
                }
                {
                    var reactorDef = def as MyReactorDefinition; // only one that has FuelInfos it seems
                    if(reactorDef != null)
                    {
                        foreach(var fuelInfo in reactorDef.FuelInfos)
                        {
                            var list = TmpBlockFuel.GetOrAdd(fuelInfo.FuelId);
                            list.Add(reactorDef);
                        }
                        continue;
                    }
                }
                {
                    var parachuteDef = def as MyParachuteDefinition;
                    if(parachuteDef != null)
                    {
                        var list = TmpBlockFuel.GetOrAdd(parachuteDef.MaterialDefinitionId);
                        list.Add(parachuteDef);
                        continue;
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
            {
                PreTooltipGeneration();
            }

            Setup?.Invoke(generate);

            if(generate)
            {
                DisposeTempObjects();
            }
        }
    }
}
