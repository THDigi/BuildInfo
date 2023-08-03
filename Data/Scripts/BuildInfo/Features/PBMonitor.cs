using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Digi.BuildInfo.Features
{
    public struct PBEcho
    {
        public string EchoText;
        public int AtTick;
    }

    public class PBMonitor : ModComponent
    {
        public Dictionary<long, PBEcho> PBEcho = new Dictionary<long, PBEcho>();

        readonly HashSet<long> EventHookedPBs = new HashSet<long>();
        readonly HashSet<long> MonitorPBs = new HashSet<long>();

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
            if(MonitorPBs.Add(pb.EntityId)) // set.Add() returns true if it was added, false if it existed already
            {
                pb.PropertiesChanged += PBPropertiesChanged;
            }
        }

        void UnmonitorPB(IMyProgrammableBlock pb)
        {
            pb.PropertiesChanged -= PBPropertiesChanged;

            MonitorPBs.Remove(pb.EntityId);
            PBEcho.Remove(pb.EntityId);
        }

        void PBPropertiesChanged(IMyTerminalBlock tb)
        {
            IMyProgrammableBlock pb = tb as IMyProgrammableBlock;
            if(pb == null || pb.MarkedForClose || string.IsNullOrEmpty(pb.ProgramData))
                return;

            StringBuilder echoSB = pb.GetDetailedInfo();
            if(echoSB == null || echoSB.Length == 0)
                return; // ignore empty detailed info, likely the bug we're trying to overcome

            bool updateStoredEcho = true;

            PBEcho pbe;
            if(PBEcho.TryGetValue(pb.EntityId, out pbe))
            {
                // StringBuilderExtensions.EqualsStrFast() inlined (because prohibited and mod profiler)
                bool equals = true;
                string prevEcho = pbe.EchoText;

                if(echoSB.Length != prevEcho.Length)
                {
                    equals = false;
                }
                else
                {
                    for(int i = 0; i < prevEcho.Length; i++)
                    {
                        if(echoSB[i] != prevEcho[i])
                        {
                            equals = false;
                            break;
                        }
                    }
                }

                updateStoredEcho = !equals;
            }

            if(updateStoredEcho)
            {
                PBEcho[pb.EntityId] = new PBEcho()
                {
                    EchoText = echoSB.ToString(),
                    AtTick = Main.Tick,
                };
            }

            pb.RefreshCustomInfo(); // fix for previous custominfo lingering when PB actually has detailed info
        }
    }
}
