using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
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

        public readonly Dictionary<MyDefinitionId, List<WcApiDef.WeaponDefinition>> Weapons = new Dictionary<MyDefinitionId, List<WcApiDef.WeaponDefinition>>(MyDefinitionId.Comparer);

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
            var weaponSubtypes = new Dictionary<string, List<MyObjectBuilderType>>(32);

            foreach(var blockDef in Main.Caches.BlockDefs)
            {
                if(blockDef is MyWeaponBlockDefinition || blockDef is MyConveyorSorterDefinition)
                {
                    weaponSubtypes.GetOrAdd(blockDef.Id.SubtypeName).Add(blockDef.Id.TypeId);
                }
            }

            foreach(var physItemDef in Main.Caches.ItemDefs)
            {
                if(physItemDef is MyWeaponItemDefinition)
                {
                    weaponSubtypes.GetOrAdd(physItemDef.Id.SubtypeName).Add(physItemDef.Id.TypeId);
                }
            }

            var definitionsAsBytes = new List<byte[]>(32);
            API.GetAllWeaponDefinitions(definitionsAsBytes);

            for(int idx = 0; idx < definitionsAsBytes.Count; idx++)
            {
                byte[] bytes = definitionsAsBytes[idx];

                WcApiDef.WeaponDefinition weaponDef;
                try
                {
                    weaponDef = MyAPIGateway.Utilities.SerializeFromBinary<WcApiDef.WeaponDefinition>(bytes);
                }
                catch(Exception e)
                {
                    Log.Error($"Error deserializing WeaponCore definition bytes #{idx.ToString()} of {definitionsAsBytes.Count.ToString()}\n{e}");
                    continue;
                }

                foreach(var mount in weaponDef.Assignments.MountPoints)
                {
                    string subtype = mount.SubtypeId;

                    // HACK: vanilla replacement. https://github.com/sstixrud/WeaponCore/blob/7051b905d13bc0b36f20aecc7ab9216de2121c6a/Data/Scripts/WeaponCore/Session/SessionSupport.cs#L785
                    switch(mount.SubtypeId)
                    {
                        case "LargeGatlingTurret": Weapons.GetOrAdd(new MyDefinitionId(typeof(MyObjectBuilder_LargeGatlingTurret), null)).Add(weaponDef); continue;
                        case "LargeMissileTurret": Weapons.GetOrAdd(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), null)).Add(weaponDef); continue;
                        case "SmallGatlingGun": Weapons.GetOrAdd(new MyDefinitionId(typeof(MyObjectBuilder_SmallGatlingGun), null)).Add(weaponDef); continue;
                        case "SmallMissileLauncher": Weapons.GetOrAdd(new MyDefinitionId(typeof(MyObjectBuilder_SmallMissileLauncher), null)).Add(weaponDef); continue;
                    }

                    List<MyObjectBuilderType> types;
                    if(weaponSubtypes.TryGetValue(subtype, out types))
                    {
                        foreach(var type in types)
                        {
                            var defId = new MyDefinitionId(type, subtype);
                            Weapons.GetOrAdd(defId).Add(weaponDef);
                        }
                    }
                    else
                    {
                        Log.Info($"WARNING: Couldn't find any weapon item, weapon block, conveyor sorter with subtype '{subtype}' for WeaponCore, idx={idx.ToString()}, mod={weaponDef.ModPath}");
                    }
                }
            }

            foreach(var wcDefs in Weapons.Values)
            {
                wcDefs.TrimExcess();
            }
        }
    }
}