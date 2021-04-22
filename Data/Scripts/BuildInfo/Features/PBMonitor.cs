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

        readonly HashSet<IMyProgrammableBlock> PBs = new HashSet<IMyProgrammableBlock>();
        readonly HashSet<long> UpdatedThisTick = new HashSet<long>();

        public PBMonitor(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;

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

            var pb = slim?.FatBlock as IMyProgrammableBlock;
            if(pb != null && PBs.Add(pb)) // set.Add() returns true if it was added, false if it existed already
            {
                pb.OnMarkForClose += PBMarkedForClose;
                //pb.PropertiesChanged += PBPropertiesChanged;
            }
        }

        void PBMarkedForClose(IMyEntity ent)
        {
            try
            {
                var pb = (IMyProgrammableBlock)ent;
                pb.OnMarkForClose -= PBMarkedForClose;
                //pb.PropertiesChanged -= PBPropertiesChanged;
                PBs.Remove(pb);
                PBData.Remove(pb.EntityId);
            }
            catch(Exception e)
            {
                Log.Error(e);
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

        public override void UpdateAfterSim(int tick)
        {
            foreach(var pb in PBs)
            {
                if(pb.MarkedForClose)
                    continue; // skip deleted/destroyed/un-streamed PBs

                if(string.IsNullOrEmpty(pb.ProgramData))
                    continue; // skip PBs with nothing in them

                string echoText = pb.DetailedInfo; // allocates, so only call once
                if(!string.IsNullOrEmpty(echoText))
                {
                    PBData[pb.EntityId] = new PBData(echoText, tick);
                }
            }
        }
    }
}
