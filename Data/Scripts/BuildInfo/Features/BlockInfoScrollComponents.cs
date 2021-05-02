using System;
using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Digi.Input;
using Sandbox.Definitions;
using Sandbox.Game;
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
        public bool ShowUpHint { get; private set; }
        public bool ShowDownHint { get; private set; }

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
        }

        public override void RegisterComponent()
        {
            Main.GameConfig.HudStateChanged += HudStateChanged;
            Main.EquipmentMonitor.BlockChanged += EquipmentBlockChanged;
            Main.Config.BlockInfoAdditions.ValueAssigned += SettingValueSet;

            HudState = Main.GameConfig.HudState;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.GameConfig.HudStateChanged -= HudStateChanged;
            Main.EquipmentMonitor.BlockChanged -= EquipmentBlockChanged;
            Main.Config.BlockInfoAdditions.ValueAssigned -= SettingValueSet;
        }

        void SettingValueSet(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, (newValue && Main.EquipmentMonitor.BlockDef != null));

            CycleComponents.Clear();
            Index = 0;
            IndexOffset = 0;
            ShowDownHint = false;
            ShowUpHint = false;

            MyHud.BlockInfo.Components.Clear();
            MyHud.BlockInfo.DefinitionId = default(MyDefinitionId);
        }

        void HudStateChanged(HudState prevState, HudState state)
        {
            HudState = state;

            CycleComponents.Clear();
            Index = 0;
            IndexOffset = 0;
            ShowDownHint = false;
            ShowUpHint = false;

            if(Main.Config.BlockInfoAdditions.Value)
            {
                MyHud.BlockInfo.DefinitionId = default(MyDefinitionId);
                MyHud.BlockInfo.Components.Clear();
            }
        }

        void EquipmentBlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            CycleComponents.Clear();
            Index = 0;
            IndexOffset = 0;
            ShowDownHint = false;
            ShowUpHint = false;
            ShowScrollHint = true;

            if(Main.Config.BlockInfoAdditions.Value)
            {
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, (def != null));

                MyHud.BlockInfo.DefinitionId = default(MyDefinitionId);
                MyHud.BlockInfo.Components.Clear();
            }
        }

        public override void UpdateDraw()
        {
            if(HudState == HudState.OFF)
                return;

            if(!Main.Config.BlockInfoAdditions.Value)
                return;

            if(Main.EquipmentMonitor.BlockDef == null)
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

                var slimBlock = Main.EquipmentMonitor.AimedBlock;
                if(slimBlock != null)
                {
                    // auto-scroll to higher if block is built
                    Index = MathHelper.FloorToInt(Math.Max(0, slimBlock.BuildLevelRatio * CycleComponents.Count - 6));
                }

                int maxIndex = CycleComponents.Count - MaxVisible;
                Index = MathHelper.Clamp(Index, 0, maxIndex);
                IndexOffset = maxIndex - Index;
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
                if(inChar && Main.EquipmentMonitor.AimedBlock != null && MyAPIGateway.Input.IsGameControlPressed(MyControlsSpace.PRIMARY_TOOL_ACTION))
                {
                    Index = MathHelper.FloorToInt(Math.Max(0, Main.EquipmentMonitor.AimedBlock.BuildLevelRatio * CycleComponents.Count - 6));
                }

                int scrollShow = MaxVisible;
                int maxIndex = CycleComponents.Count - scrollShow;
                Index = MathHelper.Clamp(Index, 0, maxIndex);
                IndexOffset = maxIndex - Index;

                ShowUpHint = (ShowScrollHint && Index == 0);
                ShowDownHint = (!ShowUpHint && ShowScrollHint && Index > 0);

                if(ShowDownHint)
                {
                    // TODO: gamepad control to scroll?
                    if(MyAPIGateway.Input.IsJoystickLastUsed)
                        HintComponent.ComponentName = "v More components v";
                    else
                        HintComponent.ComponentName = "v Shift+Scroll v";

                    comps.Add(HintComponent);
                }

                int loopStart = (ShowDownHint ? 1 : 0);
                int loopEnd = (ShowUpHint ? scrollShow - 1 : scrollShow);
                for(int i = loopStart; i < loopEnd; ++i)
                {
                    int idx = i + Index;
                    if(idx >= CycleComponents.Count)
                        break; // final redundancy

                    comps.Add(CycleComponents[idx]);
                }

                if(ShowUpHint)
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

        //class CubeBuilderHax : MyCubeBuilder
        //{
        //    // HACK: protected static in public non-sealed classes? might as well make'em public.
        //    public static void RefreshBlockInfoHud() => MyCubeBuilder.UpdateBlockInfoHud();
        //}
    }
}