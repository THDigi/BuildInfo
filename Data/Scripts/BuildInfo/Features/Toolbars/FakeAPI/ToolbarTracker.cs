using System;
using System.Collections.Generic;
using Digi.BuildInfo.VanillaData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Digi.BuildInfo.Features.Toolbars.FakeAPI
{
    public enum ToolbarId : byte
    {
        Normal = 0,
        Waypoint = 1,
        LockedOn = 2,
    }

    public struct ToolbarHolder
    {
        public Toolbar SingleToolbar;
        public Dictionary<ToolbarId, Toolbar> MultipleToolbars;

        public void Dispose()
        {
            SingleToolbar?.Dispose();

            if(MultipleToolbars != null)
            {
                foreach(var tb in MultipleToolbars.Values)
                    tb.Dispose();
            }
        }
    }

    public class ToolbarTracker : ModComponent
    {
        // other toolbars not accounted for:
        //  MyToolbarComponent has universal character toolbar and points to "current toolbar" (points to Sandbox.Game.Entities.IMyControllableEntity.Toolbar)
        //  MyToolBarCollection has other characters' toolbars
        //  MyControllableSphere has a toolbar for reasons

        public readonly Dictionary<IMyEntity, ToolbarHolder> EntitiesWithToolbars = new Dictionary<IMyEntity, ToolbarHolder>();

        public ToolbarTracker(BuildInfoMod main) : base(main)
        {
            Main.BlockMonitor.BlockAdded += BlockAdded;
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            Main.BlockMonitor.BlockAdded -= BlockAdded;

            if(!Main.ComponentsRegistered)
                return;
        }

        void BlockAdded(IMySlimBlock slim)
        {
            if(slim.FatBlock == null)
                return;

            if(EntitiesWithToolbars.ContainsKey(slim.FatBlock))
                return;

            // it's created but not actually used
            //{
            //    var casted = slim.FatBlock as IMyLargeTurretBase;
            //    if(casted != null)
            //    {
            //        SingleToolbar(slim.FatBlock, MyToolbarType.LargeCockpit);
            //        return;
            //    }
            //}
            {
                var casted = slim.FatBlock as IMyTurretControlBlock;
                if(casted != null)
                {
                    SingleToolbar(slim.FatBlock, MyToolbarType.ButtonPanel, 2, 1);
                    return;
                }
            }
            {
                var casted = slim.FatBlock as MyShipController;
                if(casted != null)
                {
                    var owner = (MyEntity)slim.FatBlock;

                    var th = new ToolbarHolder()
                    {
                        MultipleToolbars = new Dictionary<ToolbarId, Toolbar>(),
                    };
                    EntitiesWithToolbars[owner] = th;

                    th.MultipleToolbars[ToolbarId.Normal] = new Toolbar(owner, casted.ToolbarType);
                    //th.MultipleToolbars[ToolbarId.BuildMode] = new Toolbar(owner, MyToolbarType.BuildCockpit);
                    th.MultipleToolbars[ToolbarId.LockedOn] = new Toolbar(owner, MyToolbarType.ButtonPanel, 2, 1);

                    if(casted is IMyRemoteControl)
                    {
                        th.MultipleToolbars[ToolbarId.Waypoint] = new Toolbar(owner, MyToolbarType.ButtonPanel, 9, 1);
                    }

                    slim.FatBlock.OnClosing += BlockClosing;
                    return;
                }
            }
            {
                var casted = slim.FatBlock as IMySensorBlock;
                if(casted != null)
                {
                    SingleToolbar(slim.FatBlock, MyToolbarType.ButtonPanel, 2, 1);
                    return;
                }
            }
            {
                var casted = slim.FatBlock as IMyTimerBlock;
                if(casted != null)
                {
                    SingleToolbar(slim.FatBlock, MyToolbarType.ButtonPanel, 9, 10);
                    return;
                }
            }
            {
                var casted = slim.FatBlock as IMyAirVent;
                if(casted != null)
                {
                    SingleToolbar(slim.FatBlock, MyToolbarType.ButtonPanel, 2, 1);
                    return;
                }
            }
            {
                var casted = slim.FatBlock as IMyButtonPanel;
                if(casted != null)
                {
                    var def = (MyButtonPanelDefinition)slim.BlockDefinition;
                    SingleToolbar(slim.FatBlock, MyToolbarType.ButtonPanel, Math.Min(def.ButtonCount, 9), def.ButtonCount / 9 + 1);
                    return;
                }
            }
            {
                var casted = slim.FatBlock as IMyEventControllerBlock;
                if(casted != null)
                {
                    SingleToolbar(slim.FatBlock, MyToolbarType.ButtonPanel, 2);
                    return;
                }
            }
            {
                var casted = slim.FatBlock as IMyTargetDummyBlock;
                if(casted != null)
                {
                    SingleToolbar(slim.FatBlock, MyToolbarType.ButtonPanel, 2, 1);
                    return;
                }
            }
            {
                var casted = slim.FatBlock as IMyBasicMissionBlock;
                if(casted != null)
                {
                    var owner = (MyEntity)slim.FatBlock;

                    EntitiesWithToolbars[owner] = new ToolbarHolder()
                    {
                        MultipleToolbars = new Dictionary<ToolbarId, Toolbar>()
                        {
                            [ToolbarId.Normal] = new Toolbar(owner, MyToolbarType.Ship, 9, 1),
                            [ToolbarId.Waypoint] = new Toolbar(owner, MyToolbarType.ButtonPanel, 1, 1),
                        }
                    };

                    slim.FatBlock.OnClosing += BlockClosing;
                    return;
                }
            }
            {
                var casted = slim.FatBlock as IMyPathRecorderBlock;
                if(casted != null)
                {
                    // MyPathRecorderComponent.SetupAction() is the real one, not the one from ctor()
                    SingleToolbar(slim.FatBlock, MyToolbarType.ButtonPanel, 1, 1);
                    return;
                }
            }
            {
                var casted = slim.FatBlock as IMyDefensiveCombatBlock;
                if(casted != null)
                {
                    SingleToolbar(slim.FatBlock, MyToolbarType.ButtonPanel, 2, 1);
                    return;
                }
            }
        }

        void SingleToolbar(IMyCubeBlock block, MyToolbarType toolbarType, int slotsPerPage = 9, int pages = 9)
        {
            var toolbar = new Toolbar((MyEntity)block, toolbarType, slotsPerPage, pages);

            EntitiesWithToolbars[block] = new ToolbarHolder()
            {
                SingleToolbar = toolbar,
            };

            block.OnClosing += BlockClosing;
        }

        void BlockClosing(IMyEntity ent)
        {
            try
            {
                ToolbarHolder th;
                if(EntitiesWithToolbars.TryGetValue(ent, out th))
                {
                    EntitiesWithToolbars.Remove(ent);
                    th.Dispose();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public static MyObjectBuilder_Toolbar GetToolbarFromEntity(IMyEntity ent, ToolbarId toolbarId = ToolbarId.Normal)
        {
            var block = ent as IMyCubeBlock;
            if(block != null)
            {
                if(toolbarId == ToolbarId.Waypoint && ent is IMyRemoteControl)
                {
                    var ap = ent.Components.Get<MyAutopilotComponent>();
                    if(ap != null)
                    {
                        List<MyAutopilotWaypoint> selected = ap.SelectedWaypoints;
                        if(selected != null && selected.Count > 0)
                        {
                            MyObjectBuilder_AutopilotWaypoint selectedOB = selected[0].GetObjectBuilder();

                            var ob = new MyObjectBuilder_Toolbar()
                            {
                                ToolbarType = MyToolbarType.ButtonPanel,
                                Slots = new List<MyObjectBuilder_Toolbar.Slot>(selectedOB.Actions?.Count ?? 0),
                            };

                            if(selectedOB.Actions != null)
                            {
                                for(int i = 0; i < selectedOB.Actions.Count; i++)
                                {
                                    var itemOB = new MyObjectBuilder_Toolbar.Slot()
                                    {
                                        Index = selectedOB.Indexes[i],
                                        Data = selectedOB.Actions[i],
                                        Item = string.Empty,
                                    };

                                    ob.Slots.Add(itemOB);
                                }
                            }

                            return ob;
                        }
                    }

                    return null;
                }

                if(ent is IMyPathRecorderBlock)
                {
                    var prc = ent.Components.Get<MyPathRecorderComponent>();
                    List<MyAutopilotWaypoint> waypoints = prc?.Waypoints;
                    if(waypoints != null && waypoints.Count > 0)
                    {
                        var ob = new MyObjectBuilder_Toolbar()
                        {
                            ToolbarType = MyToolbarType.ButtonPanel,
                            Slots = new List<MyObjectBuilder_Toolbar.Slot>(0),
                        };

                        foreach(MyAutopilotWaypoint wp in waypoints)
                        {
                            if(wp.SelectedForDraw)
                            {
                                var wpOB = wp.GetObjectBuilder();

                                if(wpOB.Actions != null)
                                {
                                    for(int i = 0; i < wpOB.Actions.Count; i++)
                                    {
                                        var itemOB = new MyObjectBuilder_Toolbar.Slot()
                                        {
                                            Index = wpOB.Indexes[i],
                                            Data = wpOB.Actions[i],
                                            Item = string.Empty,
                                        };

                                        ob.Slots.Add(itemOB);
                                    }
                                }

                                break; // HACK: same behavior MyPathRecorderComponent.SelectedWaypointsChanged(), first in order gets the toolbar
                            }
                        }

                        return ob;
                    }

                    return null;
                }

                MyObjectBuilder_CubeBlock blockOB = block.GetObjectBuilderCubeBlock(false);

                {
                    var casted = blockOB as MyObjectBuilder_TimerBlock;
                    if(casted != null)
                        return casted.Toolbar;
                }
                {
                    var casted = blockOB as MyObjectBuilder_SensorBlock;
                    if(casted != null)
                        return casted.Toolbar;
                }
                {
                    var casted = blockOB as MyObjectBuilder_ButtonPanel;
                    if(casted != null)
                        return casted.Toolbar;
                }
                {
                    var casted = blockOB as MyObjectBuilder_DefensiveCombatBlock;
                    if(casted != null)
                        return casted.Toolbar;
                }
                {
                    var casted = blockOB as MyObjectBuilder_EventControllerBlock;
                    if(casted != null)
                        return casted.Toolbar;
                }
                {
                    var casted = blockOB as MyObjectBuilder_TurretControlBlock;
                    if(casted != null)
                        return casted.Toolbar;
                }
                {
                    var casted = blockOB as MyObjectBuilder_ShipController;
                    if(casted != null)
                    {
                        switch(toolbarId)
                        {
                            case ToolbarId.Normal: return casted.Toolbar;
                            // case ToolbarId.BuildMode: return casted.BuildToolbar;
                            case ToolbarId.LockedOn: return casted.OnLockedToolbar;
                            default: Log.Error($"unknown toolbarId={toolbarId} for {block}"); return null;
                        }
                    }
                }

                // HACK: MyObjectBuilder_TargetDummyBlock is not whitelisted, hacky workarounds...
                if(block.BlockDefinition.TypeId == Hardcoded.TargetDummyType)
                {
                    // yes I did first try MyAPIGateway.Utilities.SerializeFromBinary() with the same protobuf class from below, it just does not wanna work
                    // also tried XML as seen below... /shrug.

                    //xml = xml.Replace("MyObjectBuilder_TargetDummyBlock", "FakeOB_TargetDummyBlock");
                    //var ob = MyAPIGateway.Utilities.SerializeFromXML<FakeOB_TargetDummyBlock>(xml);
                    //return ob?.Toolbar;

                    string xml = "";
                    string trimmed = "";
                    try
                    {
                        xml = MyAPIGateway.Utilities.SerializeToXML(blockOB);

                        int start = xml.IndexOf("<Toolbar>") + "<Toolbar>".Length; // skip past it because we're replacing it
                        int end = xml.IndexOf("</Toolbar>", start);
                        trimmed = "<?xml version=\"1.0\" encoding=\"utf-16\"?>\n"
                                          + "<MyObjectBuilder_Toolbar xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                                          + xml.Substring(start, end - start)
                                          + "\n</MyObjectBuilder_Toolbar>";

                        return MyAPIGateway.Utilities.SerializeFromXML<MyObjectBuilder_Toolbar>(trimmed);
                    }
                    catch(Exception e)
                    {
                        string msg = "Failed to workaround target dummy being prohibited";
                        Log.Error($"{msg}\nXML=\n{xml}\n\ntrimmed=\n{trimmed}\nError: {e}", msg);
                        return null;
                    }
                }
            }

            // TODO: other entities...?

            return null;
        }

        //[ProtoContract]
        //public class FakeOB_TargetDummyBlock // : MyObjectBuilder_FunctionalBlock
        //{
        //    [ProtoMember(13)]
        //    public MyObjectBuilder_Toolbar Toolbar;
        //}
    }
}
