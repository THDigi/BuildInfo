using System.Text;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using InternalControllableEntity = Sandbox.Game.Entities.IMyControllableEntity;

namespace Digi.BuildInfo.Features
{
    public class RelativeDampenerInfo : ClientComponent
    {
        private IMyHudNotification notify;

        private long prevControlledEntId;
        private long prevRelativeEntId;

        private StringBuilder sb = new StringBuilder(128);

        public RelativeDampenerInfo(Client mod) : base(mod)
        {
            Flags = UpdateFlags.UPDATE_AFTER_SIM;
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
        }

        public override void UpdateAfterSim(int tick)
        {
            if(!Config.RelativeDampenerInfo.Value)
                return;

            var controlled = MyAPIGateway.Session.ControlledObject as InternalControllableEntity;

            if(prevControlledEntId != controlled.Entity.EntityId)
            {
                prevControlledEntId = controlled.Entity.EntityId;
                prevRelativeEntId = 0;
            }

            var relativeEnt = controlled.RelativeDampeningEntity;
            long relativeEntId = (relativeEnt == null || relativeEnt.MarkedForClose ? 0 : relativeEnt.EntityId);

            if(relativeEntId == prevRelativeEntId)
                return;

            if(notify == null)
                notify = MyAPIGateway.Utilities.CreateNotification("");

            if(relativeEntId == 0)
            {
                notify.Text = "Dampeners no longer relative";
                notify.Font = MyFontEnum.Red;
            }
            else
            {
                if(!Hardcoded.RelativeDampeners_DistanceCheck(controlled.Entity, relativeEnt))
                {
                    // HACK preventing this confusing mess: https://support.keenswh.com/spaceengineers/general/topic/1-192-022-relative-dampeners-allow-engaging-at-larger-distances-then-turn-off
                    controlled.RelativeDampeningEntity = null;
                    prevRelativeEntId = 0;

                    //notify.Text = "Target farther than [100m].";
                    //notify.Font = MyFontEnum.Red;
                    //notify.Show();

                    return;
                }

                sb.Clear();
                sb.Append("Dampeners relative to [");

                int startIndex = sb.Length;

                GetEntName(relativeEnt, sb);

                for(int i = startIndex; i < sb.Length; ++i)
                {
                    var c = sb[i];

                    if(c == '[')
                        sb[i] = '(';
                    else if(c == ']')
                        sb[i] = ')';
                }

                sb.Append(']');

                notify.Text = sb.ToString();
                notify.Font = MyFontEnum.White;
            }

            notify.Show();
            prevRelativeEntId = relativeEntId;
        }

        static StringBuilder GetEntName(MyEntity ent, StringBuilder sb)
        {
            var targetGrid = ent as IMyCubeGrid;
            if(targetGrid != null)
                return sb.Append(targetGrid.CustomName);

            var targetChar = ent as IMyCharacter;
            if(targetChar != null)
                return sb.Append(targetChar.DisplayName);

            var targetFloatingObj = ent as MyFloatingObject;
            if(targetFloatingObj != null)
                return sb.Append("Floating ").Append(targetFloatingObj.ItemDefinition.DisplayNameText);

            var targetAsteroid = ent as IMyVoxelMap;
            if(targetAsteroid != null)
                return sb.Append("Asteroid");

            return sb.Append(ent.ToString());
        }
    }
}
