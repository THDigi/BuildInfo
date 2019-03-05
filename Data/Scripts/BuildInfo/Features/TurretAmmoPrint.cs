using System.Text;
using Digi.BuildInfo.Systems;
using Draygo.API;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.BuildInfo.Features
{
    public class TurretAmmoPrint : ClientComponent
    {
        private const int SKIP_TICKS = 5; // ticks between text updates, min value 1.

        private bool visible = false;
        //private HudAPIv2.HUDMessage msg;
        //private StringBuilder msgStr;
        private IMyHudNotification notify;

        public TurretAmmoPrint(Client mod) : base(mod)
        {
        }

        public override void RegisterComponent()
        {
            EquipmentMonitor.UpdateControlled += EquipmentMonitor_UpdateControlled;
        }

        public override void UnregisterComponent()
        {
            EquipmentMonitor.UpdateControlled -= EquipmentMonitor_UpdateControlled;
        }

        private void EquipmentMonitor_UpdateControlled(IMyCharacter character, IMyShipController shipController, IMyControllableEntity controlled, int tick)
        {
            if(tick % SKIP_TICKS != 0)
                return;

            if(!Config.TurretAmmo)
                return;

            var turret = controlled as IMyLargeTurretBase;

            if(turret != null)
            {
                var gun = (IMyGunObject<MyGunBase>)controlled;

                int rounds = gun.GunBase.GetTotalAmmunitionAmount(); // total rounds in inventory + currently used mag; gets updated on shoot only though
                int mags = gun.GetAmmunitionAmount(); // total mags in inventory (not including the one partially fired that's in the gun)

                // rounds in current ShotsInBurst internal magazine is not accessible

                // TODO keep it as a notification?
                //if(TextAPIEnabled)
                //{
                //    var inv = turret.GetInventory();
                //    var inventoryFill = (float)inv.CurrentVolume / (float)inv.MaxVolume;

                //    if(msg == null)
                //    {
                //        msgStr = new StringBuilder();
                //        msg = new HudAPIv2.HUDMessage(msgStr, new Vector2D(0.2, 0), HideHud: true, Scale: 1.25);

                //        // TODO use textAPI monospace font?
                //    }

                //    if(!visible)
                //    {
                //        visible = true;
                //        msg.Visible = true;
                //    }

                //    msgStr.Clear();

                //    if(mags == 0)
                //        msgStr.Append("<color=red>");
                //    else if(mags <= 3)
                //        msgStr.Append("<color=yellow>");

                //    msgStr.Append("[ ").Append(rounds).Append(" ]");
                //}
                //else
                {
                    if(notify == null)
                        notify = MyAPIGateway.Utilities.CreateNotification("", 16 * SKIP_TICKS, "Monospace");

                    // the extra spacing in front is because the font shadow takes [ and ] into consideration and adds shadow at the end...
                    notify.Text = (mags == 0 ? $"  [{rounds}]" : $"{rounds}");
                    notify.Show();
                    visible = true;
                }
            }
            else
            {
                if(visible)
                {
                    visible = false;

                    //if(msg != null)
                    //    msg.Visible = false;

                    if(notify != null)
                        notify.Hide();
                }
            }
        }
    }
}
