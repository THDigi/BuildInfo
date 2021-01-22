using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Weapons : StatusOverrideBase
    {
        public Weapons(ToolbarStatusProcessor processor) : base(processor)
        {
            RegisterFor(typeof(MyObjectBuilder_SmallGatlingGun));
            RegisterFor(typeof(MyObjectBuilder_SmallMissileLauncher));
            RegisterFor(typeof(MyObjectBuilder_SmallMissileLauncherReload));
            RegisterFor(typeof(MyObjectBuilder_InteriorTurret));
            RegisterFor(typeof(MyObjectBuilder_LargeGatlingTurret));
            RegisterFor(typeof(MyObjectBuilder_LargeMissileTurret));
        }

        void RegisterFor(MyObjectBuilderType type)
        {
            Processor.AddStatus(type, Shoot, "ShootOnce", "Shoot", "Shoot_On", "Shoot_Off");

            // TODO: group support
            //Processor.AddGroupStatus(type, StatusGroup, "ShootOnce", "Shoot", "Shoot_On", "Shoot_Off");
        }

        bool Shoot(StringBuilder sb, ToolbarItem item)
        {
            var gun = (IMyGunObject<MyGunBase>)item.Block;
            sb.Append(gun.GunBase.GetTotalAmmunitionAmount().ToString());
            return true;
        }

        //bool StatusGroup(StringBuilder sb, ToolbarItem item, GroupData groupData)
        //{
        //    if(!groupData.GetGroupBlocks<IMyGunObject<MyGunBase>>())
        //        return false;
        //
        //    return true;
        //}
    }
}
