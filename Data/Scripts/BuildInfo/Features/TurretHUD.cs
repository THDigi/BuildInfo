using System;
using System.Text;
using Digi.BuildInfo.Features.ReloadTracker;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features
{
    public class TurretHUD : ModComponent
    {
        private const int SKIP_TICKS = 6; // ticks between text updates, min value 1.

        private IMyLargeTurretBase prevTurret;
        private Weapon weaponTracker;
        private bool weaponCoreBlock;

        private bool visible = false;
        private IMyHudNotification notify;
        private HudAPIv2.HUDMessage hudMsg;
        private StringBuilder sb;

        private readonly MyStringId MATERIAL_SQUARE = MyStringId.GetOrCompute("Square");
        private readonly MyStringId MATERIAL_DOT = MyStringId.GetOrCompute("WhiteDot");

        public TurretHUD(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.Config.Handler.SettingsLoaded += SettingsLoaded;
            SettingsLoaded();
        }

        public override void UnregisterComponent()
        {
            Main.Config.Handler.SettingsLoaded -= SettingsLoaded;
        }

        private void SettingsLoaded()
        {
            if(Main.Config.TurretHUD.Value)
            {
                UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM | UpdateFlags.UPDATE_DRAW;
            }
            else
            {
                UpdateMethods = UpdateFlags.NONE;
                HideHUD();
            }
        }

        // draw ship relative direction indicator
        public override void UpdateDraw()
        {
            var turret = MyAPIGateway.Session.ControlledObject as IMyLargeTurretBase;

            if(turret == null)
                return;

            var cockpit = MyAPIGateway.Session.Player?.Character?.Parent as IMyCockpit;

            if(cockpit == null)
                return;

            if(!cockpit.IsSameConstructAs(turret))
                return; // different ships

            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;

            var pos = Main.DrawUtils.TextAPIHUDtoWorld(new Vector2D(0, -0.5));

            var scaleFOV = Main.DrawUtils.ScaleFOV;
            var length = 0.01f * scaleFOV;
            var thick = 0.0002f * scaleFOV;

            var screenToPosDir = Vector3D.Normalize(pos - camMatrix.Translation);
            var angle = Math.Acos(Vector3D.Dot(screenToPosDir, camMatrix.Forward));
            var shipForwardScreen = Vector3D.TransformNormal(cockpit.WorldMatrix.Forward, MatrixD.CreateFromAxisAngle(camMatrix.Right, -angle));

            var dirDot = Vector3D.Dot(camMatrix.Forward, shipForwardScreen);
            var color = Color.Lerp(Color.Red, Color.Lime, (float)((1 + dirDot) / 2));

            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, color, pos, shipForwardScreen, length, thick, BlendTypeEnum.PostPP);

            var radius = 0.0005f * scaleFOV;
            MyTransparentGeometry.AddPointBillboard(MATERIAL_DOT, color, pos, radius, 0, blendType: BlendTypeEnum.PostPP);
        }

        public override void UpdateAfterSim(int tick)
        {
            if(tick % SKIP_TICKS != 0)
                return;

            var turret = MyAPIGateway.Session.ControlledObject as IMyLargeTurretBase;
            if(turret != null)
            {
                if(prevTurret != turret)
                {
                    weaponCoreBlock = Main.WeaponCoreAPIHandler.IsBlockWeapon(turret.BlockDefinition);

                    if(weaponCoreBlock)
                        weaponTracker = null;
                    else
                        weaponTracker = Main.ReloadTracking.GetWeaponInfo(turret);

                    prevTurret = turret;
                }

                if(weaponCoreBlock)
                {
                    HideHUD();
                    return;
                }

                GenerateText(turret);
                DrawHUD();
            }
            else
            {
                HideHUD();
            }
        }

        private void GenerateText(IMyLargeTurretBase turret)
        {
            var gun = (IMyGunObject<MyGunBase>)turret;
            var magDef = gun.GunBase?.CurrentAmmoMagazineDefinition;

            var inv = turret.GetInventory();

            int loadedMag = gun.GunBase.CurrentAmmo; // rounds left in currently loaded magazine, has no relation to reloading!
            int mags = gun.GetAmmunitionAmount(); // total mags in inventory (not including the one partially fired that's in the gun)

            if(sb == null)
                sb = new StringBuilder(160);

            sb.Clear();

            if(weaponTracker != null)
            {
                sb.Append("Ammo: ");
                var ammo = weaponTracker.Ammo;
                var ammoMax = weaponTracker.AmmoMax;

                if(weaponTracker.Reloading)
                    sb.Color(Color.Red);
                else if(ammo <= (ammoMax / 4))
                    sb.Color(Color.Yellow);

                if(weaponTracker.Reloading)
                    sb.Append("Reloading");
                else
                    sb.Append(ammo);

                sb.ResetFormatting().Append(" / ").Append(ammoMax).NewLine();
            }

            if(magDef != null)
            {
                sb.Append("Type: ").Append(magDef.DisplayNameText).NewLine();

                if(magDef.Capacity > 1)
                    sb.Append("Magazine: ").Append(loadedMag).NewLine();
            }

            sb.Append("Inventory: ").Append(mags);
            if(inv != null && magDef != null)
                sb.Append(" / ").Append(Math.Floor((float)inv.MaxVolume / magDef.Volume));
            sb.Append(" mags").NewLine();

            // TODO: toggleable between showing vanilla HUD and showing this?

            //var grid = turret.CubeGrid;

            //if(!grid.IsStatic)
            //{
            //    sb.NewLine();

            //    sb.Append("Ship speed: ").SpeedFormat(grid.Physics.LinearVelocity.Length()).NewLine();

            //    var cockpit = MyAPIGateway.Session.Player?.Character?.Parent as IMyCockpit;

            //    if(cockpit != null && cockpit.IsSameConstructAs(turret))
            //    {
            //        var internalCockpit = (MyCockpit)cockpit;

            //        sb.Append("Dampeners: ");

            //        if(internalCockpit.RelativeDampeningEntity != null)
            //        {
            //            sb.Color(Color.Lime).Append("Relative");
            //        }
            //        else
            //        {
            //            if(!cockpit.DampenersOverride)
            //                sb.Color(Color.Red);

            //            sb.Append(cockpit.DampenersOverride ? "On" : "Off");
            //        }

            //        sb.NewLine();
            //    }
            //}
        }

        private void DrawHUD()
        {
            if(Main.TextAPI.IsEnabled)
            {
                if(hudMsg == null)
                {
                    hudMsg = new HudAPIv2.HUDMessage(sb, new Vector2D(0.4, 0), HideHud: true, Scale: 1.2, Shadowing: true, ShadowColor: Color.Black, Blend: BlendTypeEnum.PostPP);

                    if(notify != null)
                        notify.Hide();
                }

                var textLen = hudMsg.GetTextLength();
                hudMsg.Offset = new Vector2D(0, -(textLen.Y / 2));

                if(!visible)
                {
                    visible = true;
                    hudMsg.Visible = true;
                }
            }
            else
            {
                if(notify == null)
                    notify = notify = MyAPIGateway.Utilities.CreateNotification("", int.MaxValue);

                notify.Hide(); // required since SE v1.194
                notify.Text = sb.ToString();

                if(!visible)
                {
                    visible = true;
                    notify.Show();
                }
            }
        }

        private void HideHUD()
        {
            if(!visible)
                return;

            visible = false;
            prevTurret = null;
            weaponTracker = null;

            if(hudMsg != null)
                hudMsg.Visible = false;

            if(notify != null)
                notify.Hide();
        }
    }
}