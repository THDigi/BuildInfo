using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Utilities
{
    /// <summary>
    /// Various random utility methods
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Chat message with the sender name being colored.
        /// NOTE: this is synchronized to all players but only the intended player(s) will see it.
        /// <paramref name="identityId"/> set to 0 will show to all players, default (-1) will show to local player.
        /// </summary>
        public static void ShowColoredChatMessage(string from, string message, MyFontEnum font, long identityId = -1)
        {
            if(identityId == -1)
            {
                if(MyAPIGateway.Session?.Player == null)
                    return;

                identityId = MyAPIGateway.Session.Player.IdentityId;
            }

            // NOTE: this is sent to all players and only shown if their identityId matches the one sent.
            MyVisualScriptLogicProvider.SendChatMessage(message, from, identityId, font);
        }

        public static MyOwnershipShareModeEnum GetBlockShareMode(IMyCubeBlock block)
        {
            if(block != null)
            {
                var internalBlock = (MyCubeBlock)block;

                // Because the game has 2 ownership systems and I've no idea which one is actually used in what case, and it doesn't seem it knows either since it uses both in initialization.
                // HACK MyEntityOwnershipComponent is not whitelisted
                //var ownershipComp = internalBlock.Components.Get<MyEntityOwnershipComponent>();
                //
                //if(ownershipComp != null)
                //    return ownershipComp.ShareMode;

                if(internalBlock.IDModule != null)
                    return internalBlock.IDModule.ShareMode;
            }

            return MyOwnershipShareModeEnum.None;
        }

        /// <summary>
        /// Returns true if specified definition has all faces fully airtight.
        /// The referenced arguments are assigned with the said values which should only really be used if it returns false (due to the quick escape return true).
        /// An fully airtight face means it keeps the grid airtight when the face is the only obstacle between empty void and the ship's interior.
        /// Due to the complexity of airtightness when connecting blocks, this method simply can not indicate that, that's what the mount points view is for.
        /// </summary>
        public static AirTightMode GetAirTightFaces(MyCubeBlockDefinition def, ref int airTightFaces, ref int totalFaces)
        {
            airTightFaces = 0;
            totalFaces = 0;

            if(def.IsAirTight.HasValue)
                return (def.IsAirTight.Value ? AirTightMode.SEALED : AirTightMode.NOT_SEALED);

            var cubes = BuildInfoMod.Instance.Caches.Vector3ISet;
            cubes.Clear();

            foreach(var kv in def.IsCubePressurized)
            {
                cubes.Add(kv.Key);
            }

            foreach(var kv in def.IsCubePressurized)
            {
                foreach(var kv2 in kv.Value)
                {
                    if(cubes.Contains(kv.Key + kv2.Key))
                        continue;

                    if(kv2.Value)
                        airTightFaces++;

                    totalFaces++;
                }
            }

            cubes.Clear();

            if(airTightFaces == 0)
                return AirTightMode.NOT_SEALED;

            if(airTightFaces == totalFaces)
                return AirTightMode.SEALED;

            return AirTightMode.USE_MOUNTS;
        }

        /// <summary>
        /// Gets the inventory volume from the EntityComponents and EntityContainers definitions.
        /// </summary>
        public static bool GetInventoryVolumeFromComponent(MyDefinitionBase def, out float volume)
        {
            var invComp = GetInventoryFromComponent(def);

            if(invComp != null)
            {
                volume = invComp.Volume;
                return true;
            }
            else
            {
                volume = 0;
                return false;
            }
        }

        /// <summary>
        /// Gets the inventory definition from the EntityComponents and EntityContainers definitions.
        /// </summary>
        public static MyInventoryComponentDefinition GetInventoryFromComponent(MyDefinitionBase def)
        {
            MyContainerDefinition containerDef;

            if(MyDefinitionManager.Static.TryGetContainerDefinition(def.Id, out containerDef) && containerDef.DefaultComponents != null)
            {
                MyComponentDefinitionBase compDefBase;

                foreach(var compPointer in containerDef.DefaultComponents)
                {
                    if(compPointer.BuilderType == typeof(MyObjectBuilder_Inventory) && MyComponentContainerExtension.TryGetComponentDefinition(compPointer.BuilderType, compPointer.SubtypeId.GetValueOrDefault(def.Id.SubtypeId), out compDefBase))
                    {
                        var invComp = compDefBase as MyInventoryComponentDefinition;

                        if(invComp != null && invComp.Id.SubtypeId == def.Id.SubtypeId)
                        {
                            return invComp;
                        }
                    }
                }
            }

            return null;
        }

        public static string ColorTag(Color color)
        {
            return $"<color={color.R.ToString()},{color.G.ToString()},{color.B.ToString()}>";
        }

        public static string ColorTag(Color color, string value)
        {
            return $"<color={color.R.ToString()},{color.G.ToString()},{color.B.ToString()}>{value}";
        }

        public static string ColorTag(Color color, string value1, string value2)
        {
            return $"<color={color.R.ToString()},{color.G.ToString()},{color.B.ToString()}>{value1}{value2}";
        }

        public static bool CreativeToolsEnabled => MyAPIGateway.Session.CreativeMode || (MyAPIGateway.Session.HasCreativeRights && MyAPIGateway.Session.EnableCopyPaste);

        public static int DamageMultiplierToResistance(float damageMultiplier)
        {
            const float initialHpMul = 1f;

            float hpMul = 1f / damageMultiplier;
            float damageResistance = ((hpMul - initialHpMul) / initialHpMul);

            return (int)(damageResistance * 100);
        }
    }
}
