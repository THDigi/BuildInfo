using System.Collections.Generic;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.BlockLogic
{
    // registered in BlockAttachedLogic
    class UpgradeModuleIndicator : BlockAttachedLogic.LogicBase
    {
        IMyUpgradeModule Module;

        bool NeedsRefresh = true;
        int TotalPorts;
        bool AnyIncompatible;
        int Step;

        const BlockAttachedLogic.BlockUpdate UpdateToUse = BlockAttachedLogic.BlockUpdate.Update10; // if editing this, change method override too

        static readonly Color EmissiveColorBlinkA = Color.Yellow;
        static readonly Color EmissiveColorBlinkB = Color.Red;
        static readonly List<string> EmissiveNames = new List<string>(2);

        public override void Added()
        {
            Module = (IMyUpgradeModule)Block;
            Module.CubeGridChanged += ModuleMoved;
            ModuleMoved(oldGrid: null);
        }

        public override void Removed()
        {
            if(Module != null)
            {
                Module.CubeGrid.OnBlockAdded -= GridBlockAdded;
                Module.CubeGrid.OnBlockRemoved -= GridBlockRemoved;
            }
        }

        void ModuleMoved(IMyCubeGrid oldGrid)
        {
            if(oldGrid != null)
            {
                oldGrid.OnBlockAdded -= GridBlockAdded;
                oldGrid.OnBlockRemoved -= GridBlockRemoved;
            }

            Module.CubeGrid.OnBlockAdded += GridBlockAdded;
            Module.CubeGrid.OnBlockRemoved += GridBlockRemoved;

            NeedsRefresh = true;

            SetUpdate(UpdateToUse, true);
        }

        void GridBlockRemoved(IMySlimBlock slim)
        {
            NeedsRefresh = true;
            SetUpdate(UpdateToUse, true);
        }

        void GridBlockAdded(IMySlimBlock slim)
        {
            NeedsRefresh = true;
            SetUpdate(UpdateToUse, true);
        }

        public override void Update10()
        {
            if(NeedsRefresh)
            {
                NeedsRefresh = false;

                RefreshConnections();

                if(!NeedsRefresh && !AnyIncompatible)
                {
                    SetUpdate(UpdateToUse, false);
                    return;
                }
            }

            if(AnyIncompatible)
            {
                if(++Step >= 6)
                    Step = 0;

                if(Step == 0 || Step == 3)
                {
                    if(TotalPorts == 0)
                    {
                        var def = (MyCubeBlockDefinition)Module.SlimBlock.BlockDefinition;
                        BData_Base data = BuildInfoMod.Instance.LiveDataHandler.Get<BData_Base>(def);
                        if(data == null)
                        {
                            Log.Error($"Failed to retrieve BData_Base for live block: {def.Id.ToString()} ({def.Context.GetNameAndId()})");
                        }
                        else
                        {
                            TotalPorts = data.UpgradePorts?.Count ?? 0;

                            if(TotalPorts > EmissiveNames.Count)
                            {
                                int remaining = TotalPorts - EmissiveNames.Count;
                                while(--remaining >= 0)
                                {
                                    EmissiveNames.Add($"Emissive{EmissiveNames.Count}");
                                }
                            }
                        }
                    }

                    for(int i = 0; i < TotalPorts; i++)
                    {
                        Module.SetEmissiveParts(EmissiveNames[i], Step == 0 ? EmissiveColorBlinkA : EmissiveColorBlinkB, 1f);
                    }
                }
            }
        }

        void RefreshConnections()
        {
            AnyIncompatible = false;

            using(Utils.UpgradeModule.Result result = Utils.UpgradeModule.GetAttached(Module))
            {
                if(!result.HasData)
                {
                    NeedsRefresh = true; // try again later
                }
                else
                {
                    foreach(var attached in result.Attached)
                    {
                        if(!attached.Compatible)
                        {
                            AnyIncompatible = true;
                            break;
                        }
                    }
                }
            }
        }
    }
}