using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using VRage.Library.Utils;

namespace Digi.BuildInfo.Features
{
    public class FixTextures : ModComponent
    {
        UndoableEditToolset DefEdits = new UndoableEditToolset();

        public FixTextures(BuildInfoMod main) : base(main)
        {
            var replaceButtonTextures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PathUtils.Normalize(@"Textures\GUI\Icons\buttons\OneButton.dds")] = Utils.GetModFullPath(@"Textures\Fixes\Button1.dds"),
                [PathUtils.Normalize(@"Textures\GUI\Icons\buttons\TwoButton.dds")] = Utils.GetModFullPath(@"Textures\Fixes\Button2.dds"),
                [PathUtils.Normalize(@"Textures\GUI\Icons\buttons\ThreeButton.dds")] = Utils.GetModFullPath(@"Textures\Fixes\Button3.dds"),
                [PathUtils.Normalize(@"Textures\GUI\Icons\buttons\FourButton.dds")] = Utils.GetModFullPath(@"Textures\Fixes\Button4.dds"),
            };

            foreach(MyCubeBlockDefinition blockDef in main.Caches.BlockDefs)
            {
                var buttonDef = blockDef as MyButtonPanelDefinition;
                if(buttonDef != null)
                {
                    if(buttonDef.ButtonSymbols != null)
                    {
                        for(int i = 0; i < buttonDef.ButtonSymbols.Length; i++)
                        {
                            string texture = PathUtils.Normalize(buttonDef.ButtonSymbols[i]);
                            string replacement;
                            if(replaceButtonTextures.TryGetValue(texture, out replacement))
                            {
                                int captureIndex = i; // required for the capture below to be reliable
                                DefEdits.MakeEdit(buttonDef, (d, v) => d.ButtonSymbols[captureIndex] = v, texture, replacement);
                            }
                        }
                    }
                }
            }
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            DefEdits.UndoAll();
        }
    }
}
