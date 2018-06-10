using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo
{
    public partial class BuildInfo
    {
        /// <summary>
        /// Returns true if specified definition has all faces fully airtight.
        /// The referenced arguments are assigned with the said values which should only really be used if it returns false (due to the quick escape return true).
        /// An fully airtight face means it keeps the grid airtight when the face is the only obstacle between empty void and the ship's interior.
        /// Due to the complexity of airtightness when connecting blocks, this method simply can not indicate that, that's what the mount points view is for.
        /// </summary>
        private bool IsAirTight(MyCubeBlockDefinition def, ref int airTightFaces, ref int totalFaces)
        {
            if(def.IsAirTight)
                return true;

            airTightFaces = 0;
            totalFaces = 0;
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
            return (airTightFaces == totalFaces);
        }

        /// <summary>
        /// Gets the inventory volume from the EntityComponents and EntityContainers definitions.
        /// </summary>
        public static bool GetInventoryFromComponent(MyDefinitionBase def, out float volume)
        {
            volume = 0;
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
                            volume = invComp.Volume;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        #region Classes for storing generated info
        public class Cache
        {
            public long expires;

            public void ResetExpiry()
            {
                expires = DateTime.UtcNow.Ticks + (TimeSpan.TicksPerSecond * CACHE_EXPIRE_SECONDS);
            }
        }

        public class CacheTextAPI : Cache
        {
            public readonly StringBuilder Text = new StringBuilder();
            public readonly Vector2D TextSize;

            public CacheTextAPI(StringBuilder textSB, Vector2D textSize)
            {
                ResetExpiry();
                Text.AppendSB(textSB);
                TextSize = textSize;
            }
        }

        public class CacheNotifications : Cache
        {
            public readonly List<IMyHudNotification> Lines = new List<IMyHudNotification>();

            public CacheNotifications(List<HudLine> hudLines)
            {
                ResetExpiry();

                for(int i = 0; i < hudLines.Count; ++i)
                {
                    var line = hudLines[i];

                    if(line.str.Length > 0)
                    {
                        Lines.Add(MyAPIGateway.Utilities.CreateNotification(line.str.ToString(), 16, line.font));
                    }
                }
            }
        }

        public class HudLine
        {
            public StringBuilder str = new StringBuilder();
            public string font;
            public int lineWidthPx;
        }
        #endregion

        #region Notification font character width data
        private void ComputeCharacterSizes()
        {
            charSize.Clear();

            // generated from fonts/white_shadow/FontData.xml
            AddCharsSize(" !I`ijl ¡¨¯´¸ÌÍÎÏìíîïĨĩĪīĮįİıĵĺļľłˆˇ˘˙˚˛˜˝ІЇії‹›∙", 8);
            AddCharsSize("\"-rª­ºŀŕŗř", 10);
            AddCharsSize("#0245689CXZ¤¥ÇßĆĈĊČŹŻŽƒЁЌАБВДИЙПРСТУХЬ€", 19);
            AddCharsSize("$&GHPUVY§ÙÚÛÜÞĀĜĞĠĢĤĦŨŪŬŮŰŲОФЦЪЯжы†‡", 20);
            AddCharsSize("%ĲЫ", 24);
            AddCharsSize("'|¦ˉ‘’‚", 6);
            AddCharsSize("(),.1:;[]ft{}·ţťŧț", 9);
            AddCharsSize("*²³¹", 11);
            AddCharsSize("+<=>E^~¬±¶ÈÉÊË×÷ĒĔĖĘĚЄЏЕНЭ−", 18);
            AddCharsSize("/ĳтэє", 14);
            AddCharsSize("3FKTabdeghknopqsuy£µÝàáâãäåèéêëðñòóôõöøùúûüýþÿāăąďđēĕėęěĝğġģĥħĶķńņňŉōŏőśŝşšŢŤŦũūŭůűųŶŷŸșȚЎЗКЛбдекруцяёђћўџ", 17);
            AddCharsSize("7?Jcz¢¿çćĉċčĴźżžЃЈЧавийнопсъьѓѕќ", 16);
            AddCharsSize("@©®мшњ", 25);
            AddCharsSize("ABDNOQRSÀÁÂÃÄÅÐÑÒÓÔÕÖØĂĄĎĐŃŅŇŌŎŐŔŖŘŚŜŞŠȘЅЊЖф□", 21);
            AddCharsSize("L_vx«»ĹĻĽĿŁГгзлхчҐ–•", 15);
            AddCharsSize("MМШ", 26);
            AddCharsSize("WÆŒŴ—…‰", 31);
            AddCharsSize("\\°“”„", 12);
            AddCharsSize("mw¼ŵЮщ", 27);
            AddCharsSize("½Щ", 29);
            AddCharsSize("¾æœЉ", 28);
            AddCharsSize("ю", 23);
            AddCharsSize("ј", 7);
            AddCharsSize("љ", 22);
            AddCharsSize("ґ", 13);
            AddCharsSize("™", 30);
            AddCharsSize("", 40);
            AddCharsSize("", 41);
            AddCharsSize("", 32);
            AddCharsSize("", 34);
        }

        private void AddCharsSize(string chars, int size)
        {
            for(int i = 0; i < chars.Length; i++)
            {
                charSize.Add(chars[i], size);
            }
        }
        #endregion

        #region Resource group priorities
        private void ComputeResourceGroups()
        {
            resourceGroupPriority.Clear();
            resourceSourceGroups = 0;
            resourceSinkGroups = 0;

            var groupDefs = MyDefinitionManager.Static.GetDefinitionsOfType<MyResourceDistributionGroupDefinition>();
            var orderedGroups = groupDefs.OrderBy(GroupOrderBy);

            foreach(var group in orderedGroups)
            {
                int priority = 0;

                if(group.IsSource)
                {
                    resourceSourceGroups++;
                    priority = resourceSourceGroups;
                }
                else
                {
                    resourceSinkGroups++;
                    priority = resourceSinkGroups;
                }

                resourceGroupPriority.Add(group.Id.SubtypeId, new ResourceGroupData()
                {
                    def = group,
                    priority = priority,
                });
            }
        }

        private int GroupOrderBy(MyResourceDistributionGroupDefinition def)
        {
            return def.Priority;
        }

        public struct ResourceGroupData
        {
            public MyResourceDistributionGroupDefinition def;
            public int priority;
        }
        #endregion

        private void ResetDrawCaches()
        {
            viewProjInvCompute = true;
            scaleFovCompute = true;
        }

        /// <summary>
        /// This ensures the math is done at most once per frame, if ResetDrawCaches() is correctly called at the start of Draw().
        /// </summary>
        public MatrixD ViewProjectionInv
        {
            get
            {
                if(viewProjInvCompute)
                {
                    var cam = MyAPIGateway.Session.Camera;

                    // HACK ProjectionMatrix needs recomputing because camera's m_fovSpring is set after ProjectionMatrix is computed, MyCamera.Update(float updateStepTime) and MyCamera.FovWithZoom
                    var aspectRatio = cam.ViewportSize.X / cam.ViewportSize.Y;
                    var safeNear = Math.Min(4f, cam.NearPlaneDistance); // MyCamera.GetSafeNear()
                    var projectionMatrix = MatrixD.CreatePerspectiveFieldOfView(cam.FovWithZoom, aspectRatio, safeNear, cam.FarPlaneDistance);
                    viewProjInvCache = MatrixD.Invert(cam.ViewMatrix * projectionMatrix);
                    viewProjInvCompute = false;
                }

                return viewProjInvCache;
            }
        }

        /// <summary>
        /// This ensures the math is done at most once per frame, if ResetDrawCaches() is correctly called at the start of Draw().
        /// </summary>
        public float ScaleFOV
        {
            get
            {
                if(scaleFovCompute)
                {
                    var cam = MyAPIGateway.Session.Camera;
                    scaleFovCache = (float)Math.Tan(cam.FovWithZoom * 0.5);
                    scaleFovCompute = false;
                }

                return scaleFovCache;
            }
        }

        public Vector3D GameHudToWorld(Vector2 hud)
        {
            var hudX = (2.0 * hud.X - 1);
            var hudY = (1 - 2.0 * hud.Y);

            // Vector4D.Transform(new Vector4D(hud.X, hud.Y, 0d, 1d), ref ViewProjectionInv, out ...) 

            var matrix = ViewProjectionInv;
            var x = hudX * matrix.M11 + hudY * matrix.M21 + /* 0 * matrix.M31 + 1 * */ matrix.M41;
            var y = hudX * matrix.M12 + hudY * matrix.M22 + /* 0 * matrix.M32 + 1 * */ matrix.M42;
            var z = hudX * matrix.M13 + hudY * matrix.M23 + /* 0 * matrix.M33 + 1 * */ matrix.M43;
            var w = hudX * matrix.M14 + hudY * matrix.M24 + /* 0 * matrix.M34 + 1 * */ matrix.M44;
            return new Vector3D(x / w, y / w, z / w);
        }

        public Vector2 GetGameHudBlockInfoSize(float Ymultiplier)
        {
            var size = BLOCKINFO_SIZE;
            size.Y *= Ymultiplier;
            size.Y += BLOCKINFO_TEXT_PADDING;
            size *= ScaleFOV;

            if(Math.Abs(aspectRatio - (1280.0 / 1024.0)) <= 0.0001) // HACK 5:4 aspect ratio manual fix
            {
                size.X *= ASPECT_RATIO_54_FIX;
            }

            return size;
        }

        public Vector2 GetGameHudBlockInfoPos()
        {
            // HACK hardcoded from MyGuiScreenHudSpace.Draw() with some fine adjustments
            Vector2 posHUD = new Vector2(0.9894f, 0.7487f);

            if(MyAPIGateway.Session.ControlledObject is IMyShipController)
                posHUD.Y -= 0.1f; // HACK cockpits intentionally bump up the block info

            if(aspectRatio > 5) // triple monitor
                posHUD.X += 0.75f;

            return posHUD;
        }

        public static void ShowChatMessage(string from, string message, MyFontEnum font)
        {
            if(MyAPIGateway.Session?.Player == null)
                return;

            // NOTE: this is sent to all players and only shown if their identityId matches the one sent.
            MyVisualScriptLogicProvider.SendChatMessage(message, from, MyAPIGateway.Session.Player.IdentityId, font);
        }
    }
}
