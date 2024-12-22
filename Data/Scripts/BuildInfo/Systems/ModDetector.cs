using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRageMath;
using ModId = VRage.MyTuple<string, ulong>;

namespace Digi.BuildInfo.Systems
{
    public class ModDetector : ModComponent
    {
        public bool DetectedAwwScrap = false;

        public ModDetector(BuildInfoMod main) : base(main)
        {
            FindSpecificMods();
            //FindPluginLoaderMods();
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
        }

        void FindSpecificMods()
        {
            foreach(MyObjectBuilder_Checkpoint.ModItem modItem in MyAPIGateway.Session.Mods)
            {
                switch(modItem.PublishedFileId)
                {
                    case 1542310718:
                        DetectedAwwScrap = true;
                        break;
                }
            }
        }

        //void FindPluginLoaderMods()
        //{
        //    MyTupleComparer<string, ulong> comparer = new MyTupleComparer<string, ulong>();
        //    int capacity = MathHelper.GetNearestBiggerPowerOfTwo(MyAPIGateway.Session.Mods.Count + 4);
        //
        //    HashSet<ModId> realMods = new HashSet<ModId>(capacity, comparer);
        //    HashSet<ModId> unknownMods = new HashSet<ModId>(4);
        //
        //    foreach(MyObjectBuilder_Checkpoint.ModItem modItem in MyAPIGateway.Session.Mods)
        //    {
        //        ulong publishedId = modItem.PublishedFileId;
        //        if(publishedId == 0)
        //            continue; // skip local mods
        //
        //        realMods.Add(new ModId(modItem.PublishedServiceName, publishedId));
        //    }
        //
        //    foreach(MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
        //    {
        //        if(def.Context == null)
        //            continue;
        //
        //        if(def.Context.IsBaseGame)
        //            continue;
        //
        //        MyObjectBuilder_Checkpoint.ModItem modItem = def.Context.ModItem;
        //        ulong publishedId = modItem.PublishedFileId;
        //        if(publishedId == 0)
        //            continue;
        //
        //        ModId id = new ModId(modItem.PublishedServiceName, publishedId);
        //        if(realMods.Contains(id))
        //            continue;
        //
        //        if(unknownMods.Add(id))
        //        {
        //            Log.Info($"Detected unnamed mod: {id.Item1}:{id.Item2} - likely from PluginLoader");
        //        }
        //    }
        //}
    }
}