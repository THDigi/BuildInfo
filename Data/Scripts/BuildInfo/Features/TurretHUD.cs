using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.ReloadTracker;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features
{
    // TODO: show ammo for CTC?

    public class TurretHUD : ModComponent
    {
        readonly Vector2D AmmoTextPosition = new Vector2D(0.4, 0.01);
        const double AmmoTextScale = 1.2;

        readonly Vector2D HudTextPosition = new Vector2D(0.4, -0.01);
        const double HudTextScale = 0.8;

        readonly Vector2D ShadowOffset = new Vector2D(0.002, -0.002);

        const bool UseShadowMessage = false;
        const string TextFont = FontsHandler.BI_SEOutlined;

        const int SkipTicks = 6; // ticks between text updates, min value 1.

        IMyLargeTurretBase prevTurret;
        MyWeaponBlockDefinition weaponBlockDef;
        MyWeaponDefinition weaponDef;
        TrackedWeapon weaponTracker;
        bool weaponCoreBlock;

        bool visible = false;
        IMyHudNotification notify;
        StringBuilder notifySB;

        TextAPI.TextPackage AmmoText;
        TextAPI.TextPackage HudText;

        readonly MyStringId MaterialSquare = MyStringId.GetOrCompute("Square");
        readonly MyStringId MaterialDot = MyStringId.GetOrCompute("WhiteDot");

        public TurretHUD(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.EquipmentMonitor.ControlledChanged += ComputeUpdateRequirements;
            Main.Config.TurretHUD.ValueAssigned += SettingChanged;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.EquipmentMonitor.ControlledChanged -= ComputeUpdateRequirements;
            Main.Config.TurretHUD.ValueAssigned -= SettingChanged;
        }

        void SettingChanged(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            ComputeUpdateRequirements(MyAPIGateway.Session.ControlledObject);
        }

        void ComputeUpdateRequirements(VRage.Game.ModAPI.Interfaces.IMyControllableEntity controlled)
        {
            IMyLargeTurretBase turret = (Main.Config.TurretHUD.Value ? controlled as IMyLargeTurretBase : null);
            bool updateSim = (turret != null);
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, updateSim);

            if(!updateSim)
                HideHUD();

            IMyCockpit cockpit = (updateSim ? MyAPIGateway.Session.Player?.Character?.Parent as IMyCockpit : null);
            bool updateDraw = (cockpit != null && cockpit.IsSameConstructAs(turret));
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, updateDraw);
        }

        // draw ship relative direction indicator
        public override void UpdateDraw()
        {
            IMyLargeTurretBase turret = MyAPIGateway.Session.ControlledObject as IMyLargeTurretBase;
            if(turret == null)
                return;

            IMyCockpit cockpit = MyAPIGateway.Session.Player?.Character?.Parent as IMyCockpit;
            if(cockpit == null)
                return;

            if(!cockpit.IsSameConstructAs(turret))
                return; // different ships

            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;

            Vector3D pos = Main.DrawUtils.TextAPIHUDtoWorld(new Vector2D(0, -0.5));

            float scaleFOV = Main.DrawUtils.ScaleFOV;
            float length = 0.01f * scaleFOV;
            float thick = 0.0002f * scaleFOV;

            Vector3D screenToPosDir = Vector3D.Normalize(pos - camMatrix.Translation);
            double angle = Math.Acos(Vector3D.Dot(screenToPosDir, camMatrix.Forward));
            Vector3D shipForwardScreen = Vector3D.TransformNormal(cockpit.WorldMatrix.Forward, MatrixD.CreateFromAxisAngle(camMatrix.Right, -angle));

            double dirDot = Vector3D.Dot(camMatrix.Forward, shipForwardScreen);
            Color color = Color.Lerp(Color.Red, Color.Lime, (float)((1 + dirDot) / 2));

            MyTransparentGeometry.AddLineBillboard(MaterialSquare, color, pos, (Vector3)shipForwardScreen, length, thick, BlendTypeEnum.PostPP);

            float radius = 0.0005f * scaleFOV;
            MyTransparentGeometry.AddPointBillboard(MaterialDot, color, pos, radius, 0, blendType: BlendTypeEnum.PostPP);
        }

        public override void UpdateAfterSim(int tick)
        {
            if(tick % SkipTicks != 0)
                return;

            IMyLargeTurretBase turret = MyAPIGateway.Session.ControlledObject as IMyLargeTurretBase;
            if(turret != null)
            {
                if(prevTurret != turret)
                {
                    weaponTracker = null;
                    weaponBlockDef = null;
                    weaponDef = null;
                    weaponCoreBlock = Main.CoreSystemsAPIHandler.Weapons.ContainsKey(turret.BlockDefinition);

                    if(!weaponCoreBlock)
                    {
                        weaponTracker = Main.ReloadTracking.WeaponLookup.GetValueOrDefault(turret.EntityId, null);
                        weaponBlockDef = turret.SlimBlock.BlockDefinition as MyWeaponBlockDefinition;
                        weaponDef = (weaponBlockDef != null ? MyDefinitionManager.Static.GetWeaponDefinition(weaponBlockDef.WeaponDefinitionId) : null);

                        if(weaponDef == null || !weaponDef.HasAmmoMagazines())
                        {
                            weaponTracker = null;
                            weaponBlockDef = null;
                            weaponDef = null;
                        }
                    }

                    prevTurret = turret;

                    // TODO: add or not?
                    // NOTE: if add, must draw a crosshair... and must set these on all turrets beforehand to avoid the flashing
                    //MyLargeTurretBaseDefinition def = (MyLargeTurretBaseDefinition)turret.SlimBlock.BlockDefinition;
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

        void GenerateText(IMyLargeTurretBase turret)
        {
            if(weaponDef == null)
                return;

            IMyGunObject<MyGunBase> gun = turret as IMyGunObject<MyGunBase>;
            MyAmmoMagazineDefinition magDef = gun?.GunBase?.CurrentAmmoMagazineDefinition;
            if(gun?.GunBase == null || magDef == null || !gun.GunBase.HasAmmoMagazines)
                return;

            int loadedAmmo = gun.GunBase.CurrentAmmo; // rounds left in currently loaded magazine, has no relation to reloading!
            int magsInInv = gun.GunBase.GetInventoryAmmoMagazinesCount();
            int totalAmmo = loadedAmmo + (magsInInv * magDef.Capacity);

            // assume one mag is loaded for simplicty sake
            if(magDef.Capacity > 1 && loadedAmmo == 0 && magsInInv > 0)
            {
                loadedAmmo = magDef.Capacity;
                magsInInv -= 1;
            }

            int maxMags = 0;
            IMyInventory inv = turret.GetInventory();
            if(inv != null)
            {
                double invCap = (double)inv.MaxVolume + 0.0001f; // HACK: ensure items that fit perfectly get detected
                maxMags = (int)Math.Floor(invCap / magDef.Volume);
            }

            if(Main.TextAPI.IsEnabled)
            {
                #region Simple ammo indicator
                if(AmmoText == null)
                {
                    AmmoText = new TextAPI.TextPackage(256, useShadow: UseShadowMessage);
                    AmmoText.Font = TextFont;
                    AmmoText.Position = AmmoTextPosition;
                    AmmoText.Scale = AmmoTextScale;

                    notify?.Hide();
                }

                StringBuilder ammoSB = AmmoText.TextStringBuilder.Clear();

                if(totalAmmo == 0)
                {
                    ammoSB.Color(Color.Red).Append("No ammo!");
                }
                else if(weaponTracker != null) // supports reloading
                {
                    int shotsLeft = weaponTracker.ShotsUntilReload;
                    int current = Math.Min(shotsLeft, totalAmmo);
                    int internalMagSize = weaponTracker.InternalMagazineCapacity;

                    if(weaponTracker.ReloadUntilTick > 0)
                    {
                        ammoSB.Color(Color.Red).Append("Reloading");
                    }
                    else
                    {
                        if(current <= 0)
                            ammoSB.Color(Color.Red);
                        else if(current <= (internalMagSize / 10))
                            ammoSB.Color(new Color(255, 155, 0));
                        else if(current <= (internalMagSize / 4))
                            ammoSB.Color(Color.Yellow);

                        ammoSB.Number(current).Append(" rounds");
                    }

                    totalAmmo -= current; // intended to affect the other texts

                    //ammoSB.Color(Color.Gray).Append(" / ").Number(totalAmmo);
                }
                else
                {
                    // limit the max rounds only in the coloring context, as interior turret can fit thousands of magazines...
                    int maxRounds = Math.Min(maxMags, (300 / magDef.Capacity)) * magDef.Capacity;

                    if(totalAmmo <= 0)
                        ammoSB.Color(Color.Red);
                    else if(totalAmmo > 0 && totalAmmo <= (maxRounds / 10))
                        ammoSB.Color(new Color(255, 155, 0));
                    else if(totalAmmo > 0 && totalAmmo <= (maxRounds / 4))
                        ammoSB.Color(Color.Yellow);

                    ammoSB.Number(totalAmmo);
                }

                if(UseShadowMessage)
                    TextAPI.CopyWithoutColor(AmmoText.TextStringBuilder, AmmoText.ShadowStringBuilder);
                #endregion

                #region Other text
                if(HudText == null)
                {
                    HudText = new TextAPI.TextPackage(256, useShadow: UseShadowMessage);
                    HudText.Position = HudTextPosition;
                    HudText.Scale = HudTextScale;
                }

                StringBuilder hudSB = HudText.TextStringBuilder.Clear();

                // only show inventory if weapon can be reloaded, otherwise total ammo is shown above
                if(weaponTracker != null)
                    hudSB.Append("Inventory: ").Number(totalAmmo).Append(" rounds").NewCleanLine();

#if false
                if(magDef.Capacity > 1)
                {
                    hudSB.Append("Loaded rounds: ").Append(loadedAmmo).NewCleanLine();
                }

                if(weaponTracker != null)
                {
                    hudSB.Append("Shots until reload: ");
                    int shotsLeft = weaponTracker.ShotsUntilReload;
                    int internalMag = weaponTracker.InternalMagazineCapacity;

                    //if(weaponTracker.ReloadUntilTick > 0)
                    //    hudSB.Color(Color.Red);
                    //else if(shotsLeft <= (internalMag / 10))
                    //    hudSB.Color(new Color(255, 155, 0));
                    //else if(shotsLeft <= (internalMag / 4))
                    //    hudSB.Color(Color.Yellow);

                    if(weaponTracker.ReloadUntilTick > 0)
                        hudSB.Append("Reloading");
                    else
                        hudSB.Append(shotsLeft);

                    hudSB.ResetFormatting().Color(Main.TextGeneration.COLOR_UNIMPORTANT).Append(" / ").Append(internalMag).NewCleanLine();
                }

                hudSB.Append("Inventory: ");

                //if(mags <= 0)
                //    hudSB.Color(Color.Red);
                //else if(maxMags > 0 && mags <= (maxMags / 10))
                //    hudSB.Color(new Color(255, 155, 0));
                //else if(maxMags > 0 && mags <= (maxMags / 4))
                //    hudSB.Color(Color.Yellow);

                hudSB.Append(magsInInv).ResetFormatting();

                if(maxMags > 0)
                    hudSB.Color(Main.TextGeneration.COLOR_UNIMPORTANT).Append(" / ").Append(maxMags);

                hudSB.Append(magDef.Capacity == 1 ? " rounds" : " mags").NewCleanLine();
#endif


                hudSB.Append('\n');

                bool hasMultipleMags = (weaponDef.AmmoMagazinesId.Length > 1);
                if(hasMultipleMags)
                    hudSB.Append("> ");

                hudSB.Append(magDef.DisplayNameText).NewLine();

                if(hasMultipleMags)
                    hudSB.Color(Main.TextGeneration.COLOR_UNIMPORTANT);

                foreach(MyDefinitionId otherMagId in weaponDef.AmmoMagazinesId)
                {
                    if(otherMagId == magDef.Id)
                        continue;

                    MyAmmoMagazineDefinition otherMagDef = Utils.TryGetMagazineDefinition(otherMagId);
                    if(otherMagDef != null)
                        hudSB.Append("   ").Append(otherMagDef.DisplayNameText).NewLine();
                }

                //hudSB.ResetFormatting();

#if false // TODO: toggleable between showing vanilla HUD and showing this?
                //IMyCubeGrid grid = turret.CubeGrid;

                //if(!grid.IsStatic)
                //{
                //    sb.NewLine();

                //    sb.Append("Ship speed: ").SpeedFormat(grid.Physics.LinearVelocity.Length()).NewLine();

                //    IMyCockpit cockpit = MyAPIGateway.Session.Player?.Character?.Parent as IMyCockpit;

                //    if(cockpit != null && cockpit.IsSameConstructAs(turret))
                //    {
                //        MyCockpit internalCockpit = (MyCockpit)cockpit;

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
#endif

                if(UseShadowMessage)
                    TextAPI.CopyWithoutColor(HudText.TextStringBuilder, HudText.ShadowStringBuilder);
                #endregion
            }
            else
            {
                if(notifySB == null)
                    notifySB = new StringBuilder(64);

                notifySB.Clear().Append("Ammo: ");

                if(weaponTracker != null)
                {
                    int shotsLeft = weaponTracker.ShotsUntilReload;
                    int current = Math.Min(shotsLeft, totalAmmo);

                    if(weaponTracker.ReloadUntilTick > 0)
                        notifySB.Append("Reloading");
                    else
                        notifySB.Number(current);

                    notifySB.Append(" / ").Number(totalAmmo - current);
                }
                else
                {
                    notifySB.Number(totalAmmo);
                }
            }
        }

        void DrawHUD()
        {
            if(Main.TextAPI.IsEnabled)
            {
                if(AmmoText == null)
                    return;

                Vector2D ammoTextLen = AmmoText.Text.GetTextLength();
                Vector2D ammoTextOffset = new Vector2D(0, -ammoTextLen.Y); // pivot left-bottom
                AmmoText.Text.Offset = ammoTextOffset;

                if(UseShadowMessage)
                    AmmoText.Shadow.Offset = ammoTextOffset + ShadowOffset;

                // no more pivot as the ammo is moved above this text
                //Vector2D hudTextLen = hudText.GetTextLength();
                //Vector2D hudTextOffset = new Vector2D(0, hudTextLen.Y / -2); // pivot left-center
                //hudText.Offset = hudTextOffset;
                //hudShadow.Offset = hudTextOffset + ShadowOffset;

                if(!visible)
                {
                    visible = true;
                    AmmoText.Visible = true;
                    HudText.Visible = true;
                }
            }
            else
            {
                if(Main.IsPaused)
                    return; // HACK: avoid notification glitching out if showing them continuously when game is paused

                if(notify == null)
                    notify = MyAPIGateway.Utilities.CreateNotification("", 1000);

                notify.Hide(); // required since SE v1.194
                notify.Text = notifySB.ToString();
                notify.Show();

                visible = true;
            }
        }

        void HideHUD()
        {
            if(!visible)
                return;

            weaponTracker = null;
            weaponBlockDef = null;
            weaponDef = null;

            visible = false;
            prevTurret = null;
            weaponTracker = null;

            if(AmmoText != null)
            {
                AmmoText.Visible = false;
                HudText.Visible = false;
            }

            if(notify != null)
            {
                notify.Hide();
            }
        }
    }
}