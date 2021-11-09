using System.Text;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using InternalControllableEntity = Sandbox.Game.Entities.IMyControllableEntity;

namespace Digi.BuildInfo.Features
{
    public class RelativeDampenerInfo : ModComponent
    {
        private IMyHudNotification notify;

        private long prevControlledEntId;
        private long prevRelativeEntId;

        private StringBuilder sb = new StringBuilder(128);

        public RelativeDampenerInfo(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.Config.RelativeDampenerInfo.ValueAssigned += ConfigChanged;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.Config.RelativeDampenerInfo.ValueAssigned -= ConfigChanged;
        }

        void ConfigChanged(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, newValue);
        }

        public override void UpdateAfterSim(int tick)
        {
            InternalControllableEntity controlled = MyAPIGateway.Session?.ControlledObject as InternalControllableEntity;
            MyEntity controlledEnt = controlled?.Entity;

            if(controlledEnt == null)
            {
                prevControlledEntId = 0;
                prevRelativeEntId = 0;
                return;
            }

            if(prevControlledEntId != controlledEnt.EntityId)
            {
                prevControlledEntId = controlledEnt.EntityId;
                prevRelativeEntId = 0;
            }

            if(controlledEnt == null)
                return;

            MyEntity relativeEnt = controlled.RelativeDampeningEntity;
            long relativeEntId = (relativeEnt == null || relativeEnt.MarkedForClose ? 0 : relativeEnt.EntityId);

            if(relativeEntId == prevRelativeEntId)
                return;

            if(notify == null)
                notify = MyAPIGateway.Utilities.CreateNotification("");

            notify.Hide(); // required since SE v1.194

            if(relativeEntId == 0)
            {
                notify.Text = "Dampeners no longer relative";
                notify.Font = FontsHandler.YellowSh;
            }
            else
            {
                if(!Hardcoded.RelativeDampeners_DistanceCheck(controlledEnt, relativeEnt))
                {
                    // HACK preventing this confusing mess: https://support.keenswh.com/spaceengineers/pc/topic/1-192-022-relative-dampeners-allow-engaging-at-larger-distances-then-turn-off
                    controlled.RelativeDampeningEntity = null;
                    prevRelativeEntId = 0;

                    //notify.Hide(); // required since SE v1.194
                    //notify.Text = "Target farther than [100m].";
                    //notify.Font = FontsHandler.RedSh;
                    //notify.Show();

                    return;
                }

                sb.Clear();
                sb.Append("Dampeners relative to [");

                int startIndex = sb.Length;

                AppendEntityName(relativeEnt, sb);

                // escape [ and ] by adding another
                for(int i = startIndex; i < sb.Length; ++i)
                {
                    char c = sb[i];
                    if(c == '[')
                    {
                        sb.Insert(i, '[');
                        i++;
                    }
                    else if(c == ']')
                    {
                        sb.Insert(i, ']');
                        i++;
                    }
                }

                sb.Append(']');

                notify.Text = sb.ToString();
                notify.Font = FontsHandler.WhiteSh;
            }

            notify.Show();
            prevRelativeEntId = relativeEntId;
        }

        static StringBuilder AppendEntityName(MyEntity ent, StringBuilder sb)
        {
            IMyCubeGrid targetGrid = ent as IMyCubeGrid;
            if(targetGrid != null)
                return sb.Append(targetGrid.CustomName);

            IMyCharacter targetChar = ent as IMyCharacter;
            if(targetChar != null)
                return sb.Append(targetChar.DisplayName);

            MyFloatingObject targetFloatingObj = ent as MyFloatingObject;
            if(targetFloatingObj != null)
                return sb.Append("Floating ").Append(targetFloatingObj.ItemDefinition.DisplayNameText);

            string display = ent.DisplayNameText;

            if(string.IsNullOrEmpty(display))
                display = ent.DisplayName;

            if(string.IsNullOrEmpty(display))
                display = ent.ToString();

            return sb.Append(display);
        }
    }
}
