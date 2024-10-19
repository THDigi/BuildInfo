using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), useEntityUpdate: false)]
    public class UpgradeModuleGL : MyGameLogicComponent
    {
        public UpgradeModule_PlayerSide Player;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if(BuildInfo_GameSession.GetOrComputeIsKilled(this.GetType().Name))
                return;

            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                if(MyAPIGateway.Utilities.IsDedicated)
                    return;

                var block = Entity as IMyCubeBlock;
                if(block?.CubeGrid?.Physics == null)
                    return;

                Player = new UpgradeModule_PlayerSide(this);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void MarkForClose()
        {
            try
            {
                Player?.Dispose();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                Player?.Update(MyEntityUpdateEnum.EACH_FRAME);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateAfterSimulation10()
        {
            try
            {
                Player?.Update(MyEntityUpdateEnum.EACH_10TH_FRAME);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateAfterSimulation100()
        {
            try
            {
                Player?.Update(MyEntityUpdateEnum.EACH_100TH_FRAME);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }

    public class UpgradeModule_PlayerSide
    {
        readonly UpgradeModuleGL GameLogic;
        readonly IMyUpgradeModule Block;

        bool NeedsRefresh = true;
        int TotalPorts;
        bool AnyIncompatible;
        int Step;

        const MyEntityUpdateEnum UpdateFlag = MyEntityUpdateEnum.EACH_10TH_FRAME;

        static readonly Color EmissiveColorBlinkA = Color.Yellow;
        static readonly Color EmissiveColorBlinkB = Color.Red;
        static readonly List<string> EmissiveNames = new List<string>(2);

        public UpgradeModule_PlayerSide(UpgradeModuleGL gamelogic)
        {
            GameLogic = gamelogic;
            Block = (IMyUpgradeModule)GameLogic.Entity;

            Block.CubeGridChanged += GridChanged;
            GridChanged(oldGrid: null);
        }

        public void Dispose()
        {
            if(Block != null)
            {
                Block.CubeGrid.OnBlockAdded -= GridBlockAdded;
                Block.CubeGrid.OnBlockRemoved -= GridBlockRemoved;
            }
        }

        void GridChanged(IMyCubeGrid oldGrid)
        {
            if(oldGrid != null)
            {
                oldGrid.OnBlockAdded -= GridBlockAdded;
                oldGrid.OnBlockRemoved -= GridBlockRemoved;
            }

            Block.CubeGrid.OnBlockAdded += GridBlockAdded;
            Block.CubeGrid.OnBlockRemoved += GridBlockRemoved;

            NeedsRefresh = true;
            GameLogic.NeedsUpdate |= UpdateFlag;
        }

        void GridBlockRemoved(IMySlimBlock slim)
        {
            NeedsRefresh = true;
            GameLogic.NeedsUpdate |= UpdateFlag;
        }

        void GridBlockAdded(IMySlimBlock slim)
        {
            NeedsRefresh = true;
            GameLogic.NeedsUpdate |= UpdateFlag;
        }

        /// <summary>
        /// Update1/10/100 all call this, use <see cref="UpdateFlag"/> to choose which.
        /// The <paramref name="source"/> informs this which one triggerd it in case you have multiple active.
        /// </summary>
        public void Update(MyEntityUpdateEnum source)
        {
            if(NeedsRefresh)
            {
                NeedsRefresh = false;

                RefreshConnections();

                if(!NeedsRefresh && !AnyIncompatible)
                {
                    GameLogic.NeedsUpdate &= ~UpdateFlag;
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
                        var def = (MyCubeBlockDefinition)Block.SlimBlock.BlockDefinition;
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
                        Block.SetEmissiveParts(EmissiveNames[i], Step == 0 ? EmissiveColorBlinkA : EmissiveColorBlinkB, 1f);
                    }
                }
            }
        }

        void RefreshConnections()
        {
            AnyIncompatible = false;

            using(Utils.UpgradeModule.Result result = Utils.UpgradeModule.GetAttached(Block))
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