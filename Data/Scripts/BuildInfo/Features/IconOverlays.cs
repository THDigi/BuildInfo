using System.Collections.Generic;
using System.IO;
using Sandbox.Definitions;

namespace Digi.BuildInfo.Features
{
    public class IconOverlays : ModComponent
    {
        public IconOverlays(BuildInfoMod main) : base(main)
        {
            SetupIcons(Main.Config.BlockIconOverlays.Value);

            Main.Config.BlockIconOverlays.ValueAssigned += BlockIconOverlays_ValueAssigned;
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            Main.Config.BlockIconOverlays.ValueAssigned -= BlockIconOverlays_ValueAssigned;

            // reset icons to default
            SetupIcons(false);
        }

        void BlockIconOverlays_ValueAssigned(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            SetupIcons(newValue);
        }

        void SetupIcons(bool addIcon)
        {
            string weightIcon = Path.Combine(Main.Session.ModContext.ModPath, @"Textures\IconOverlays\Weight.dds");
            SetupGroup("ArmorHeavyCubeGroup", weightIcon, addIcon);
            SetupGroup("ArmorHeavyRampGroup", weightIcon, addIcon);
            SetupGroup("ArmorHeavyRampCornerGroup", weightIcon, addIcon);
            SetupGroup("ArmorHeavyRoundGroup", weightIcon, addIcon);
            SetupGroup("ArmorHeavySlopedCorners", weightIcon, addIcon);
            SetupGroup("ArmorHeavyTransitionBlocks", weightIcon, addIcon);
            SetupGroup("HeavyArmorPanelsGroup", weightIcon, addIcon);

            //string featherIcon = Path.Combine(Main.Session.ModContext.ModPath, @"Textures\IconOverlays\Feather.dds");
            //SetupGroup("ArmorLightCubeGroup", featherIcon, addIcon);
            //SetupGroup("ArmorLightRampGroup", featherIcon, addIcon);
            //SetupGroup("ArmorLightRampCornerGroup", featherIcon, addIcon);
            //SetupGroup("ArmorLightRoundGroup", featherIcon, addIcon);
            //SetupGroup("ArmorLightSlopedCorners", featherIcon, addIcon);
            //SetupGroup("ArmorLightTransitionBlocks", featherIcon, addIcon);
            //SetupGroup("LightArmorPanelsGroup", featherIcon, addIcon);
        }

        readonly List<string> _TempIcons = new List<string>(4);
        void SetupGroup(string groupSubtypeId, string iconPath, bool addIcon)
        {
            MyBlockVariantGroup group;
            if(!MyDefinitionManager.Static.GetBlockVariantGroupDefinitions().TryGetValue(groupSubtypeId, out group))
                return;

            foreach(MyCubeBlockDefinition def in group.Blocks)
            {
                _TempIcons.Clear();

                for(int i = 0; i < def.Icons.Length; i++)
                {
                    string filePath = def.Icons[i];

                    // remove regardless, add later only if enabled
                    if(filePath == iconPath)
                        continue;

                    _TempIcons.Add(filePath);
                }

                if(addIcon)
                {
                    _TempIcons.Add(iconPath);
                }

                if(def.Icons.Length != _TempIcons.Count)
                {
                    def.Icons = _TempIcons.ToArray();
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
