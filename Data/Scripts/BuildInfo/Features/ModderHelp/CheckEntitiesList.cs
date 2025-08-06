using System;
using System.Collections.Generic;
using System.Text;
using Digi.ComponentLib;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.ModderHelp
{
    /// <summary>
    /// Checks the MyEntities.OverlapRBElementList if it has elements, which means it's not been cleared by
    ///   other users and it WILL leak into other reads, causing all sorts of bugs.
    ///   
    /// This detection method isn't going to catch all the offenders on the main thread.
    ///   e.g. someone doesn't clear their list first, then someone reads that and clears their list, then this method runs.
    ///   
    /// This doesn't cover uses from threads, because m_overlapRBElementList is ThreadStatic it would need more research on how to read those without causing problems.
    /// </summary>
    public class CheckEntitiesList : ModComponent
    {
        const int ErrorCooldownSeconds = 60;

        List<MyEntity> Entities;
        int ErrorCooldown;

        public CheckEntitiesList(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            var sphere = new BoundingSphereD(new Vector3D(double.MinValue), 0f);

            Entities = MyEntities.GetTopMostEntitiesInSphere(ref sphere); // HACK: gets us the reference to the MyEntities.OverlapRBElementList

            if(Entities == null)
            {
                Log.Error("GetTopMostEntitiesInSphere() returned null?!");
                return;
            }

            Entities.Clear(); // just making sure this mod isn't causing the error

            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public override void UnregisterComponent()
        {
        }

        public override void UpdateAfterSim(int tick)
        {
            if(Entities != null && Entities.Count > 0 && ErrorCooldown < tick)
            {
                const string IngameMessage = "OverlapRBElementList is not empty! SE Log has details.";

                StringBuilder sb = new StringBuilder(1024);

                sb.AppendLine("OverlapRBElementList is not empty! This can cause sneaky bugs in game and mods using the methods mentioned below.");
                sb.AppendLine("");
                sb.AppendLine("The list of methods that can cause the problem or suffer from it:");
                sb.AppendLine("- MyEntities.GetEntitiesInAABB() (both overloads)");
                sb.AppendLine("- MyEntities.GetEntitiesInSphere()");
                sb.AppendLine("- MyEntities.GetEntitiesInOBB()");
                sb.AppendLine("- MyEntities.GetTopMostEntitiesInSphere()");
                sb.AppendLine("");
                sb.AppendLine("Problem for users of these methods is that they can get results that are nowhere near their requested area.");
                sb.AppendLine("And causing the problem involves not clearing the list given by the above methods... and yes this all could've been avoided if Keen cleared them, which was reported and dismissed, so here we are.");
                sb.AppendLine("");
                sb.AppendLine("One way to find the cause (which is likely a mod) is to search for \"MyEntities.GetEntitiesIn\" and \"MyEntities.GetTopMostEntitiesInSphere\" in all .cs files in your workshop download folder, and see which ones do not call .Clear() on the list given by those methods, also they should not keep it for later!");
                sb.AppendLine("If you need help tracking down the offending mod, ask in the KeenSWH discord server ( https://discord.gg/keenswh ) at #modding-programming.");

                if(MyAPIGateway.Session.PromoteLevel >= MyPromoteLevel.SpaceMaster)
                {
                    sb.AppendLine("");
                    sb.AppendLine("Since you're SpaceMaster or higher, here's the list of entities (up to 30) to maybe identify the cause.");

                    for(int i = 0; i < Math.Min(30, Entities.Count); i++)
                    {
                        MyEntity ent = Entities[i];
                        if(ent == null)
                        {
                            sb.AppendLine("    null entry!! this can crash the game as it's unexpected.");
                        }
                        else
                        {
                            Vector3D p = ent.PositionComp.WorldVolume.Center;
                            sb.AppendLine($"    GPS:{ent.GetType().Name}:{p.X:0.##}:{p.Y:0.##}:{p.Z:0.##}:#FF0000:");
                        }
                    }

                    if(Entities.Count > 30)
                        sb.AppendLine($"    ...and {Entities.Count - 30} more.");
                }

                Log.Error(sb.ToString(), null);
                MyAPIGateway.Utilities.ShowMessage("ERROR", IngameMessage);

                Entities.Clear(); // fix the issue to not get the error again

                ErrorCooldown = tick + Constants.TicksPerSecond * ErrorCooldownSeconds;
            }

            //if(MyAPIGateway.Input.IsKeyPress(VRage.Input.MyKeys.N))
            //{
            //    MyAPIGateway.Utilities.ShowNotification("messing things up...", 16);
            //    var sphere = new BoundingSphereD(MyAPIGateway.Session.Camera.Position, 5000);
            //    MyEntities.GetTopMostEntitiesInSphere(ref sphere);
            //}
        }
    }
}