using System;
using System.IO;
using System.Text;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public abstract class PreviewInstanceBase
    {
        protected readonly BuildInfoMod Main;

        public MyCubeBlockDefinition BlockDef { get; private set; }

        public PreviewInstanceBase()
        {
            Main = BuildInfoMod.Instance;
        }

        public void Setup(MyCubeBlockDefinition def)
        {
            BlockDef = def;
            Initialized();
        }

        public void Dispose()
        {
            try
            {
                Disposed();
            }
            finally
            {
                BlockDef = null;
            }
        }

        protected abstract void Initialized();

        protected abstract void Disposed();

        public abstract void Update(ref MatrixD drawMatrix);

        protected class PreviewEntityWrapper
        {
            readonly MyEntity Entity;

            MyStringHash Skin;
            Vector3? SkinPaintOverride;

            public PreviewEntityWrapper(string modelFullPath)
            {
                string name = $"BuildInfo_PreviewModel:{Path.GetFileName(modelFullPath)}";

                Entity = new MyEntity();
                Entity.Save = false;
                Entity.SyncFlag = false;
                Entity.IsPreview = true;
                Entity.Init(new StringBuilder(name), modelFullPath, null, null, null);
                Entity.Render.EnableColorMaskHsv = true;
                Entity.Render.CastShadows = false;
                MyEntities.Add(Entity, true);
            }

            public void Close()
            {
                Entity.Close();
            }

            public void Update(ref MatrixD worldMatrix)
            {
                Entity.PositionComp.SetWorldMatrix(ref worldMatrix);

                float transparency = (MyCubeBuilder.Static == null || MyCubeBuilder.Static.UseTransparency ? Hardcoded.CubeBuilderTransparency : 0f);
                if(transparency != Entity.Render.Transparency)
                {
                    Entity.Render.Transparency = transparency;
                    Entity.Render.UpdateTransparency();
                }

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
                }

                if(Entity.Render.CastShadows)
                    Entity.Render.CastShadows = false;
            }
        }
    }
}
