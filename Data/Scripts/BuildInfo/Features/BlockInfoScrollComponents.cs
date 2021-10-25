using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Digi.Input;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    // TODO: maybe also scroll description if it's insanely long? (it can get too long with this mod adding Mod: and DLC: lines)

    public class BlockInfoScrollComponents : ModComponent
    {
        const int MaxCompsCharacterHudHints = 9;
        const int MaxCompsCharacter = 13;

        const int MaxCompsShipHudHints = 9;
        const int MaxCompsShip = 7;

        public int IndexOffset { get; private set; }
        public int Index { get; private set; }
        public int MaxVisible { get; private set; } = MaxCompsCharacterHudHints;
        public bool ShowUpHint { get; private set; }
        public bool ShowDownHint { get; private set; }

        bool Refresh;
        bool ShowScrollHint;
        HudState HudState;

        MyDefinitionId? ComputedForDefinitionId;
        readonly List<MyHudBlockInfo.ComponentInfo> CycleComponents = new List<MyHudBlockInfo.ComponentInfo>(16);

        MyHudBlockInfo.ComponentInfo HintComponent = new MyHudBlockInfo.ComponentInfo()
        {
            Icons = new string[1],
        };

        public BlockInfoScrollComponents(BuildInfoMod main) : base(main)
        {
            HintComponent.Icons[0] = Utils.GetModFullPath(@"Textures\UIHelpIcon.dds");
        }

        public override void RegisterComponent()
        {
            Main.GameConfig.HudStateChanged += HudStateChanged;
            Main.EquipmentMonitor.BlockChanged += EquipmentBlockChanged;
            Main.Config.ScrollableComponentsList.ValueAssigned += SettingValueSet;

            HudState = Main.GameConfig.HudState;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.GameConfig.HudStateChanged -= HudStateChanged;
            Main.EquipmentMonitor.BlockChanged -= EquipmentBlockChanged;
            Main.Config.ScrollableComponentsList.ValueAssigned -= SettingValueSet;
        }

        void SettingValueSet(bool oldValue, bool newValue, SettingBase<bool> setting)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, (newValue && Main.EquipmentMonitor.BlockDef != null));

            if(oldValue != newValue)
            {
                ComputedForDefinitionId = null;
                CycleComponents.Clear();
                Index = 0;
                IndexOffset = 0;
                ShowDownHint = false;
                ShowUpHint = false;

                MyHud.BlockInfo.Components.Clear();
                MyHud.BlockInfo.DefinitionId = default(MyDefinitionId);
            }
        }

        void HudStateChanged(HudState prevState, HudState state)
        {
            if(prevState == state)
                return;

            HudState = state;

            ComputedForDefinitionId = null;
            CycleComponents.Clear();
            Index = 0;
            IndexOffset = 0;
            ShowDownHint = false;
            ShowUpHint = false;

            if(Main.Config.ScrollableComponentsList.Value)
            {
                MyHud.BlockInfo.Components.Clear();
                MyHud.BlockInfo.DefinitionId = default(MyDefinitionId);

                if(MyAPIGateway.Session.ControlledObject is IMyCubeBlock)
                {
                    // refreshing doesn't seem necessary for ship
                }
                else
                {
                    IMyGunObject<MyToolBase> toolEnt = MyAPIGateway.Session?.Player?.Character?.EquippedTool as IMyGunObject<MyToolBase>;
                    if(toolEnt != null)
                    {
                        // HACK: forcing tool to refresh HUD... also needs a MyCharacter so had to hackily cast that aswell.
                        toolEnt.OnControlReleased();
                        toolEnt.OnControlAcquired(Utils.CastHax(new MyCockpit().Pilot, MyAPIGateway.Session.Player.Character));
                    }
                }
            }
        }

        void EquipmentBlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, (def != null && Main.Config.ScrollableComponentsList.Value));

            if(def == null || (ComputedForDefinitionId != null && ComputedForDefinitionId != def.Id))
            {
                ComputedForDefinitionId = null;
                CycleComponents.Clear();
                Index = 0;
                IndexOffset = 0;
                ShowDownHint = false;
                ShowUpHint = false;
                ShowScrollHint = true;
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

            bool hudHints = (HudState == HudState.HINTS);
            bool inShip = (MyAPIGateway.Session.ControlledObject is IMyCubeBlock);

            if(inShip)
                MaxVisible = (hudHints ? MaxCompsShipHudHints : MaxCompsShip);
            else
                MaxVisible = (hudHints ? MaxCompsCharacterHudHints : MaxCompsCharacter);

            List<MyHudBlockInfo.ComponentInfo> hudComps = MyHud.BlockInfo.Components;
            if(hudComps.Count > MaxVisible)
            {
                CycleComponents.Clear();

                for(int i = 0; i < hudComps.Count; i++)
                {
                    MyHudBlockInfo.ComponentInfo comp = hudComps[i];

                    // rename it in comps list (not the def) to better visually track
                    comp.ComponentName = $"{(i + 1).ToString()}. {comp.ComponentName}";

                    CycleComponents.Add(comp);
                }

                ComputedForDefinitionId = MyHud.BlockInfo.DefinitionId;
                Refresh = true;

                #region auto-scroll depending on build progress
                IMySlimBlock aimedBlock = Main.EquipmentMonitor.AimedBlock;
                if(aimedBlock != null)
                {
                    if(Main.EquipmentMonitor.AimedProjectedBy != null)
                    {
                        Index = 0; // projected, never has placed components
                    }
                    else
                    {
                        // follow the highest mounted or stockpiled component.
                        int totalComponents = CycleComponents.Count;
                        int midPoint = MaxVisible / 2;

                        for(int i = 0; i < totalComponents; i++)
                        {
                            if(CycleComponents[i].InstalledCount > 0)
                            {
                                Index = i;
                            }
                        }

                        Index -= midPoint;
                    }
                }
                #endregion

                int maxIndex = CycleComponents.Count - MaxVisible;
                Index = MathHelper.Clamp(Index, 0, maxIndex);
                IndexOffset = maxIndex - Index;
            }

            if(CycleComponents.Count == 0)
                return;

            bool inputReadable = InputLib.IsInputReadable(checkSpectator: false);
            if(inputReadable && MyAPIGateway.Input.IsAnyShiftKeyPressed())
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

                List<MyHudBlockInfo.ComponentInfo> comps = MyHud.BlockInfo.Components;
                comps.Clear();

                int totalComponents = CycleComponents.Count;

                // commented out because it's not necessary, when welding/grinding it gets fully recomputed anyway.
                // auto-scroll while welding/grinding the block
                // also nice side effect of this not triggering unless components change
                //IMySlimBlock aimedBlock = Main.EquipmentMonitor.AimedBlock;
                //if(inputReadable && aimedBlock != null && MyAPIGateway.Input.IsGameControlPressed(MyControlsSpace.PRIMARY_TOOL_ACTION))
                //{
                //    int midPoint = MaxVisible / 2;
                //    Index = MathHelper.FloorToInt(Math.Max(0, (aimedBlock.BuildLevelRatio * totalComponents) - midPoint));
                //}

                int scrollShow = MaxVisible;
                int maxIndex = totalComponents - scrollShow;
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
                    if(idx >= totalComponents)
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