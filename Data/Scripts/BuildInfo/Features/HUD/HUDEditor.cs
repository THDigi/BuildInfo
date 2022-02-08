using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Definitions.GUI;

namespace Digi.BuildInfo.Features
{
    public class HUDEditor : ModComponent
    {
        readonly List<IDefinitionEdit> Edits = new List<IDefinitionEdit>();

        public HUDEditor(BuildInfoMod main) : base(main)
        {
            const float MaxFontSize = 0.46f;
            const string SetFont = "BI_SEOutlined";

            foreach(MyHudDefinition hudDef in MyDefinitionManager.Static.GetAllDefinitions<MyHudDefinition>())
            {
                if(hudDef?.Toolbar?.ItemStyle != null)
                {
                    Log.Info($"Editing HUD definition '{hudDef.Id.SubtypeName}' to change font to '{SetFont}' at max {MaxFontSize} scale (smaller ignored).");

                    if(hudDef.Toolbar.ItemStyle.TextScale < MaxFontSize)
                        Edits.Add(DefinitionEdit.Create(hudDef, (d, v) => d.Toolbar.ItemStyle.TextScale = v, hudDef.Toolbar.ItemStyle.TextScale, MaxFontSize));

                    Edits.Add(DefinitionEdit.Create(hudDef, (d, v) => d.Toolbar.ItemStyle.FontNormal = v, hudDef.Toolbar.ItemStyle.FontNormal, SetFont));
                    Edits.Add(DefinitionEdit.Create(hudDef, (d, v) => d.Toolbar.ItemStyle.FontHighlight = v, hudDef.Toolbar.ItemStyle.FontHighlight, SetFont));
                }
            }
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            foreach(IDefinitionEdit edit in Edits)
            {
                edit.Restore();
            }
        }
    }
}
