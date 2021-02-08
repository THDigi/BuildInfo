using System;
using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Digi.Input;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    public class BlockInfoScrollComponents : ModComponent
    {
        public const int MaxVisibleHudHints = 9;
        public const int MaxVisibleHudMinChar = 13;
        public const int MaxVisibleHudMinShip = 7;

        public int IndexOffset { get; private set; }
        public int Index { get; private set; }
        public int MaxVisible { get; private set; } = MaxVisibleHudHints;

        bool Refresh;
        bool ShowScrollHint;
        HudState HudState;

        readonly List<MyHudBlockInfo.ComponentInfo> CycleComponents = new List<MyHudBlockInfo.ComponentInfo>(16);

        MyHudBlockInfo.ComponentInfo HintComponent = new MyHudBlockInfo.ComponentInfo()
        {
            Icons = new string[] { @"Textures\GUI\Icons\HUD 2017\HelpScreen.png" },
        };

        public BlockInfoScrollComponents(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_DRAW;
        }

        protected override void RegisterComponent()
        {
            Config.BlockInfoAdditions.ValueAssigned += BlockInfoAdditionsChanged;
            GameConfig.HudStateChanged += HudStateChanged;
            EquipmentMonitor.BlockChanged += EquipmentBlockChanged;

            HudState = Main.GameConfig.HudState;
        }

        protected override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Config.BlockInfoAdditions.ValueAssigned -= BlockInfoAdditionsChanged;
            GameConfig.HudStateChanged -= HudStateChanged;
            EquipmentMonitor.BlockChanged -= EquipmentBlockChanged;
        }

        void BlockInfoAdditionsChanged(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            Index = 0;
            IndexOffset = 0;

            MyHud.BlockInfo.Components.Clear();
            MyHud.BlockInfo.DefinitionId = default(MyDefinitionId);
        }

        void HudStateChanged(HudState prevState, HudState state)
        {
            HudState = state;

            MyHud.BlockInfo.DefinitionId = default(MyDefinitionId);
            MyHud.BlockInfo.Components.Clear();
            Refresh = true;
        }

        void EquipmentBlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            Index = 0;
            IndexOffset = 0;
            ShowScrollHint = true;

            if(slimBlock != null)
            {
                // auto-scroll to higher if block is built
                Index = MathHelper.FloorToInt(Math.Max(0, slimBlock.BuildLevelRatio * CycleComponents.Count - 6));
            }

            if(CycleComponents.Count > 0)
            {
                int maxIndex = CycleComponents.Count - MaxVisible;
                Index = MathHelper.Clamp(Index, 0, maxIndex);
                IndexOffset = maxIndex - Index;
            }
        }

        protected override void UpdateDraw()
        {
            if(EquipmentMonitor.BlockDef == null)
                return;

            MaxVisible = MaxVisibleHudHints;
            if(HudState == HudState.BASIC)
            {
                if(MyAPIGateway.Session.ControlledObject is IMyShipController)
                    MaxVisible = MaxVisibleHudMinShip;
                else
                    MaxVisible = MaxVisibleHudMinChar;
            }

            var hudComps = MyHud.BlockInfo.Components;
            if(hudComps.Count > MaxVisible)
            {
                CycleComponents.Clear();

                for(int i = 0; i < hudComps.Count; i++)
                {
                    var comp = hudComps[i];

                    // rename it in comps list (not the def) to better visually track
                    comp.ComponentName = $"{(i + 1).ToString()}. {comp.ComponentName}";

                    CycleComponents.Add(comp);
                }

                Refresh = true;
            }

            if(CycleComponents.Count == 0)
                return;

            bool inChar = InputLib.IsInputReadable(checkSpectator: false);
            if(inChar && MyAPIGateway.Input.IsAnyShiftKeyPressed())
            {
                int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                if(scroll > 0)
                {
                    Index++;
                    Refresh = true;
                    ShowScrollHint = false;
                }
                else if(scroll < 0)
                {
                    Index--;
                    Refresh = true;
                    ShowScrollHint = false;
                }
            }

            if(Refresh)
            {
                Refresh = false;

                var comps = MyHud.BlockInfo.Components;
                comps.Clear();

                // auto-scroll while welding/grinding the block
                // also nice side effect of this not triggering unless components change
                if(inChar && EquipmentMonitor.AimedBlock != null && MyAPIGateway.Input.IsGameControlPressed(MyControlsSpace.PRIMARY_TOOL_ACTION))
                {
                    Index = MathHelper.FloorToInt(Math.Max(0, EquipmentMonitor.AimedBlock.BuildLevelRatio * CycleComponents.Count - 6));
                }

                int scrollShow = MaxVisible;
                int maxIndex = CycleComponents.Count - scrollShow;
                Index = MathHelper.Clamp(Index, 0, maxIndex);
                IndexOffset = maxIndex - Index;

                bool showUpHint = (ShowScrollHint && Index == 0);
                bool showDownHint = (!showUpHint && ShowScrollHint && Index > 0);

                if(showDownHint)
                {
                    // TODO: gamepad control to scroll?
                    if(MyAPIGateway.Input.IsJoystickLastUsed)
                        HintComponent.ComponentName = "v More components v";
                    else
                        HintComponent.ComponentName = "v Shift+Scroll v";

                    comps.Add(HintComponent);
                }

                int loopStart = (showDownHint ? 1 : 0);
                int loopEnd = (showUpHint ? scrollShow - 1 : scrollShow);
                for(int i = loopStart; i < loopEnd; ++i)
                {
                    comps.Add(CycleComponents[i + Index]);
                }

                if(showUpHint)
                {
                    if(MyAPIGateway.Input.IsJoystickLastUsed)
                        HintComponent.ComponentName = "^ More components ^";
                    else
                        HintComponent.ComponentName = "^ Shift+Scroll ^";

                    comps.Add(HintComponent);
                }

                // HACK: this causes Version++ making the HUD to refresh component draw
                MyHud.BlockInfo.DefinitionId = MyHud.BlockInfo.DefinitionId;
            }
        }

        class CubeBuilderHax : MyCubeBuilder
        {
            // HACK: protected static in public non-sealed classes? might as well make'em public.
            public static void RefreshBlockInfoHud() => MyCubeBuilder.UpdateBlockInfoHud();
        }
    }
}
