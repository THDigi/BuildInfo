using System;
using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo.Features.ReloadTracker
{
    public class ReloadTracking : ModComponent
    {
        /// <summary>
        /// Tracked weapon by block EntityId lookup
        /// </summary>
        public readonly Dictionary<long, TrackedWeapon> WeaponLookup = new Dictionary<long, TrackedWeapon>();

        /// <summary>
        /// Weapons per grid entityId as a fast "nearby" check.
        /// </summary>
        readonly Dictionary<long, HashSet<TrackedWeapon>> ProjectileWeaponsPerGrid = new Dictionary<long, HashSet<TrackedWeapon>>();

        /// <summary>
        /// Scheduled by missile and projectile shoot to check if it actually fired.
        /// </summary>
        readonly HashSet<TrackedWeapon> NextTickUpdate = new HashSet<TrackedWeapon>();

        /// <summary>
        /// For projectile code to have an early exit if it's not a relevant weapon definition
        /// </summary>
        readonly HashSet<MyDefinitionId> ReloadableWeaponDefs = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        readonly List<MyEntity> TempEntities = new List<MyEntity>();

        readonly MyConcurrentPool<TrackedWeapon> WeaponPool = new MyConcurrentPool<TrackedWeapon>();

        public ReloadTracking(BuildInfoMod main) : base(main)
        {
            RegisterBlockMonitoring();
            FindReloadableDefinitions();
        }

        public override void RegisterComponent()
        {
            MyAPIGateway.Missiles.OnMissileAdded += MissileSpawned;
            MyAPIGateway.Projectiles.OnProjectileAdded += ProjectileSpawned;
        }

        public override void UnregisterComponent()
        {
            NextTickUpdate.Clear();
            WeaponLookup.Clear();
            WeaponPool.Clean();

            MyAPIGateway.Missiles.OnMissileAdded -= MissileSpawned;
            MyAPIGateway.Projectiles.OnProjectileAdded -= ProjectileSpawned;
        }

        void FindReloadableDefinitions()
        {
            foreach(MyCubeBlockDefinition def in Main.Caches.BlockDefs)
            {
                var weaponBlockDef = def as MyWeaponBlockDefinition;
                if(weaponBlockDef == null)
                    continue;

                if(ReloadableWeaponDefs.Contains(weaponBlockDef.WeaponDefinitionId))
                    continue; // already computed

                if(!Hardcoded.ReloadableBlockTypes.Contains(def.Id.TypeId))
                    continue; // block type cannot reload

                MyWeaponDefinition weaponDef;
                if(!MyDefinitionManager.Static.TryGetWeaponDefinition(weaponBlockDef.WeaponDefinitionId, out weaponDef))
                    continue;

                if(weaponDef.ReloadTime == 0 || !weaponDef.HasAmmoMagazines())
                    continue;

                int projectileShotsInBurst = 0;
                int missileShotsInBurst = 0;

                if(weaponDef.HasProjectileAmmoDefined)
                    projectileShotsInBurst = weaponDef.WeaponAmmoDatas[(int)MyAmmoType.HighSpeed].ShotsInBurst;

                if(weaponDef.HasMissileAmmoDefined)
                    missileShotsInBurst = weaponDef.WeaponAmmoDatas[(int)MyAmmoType.Missile].ShotsInBurst;

                if(projectileShotsInBurst == 0 && missileShotsInBurst == 0)
                    continue;

                ReloadableWeaponDefs.Add(weaponBlockDef.WeaponDefinitionId);
            }
        }

        #region Block spawn tracking
        void RegisterBlockMonitoring()
        {
            BlockMonitor.CallbackDelegate action = new BlockMonitor.CallbackDelegate(WeaponBlockAdded);

            foreach(MyObjectBuilderType type in Hardcoded.ReloadableBlockTypes)
            {
                Main.BlockMonitor.MonitorType(type, action);
            }
        }

        void WeaponBlockAdded(IMySlimBlock block)
        {
            if(block.CubeGrid?.Physics == null)
                return; // no tracking for ghost grids

            IMyUserControllableGun gunBlock = block.FatBlock as IMyUserControllableGun;
            if(gunBlock == null)
                return; // ignore weirdness

            if(Main.CoreSystemsAPIHandler.Weapons.ContainsKey(block.BlockDefinition.Id))
                return; // no tracking of weaponcore blocks

            if(WeaponLookup.ContainsKey(gunBlock.EntityId))
                return; // ignore grid merge/split if gun is already tracked

            TrackedWeapon tw = WeaponPool.Get();
            if(!tw.Init(gunBlock))
            {
                tw.Clear();
                WeaponPool.Return(tw);
                return;
            }

            WeaponLookup.Add(gunBlock.EntityId, tw);

            gunBlock.OnMarkForClose += GunBlockMarkedForClose;

            // see reason in ProjectileSpawned()
            if(tw.Gun.GunBase.HasProjectileAmmoDefined)
            {
                ProjectileWeaponsPerGrid.GetValueOrNew(tw.Block.CubeGrid.EntityId).Add(tw);

                // does not pass block ref so we capturing it like this, doesn't really need to unhook anyway
                gunBlock.CubeGridChanged += (oldGrid) =>
                {
                    try
                    {
                        ProjectileWeaponsPerGrid.GetValueOrDefault(oldGrid.EntityId)?.Remove(tw);
                        ProjectileWeaponsPerGrid.GetValueOrNew(tw.Block.CubeGrid.EntityId).Add(tw);
                    }
                    catch(Exception e)
                    {
                        Log.Error(e);
                    }
                };
            }
        }

        void GunBlockMarkedForClose(IMyEntity ent)
        {
            try
            {
                TrackedWeapon tw = WeaponLookup.GetValueOrDefault(ent.EntityId);
                if(tw == null)
                    return;

                WeaponLookup.Remove(ent.EntityId);
                ProjectileWeaponsPerGrid.GetValueOrDefault(tw.Block.CubeGrid.EntityId)?.Remove(tw);

                tw.Clear();
                WeaponPool.Return(tw);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
        #endregion

        #region Shoot tracking
        int IgnoreNextEvents = 0;

        void ProjectileSpawned(ref MyProjectileInfo projectile, int index)
        {
            if(IgnoreNextEvents > 0)
            {
                IgnoreNextEvents--;
                return;
            }

            try
            {
                //DebugLog.PrintHUD(this, $"projectile spawned; ent={projectile.OwnerEntity?.ToString() ?? "null"}; entAbs={projectile.OwnerEntityAbsolute?.ToString() ?? "null"}; player={projectile.OwningPlayer}");
                // because the projectile does not give the weapon block (ent is grid),
                //   I have to check all weapons on said grid or nearby as a fallback


                // ProjectileCount just does it in a for loop and in main thread so it should be safe to ignore immediate events.
                var ammoDef = (MyProjectileAmmoDefinition)projectile.ProjectileAmmoDefinition;
                if(ammoDef.ProjectileCount > 1)
                {
                    IgnoreNextEvents = ammoDef.ProjectileCount - 1;
                }

                IMyEntity ownerEnt = projectile.OwnerEntity;
                if(ownerEnt is IMyCharacter)
                    return;

                // weapon def not configured reloadable or not present in a reload-supporting block
                if(projectile.WeaponDefinition != null && !ReloadableWeaponDefs.Contains(projectile.WeaponDefinition.Id))
                    return;

                const double CheckDistance = 10;

                Vector3D origin = projectile.Position; // HACK: Position and Origin are flipped when fed to the MyProjectileInfo constructor in game code.

                if(ownerEnt != null)
                {
                    var grid = ownerEnt as IMyCubeGrid;
                    if(grid != null)
                    {
                        HashSet<TrackedWeapon> set = ProjectileWeaponsPerGrid.GetValueOrDefault(grid.EntityId);
                        if(set != null)
                        {
                            foreach(TrackedWeapon tw in set)
                            {
                                if(Vector3D.DistanceSquared(origin, tw.Block.GetPosition()) < CheckDistance * CheckDistance)
                                {
                                    NextTickUpdate.Add(tw);
                                }
                            }
                        }

                        return;
                    }

                    // unlikely xD
                    var block = ownerEnt as IMyCubeBlock;
                    if(block != null)
                    {
                        TrackedWeapon tw = WeaponLookup.GetValueOrDefault(block.EntityId);
                        if(tw != null)
                        {
                            NextTickUpdate.Add(tw);
                        }

                        return;
                    }
                }

                #region Find nearby grids and check their closest weapons
                {
                    BoundingSphereD sphere = new BoundingSphereD(origin, CheckDistance);

                    TempEntities.Clear();
                    MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, TempEntities, MyEntityQueryType.Both);

                    foreach(MyEntity ent in TempEntities)
                    {
                        IMyCubeGrid grid = ent as IMyCubeGrid;
                        if(grid == null)
                            continue;

                        HashSet<TrackedWeapon> set = ProjectileWeaponsPerGrid.GetValueOrDefault(grid.EntityId);
                        if(set == null)
                            continue;

                        foreach(TrackedWeapon tw in set)
                        {
                            if(Vector3D.DistanceSquared(origin, tw.Block.GetPosition()) < CheckDistance * CheckDistance)
                            {
                                NextTickUpdate.Add(tw);
                            }
                        }
                    }
                }
                #endregion
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
            finally
            {
                TempEntities.Clear();

                if(NextTickUpdate.Count > 0)
                    SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
            }
        }

        void MissileSpawned(IMyMissile missile)
        {
            try
            {
                TrackedWeapon tw;
                if(missile.LauncherId == 0 || !WeaponLookup.TryGetValue(missile.LauncherId, out tw))
                    return;

                //NextTickUpdate.Add(tw);
                //SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);

                // HACK: replaced above because gunbase.LastShootTime does not get set clientside for missile shoots (only projectiles)
                // this however is bad if something else spawns a missile that isn't actually shot by the launcher
                if(--tw.ShotsUntilReload == 0)
                {
                    tw.ShotsUntilReload = tw.MissileShotsInBurst;
                    tw.ReloadUntilTick = Main.Tick + tw.ReloadDurationTicks;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateAfterSim(int tick)
        {
            try
            {
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);

                foreach(TrackedWeapon tw in NextTickUpdate)
                {
                    if(tw.Block == null || tw.Block.MarkedForClose)
                        continue;

                    // Cannot use IMyGunObject<T>.GetAmmunitionAmount() because:
                    // - only serverside
                    // - it lies, it's not actually the shots before a reload.
                    // because for block weapons ShotsInBurst is what controls how many rounds until reload,
                    //   not magazine size like it does on hand weapons.

                    MyGunBase gunbase = tw.Gun?.GunBase;
                    if(gunbase == null)
                        continue;

                    // WARNING: only gets set for projectile shots!
                    long lastShotTime = gunbase.LastShootTime.Ticks;
                    if(tw.LastShotTime < lastShotTime)
                    {
                        tw.LastShotTime = lastShotTime;

                        if(--tw.ShotsUntilReload == 0)
                        {
                            // NOTE: a bug in game code allows you to go over max shots by switching to other ammo type (projectile<>missile).
                            if(gunbase.IsAmmoProjectile)
                                tw.ShotsUntilReload = tw.ProjectileShotsInBurst;
                            else if(gunbase.IsAmmoMissile)
                                tw.ShotsUntilReload = tw.MissileShotsInBurst;

                            tw.ReloadUntilTick = tick + tw.ReloadDurationTicks;
                        }
                    }
                }
            }
            finally
            {
                NextTickUpdate.Clear();
            }
        }
        #endregion
    }
}