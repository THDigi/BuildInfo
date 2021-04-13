using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.ReloadTracker;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Definitions;
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
        private readonly Vector2D HudPosition = new Vector2D(0.4, 0);
        private const double TextScale = 1.2;
        private readonly Vector2D ShadowOffset = new Vector2D(0.002, -0.002);
        private const int SKIP_TICKS = 6; // ticks between text updates, min value 1.

        private IMyLargeTurretBase prevTurret;
        private MyWeaponBlockDefinition weaponBlockDef;
        private MyWeaponDefinition weaponDef;
        private Weapon weaponTracker;
        private bool weaponCoreBlock;

        private bool visible = false;
        private IMyHudNotification notify;
        private HudAPIv2.HUDMessage hudMsg;
        private HudAPIv2.HUDMessage shadowMsg;
        private StringBuilder sb;
        private StringBuilder shadowSb;

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
                    weaponTracker = null;
                    weaponBlockDef = null;
                    weaponDef = null;
                    weaponCoreBlock = Main.WeaponCoreAPIHandler.Weapons.ContainsKey(turret.BlockDefinition);

                    if(!weaponCoreBlock)
                    {
                        weaponTracker = Main.ReloadTracking.WeaponLookup.GetValueOrDefault(turret.EntityId, null);
                        weaponBlockDef = turret.SlimBlock.BlockDefinition as MyWeaponBlockDefinition;
                        weaponDef = (weaponBlockDef != null ? MyDefinitionManager.Static.GetWeaponDefinition(weaponBlockDef.WeaponDefinitionId) : null);
                    }

                    prevTurret = turret;

                    // TODO: add or not?
                    // NOTE: if add, must draw a crosshair... and must set these on all turrets beforehand to avoid the flashing
                    //var def = (MyLargeTurretBaseDefinition)turret.SlimBlock.BlockDefinition;
                    //string overlay = def.OverlayTexture;
                    //def.OverlayTexture = null;
                    //turret.OnAssumeControl(null);
                    //def.OverlayTexture = overlay;
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
            if(sb == null)
            {
                sb = new StringBuilder(256);
                shadowSb = new StringBuilder(256);
            }

            sb.Clear();

            if(weaponDef == null)
                return;

            var gun = turret as IMyGunObject<MyGunBase>;
            var magDef = gun?.GunBase?.CurrentAmmoMagazineDefinition;
            if(gun?.GunBase == null || magDef == null || !gun.GunBase.HasAmmoMagazines)
                return;

            var inv = turret.GetInventory();
            int loadedAmmo = gun.GunBase.CurrentAmmo; // rounds left in currently loaded magazine, has no relation to reloading!
            int mags = gun.GunBase.GetInventoryAmmoMagazinesCount();

            if(magDef.Capacity > 1)
            {
                // assume one mag is loaded for simplicty sake
                if(loadedAmmo == 0 && mags > 0)
                {
                    loadedAmmo = magDef.Capacity;
                    mags -= 1;
                }

                sb.Append("Loaded rounds: ").Append(loadedAmmo).NewCleanLine();
            }

            if(weaponTracker != null)
            {
                sb.Append("Shots until reload: ");
                var shotsLeft = weaponTracker.ShotsUntilReload;
                var internalMag = weaponTracker.InternalMagazineCapacity;

                if(weaponTracker.ReloadUntilTick > 0)
                    sb.Color(Color.Red);
                else if(shotsLeft <= (internalMag / 10))
                    sb.Color(new Color(255, 155, 0));
                else if(shotsLeft <= (internalMag / 4))
                    sb.Color(Color.Yellow);

                if(weaponTracker.ReloadUntilTick > 0)
                    sb.Append("Reloading");
                else
                    sb.Append(shotsLeft);

                sb.ResetFormatting().Color(Main.TextGeneration.COLOR_UNIMPORTANT).Append(" / ").Append(internalMag).NewCleanLine();
            }

            sb.Append("Inventory: ");

            int maxMags = 0;
            if(inv != null)
                maxMags = (int)Math.Floor((float)inv.MaxVolume / magDef.Volume);

            if(mags <= 0)
                sb.Color(Color.Red);
            else if(maxMags > 0 && mags <= (maxMags / 10))
                sb.Color(new Color(255, 155, 0));
            else if(maxMags > 0 && mags <= (maxMags / 4))
                sb.Color(Color.Yellow);

            sb.Append(mags).ResetFormatting();

            if(maxMags > 0)
                sb.Color(Main.TextGeneration.COLOR_UNIMPORTANT).Append(" / ").Append(maxMags);

            sb.Append(magDef.Capacity == 1 ? " rounds" : " mags").NewCleanLine();

            sb.Append("\n > ").Append(magDef.DisplayNameText).NewCleanLine();

            foreach(var otherMagId in weaponDef.AmmoMagazinesId)
            {
                if(otherMagId == magDef.Id)
                    continue;

                var otherMagDef = MyDefinitionManager.Static.GetAmmoMagazineDefinition(otherMagId);
                if(otherMagDef != null)
                    sb.Append("   ").Color(Main.TextGeneration.COLOR_UNIMPORTANT).Append(otherMagDef.DisplayNameText).NewCleanLine();
            }


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

            TextAPI.CopyWithoutColor(sb, shadowSb);
        }

        private void DrawHUD()
        {
            if(Main.TextAPI.IsEnabled)
            {
                if(sb == null)
                    return;

                if(hudMsg == null)
                {
                    shadowMsg = new HudAPIv2.HUDMessage(shadowSb, HudPosition, HideHud: true, Scale: TextScale, Blend: BlendTypeEnum.PostPP);
                    shadowMsg.InitialColor = Color.Black;

                    // needs to be created after to be rendered over
                    hudMsg = new HudAPIv2.HUDMessage(sb, HudPosition, HideHud: true, Scale: TextScale, Blend: BlendTypeEnum.PostPP);

                    notify?.Hide();
                }

                var textLen = hudMsg.GetTextLength();
                var offset = new Vector2D(0, textLen.Y / -2); // align left-middle
                hudMsg.Offset = offset;
                shadowMsg.Offset = offset + ShadowOffset;

                if(!visible)
                {
                    visible = true;
                    hudMsg.Visible = true;
                    shadowMsg.Visible = true;
                }
            }
            else
            {
                if(Main.IsPaused)
                    return; // HACK: avoid notification glitching out if showing them continuously when game is paused

                if(notify == null)
                    notify = MyAPIGateway.Utilities.CreateNotification("", 1000);

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

            weaponTracker = null;
            weaponBlockDef = null;
            weaponDef = null;

            visible = false;
            prevTurret = null;
            weaponTracker = null;

            if(hudMsg != null)
            {
                hudMsg.Visible = false;
                shadowMsg.Visible = false;
            }

            if(notify != null)
            {
                notify.Hide();
            }
        }
    }
}