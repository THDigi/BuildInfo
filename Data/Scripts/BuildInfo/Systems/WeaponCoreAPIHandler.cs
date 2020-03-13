using System.Collections.Generic;
using VRage.Game;
using WeaponCore.Api;

namespace Digi.BuildInfo.Systems
{
    public class WeaponCoreAPIHandler : ModComponent
    {
        /// <summary>
        /// If WeaponCore was detected and replied.
        /// </summary>
        public bool IsRunning = false;

        public WcApi API;

        public HashSet<MyDefinitionId> BlocksWithWeapons = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        public WeaponCoreAPIHandler(BuildInfoMod main) : base(main)
        {
        }

        protected override void RegisterComponent()
        {
            API = new WcApi();
            API.Load(Replied, getWeaponDefinitions: false);
        }

        private void Replied()
        {
            IsRunning = true;
            API.GetAllCoreWeapons(BlocksWithWeapons);
        }

        protected override void UnregisterComponent()
        {
            if(API != null)
            {
                API.Unload();
                API = null;
            }

            IsRunning = false;
        }

        public bool IsBlockWeapon(MyDefinitionId id)
        {
            return IsRunning && BlocksWithWeapons.Contains(id);
        }
    }
}