using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.HUD
{
    public class ShipPowerStat : UnitFormatStatBase
    {
        public ShipPowerStat() : base("controlled_power_usage")
        {
            UnitSymbol = "W";
        }

        protected override void Update(int tick, bool enabled)
        {
            MyShipController shipCtrl = MyAPIGateway.Session?.ControlledObject as MyShipController;
            if(shipCtrl == null)
            {
                var turret = MyAPIGateway.Session?.ControlledObject as IMyLargeTurretBase;
                if(turret != null)
                    shipCtrl = MyAPIGateway.Session?.Player?.Character?.Parent as MyShipController;
            }

            if(shipCtrl == null || shipCtrl.GridResourceDistributor == null)
            {
                MaxValue = 0f;
                CurrentValue = 0f;
                return;
            }

            float maxAvailable = shipCtrl.GridResourceDistributor.MaxAvailableResourceByType(MyResourceDistributorComponent.ElectricityId);
            float totalRequired = MathHelper.Clamp(shipCtrl.GridResourceDistributor.TotalRequiredInputByType(MyResourceDistributorComponent.ElectricityId), 0f, maxAvailable);

            if(!enabled)
            {
                MaxValue = maxAvailable;
                CurrentValue = totalRequired;
                return;
            }

            // MW to W
            MaxValue = maxAvailable * 1000000f;
            CurrentValue = totalRequired * 1000000f;
        }
    }
}