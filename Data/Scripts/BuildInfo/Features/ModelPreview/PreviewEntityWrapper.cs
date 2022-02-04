using System;
using System.Collections.Generic;
using System.IO;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public class PreviewEntityWrapper
    {
        public readonly MyEntity Entity;
        public readonly List<MyEntitySubpart> AllSubparts;

        public Matrix? LocalMatrix;

        /// <summary>
        /// Wether this entity's model is visible, does not affect subparts.
        /// </summary>
        public bool BaseModelVisible;

        MyStringHash Skin = MyStringHash.NullOrEmpty;
        Vector3? SkinPaintOverride = null;
        float Transparency;

        public PreviewEntityWrapper(string modelFullPath, Matrix? localMatrix = null, MyCubeBlockDefinition defForInfo = null, bool modelVisible = true)
        {
            LocalMatrix = localMatrix;
            BaseModelVisible = modelVisible;

            Entity = new MyEntity();
            Entity.Save = false;
            Entity.SyncFlag = false;
            Entity.IsPreview = true;

            // quick and dirty way of preventing model spawning with last LOD
            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            double distance = 3;
            if(defForInfo != null)
                distance = (defForInfo.Size.AbsMax() * MyDefinitionManager.Static.GetCubeSize(defForInfo.CubeSize));
            Entity.WorldMatrix = MatrixD.CreateTranslation(camMatrix.Translation + camMatrix.Backward * distance);

            Entity.Init(null, modelFullPath, null, null, null);
            Entity.DisplayName = $"BuildInfo_PreviewModel:{Path.GetFileName(modelFullPath)}";
            Entity.Render.EnableColorMaskHsv = true;
            Entity.Render.CastShadows = false;
            //Entity.Render.RemoveRenderObjects();
            //Entity.Render.AddRenderObjects();

            if(Entity.Subparts != null && Entity.Subparts.Count > 0)
            {
                // simplifies iterating all of them later on to not require recursive scan
                AllSubparts = new List<MyEntitySubpart>();
                RecursiveSubpartInit(Entity, AllSubparts);
            }

            // add last to allow all subparts to get shadows off and all that stuff
            Entity.Flags &= ~EntityFlags.IsGamePrunningStructureObject;
            Entity.Flags |= EntityFlags.IsNotGamePrunningStructureObject;
            MyEntities.Add(Entity, true);
            Entity.RemoveFromGamePruningStructure();
        }

        public void Close()
        {
            AllSubparts?.Clear();
            Entity.Close(); // should close its subparts too
        }

        static void RecursiveSubpartInit(MyEntity entity, List<MyEntitySubpart> addTo)
        {
            foreach(MyEntitySubpart subpart in entity.Subparts.Values)
            {
                addTo.Add(subpart);

                subpart.SyncFlag = false;
                subpart.IsPreview = true;
                subpart.Render.EnableColorMaskHsv = true;
                subpart.Render.CastShadows = false;
                //subpart.Render.RemoveRenderObjects();
                //subpart.Render.AddRenderObjects();

                if(subpart.Subparts != null)
                    RecursiveSubpartInit(subpart, addTo);
            }
        }

        public void Update(ref MatrixD matrix, float? customTransparency = null)
        {
            float transparency = (MyCubeBuilder.Static == null || MyCubeBuilder.Static.UseTransparency ? customTransparency ?? Hardcoded.CubeBuilderTransparency : 0f);
            if(Transparency != transparency)
            {
                Transparency = transparency;
                Entity.Render.Transparency = (BaseModelVisible ? transparency : 1f);
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

            Entity.PositionComp.SetWorldMatrix(ref matrix);
        }
    }
}
