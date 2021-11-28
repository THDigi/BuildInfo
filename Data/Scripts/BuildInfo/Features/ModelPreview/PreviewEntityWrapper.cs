﻿using System;
using System.Collections.Generic;
using System.IO;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public class PreviewEntityWrapper
    {
        public readonly MyEntity Entity;
        public readonly List<MyEntitySubpart> AllSubparts;

        public Matrix? LocalMatrix;

        MyStringHash Skin = MyStringHash.NullOrEmpty;
        Vector3? SkinPaintOverride = null;
        float Transparency;

        public PreviewEntityWrapper(string modelFullPath, Matrix? localMatrix = null)
        {
            LocalMatrix = localMatrix;

            Entity = new MyEntity();
            Entity.Save = false;
            Entity.SyncFlag = false;
            Entity.IsPreview = true;
            Entity.Init(null, modelFullPath, null, null, null);
            Entity.DisplayName = $"BuildInfo_PreviewModel:{Path.GetFileName(modelFullPath)}";
            Entity.Render.EnableColorMaskHsv = true;
            Entity.Render.CastShadows = false;
            MyEntities.Add(Entity, true);
            Entity.RemoveFromGamePruningStructure();

            if(Entity.Subparts != null && Entity.Subparts.Count > 0)
            {
                // simplifies iterating all of them later on to not require recursive scan
                AllSubparts = new List<MyEntitySubpart>();
                RecursiveSubpartInit(Entity, AllSubparts);
            }
        }

        public void Close()
        {
            Entity.Close(); // should close its subparts too
        }

        static void RecursiveSubpartInit(MyEntity entity, List<MyEntitySubpart> addTo)
        {
            foreach(MyEntitySubpart subpart in entity.Subparts.Values)
            {
                addTo.Add(subpart);

                subpart.Render.EnableColorMaskHsv = true;
                subpart.Render.CastShadows = false;
                subpart.SyncFlag = false;
                subpart.IsPreview = true;

                if(subpart.Subparts != null)
                    RecursiveSubpartInit(subpart, addTo);
            }
        }

        public void Update(ref MatrixD matrix, float? customTransparency = null, bool invisibleRoot = false)
        {
            float transparency = (MyCubeBuilder.Static == null || MyCubeBuilder.Static.UseTransparency ? customTransparency ?? Hardcoded.CubeBuilderTransparency : 0f);
            if(Transparency != transparency)
            {
                Transparency = transparency;
                Entity.Render.Transparency = (invisibleRoot ? 1f : transparency);
                Entity.Render.UpdateTransparency();

                if(AllSubparts != null)
                {
                    foreach(MyEntitySubpart subpart in AllSubparts)
                    {
                        subpart.Render.Transparency = transparency;
                        subpart.Render.UpdateTransparency();
                    }
                }
            }

            #region Skin&Color linking
            IMyPlayer player = MyAPIGateway.Session?.Player;
            if(player != null)
            {
                MyStringHash selectedSkin = MyStringHash.NullOrEmpty; // TODO: player's selected skin if that ever gets exposed
                if(Skin != selectedSkin)
                {
                    Skin = selectedSkin;
                    SkinPaintOverride = null;

                    // cloned from MyCubeBlock.UpdateSkin()
                    if(selectedSkin != MyStringHash.NullOrEmpty)
                    {
                        MyDefinitionManager.MyAssetModifiers skinRender = MyDefinitionManager.Static.GetAssetModifierDefinitionForRender(selectedSkin);
                        Entity.Render.MetalnessColorable = skinRender.MetalnessColorable;
                        Entity.Render.TextureChanges = skinRender.SkinTextureChanges;

                        MyAssetModifierDefinition skinDef = MyDefinitionManager.Static.GetAssetModifierDefinition(new MyDefinitionId(typeof(MyObjectBuilder_AssetModifierDefinition), selectedSkin));
                        if(skinDef != null && skinDef.DefaultColor.HasValue)
                        {
                            SkinPaintOverride = skinDef.DefaultColor.Value.ColorToHSVDX11();
                        }
                    }
                    else
                    {
                        Entity.Render.MetalnessColorable = false;
                        Entity.Render.TextureChanges = null;
                    }

                    // seems only required to remove skin, weird.
                    Entity.Render.RemoveRenderObjects();
                    Entity.Render.AddRenderObjects();
                }

                Vector3 color = SkinPaintOverride ?? player.SelectedBuildColor;
                if(Entity.Render.ColorMaskHsv != color)
                {
                    Entity.Render.ColorMaskHsv = color;
                }

                // color and skin seems to automatically propagate to subparts
            }
            #endregion

            if(Entity.Render.CastShadows)
                Entity.Render.CastShadows = false;

            // FIXME: doesn't turn off shadows on subparts...
            if(AllSubparts != null)
            {
                foreach(MyEntitySubpart subpart in AllSubparts)
                {
                    subpart.Render.CastShadows = false;
                }
            }

            Entity.PositionComp.SetWorldMatrix(ref matrix);
        }
    }
}