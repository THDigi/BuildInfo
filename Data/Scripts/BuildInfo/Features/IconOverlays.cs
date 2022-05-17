using System.Collections.Generic;
using System.IO;
using Sandbox.Definitions;

namespace Digi.BuildInfo.Features.Tooltips
{
    public class IconOverlays : ModComponent
    {
        bool AddIcons; // used for unloading
        readonly List<string> TempIcons = new List<string>(4);

        public IconOverlays(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.Config.BlockIconOverlays.ValueAssigned += BlockIconOverlays_ValueAssigned;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.Config.BlockIconOverlays.ValueAssigned -= BlockIconOverlays_ValueAssigned;

            // reset icons to default
            AddIcons = false;
            ReplaceIcons();
        }

        void BlockIconOverlays_ValueAssigned(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            AddIcons = newValue;
            ReplaceIcons();
        }

        void ReplaceIcons()
        {
            string weightIcon = Path.Combine(Main.Session.ModContext.ModPath, @"Textures\IconOverlays\Weight.dds");
            SetupGroup("ArmorHeavyCubeGroup", weightIcon);
            SetupGroup("ArmorHeavyRampGroup", weightIcon);
            SetupGroup("ArmorHeavyRampCornerGroup", weightIcon);
            SetupGroup("ArmorHeavyRoundGroup", weightIcon);
            SetupGroup("ArmorHeavySlopedCorners", weightIcon);
            SetupGroup("ArmorHeavyTransitionBlocks", weightIcon);
            SetupGroup("HeavyArmorPanelsGroup", weightIcon);

            //string featherIcon = Path.Combine(Main.Session.ModContext.ModPath, @"Textures\IconOverlays\Feather.dds");
            //SetupGroup("ArmorLightCubeGroup", featherIcon);
            //SetupGroup("ArmorLightRampGroup", featherIcon);
            //SetupGroup("ArmorLightRampCornerGroup", featherIcon);
            //SetupGroup("ArmorLightRoundGroup", featherIcon);
            //SetupGroup("ArmorLightSlopedCorners", featherIcon);
            //SetupGroup("ArmorLightTransitionBlocks", featherIcon);
            //SetupGroup("LightArmorPanelsGroup", featherIcon);
        }

        void SetupGroup(string groupSubtypeId, string iconPath)
        {
            MyBlockVariantGroup group;
            if(!MyDefinitionManager.Static.GetBlockVariantGroupDefinitions().TryGetValue(groupSubtypeId, out group))
                return;

            foreach(MyCubeBlockDefinition def in group.Blocks)
            {
                TempIcons.Clear();

                for(int i = 0; i < def.Icons.Length; i++)
                {
                    string filePath = def.Icons[i];

                    // remove regardless, add later only if enabled
                    if(filePath == iconPath)
                        continue;

                    TempIcons.Add(filePath);
                }

                if(AddIcons)
                {
                    TempIcons.Add(iconPath);
                }

                if(def.Icons.Length != TempIcons.Count)
                {
                    def.Icons = TempIcons.ToArray();
                }

                if(group.PrimaryGUIBlock == def)
                {
                    group.Icons = def.Icons;
                }

                // NOTE: these icons refresh in realtime in G-menu and right side info, but do not in toolbar
            }
        }
    }
}
