﻿using System;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Draygo.API;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    public class CrosshairMessages : ModComponent
    {
        HudAPIv2.HUDMessage Text;
        StringBuilder TextSB;

        string CurrentText;

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
            Text = TextAPI.CreateHUDText(TextSB, new Vector2D(0, -0.1));
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
            if(aimedBlock != null && Main.EquipmentMonitor.IsAnyGrinder && Main.EquipmentMonitor.AimedProjectedBy == null)
            {
                SplitFlags splitInfo = Main.SplitChecking.GetSplitInfo(aimedBlock);

                string message = null;

                if(splitInfo.IsSet(SplitFlags.Disconnect))
                {
                    message = "<color=yellow>Something will disconnect if this block is removed.";
                }

                if(splitInfo.IsSet(SplitFlags.Split))
                {
                    message = "<color=yellow>Grid will split if this block is removed.";
                }

                if(splitInfo.IsSet(SplitFlags.BlockLoss))
                {
                    message = "<color=red>Another block will vanish if this block is removed!";
                }

                if(message != null)
                {
                    if(CurrentText != message)
                    {
                        CurrentText = message;
                        TextSB.Clear().Append(message);
                        Vector2D textSize = Text.GetTextLength();
                        Text.Offset = new Vector2D(Math.Abs(textSize.X) / -2, 0); // Math.Abs(textSize.Y) / -2
                    }

                    Text.Draw();
                }
            }
        }
    }
}
