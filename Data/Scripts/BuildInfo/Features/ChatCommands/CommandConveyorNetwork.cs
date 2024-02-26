using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.Overlays.ConveyorNetwork;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandConveyorNetwork : Command
    {
        public CommandConveyorNetwork() : base("conveyorvis", "conveyors", "cn")
        {
        }

        public override void Execute(Arguments args)
        {
            if(MyAPIGateway.Session?.Player == null)
            {
                PrintChat(Constants.WarnPlayerIsNull, FontsHandler.RedSh);
                return;
            }

            bool isRendering = Main.ConveyorNetworkView.Render.RenderGrids.Count > 0;
            bool aimedAtRendering = false;

            IMySlimBlock optionalAimedBlock = null; // Main.EquipmentMonitor.AimedBlock ?? Main.EquipmentMonitor.BuilderAimedBlock;
            IMyCubeGrid aimedGrid = optionalAimedBlock?.CubeGrid;

            // look-at grid support
            if(aimedGrid == null)
            {
                MatrixD camWM = MyAPIGateway.Session.Camera.WorldMatrix;

                const double MaxDistance = 50;

                List<IHitInfo> hits = new List<IHitInfo>(16);
                MyAPIGateway.Physics.CastRay(camWM.Translation, camWM.Translation + camWM.Forward * MaxDistance, hits, CollisionLayers.NoVoxelCollisionLayer);

                // find first grid hit, ignore everything else
                foreach(IHitInfo hit in hits)
                {
                    aimedGrid = hit.HitEntity as IMyCubeGrid;
                    if(aimedGrid != null)
                        break;
                }
            }

            if(aimedGrid != null)
            {
                foreach(var grid in Main.ConveyorNetworkView.Render.RenderGrids.Keys)
                {
                    if(aimedGrid == grid)
                    {
                        aimedAtRendering = true;
                        break;
                    }
                }
            }

            // change aimed block even if same grid
            if(optionalAimedBlock != null && optionalAimedBlock != Main.ConveyorNetworkView.TargetBlock)
            {
                Main.ConveyorNetworkView.ShowFor(aimedGrid, optionalAimedBlock);
                return;
            }

            // change grid with look-at or aiming
            if(aimedGrid != null && !aimedAtRendering)
            {
                Main.ConveyorNetworkView.ShowFor(aimedGrid, optionalAimedBlock);
                return;
            }

            if(isRendering)
            {
                Main.ConveyorNetworkView.StopShowing();
                return;
            }

            if(aimedGrid != null)
            {
                Main.ConveyorNetworkView.ShowFor(aimedGrid, optionalAimedBlock);
            }
            else
            {
                //MyAPIGateway.Utilities.ShowNotification($"{ConveyorNetworkCompute.NotifyPrefix}First look at a grid, or aim at a block with cubebuilder/welder/grinder.", 4000, FontsHandler.RedSh);
                MyAPIGateway.Utilities.ShowNotification($"{ConveyorNetworkCompute.NotifyPrefix}First look at a grid.", 4000, FontsHandler.RedSh);
            }
        }

        public override void PrintHelp(StringBuilder sb)
        {
            foreach(string alias in Aliases)
            {
                sb.Append(ChatCommandHandler.MainCommandPrefix).Append(' ').Append(alias).NewLine();
            }

            sb.Append("  Renders conveyor networks from the ship you're looking at (must be friendly).").NewLine();
            //sb.Append("  Aiming with cubebuilder/tool at a conveyor-able block will trace from that block.").NewLine();
            sb.Append("  Use command again to turn off.").NewLine();
        }
    }
}