using System;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace Digi.BuildInfo.Features.HUD
{
    public class HydrogenBottleStat : BottleBaseStat
    {
        public HydrogenBottleStat()
        {
            if(!BuildInfo_GameSession.GetOrComputeIsKilled(this.GetType().Name))
            {
                Id = MyStringHash.GetOrCompute("player_hydrogen_bottles");
                Gas = MyResourceDistributorComponent.HydrogenId;
                TargetTickDivision = 0;
            }
        }
    }

    public class OxygenBottleStat : BottleBaseStat
    {
        public OxygenBottleStat()
        {
            if(!BuildInfo_GameSession.GetOrComputeIsKilled(this.GetType().Name))
            {
                Id = MyStringHash.GetOrCompute("player_oxygen_bottles");
                Gas = MyResourceDistributorComponent.OxygenId;
                TargetTickDivision = 30;
            }
        }
    }

    public abstract class BottleBaseStat : IMyHudStat
    {
        public MyStringHash Id { get; protected set; }
        public float CurrentValue { get; protected set; }
        public float MinValue { get; } = 0f;
        public float MaxValue { get; } = 1f;
        public string GetValueString() => CurrentValue.ToString();

        protected MyDefinitionId Gas { get; set; }
        protected int TargetTickDivision = 0;

        public void Update()
        {
            if(BuildInfo_GameSession.IsKilled)
                return;

            try
            {
                if(MyAPIGateway.Session == null)
                    return;

                if(MyAPIGateway.Session.GameplayFrameCounter % 60 != TargetTickDivision)
                    return;

                IMyCharacter chr = MyAPIGateway.Session?.Player?.Character;
                MyInventory inv = chr?.GetInventory() as MyInventory;

                CurrentValue = 0;

                if(inv == null)
                    return;

                foreach(MyPhysicalInventoryItem item in inv.GetItems())
                {
                    MyObjectBuilder_GasContainerObject gasContainer = item.Content as MyObjectBuilder_GasContainerObject;
                    if(gasContainer != null && gasContainer.GasLevel > 1e-06f)
                    {
                        MyOxygenContainerDefinition def = MyDefinitionManager.Static.GetPhysicalItemDefinition(item.Content.GetId()) as MyOxygenContainerDefinition;
                        if(def != null && def.StoredGasId == Gas)
                        {
                            CurrentValue += 1f;
                        }
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}