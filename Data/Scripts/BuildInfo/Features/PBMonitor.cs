using System;
using System.Collections.Generic;
using Digi.ComponentLib;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Digi.BuildInfo.Features
{
    public struct PBData
    {
        public readonly string EchoText;
        public readonly int SavedAtTick;

        public PBData(string echoText, int savedAtTick)
        {
            EchoText = echoText;
            SavedAtTick = savedAtTick;
        }
    }

    public class PBMonitor : ModComponent
    {
        public Dictionary<long, PBData> PBData = new Dictionary<long, PBData>();

        readonly HashSet<long> EventHookedPBs = new HashSet<long>();
        readonly HashSet<IMyProgrammableBlock> MonitorPBs = new HashSet<IMyProgrammableBlock>();

        public PBMonitor(BuildInfoMod main) : base(main)
        {
            // HACK: MP clients only get PB detailed info when in terminal, making this feature useless for them
            if(!MyAPIGateway.Multiplayer.IsServer)
                return;

            // catch already placed blocks too
            Main.BlockMonitor.BlockAdded += GlobalBlockAdded;
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            Main.BlockMonitor.BlockAdded -= GlobalBlockAdded;
        }

        void GlobalBlockAdded(IMySlimBlock slim)
        {
            // Reminder: this can get called multiple times for same block, like for grid merging/splitting.

            IMyProgrammableBlock pb = slim?.FatBlock as IMyProgrammableBlock;
            if(pb != null && EventHookedPBs.Add(pb.EntityId))
            {
                pb.OnMarkForClose += PBMarkedForClose;
                pb.OwnershipChanged += PBOwnershipChanged;

                if(pb.HasLocalPlayerAccess())
                {
                    MonitorPB(pb);
                }
            }
        }

        void PBOwnershipChanged(IMyTerminalBlock tb)
        {
            try
            {
                IMyProgrammableBlock pb = (IMyProgrammableBlock)tb;
                if(pb.HasLocalPlayerAccess())
                {
                    MonitorPB(pb);
                }
                else
                {
                    UnmonitorPB(pb);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void PBMarkedForClose(IMyEntity ent)
        {
            try
            {
                IMyProgrammableBlock pb = (IMyProgrammableBlock)ent;
                pb.OnMarkForClose -= PBMarkedForClose;
                pb.OwnershipChanged -= PBOwnershipChanged;
                EventHookedPBs.Remove(pb.EntityId);

                UnmonitorPB(pb);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void MonitorPB(IMyProgrammableBlock pb)
        {
            if(MonitorPBs.Add(pb)) // set.Add() returns true if it was added, false if it existed already
            {
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);

                //pb.PropertiesChanged += PBPropertiesChanged;
            }
        }

        void UnmonitorPB(IMyProgrammableBlock pb)
        {
            //pb.PropertiesChanged -= PBPropertiesChanged;

            MonitorPBs.Remove(pb);
            PBData.Remove(pb.EntityId);

            if(MonitorPBs.Count == 0)
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
        }

        public override void UpdateAfterSim(int tick)
        {
            foreach(IMyProgrammableBlock pb in MonitorPBs)
            {
                if(pb.MarkedForClose)
                    continue; // skip deleted/destroyed/un-streamed PBs, gets removed elsewhere

                if(string.IsNullOrEmpty(pb.ProgramData))
                    continue; // skip PBs with nothing in them

                string echoText = pb.DetailedInfo; // allocates, so only call once
                if(!string.IsNullOrEmpty(echoText))
                {
                    PBData[pb.EntityId] = new PBData(echoText, tick);
                }
            }
        }

        // doesn't seem reliable, like editing a PB with a script that instantly throws isn't captured
        //void PBPropertiesChanged(IMyTerminalBlock tb)
        //{
        //    string echoText = tb.DetailedInfo; // allocates, so only call once
        //    if(!string.IsNullOrEmpty(echoText))
        //    {
        //        PBData[tb.EntityId] = new PBData(echoText, Main.Tick);
        //    }
        //}
    }
}
