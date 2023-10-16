using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Systems
{
    public class GameBlockInfoHandler : ModComponent
    {
        int HudInfoVersion = 0;
        List<Callback> HudChangedCallbacks = new List<Callback>();

        struct Callback
        {
            public readonly Action<MyHudBlockInfo> Action;
            public readonly int Priority;

            public Callback(Action<MyHudBlockInfo> action, int priority)
            {
                Action = action;
                Priority = priority;
            }
        }

        readonly Dictionary<MyDefinitionId, MyComponentStack> ComponentStacks = new Dictionary<MyDefinitionId, MyComponentStack>(MyDefinitionId.Comparer);

        public GameBlockInfoHandler(BuildInfoMod main) : base(main)
        {
            UpdateOrder = -10;
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true); // TODO: optimize?
        }

        public override void RegisterComponent()
        {
            MyAPIGateway.Gui.GuiControlRemoved += GuiScreenRemoved;

            Main.EquipmentMonitor.BlockChanged += BlockChanged;
            Main.GameConfig.HudStateChanged += HudStateChanged;
        }

        public override void UnregisterComponent()
        {
            HudChangedCallbacks.Clear();

            if(MyAPIGateway.Gui != null)
                MyAPIGateway.Gui.GuiControlRemoved -= GuiScreenRemoved;

            if(!Main.ComponentsRegistered)
                return;

            Main.EquipmentMonitor.BlockChanged -= BlockChanged;
            Main.GameConfig.HudStateChanged -= HudStateChanged;
        }

        /// <summary>
        /// Make game redraw block info. Does not cause <see cref="RegisterHudChangedEvent(Action{MyHudBlockInfo}, int)"/> callbacks to trigger.
        /// </summary>
        public void RedrawBlockInfo()
        {
            // HACK: this causes Version++ making the blockinfo HUD to redraw
            MyHud.BlockInfo.DefinitionId = MyHud.BlockInfo.DefinitionId;
            HudInfoVersion = MyHud.BlockInfo.Version; // don't trigger event for this version change
        }

        /// <summary>
        /// Redraw + clear current definitionid, makes game re-add components and etc on its own and will trigger <see cref="RegisterHudChangedEvent(Action{MyHudBlockInfo}, int)"/> callbacks if it does so.
        /// </summary>
        public void ForceResetBlockInfo()
        {
            MyHud.BlockInfo.DefinitionId = default(MyDefinitionId);
            MyHud.BlockInfo.Components?.Clear();

            HudInfoVersion = MyHud.BlockInfo.Version - 5;

            // HACK: force refresh held tool's block info
            IMyEngineerToolBase tool = MyAPIGateway.Session?.Player?.Character?.EquippedTool as IMyEngineerToolBase;
            if(tool != null && (tool is IMyWelder || tool is IMyAngleGrinder)) // only these tools require it
            {
                MyCockpit cockpitHax = null; // doesn't need any object
                tool.OnControlReleased();
                tool.OnControlAcquired(Utils.CastHax(cockpitHax?.Pilot, MyAPIGateway.Session.Player.Character));
            }
        }

        /// <summary>
        /// Called when MyHud.BlockInfo.DefinitionId changes or an equipped is changed and that tool changes BlockInfo.
        /// </summary>
        public void RegisterHudChangedEvent(Action<MyHudBlockInfo> callback, int priority)
        {
            HudChangedCallbacks.Add(new Callback(callback, priority));
            HudChangedCallbacks.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        public void UnregisterDefIdChanged(Action<MyHudBlockInfo> callback)
        {
            for(int i = (HudChangedCallbacks.Count - 1); i >= 0; i--)
            {
                Callback cb = HudChangedCallbacks[i];
                if(cb.Action == callback)
                    HudChangedCallbacks.RemoveAt(i);
            }
        }

        /// <summary>
        /// Set the BlockInfo HUD to all the data of the given block definition.
        /// Does not trigger the <see cref="RegisterHudChangedEvent(Action{MyHudBlockInfo}, int)"/> callbacks.
        /// </summary>
        public void SetHudInfoForBlock(MyCubeBlockDefinition blockDef)
        {
            MyHudBlockInfo hud = MyHud.BlockInfo;

            // HACK TODO: continue hardcoded hunt

            // HACK: hardcoded from MyWelder.DrawHud()

            hud.DefinitionId = blockDef.Id;
            //HudInfoVersion = hud.Version; // allow event to trigger

            hud.MissingComponentIndex = -1;
            hud.BlockName = blockDef.DisplayNameText;
            hud.PCUCost = blockDef.PCU;
            hud.BlockIcons = blockDef.Icons;
            hud.BlockIntegrity = 0.0000001f;
            hud.CriticalIntegrity = blockDef.CriticalIntegrityRatio;
            hud.CriticalComponentIndex = blockDef.CriticalGroup;
            hud.OwnershipIntegrity = blockDef.OwnershipIntegrityRatio;
            hud.BlockBuiltBy = 0;
            hud.GridSize = blockDef.CubeSize;

            hud.Components.Clear();

            // HACK: a simpler variant of MySlimBlock.SetBlockComponentsInternal(), also inaccessible MySlimBlock.ComponentStack so have to make'em and cache'em
            MyComponentStack compStack;
            if(!ComponentStacks.TryGetValue(blockDef.Id, out compStack))
            {
                compStack = new MyComponentStack(blockDef, 0, 0);
                ComponentStacks[blockDef.Id] = compStack;
            }

            for(int i = 0; i < compStack.GroupCount; i++)
            {
                MyComponentStack.GroupInfo groupInfo = compStack.GetGroupInfo(i);
                hud.Components.Add(new MyHudBlockInfo.ComponentInfo()
                {
                    DefinitionId = groupInfo.Component.Id,
                    ComponentName = groupInfo.Component.DisplayNameText,
                    Icons = groupInfo.Component.Icons,
                    TotalCount = groupInfo.TotalCount,
                    MountedCount = 0,
                    StockpileCount = 0,
                    AvailableAmount = 0,
                });
            }

            hud.SetContextHelp(blockDef);
        }

        /// <summary>
        /// Set the BlockInfo HUD to all the data of the given tool definition.
        /// Does not trigger the <see cref="RegisterHudChangedEvent(Action{MyHudBlockInfo}, int)"/> callbacks.
        /// </summary>
        public void SetHudInfoForTool(MyPhysicalItemDefinition toolDef)
        {
            MyHudBlockInfo hud = MyHud.BlockInfo;

            // HACK: hardcoded from MyEngineerToolBase.DrawHud() because calling it doesn't work

            hud.DefinitionId = toolDef.Id;
            //HudInfoVersion = hud.Version; // allow event to trigger

            hud.MissingComponentIndex = -1;
            hud.BlockName = toolDef.DisplayNameText;
            hud.PCUCost = 0;
            hud.BlockIcons = toolDef.Icons;
            hud.BlockIntegrity = 1f;
            hud.CriticalIntegrity = 0f;
            hud.CriticalComponentIndex = 0;
            hud.OwnershipIntegrity = 0f;
            hud.BlockBuiltBy = 0L;
            hud.GridSize = MyCubeSize.Small;
            hud.Components.Clear();

            hud.SetContextHelp(toolDef);
        }

        void BlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            // tool holstering forces recompute, required for cubebuilder unequipping and re-equipping the same block
            if(def == null)
            {
                ForceResetBlockInfo();
            }
        }

        // mainly for creative tools toggle, but also whatever else would cause blockinfo to require a re-compute
        void GuiScreenRemoved(object screen)
        {
            ForceResetBlockInfo();
        }

        void HudStateChanged(HudStateChangedInfo info)
        {
            ForceResetBlockInfo();
        }

        // HACK: it's very important this triggers before MyGuiControlBlockInfo.Draw() gets called but also after MyWelder.DrawHud()
        public override void UpdateDraw()
        {
            if(Main.IsPaused)
                return;

            MyHudBlockInfo hud = MyHud.BlockInfo;
            if(!hud.Visible)
                return;

            if(HudInfoVersion != hud.Version)
            {
                HudInfoVersion = hud.Version;

                for(int i = 0; i < HudChangedCallbacks.Count; i++)
                {
                    HudChangedCallbacks[i].Action.Invoke(hud);
                }
            }
        }
    }
}
