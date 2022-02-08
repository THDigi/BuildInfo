using System;
using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using VRage.Collections;
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

        /// <summary>
        /// Called when HUD components are changed by scroll, with the original full list of components given as parameter.
        /// </summary>
        public event Action<ListReader<MyHudBlockInfo.ComponentInfo>> ScrollUpdate;

        bool ShowScrollHint;
        MyDefinitionId? ComputedForDefinitionId;
        readonly List<MyHudBlockInfo.ComponentInfo> CycleComponents = new List<MyHudBlockInfo.ComponentInfo>(16);

        const int ModifiedComponentIdentifier = -69;

        MyHudBlockInfo.ComponentInfo HintComponent = new MyHudBlockInfo.ComponentInfo()
        {
            Icons = new string[1],
            AvailableAmount = ModifiedComponentIdentifier,
        };

        readonly string IconUp;
        readonly string IconDown;

        public BlockInfoScrollComponents(BuildInfoMod main) : base(main)
        {
            IconUp = Utils.GetModFullPath(@"Textures\ScrollComponentsUp.dds");
            IconDown = Utils.GetModFullPath(@"Textures\ScrollComponentsDown.dds");
        }

        public override void RegisterComponent()
        {
            Main.GameBlockInfoHandler.RegisterHudChangedEvent(HudInfoChanged, 50000);
            Main.EquipmentMonitor.BlockChanged += EquipmentBlockChanged;
            Main.Config.ScrollableComponentsList.ValueAssigned += ConfigValueSet;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.EquipmentMonitor.BlockChanged -= EquipmentBlockChanged;
            Main.Config.ScrollableComponentsList.ValueAssigned -= ConfigValueSet;
        }

        void ConfigValueSet(bool oldValue, bool newValue, SettingBase<bool> setting)
        {
            if(oldValue != newValue)
            {
                ComputedForDefinitionId = null;
                CycleComponents.Clear();
                Index = 0;
                IndexOffset = 0;
                ShowDownHint = false;
                ShowUpHint = false;

                Main.GameBlockInfoHandler.ForceResetBlockInfo();
            }
        }

        void EquipmentBlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            if(Main.GameConfig.HudState == HudState.HINTS)
            {
                ShowScrollHint = true;
            }

            if(def == null)
            {
                SetUpdateMethods(UpdateFlags.UPDATE_INPUT, false);
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
            }
        }

        void HudInfoChanged(MyHudBlockInfo hud)
        {
            if(!Main.Config.ScrollableComponentsList.Value)
                return;

            bool enableUpdate = false;

            try
            {
                List<MyHudBlockInfo.ComponentInfo> hudComps = hud.Components;

                // check if components are already modified by this
                if(hudComps.Count > 0 && hudComps[0].AvailableAmount == ModifiedComponentIdentifier)
                {
                    if(CycleComponents.Count > 0)
                    {
                        //MyAPIGateway.Utilities.ShowNotification($"{GetType().Name}: already edited comps and they're stored, ignored.", 3000, FontsHandler.YellowSh); // DEBUG notify
                    }
                    else
                    {
                        CycleComponents.Clear();
                        ComputedForDefinitionId = null;
                        Index = 0;
                        IndexOffset = 0;

                        Main.GameBlockInfoHandler.ForceResetBlockInfo();

                        //MyAPIGateway.Utilities.ShowNotification($"{GetType().Name}: already edited comps but not stored, forced reset", 3000, FontsHandler.RedSh); // DEBUG notify
                    }
                    return;
                }

                CycleComponents.Clear();
                ComputedForDefinitionId = null;
                Index = 0;
                IndexOffset = 0;

                MyCubeBlockDefinition blockDef;
                if(MyDefinitionManager.Static.TryGetCubeBlockDefinition(hud.DefinitionId, out blockDef))
                {
                    #region Refresh MaxVisible
                    bool hudHints = (Main.GameConfig.HudState == HudState.HINTS);
                    bool inShip = (MyAPIGateway.Session.ControlledObject is IMyCubeBlock);

                    if(inShip)
                        MaxVisible = (hudHints ? MaxCompsShipHudHints : MaxCompsShip);
                    else
                        MaxVisible = (hudHints ? MaxCompsCharacterHudHints : MaxCompsCharacter);
                    #endregion

                    if(hudComps.Count > MaxVisible)
                    {
                        CycleComponents.Clear();

                        for(int i = 0; i < hudComps.Count; i++)
                        {
                            MyHudBlockInfo.ComponentInfo comp = hudComps[i];

                            // scrollbar made this obsolete
                            // rename it in comps list (not the def) to better visually track
                            //comp.ComponentName = $"{(i + 1).ToString()}. {comp.ComponentName}";

                            // using this as a modified identifier because it's never shown.
                            comp.AvailableAmount = ModifiedComponentIdentifier;

                            CycleComponents.Add(comp);
                        }

                        ComputedForDefinitionId = hud.DefinitionId;

                        #region auto-scroll depending on build progress
                        if(!MyCubeBuilder.Static.IsActivated)
                        {
                            int totalComponents = CycleComponents.Count;
                            int midPoint = MaxVisible / 2;

                            Index = totalComponents; // start from the top, then go lower if any component has no stockpile/mounted comps

                            for(int i = 0; i < totalComponents; i++)
                            {
                                MyHudBlockInfo.ComponentInfo comp = CycleComponents[i];
                                if(comp.InstalledCount < comp.TotalCount)
                                {
                                    Index = i - 1;
                                    break;
                                }
                            }

                            Index -= midPoint;
                        }
                        #endregion

                        int maxIndex = CycleComponents.Count - MaxVisible;
                        Index = MathHelper.Clamp(Index, 0, maxIndex);
                        IndexOffset = maxIndex - Index;

                        RefreshVisibleComponents();
                        enableUpdate = true;
                    }
                }
            }
            finally
            {
                SetUpdateMethods(UpdateFlags.UPDATE_INPUT, enableUpdate);
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, enableUpdate);
            }
        }

        public override void UpdateDraw()
        {
            if(!Main.Config.ScrollableComponentsList.Value)
                return;

            if(CycleComponents.Count == 0)
                return;

            MyHudBlockInfo hud = MyHud.BlockInfo;

            // override welder's missing component highlight to account for scrolling
            if(hud.MissingComponentIndex >= 0 && Main.EquipmentMonitor.IsAnyWelder)
            {
                int newMissingIndex = -1;
                for(int i = 0; i < hud.Components.Count; i++)
                {
                    MyHudBlockInfo.ComponentInfo comp = hud.Components[i];
                    if(comp.InstalledCount < comp.TotalCount)
                    {
                        newMissingIndex = i;
                        break;
                    }
                }

                if(hud.MissingComponentIndex != newMissingIndex)
                {
                    hud.MissingComponentIndex = newMissingIndex;
                    Main.GameBlockInfoHandler.RedrawBlockInfo();
                }
            }
        }

        public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(paused || inMenu
            || CycleComponents.Count == 0
            || Main.EquipmentMonitor.BlockDef == null
            || !Main.Config.ScrollableComponentsList.Value
            || Main.GameConfig.HudState == HudState.OFF)
                return;

            int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
            if(scroll != 0 && MyAPIGateway.Input.IsAnyShiftKeyPressed())
            {
                if(!ShowScrollHint) // first scroll should uncover the scroll hint component
                {
                    if(scroll > 0)
                        Index++;
                    else if(scroll < 0)
                        Index--;
                }

                ShowScrollHint = false;
                RefreshVisibleComponents();
                Main.GameBlockInfoHandler.RedrawBlockInfo();
            }
        }

        void RefreshVisibleComponents()
        {
            List<MyHudBlockInfo.ComponentInfo> comps = MyHud.BlockInfo.Components;
            comps.Clear();

            int totalComponents = CycleComponents.Count;

            int maxScroll = totalComponents - MaxVisible;
            Index = MathHelper.Clamp(Index, 0, maxScroll);
            IndexOffset = maxScroll - Index;

            ShowUpHint = (ShowScrollHint && Index == 0);
            ShowDownHint = (!ShowUpHint && ShowScrollHint && Index > 0);

            // HACK: based on math from MyGuiControlBlockInfo.Draw(float transitionAlpha, float backgroundTransitionAlpha) to make the font be white while also having any numbers.
            HintComponent.TotalCount = totalComponents;
            HintComponent.MountedCount = totalComponents;
            HintComponent.ComponentName = (MyAPIGateway.Input.IsJoystickLastUsed ? "(More components)" : "(Shift+Scroll)"); // TODO: gamepad control to scroll

            int lastIdx = (MaxVisible - 1);

            for(int i = 0; i <= lastIdx; i++)
            {
                int idx = i + Index;
                if(idx >= totalComponents)
                    break; // final redundancy

                if(ShowDownHint && i == 0)
                {
                    HintComponent.Icons[0] = IconDown;
                    HintComponent.StockpileCount = (Index + 1) - totalComponents;
                    comps.Add(HintComponent);
                    continue;
                }

                if(ShowUpHint && i == lastIdx)
                {
                    HintComponent.Icons[0] = IconUp;
                    HintComponent.StockpileCount = (IndexOffset + 1) - totalComponents;
                    comps.Add(HintComponent);
                    continue;
                }

                comps.Add(CycleComponents[idx]);
            }

            try
            {
                ScrollUpdate?.Invoke(CycleComponents);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}