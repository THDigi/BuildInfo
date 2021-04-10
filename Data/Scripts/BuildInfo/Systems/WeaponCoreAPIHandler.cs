using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.ObjectBuilders;
using WeaponCore.Api;

namespace Digi.BuildInfo.Systems
{
    public class WeaponCoreAPIHandler : ModComponent
    {
        /// <summary>
        /// If WeaponCore was detected and replied.
        /// </summary>
        public bool IsRunning = false;

        public WcApi API { get; private set; }

        public readonly Dictionary<MyDefinitionId, WcApiDef.WeaponDefinition> Weapons = new Dictionary<MyDefinitionId, WcApiDef.WeaponDefinition>(MyDefinitionId.Comparer);

        public WeaponCoreAPIHandler(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            API = new WcApi();
            API.Load(Replied, getWeaponDefinitions: false);
        }

        private void Replied()
        {
            IsRunning = true;
            ParseDefinitions();
        }

        public override void UnregisterComponent()
        {
            API?.Unload();
            API = null;

            IsRunning = false;
        }

        public bool IsBlockWeapon(MyDefinitionId id)
        {
            return IsRunning && Weapons.ContainsKey(id);
        }

        void ParseDefinitions()
        {
            var weaponSubtypes = new Dictionary<string, List<MyObjectBuilderType>>();
            foreach(var def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                if(def is MyWeaponBlockDefinition || def is MyConveyorSorterDefinition)
                {
                    List<MyObjectBuilderType> types;
                    if(!weaponSubtypes.TryGetValue(def.Id.SubtypeName, out types))
                    {
                        types = new List<MyObjectBuilderType>();
                        weaponSubtypes[def.Id.SubtypeName] = types;
                    }

                    types.Add(def.Id.TypeId);
                }
            }

            var definitionsAsBytes = new List<byte[]>(32);
            API.GetAllWeaponDefinitions(definitionsAsBytes);

            for(int idx = 0; idx < definitionsAsBytes.Count; idx++)
            {
                byte[] bytes = definitionsAsBytes[idx];
                var weaponDef = MyAPIGateway.Utilities.SerializeFromBinary<WcApiDef.WeaponDefinition>(bytes);

                foreach(var mount in weaponDef.Assignments.MountPoints)
                {
                    string subtype = mount.SubtypeId;

                    // HACK: vanilla replacement. https://github.com/sstixrud/WeaponCore/blob/7051b905d13bc0b36f20aecc7ab9216de2121c6a/Data/Scripts/WeaponCore/Session/SessionSupport.cs#L785
                    switch(mount.SubtypeId)
                    {
                        case "LargeGatlingTurret": Weapons[new MyDefinitionId(typeof(MyObjectBuilder_LargeGatlingTurret), null)] = weaponDef; continue;
                        case "LargeMissileTurret": Weapons[new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), null)] = weaponDef; continue;
                        case "SmallGatlingGun": Weapons[new MyDefinitionId(typeof(MyObjectBuilder_SmallGatlingGun), null)] = weaponDef; continue;
                        case "SmallMissileLauncher": Weapons[new MyDefinitionId(typeof(MyObjectBuilder_SmallMissileLauncher), null)] = weaponDef; continue;
                    }

                    List<MyObjectBuilderType> types;
                    if(weaponSubtypes.TryGetValue(subtype, out types))
                    {
                        foreach(var type in types)
                        {
                            Weapons[new MyDefinitionId(type, subtype)] = weaponDef;
                        }
                    }
                    else
                    {
                        Log.Info($"WARNING: Couldn't find any weapon or conveyor sorter block with subtype '{subtype}' for WeaponCore, idx={idx.ToString()}, mod={weaponDef.ModPath}");
                    }
                }
            }
        }
    }
}