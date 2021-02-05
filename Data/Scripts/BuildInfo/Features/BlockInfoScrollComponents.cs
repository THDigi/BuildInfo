using System;
using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Digi.Input;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    // HACK: 1005 priority as it needs to be right after MyCubeBuilder.UpdateAfterSimulation() which is also a session comp.
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation, priority: 1005)]
    public class ScrollableComponents : MySessionComponentBase
    {
        public const int MaxVisibleHudHints = 9;
        public const int MaxVisibleHudMinChar = 13;
        public const int MaxVisibleHudMinShip = 7;

        public static ScrollableComponents Instance { get; private set; }

        public int IndexOffset { get; private set; }
        public int Index { get; private set; }
        public int MaxVisible { get; private set; } = 9;

        readonly List<MyHudBlockInfo.ComponentInfo> CycleComponents = new List<MyHudBlockInfo.ComponentInfo>(16);

        MyDefinitionId PrevHeldId;
        HudState HudState;
        int Counter;
        bool Refresh;
        bool ShowScrollHint = true;

        MyHudBlockInfo.ComponentInfo HintComponent = new MyHudBlockInfo.ComponentInfo()
        {
            ComponentName = "^ Shift+Scroll ^",
            Icons = new string[] { @"Textures\GUI\Icons\HUD 2017\HelpScreen.png" },
        };

        private static BuildInfoMod Main => BuildInfoMod.Instance;

        public override void LoadData()
        {
            Instance = this;
        }

        public override void BeforeStart()
        {
            // HACK: avoid some issue with first equipped block after reloading
            if(MyHud.BlockInfo != null)
            {
                MyHud.BlockInfo.DefinitionId = default(MyDefinitionId);
                MyHud.BlockInfo.Components?.Clear();
            }

            Main.Config.BlockInfoAdditions.ValueAssigned += BlockInfoAdditionsChanged;
            Main.GameConfig.HudStateChanged += HudStateChanged;
            HudState = Main.GameConfig.HudState;
        }

        protected override void UnloadData()
        {
            Instance = null;

            if(!Main.ComponentsRegistered)
                return;

            Main.Config.BlockInfoAdditions.ValueAssigned -= BlockInfoAdditionsChanged;
            Main.GameConfig.HudStateChanged -= HudStateChanged;
        }

        void BlockInfoAdditionsChanged(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            Index = 0;
            IndexOffset = 0;

            // force refresh, both in this mod and in game code
            MyHud.BlockInfo.DefinitionId = default(MyDefinitionId);
            MyHud.BlockInfo.Components.Clear();
        }

        void HudStateChanged(HudState prevState, HudState state)
        {
            HudState = Main.GameConfig.HudState;

            // refresh all the things
            PrevHeldId = default(MyDefinitionId);
            MyHud.BlockInfo.DefinitionId = default(MyDefinitionId);
            MyHud.BlockInfo.Components.Clear();
            Refresh = true;
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if(!Main.Config.BlockInfoAdditions.Value)
                    return;

                var cubeBuilder = MyCubeBuilder.Static;
                var currentDef = cubeBuilder?.CurrentBlockDefinition;
                if(currentDef == null || !cubeBuilder.IsActivated)
                    return;

                MaxVisible = MaxVisibleHudHints;
                if(HudState == HudState.BASIC)
                {
                    if(MyAPIGateway.Session.ControlledObject is IMyShipController)
                        MaxVisible = MaxVisibleHudMinShip;
                    else
                        MaxVisible = MaxVisibleHudMinChar;
                }

                if(PrevHeldId != currentDef.Id)
                {
                    Index = 0;
                    IndexOffset = 0;
                    Counter = 9999;
                    CycleComponents.Clear();
                    Refresh = true;
                    ShowScrollHint = true;

                    PrevHeldId = currentDef.Id;
                    var comps = MyHud.BlockInfo.Components;

                    if(comps.Count == 0)
                    {
                        // force component list update
                        MyHud.BlockInfo.DefinitionId = default(MyDefinitionId);
                        CubeBuilderHax.RefreshBlockInfoHud();
                    }

                    if(comps.Count > MaxVisible)
                    {
                        for(int i = 0; i < comps.Count; i++)
                        {
                            var comp = comps[i];

                            // rename it in comps list (not the def) to better visually track
                            comp.ComponentName = $"{(i + 1).ToString()}. {comp.ComponentName}";

                            CycleComponents.Add(comp);
                        }
                    }
                }

                if(CycleComponents.Count == 0)
                    return;

                if(InputLib.IsInputReadable(checkSpectator: false) && MyAPIGateway.Input.IsAnyShiftKeyPressed())
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
                    Counter = 0;
                    var comps = MyHud.BlockInfo.Components;

                    comps.Clear();

                    int scrollShow = MaxVisible;
                    int maxIndex = CycleComponents.Count - scrollShow;
                    Index = MathHelper.Clamp(Index, 0, maxIndex);
                    IndexOffset = maxIndex - Index;

                    int show = (ShowScrollHint ? scrollShow - 1 : scrollShow);

                    for(int i = 0; i < show; ++i)
                    {
                        comps.Add(CycleComponents[i + Index]);
                    }

                    if(ShowScrollHint)
                        comps.Add(HintComponent);

                    // HACK: this causes Version++ making the HUD to refresh component draw
                    MyHud.BlockInfo.DefinitionId = MyHud.BlockInfo.DefinitionId;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        class CubeBuilderHax : MyCubeBuilder
        {
            // HACK: protected static in public non-sealed classes? might as well make'em public.
            public static void RefreshBlockInfoHud() => MyCubeBuilder.UpdateBlockInfoHud();
        }
    }
}
