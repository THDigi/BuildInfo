using System;
using System.Collections.Generic;
using CoreSystems.Api;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.Systems
{
    public class CoreSystemsAPIHandler : ModComponent
    {
        /// <summary>
        /// If WeaponCore was detected and replied.
        /// </summary>
        public bool IsRunning = false;

        public const string APIName = "CoreSystems";

        public CoreSystemsAPI API { get; private set; }

        public readonly Dictionary<MyDefinitionId, List<CoreSystemsDef.WeaponDefinition>> Weapons = new Dictionary<MyDefinitionId, List<CoreSystemsDef.WeaponDefinition>>(MyDefinitionId.Comparer);

        public readonly Dictionary<string, CoreSystemsDef.ArmorDefinition> Armor = new Dictionary<string, CoreSystemsDef.ArmorDefinition>();

        public CoreSystemsAPIHandler(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            API = new CoreSystemsAPI();
            API.Load(Replied, getWeaponDefinitions: false);
        }

        private void Replied()
        {
            try
            {
                IsRunning = true;
                ParseDefinitions();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UnregisterComponent()
        {
            API?.Unload();
            API = null;

            IsRunning = false;
        }

        void ParseDefinitions()
        {
            Dictionary<string, List<MyObjectBuilderType>> weaponSubtypes = new Dictionary<string, List<MyObjectBuilderType>>(32);

            foreach(MyCubeBlockDefinition blockDef in Main.Caches.BlockDefs)
            {
                if(blockDef is MyWeaponBlockDefinition || blockDef is MyConveyorSorterDefinition)
                {
                    weaponSubtypes.GetValueOrNew(blockDef.Id.SubtypeName).Add(blockDef.Id.TypeId);
                }
            }

            List<byte[]> definitionsAsBytes = new List<byte[]>(32);
            API.GetAllWeaponDefinitions(definitionsAsBytes);

            for(int idx = 0; idx < definitionsAsBytes.Count; idx++)
            {
                byte[] bytes = definitionsAsBytes[idx];

                CoreSystemsDef.WeaponDefinition weaponDef;
                try
                {
                    weaponDef = MyAPIGateway.Utilities.SerializeFromBinary<CoreSystemsDef.WeaponDefinition>(bytes);
                }
                catch(Exception e)
                {
                    Log.Error($"Error deserializing {APIName} weapon definition bytes #{idx.ToString()} of {definitionsAsBytes.Count.ToString()}\n{e}");
                    continue;
                }

                if(weaponDef.HardPoint.HardWare.Type == CoreSystemsDef.WeaponDefinition.HardPointDef.HardwareDef.HardwareType.BlockWeapon)
                {
                    foreach(CoreSystemsDef.WeaponDefinition.ModelAssignmentsDef.MountPointDef mount in weaponDef.Assignments.MountPoints)
                    {
                        string subtype = mount.SubtypeId;

                        // HACK: vanilla replacement, SessionSupport.LoadVanillaData() https://github.com/sstixrud/WeaponCore/blob/master/Data/Scripts/CoreSystems/Session/SessionSupport.cs#L1104
                        switch(mount.SubtypeId)
                        {
                            case "LargeGatlingTurret": Weapons.GetValueOrNew(new MyDefinitionId(typeof(MyObjectBuilder_LargeGatlingTurret), null)).Add(weaponDef); continue;
                            case "LargeMissileTurret": Weapons.GetValueOrNew(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), null)).Add(weaponDef); continue;
                            case "SmallGatlingGun": Weapons.GetValueOrNew(new MyDefinitionId(typeof(MyObjectBuilder_SmallGatlingGun), null)).Add(weaponDef); continue;
                            case "SmallMissileLauncher": Weapons.GetValueOrNew(new MyDefinitionId(typeof(MyObjectBuilder_SmallMissileLauncher), null)).Add(weaponDef); continue;
                        }

                        List<MyObjectBuilderType> types;
                        if(weaponSubtypes.TryGetValue(subtype, out types))
                        {
                            foreach(MyObjectBuilderType type in types)
                            {
                                MyDefinitionId defId = new MyDefinitionId(type, subtype);
                                Weapons.GetValueOrNew(defId).Add(weaponDef);
                            }
                        }
                        else
                        {
                            Log.Info($"WARNING: Couldn't find any weapon block or conveyor sorter with subtype '{subtype}' for {APIName}, idx={idx.ToString()}, mod={weaponDef.ModPath}");
                        }
                    }
                }
                else if(weaponDef.HardPoint.HardWare.Type == CoreSystemsDef.WeaponDefinition.HardPointDef.HardwareDef.HardwareType.HandWeapon)
                {
                    foreach(CoreSystemsDef.WeaponDefinition.ModelAssignmentsDef.MountPointDef mount in weaponDef.Assignments.MountPoints)
                    {
                        string subtype = mount.SubtypeId;

                        MyDefinitionId defId = new MyDefinitionId(typeof(MyObjectBuilder_PhysicalGunObject), subtype);
                        MyPhysicalItemDefinition physDef;
                        if(MyDefinitionManager.Static.TryGetPhysicalItemDefinition(defId, out physDef))
                        {
                            Weapons.GetValueOrNew(defId).Add(weaponDef);
                        }
                        else
                        {
                            Log.Info($"WARNING: Couldn't find any PhysicalGunObject with subtype '{subtype}' for {APIName}, idx={idx.ToString()}, mod={weaponDef.ModPath}");
                        }
                    }
                }
            }

            foreach(List<CoreSystemsDef.WeaponDefinition> wcDefs in Weapons.Values)
            {
                wcDefs.TrimExcess();
            }

            definitionsAsBytes.Clear();
            API.GetAllCoreArmors(definitionsAsBytes);

            for(int idx = 0; idx < definitionsAsBytes.Count; idx++)
            {
                byte[] bytes = definitionsAsBytes[idx];

                CoreSystemsDef.ArmorDefinition armorDef;
                try
                {
                    armorDef = MyAPIGateway.Utilities.SerializeFromBinary<CoreSystemsDef.ArmorDefinition>(bytes);
                }
                catch(Exception e)
                {
                    Log.Error($"Error deserializing {APIName} armor definition bytes #{idx.ToString()} of {definitionsAsBytes.Count.ToString()}\n{e}");
                    continue;
                }

                foreach(string subtypeId in armorDef.SubtypeIds)
                {
                    CoreSystemsDef.ArmorDefinition existsInDef;
                    if(Armor.TryGetValue(subtypeId, out existsInDef))
                    {
                        Log.Error($"Error in {APIName} armor definition, {subtypeId} is used by multiple armor definitions! Also in: kind={existsInDef.Kind.ToString()}; ids={string.Join(",", existsInDef.SubtypeIds)}");
                        continue;
                    }

                    Armor.Add(subtypeId, armorDef);
                }
            }

            Log.Info($"{APIName} API registered. Got {Weapons.Count.ToString()} weapons and {Armor.Count.ToString()} armor definitions.");
        }
    }
}