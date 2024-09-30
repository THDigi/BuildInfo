using System.Collections.Generic;
using System.Text;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Utils;

namespace Digi.BuildInfo.Features
{
    public class TextReplacer : ModComponent
    {
        Dictionary<MyStringId, string> Original = new Dictionary<MyStringId, string>(MyStringId.Comparer);

        public TextReplacer(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            DoChanges();
            Main.GUIMonitor.OptionsMenuClosed += DoChanges;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            RevertChanges();

            Main.GUIMonitor.OptionsMenuClosed -= DoChanges;
        }

        void RevertChanges()
        {
            foreach(var kv in Original)
            {
                MyTexts.Get(kv.Key).Clear().Append(kv.Value);
            }
        }

        void DoChanges()
        {
            if(!Main.Config.TextReplace.Value)
                return;

            MyLanguagesEnum lang = MyAPIGateway.Session?.Config?.Language ?? MyLanguagesEnum.English;
            if(lang != MyLanguagesEnum.English)
                return;

            Change("DisplayName_EventAngleChanged", "Rotor/Hinge Angle");

            Change("DisplayName_MyEventBlockOnOff", "Block Switched On/Off");

            Change("DisplayName_EventDoorOpened", "Door Finished Open/Close");
            Change("DisplayName_EventCockpitOccupied", "Cockpit/Seat Enter/Leave");

            Change("DisplayName_EventConnectorConnected", "Connector Lock/Unlock");
            Change("DisplayName_EventConnectorReadyToLock", "Connector Ready/Idle");

            Change("DisplayName_EventLandingGearLocked", "LandingGear/Magplate Lock/Unlock");
            Change("DisplayName_EventMagneticLockReady", "LandingGear/Magplate Ready/Idle");

            Change("DisplayName_EventMerged", "Merge Block Merged/Unmerged");

            // the game does sort EC's events by name but it uses GetString() which doesn't seem to be affected by Get() changes.
        }

        void Change(string idStr, string text)
        {
            MyStringId id = MyStringId.GetOrCompute(idStr);

            StringBuilder sb = MyTexts.Get(id);

            string existing = sb.ToString();

            if(text == existing)
                return; // already changed

            Original[id] = existing;

            sb.Clear().Append(text);
        }
    }
}
