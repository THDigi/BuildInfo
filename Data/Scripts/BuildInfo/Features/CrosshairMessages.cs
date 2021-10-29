using System;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Draygo.API;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using BlendType = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features
{
    public class CrosshairMessages : ModComponent
    {
        HudAPIv2.HUDMessage Text;
        StringBuilder TextSB;

        string CurrentText;

        const string SplitText = "<color=yellow>Grid will split if block is removed";

        public CrosshairMessages(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.TextAPI.Detected += TextAPI_Detected;
            Main.EquipmentMonitor.ToolChanged += ToolChanged;
            Main.Config.UnderCrosshairMessages.ValueAssigned += UnderCrosshairMessagesChanged;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.TextAPI.Detected -= TextAPI_Detected;
            Main.EquipmentMonitor.ToolChanged -= ToolChanged;
            Main.Config.UnderCrosshairMessages.ValueAssigned -= UnderCrosshairMessagesChanged;
        }

        void TextAPI_Detected()
        {
            if(Text != null)
                return;

            TextSB = new StringBuilder(128);
            Text = new HudAPIv2.HUDMessage(TextSB, new Vector2D(0, -0.1), Scale: 1, HideHud: true, Shadowing: true, Blend: BlendType.PostPP);
            Text.Visible = false;
            SetUpdate();
        }

        void ToolChanged(MyDefinitionId toolDefId)
        {
            SetUpdate();
        }

        void UnderCrosshairMessagesChanged(bool oldValue, bool newValue, SettingBase<bool> setting)
        {
            if(oldValue != newValue)
                SetUpdate();
        }

        void SetUpdate()
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, Text != null && Main.Config.UnderCrosshairMessages.Value && Main.Config.AimInfo.IsSet(AimInfoFlags.GrindGridSplit) && Main.EquipmentMonitor.IsAnyGrinder);
        }

        public override void UpdateDraw()
        {
            if(Text == null)
                return;

            IMySlimBlock aimedBlock = Main.EquipmentMonitor.AimedBlock;
            if(aimedBlock != null && Main.EquipmentMonitor.IsAnyGrinder)
            {
                if(Main.TextGeneration.WillSplitGrid == GridSplitType.Recalculate)
                    Main.TextGeneration.WillSplitGrid = aimedBlock.CubeGrid.WillRemoveBlockSplitGrid(aimedBlock) ? GridSplitType.Split : GridSplitType.NoSplit;

                if(Main.TextGeneration.WillSplitGrid == GridSplitType.Split)
                {
                    if(CurrentText != SplitText)
                    {
                        CurrentText = SplitText;
                        TextSB.Clear().Append(SplitText);
                        Vector2D textSize = Text.GetTextLength();
                        Text.Offset = new Vector2D(Math.Abs(textSize.X) / -2, 0); // Math.Abs(textSize.Y) / -2
                    }

                    Text.Draw();
                }
            }
        }
    }
}
