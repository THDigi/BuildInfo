using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Game;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;
using TypeExtensions = VRage.TypeExtensions;

namespace Digi.BuildInfo.Features
{
    public class CheckVSLPEvents : ModComponent
    {
        [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
        class CheckVSLPEventsSession : MySessionComponentBase
        {
            public CheckVSLPEventsSession()
            {
                CreateHooks(Container);
            }

            public override void LoadData()
            {
                // if mod is killed, no reason to keep these empty hooks during gameplay
                if(BuildInfo_GameSession.IsKilled)
                    Unhook();
            }

            protected override void UnloadData()
            {
                Unhook(); // fallback
            }
        }

        static CheckContainer Container = new CheckContainer();

        static void CreateHooks(CheckContainer container)
        {
            new Check_PlayerConnectRequest(container);
            new Check_PlayerLeftCockpit(container);
            new Check_PlayerEnteredCockpit(container);
            new Check_RespawnShipSpawned(container);
            new Check_CutsceneNodeEvent(container);
            new Check_CutsceneEnded(container);
            new Check_PlayerSpawned(container);
            new Check_TeamBalancerPlayerSorted(container);
            new Check_PlayerRequestsRespawn(container);
            new Check_PlayerDied(container);
            new Check_PlayerConnected(container);
            new Check_PlayerDisconnected(container);
            new Check_PlayerRespawnRequest(container);
            new Check_NPCDied(container);
            new Check_PlayerHealthRecharging(container);
            new Check_PlayerSuitRecharging(container);
            new Check_TimerBlockTriggered(container);
            new Check_TimerBlockTriggeredEntityName(container);
            new Check_PlayerPickedUp(container);
            new Check_PlayerDropped(container);
            new Check_ItemSpawned(container);
            new Check_ButtonPressedEntityName(container);
            new Check_ButtonPressedTerminalName(container);
            new Check_AreaTrigger_EntityLeft(container);
            new Check_AreaTrigger_EntityEntered(container);
            new Check_AreaTrigger_Left(container);
            new Check_AreaTrigger_Entered(container);
            new Check_ScreenAdded(container);
            new Check_ScreenRemoved(container);
            new Check_BlockDestroyed(container);
            new Check_BlockIntegrityChanged(container);
            new Check_BlockDamaged(container);
            new Check_BlockBuilt(container);
            new Check_PrefabSpawned(container);
            new Check_PrefabSpawnedDetailed(container);
            new Check_GridSpawned(container);
            new Check_BlockFunctionalityChanged(container);
            new Check_ToolEquipped(container);
            new Check_LandingGearUnlocked(container);
            new Check_GridPowerGenerationStateChanged(container);
            new Check_RoomFullyPressurized(container);
            new Check_NewItemBuilt(container);
            new Check_WeaponBlockActivated(container);
            new Check_ConnectorStateChanged(container);
            new Check_GridJumped(container);
            new Check_ShipDrillCollected(container);
            new Check_RemoteControlChanged(container);
            new Check_ToolbarItemChanged(container);
            new Check_MatchStateStarted(container);
            new Check_MatchStateEnded(container);
            new Check_MatchStateChanged(container);
            new Check_ContractAccepted(container);
            new Check_ContractFinished(container);
            new Check_ContractFailed(container);
            new Check_ContractAbandoned(container);
            new Check_MatchStateEnding(container);

            // this is a regular bool field
            container.Ignore.Add("GameIsReady");
        }

        static void Unhook()
        {
            try
            {
                if(Container?.Events != null)
                {
                    foreach(CheckForEventBase checker in Container.Events.Values)
                    {
                        checker.Unhook();
                    }
                }
            }
            finally
            {
                Container = null;
            }
        }

        public CheckVSLPEvents(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            if(Main.Config.ModderHelpAlerts.Value) // && MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE && MyAPIGateway.Session.Mods.Any(m => m.PublishedFileId == 0))
            {
                // to wait a few seconds and then check hooks
                UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;
            }

            if(BuildInfoMod.IsDevMod)
            {
                DevFindNewEvents();
                //DevTestOverwrittenEvents();
            }
        }

        public override void UnregisterComponent()
        {
            Unhook(); // just making sure; but it should be unhooked elsewhere in case mod is killed
        }

        public override void UpdateAfterSim(int tick)
        {
            if(tick < (Constants.TicksPerSecond * 3))
                return;

            try
            {
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                CheckEvents();
            }
            finally
            {
                Container = null;
            }
        }

        void CheckEvents()
        {
            if(Container?.Events == null)
                return;

            int preAssigned = 0;
            int erased = 0;
            int count = Container.Events.Count;

            foreach(CheckForEventBase checker in Container.Events.Values)
            {
                if(checker.PreCheckMessage != null)
                {
                    MyLog.Default.WriteLine($"### {BuildInfoMod.ModName}.{nameof(CheckVSLPEvents)}: {checker.PreCheckMessage}");
                    Log.Info(checker.PreCheckMessage);
                    preAssigned++;
                }

                string message = checker.Check();
                if(message != null)
                {
                    MyLog.Default.WriteLine($"### {BuildInfoMod.ModName}.{nameof(CheckVSLPEvents)}: {message}");
                    Log.Info(message);
                    erased++;
                }

                checker.Unhook();
            }

            string msg = $"Done checking events; preAssigned={preAssigned}; erased={erased}; total events={count}";
            Log.Info(msg);
            MyLog.Default.WriteLine($"### {BuildInfoMod.ModName}.{nameof(CheckVSLPEvents)}: {msg}");

            if(preAssigned > 0 || erased > 0)
            {
                MyLog.Default.WriteLine($"### {BuildInfoMod.ModName}.{nameof(CheckVSLPEvents)}: If the above involve your mod, you need to use += to hook and -= to unhook the events, don't use ="
                                      + "\nIf they are not your mods, send this info to the author if you can identify the mods, otherwise contact BuildInfo's author for help tracking down the problematic mod(s).");

                MyDefinitionErrors.Add((MyModContext)BuildInfoMod.Instance.Session.ModContext, "Script mods might have issues from VSLP events being leaked or unhooked by other mods, see SE log for details.", TErrorSeverity.Error);

                Utils.ShowColoredChatMessage(BuildInfoMod.ModName, "Script mods might have issues from VSLP events being leaked or unhooked by other mods, see SE log for details.", FontsHandler.YellowSh);
            }
        }

        void DevFindNewEvents()
        {
            bool alert = false;

            IEnumerable<MemberInfo> members = TypeExtensions.GetDataMembers(typeof(MyVisualScriptLogicProvider),
                fields: true, properties: false, nonPublic: false, inherited: false, _static: true, instance: false, read: false, write: false);

            foreach(MemberInfo member in members)
            {
                if(Container.Events.ContainsKey(member.Name))
                    continue;

                if(Container.Ignore.Contains(member.Name))
                    continue;

                alert = true;
                Log.Info($"[Dev] {GetType().Name}: new field found: {member.Name} - {member.ToString()}");
            }

            if(alert)
                Log.Error($"[Dev] {GetType().Name}: new events found", Log.PRINT_MESSAGE);
        }

#if false
        void DevTestOverwrittenEvents()
        {
            // NOTE: these don't get unhooked, gotta kill game after using this test

            //MyVisualScriptLogicProvider.PlayerConnectRequest = (a, b) => { };
            MyVisualScriptLogicProvider.BlockDamaged = (a, b, c, d, e, f, g) => { };
            MyVisualScriptLogicProvider.BlockBuilt = (a, b, c, d) => { };
            MyVisualScriptLogicProvider.BlockFunctionalityChanged = (a, b, c, d, e, f, g) => { };
            MyVisualScriptLogicProvider.ButtonPressedEntityName = (a, b, c, d) => { };
            MyVisualScriptLogicProvider.ButtonPressedTerminalName = (a, b, c, d) => { };
            MyVisualScriptLogicProvider.ConnectorStateChanged = (a, b, c, d, e, f, g, h, i) => { };
            MyVisualScriptLogicProvider.ContractAbandoned = (a, b, c, d, e, f, g) => { };
            MyVisualScriptLogicProvider.ContractAccepted = (a, b, c, d, e, f, g) => { };
            MyVisualScriptLogicProvider.ContractFailed = (a, b, c, d, e, f, g, h) => { };
            MyVisualScriptLogicProvider.ContractFinished = (a, b, c, d, e, f, g) => { };
            MyVisualScriptLogicProvider.CutsceneNodeEvent = (a) => { };
            MyVisualScriptLogicProvider.CutsceneEnded = (a) => { };
            MyVisualScriptLogicProvider.PlayerLeftCockpit = (a, b, c) => { };
            MyVisualScriptLogicProvider.PlayerEnteredCockpit = (a, b, c) => { };
            MyVisualScriptLogicProvider.PlayerPickedUp = (a, b, c, d, e) => { };
            MyVisualScriptLogicProvider.GridJumped = (a, b, c) => { };
            MyVisualScriptLogicProvider.GridPowerGenerationStateChanged = (a, b, c) => { };
            MyVisualScriptLogicProvider.ItemSpawned = (a, b, c, d, e) => { };
            MyVisualScriptLogicProvider.LandingGearUnlocked = (a, b, c, d) => { };
            MyVisualScriptLogicProvider.MatchStateChanged = (a, b) => { };
            MyVisualScriptLogicProvider.MatchStateEnded = (a) => { };
            MyVisualScriptLogicProvider.MatchStateEnding = (string a, ref bool b) => { };
            MyVisualScriptLogicProvider.MatchStateStarted = (a) => { };
            MyVisualScriptLogicProvider.NewItemBuilt = (a, b, c, d, e, f, g) => { };
            MyVisualScriptLogicProvider.PlayerHealthRecharging = (a, b, c) => { };
            MyVisualScriptLogicProvider.PlayerDropped = (a, b, c, d) => { };
            MyVisualScriptLogicProvider.PlayerSuitRecharging = (a, b) => { };
            MyVisualScriptLogicProvider.PrefabSpawnedDetailed = (a, b) => { };
            MyVisualScriptLogicProvider.RemoteControlChanged = (a, b, c, d, e, f) => { };
            MyVisualScriptLogicProvider.RespawnShipSpawned = (a, b, c) => { };
            MyVisualScriptLogicProvider.RoomFullyPressurized = (a, b, c, d) => { };
            MyVisualScriptLogicProvider.ScreenAdded = (a) => { };
            MyVisualScriptLogicProvider.ScreenRemoved = (a) => { };
            MyVisualScriptLogicProvider.ShipDrillCollected = (a, b, c, d, e, f, g) => { };
            MyVisualScriptLogicProvider.NPCDied = (a) => { };
            MyVisualScriptLogicProvider.PrefabSpawned = (a) => { };
            MyVisualScriptLogicProvider.GridSpawned = (a) => { };
            MyVisualScriptLogicProvider.TimerBlockTriggered = (a, b, c, d) => { };
            MyVisualScriptLogicProvider.TimerBlockTriggeredEntityName = (a, b, c, d) => { };
            MyVisualScriptLogicProvider.BlockDestroyed = (a, b, c, d) => { };
            MyVisualScriptLogicProvider.BlockIntegrityChanged = (a, b, c, d) => { };
            MyVisualScriptLogicProvider.PlayerSpawned = (a) => { };
            MyVisualScriptLogicProvider.PlayerRequestsRespawn = (a) => { };
            MyVisualScriptLogicProvider.PlayerDied = (a) => { };
            MyVisualScriptLogicProvider.PlayerConnected = (a) => { };
            MyVisualScriptLogicProvider.PlayerDisconnected = (a) => { };
            MyVisualScriptLogicProvider.PlayerRespawnRequest = (a) => { };
            MyVisualScriptLogicProvider.AreaTrigger_Left = (a, b) => { };
            MyVisualScriptLogicProvider.AreaTrigger_Entered = (a, b) => { };
            MyVisualScriptLogicProvider.TeamBalancerPlayerSorted = (a, b) => { };
            MyVisualScriptLogicProvider.ToolbarItemChanged = (a, b, c, d, e) => { };
            MyVisualScriptLogicProvider.ToolEquipped = (a, b, c) => { };
            MyVisualScriptLogicProvider.AreaTrigger_EntityLeft = (a, b, c) => { };
            MyVisualScriptLogicProvider.AreaTrigger_EntityEntered = (a, b, c) => { };
            MyVisualScriptLogicProvider.WeaponBlockActivated = (a, b, c, d, e, f) => { };
        }
#endif

        class CheckContainer
        {
            public Dictionary<string, CheckForEventBase> Events = new Dictionary<string, CheckForEventBase>();
            public HashSet<string> Ignore = new HashSet<string>();
        }

        abstract class CheckForEventBase
        {
            public readonly string ClassName;
            public readonly string FieldName;

            public string PreCheckMessage;

            public CheckForEventBase(CheckContainer container, string className, string fieldName)
            {
                ClassName = className;
                FieldName = fieldName;
                container.Events.Add(fieldName, this);
            }

            /// <summary>
            /// Returns error text or null if fine
            /// </summary>
            public abstract string Check();

            public abstract void Unhook();

            protected void PreCheck(Delegate eventField)
            {
                if(eventField != null)
                {
                    StringBuilder sb = new StringBuilder(256);
                    sb.Clear();
                    sb.Append($"Warning: '{ClassName}.{FieldName}' was assigned before mods loaded. If hooks are from plugins or game itself then you can ignore this.");
                    sb.Append("\nHooks:");
                    foreach(Delegate del in eventField.GetInvocationList())
                    {
                        sb.Append(GetMethodDetails(del));
                    }

                    PreCheckMessage = sb.ToString();
                }
            }

            protected string CheckEvent(Delegate eventField, Delegate callback)
            {
                foreach(Delegate del in eventField.GetInvocationList())
                {
                    if(del.Method == callback.Method)
                        return null;
                }

                StringBuilder sb = new StringBuilder(512);

                sb.Append($"Problem: '{ClassName}.{FieldName}' event has been overwritten by something! Probably a mod used = instead of +=");
                sb.Append("\nHooks (first one is likely the culprit):");
                foreach(Delegate del in eventField.GetInvocationList())
                {
                    sb.Append(GetMethodDetails(del));
                }

                return sb.ToString();
            }

            static string GetMethodDetails(Delegate del)
            {
                if(del.Target == null) // static method
                {
                    return $"\n - static method={del.Method} / deeperDelegates={(del.GetInvocationList()?.Length ?? 0) > 1} (can't get more details on static methods, do a find-in-files for this method name)";
                }
                else
                {
                    return $"\n - target={del.Target} / method={del.Method} / deeperDelegates={(del.GetInvocationList()?.Length ?? 0) > 1}";
                }
            }
        }

        #region Generated
        class Check_PlayerConnectRequest : CheckForEventBase
        {
            // HACK: event can't be hooked because of prohibited ref enum.
            // but can still be checked for null when world loads.

            public Check_PlayerConnectRequest(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.PlayerConnectRequest))
            {
                PreCheck(MyVisualScriptLogicProvider.PlayerConnectRequest);
                //MyVisualScriptLogicProvider.PlayerConnectRequest += DummyCallback;
            }

            public override void Unhook()
            {
                //MyVisualScriptLogicProvider.PlayerConnectRequest -= DummyCallback;
            }

            public override string Check() => null; // CheckEvent(MyVisualScriptLogicProvider.PlayerConnectRequest, DummyCallback);

            //SingleKeyPlayerConnectRequestEvent DummyCallback = (a, b) => { };
        }

        class Check_AreaTrigger_Left : CheckForEventBase
        {
            public Check_AreaTrigger_Left(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.AreaTrigger_Left))
            {
                PreCheck(MyVisualScriptLogicProvider.AreaTrigger_Left);
                MyVisualScriptLogicProvider.AreaTrigger_Left += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.AreaTrigger_Left -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.AreaTrigger_Left, new Action<string, long>(DummyCallback));

            void DummyCallback(string a, long b) { }
        }

        class Check_AreaTrigger_Entered : CheckForEventBase
        {
            public Check_AreaTrigger_Entered(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.AreaTrigger_Entered))
            {
                PreCheck(MyVisualScriptLogicProvider.AreaTrigger_Entered);
                MyVisualScriptLogicProvider.AreaTrigger_Entered += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.AreaTrigger_Entered -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.AreaTrigger_Entered, new Action<string, long>(DummyCallback));

            void DummyCallback(string a, long b) { }
        }

        class Check_AreaTrigger_EntityLeft : CheckForEventBase
        {
            public Check_AreaTrigger_EntityLeft(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.AreaTrigger_EntityLeft))
            {
                PreCheck(MyVisualScriptLogicProvider.AreaTrigger_EntityLeft);
                MyVisualScriptLogicProvider.AreaTrigger_EntityLeft += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.AreaTrigger_EntityLeft -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.AreaTrigger_EntityLeft, new Action<string, long, string>(DummyCallback));

            void DummyCallback(string a, long b, string c) { }
        }

        class Check_AreaTrigger_EntityEntered : CheckForEventBase
        {
            public Check_AreaTrigger_EntityEntered(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.AreaTrigger_EntityEntered))
            {
                PreCheck(MyVisualScriptLogicProvider.AreaTrigger_EntityEntered);
                MyVisualScriptLogicProvider.AreaTrigger_EntityEntered += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.AreaTrigger_EntityEntered -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.AreaTrigger_EntityEntered, new Action<string, long, string>(DummyCallback));

            void DummyCallback(string a, long b, string c) { }
        }

        class Check_BlockDamaged : CheckForEventBase
        {
            public Check_BlockDamaged(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.BlockDamaged))
            {
                PreCheck(MyVisualScriptLogicProvider.BlockDamaged);
                MyVisualScriptLogicProvider.BlockDamaged += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.BlockDamaged -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.BlockDamaged, DummyCallback);

            BlockDamagedEvent DummyCallback = (a, b, c, d, e, f, g) => { };
        }

        class Check_BlockBuilt : CheckForEventBase
        {
            public Check_BlockBuilt(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.BlockBuilt))
            {
                PreCheck(MyVisualScriptLogicProvider.BlockBuilt);
                MyVisualScriptLogicProvider.BlockBuilt += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.BlockBuilt -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.BlockBuilt, DummyCallback);

            BlockEvent DummyCallback = (a, b, c, d) => { };
        }

        class Check_BlockFunctionalityChanged : CheckForEventBase
        {
            public Check_BlockFunctionalityChanged(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.BlockFunctionalityChanged))
            {
                PreCheck(MyVisualScriptLogicProvider.BlockFunctionalityChanged);
                MyVisualScriptLogicProvider.BlockFunctionalityChanged += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.BlockFunctionalityChanged -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.BlockFunctionalityChanged, DummyCallback);

            BlockFunctionalityChangedEvent DummyCallback = (a, b, c, d, e, f, g) => { };
        }

        class Check_ButtonPressedEntityName : CheckForEventBase
        {
            public Check_ButtonPressedEntityName(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.ButtonPressedEntityName))
            {
                PreCheck(MyVisualScriptLogicProvider.ButtonPressedEntityName);
                MyVisualScriptLogicProvider.ButtonPressedEntityName += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.ButtonPressedEntityName -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.ButtonPressedEntityName, DummyCallback);

            ButtonPanelEvent DummyCallback = (a, b, c, d) => { };
        }

        class Check_ButtonPressedTerminalName : CheckForEventBase
        {
            public Check_ButtonPressedTerminalName(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.ButtonPressedTerminalName))
            {
                PreCheck(MyVisualScriptLogicProvider.ButtonPressedTerminalName);
                MyVisualScriptLogicProvider.ButtonPressedTerminalName += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.ButtonPressedTerminalName -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.ButtonPressedTerminalName, DummyCallback);

            ButtonPanelEvent DummyCallback = (a, b, c, d) => { };
        }

        class Check_ConnectorStateChanged : CheckForEventBase
        {
            public Check_ConnectorStateChanged(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.ConnectorStateChanged))
            {
                PreCheck(MyVisualScriptLogicProvider.ConnectorStateChanged);
                MyVisualScriptLogicProvider.ConnectorStateChanged += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.ConnectorStateChanged -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.ConnectorStateChanged, DummyCallback);

            ConnectorStateChangedEvent DummyCallback = (a, b, c, d, e, f, g, h, i) => { };
        }

        class Check_ContractAbandoned : CheckForEventBase
        {
            public Check_ContractAbandoned(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.ContractAbandoned))
            {
                PreCheck(MyVisualScriptLogicProvider.ContractAbandoned);
                MyVisualScriptLogicProvider.ContractAbandoned += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.ContractAbandoned -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.ContractAbandoned, DummyCallback);

            ContractAbandonedEvent DummyCallback = (a, b, c, d, e, f, g) => { };
        }

        class Check_ContractAccepted : CheckForEventBase
        {
            public Check_ContractAccepted(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.ContractAccepted))
            {
                PreCheck(MyVisualScriptLogicProvider.ContractAccepted);
                MyVisualScriptLogicProvider.ContractAccepted += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.ContractAccepted -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.ContractAccepted, DummyCallback);

            ContractAcceptedEvent DummyCallback = (a, b, c, d, e, f, g) => { };
        }

        class Check_ContractFailed : CheckForEventBase
        {
            public Check_ContractFailed(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.ContractFailed))
            {
                PreCheck(MyVisualScriptLogicProvider.ContractFailed);
                MyVisualScriptLogicProvider.ContractFailed += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.ContractFailed -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.ContractFailed, DummyCallback);

            ContractFailedEvent DummyCallback = (a, b, c, d, e, f, g, h) => { };
        }

        class Check_ContractFinished : CheckForEventBase
        {
            public Check_ContractFinished(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.ContractFinished))
            {
                PreCheck(MyVisualScriptLogicProvider.ContractFinished);
                MyVisualScriptLogicProvider.ContractFinished += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.ContractFinished -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.ContractFinished, DummyCallback);

            ContractFinishedEvent DummyCallback = (a, b, c, d, e, f, g) => { };
        }

        class Check_CutsceneNodeEvent : CheckForEventBase
        {
            public Check_CutsceneNodeEvent(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.CutsceneNodeEvent))
            {
                PreCheck(MyVisualScriptLogicProvider.CutsceneNodeEvent);
                MyVisualScriptLogicProvider.CutsceneNodeEvent += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.CutsceneNodeEvent -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.CutsceneNodeEvent, DummyCallback);

            CutsceneEvent DummyCallback = (a) => { };
        }

        class Check_CutsceneEnded : CheckForEventBase
        {
            public Check_CutsceneEnded(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.CutsceneEnded))
            {
                PreCheck(MyVisualScriptLogicProvider.CutsceneEnded);
                MyVisualScriptLogicProvider.CutsceneEnded += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.CutsceneEnded -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.CutsceneEnded, DummyCallback);

            CutsceneEvent DummyCallback = (a) => { };
        }

        class Check_PlayerLeftCockpit : CheckForEventBase
        {
            public Check_PlayerLeftCockpit(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.PlayerLeftCockpit))
            {
                PreCheck(MyVisualScriptLogicProvider.PlayerLeftCockpit);
                MyVisualScriptLogicProvider.PlayerLeftCockpit += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.PlayerLeftCockpit -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.PlayerLeftCockpit, DummyCallback);

            DoubleKeyPlayerEvent DummyCallback = (a, b, c) => { };
        }

        class Check_PlayerEnteredCockpit : CheckForEventBase
        {
            public Check_PlayerEnteredCockpit(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.PlayerEnteredCockpit))
            {
                PreCheck(MyVisualScriptLogicProvider.PlayerEnteredCockpit);
                MyVisualScriptLogicProvider.PlayerEnteredCockpit += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.PlayerEnteredCockpit -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.PlayerEnteredCockpit, DummyCallback);

            DoubleKeyPlayerEvent DummyCallback = (a, b, c) => { };
        }

        class Check_PlayerPickedUp : CheckForEventBase
        {
            public Check_PlayerPickedUp(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.PlayerPickedUp))
            {
                PreCheck(MyVisualScriptLogicProvider.PlayerPickedUp);
                MyVisualScriptLogicProvider.PlayerPickedUp += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.PlayerPickedUp -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.PlayerPickedUp, DummyCallback);

            FloatingObjectPlayerEvent DummyCallback = (a, b, c, d, e) => { };
        }

        class Check_GridJumped : CheckForEventBase
        {
            public Check_GridJumped(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.GridJumped))
            {
                PreCheck(MyVisualScriptLogicProvider.GridJumped);
                MyVisualScriptLogicProvider.GridJumped += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.GridJumped -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.GridJumped, DummyCallback);

            GridJumpedEvent DummyCallback = (a, b, c) => { };
        }

        class Check_GridPowerGenerationStateChanged : CheckForEventBase
        {
            public Check_GridPowerGenerationStateChanged(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.GridPowerGenerationStateChanged))
            {
                PreCheck(MyVisualScriptLogicProvider.GridPowerGenerationStateChanged);
                MyVisualScriptLogicProvider.GridPowerGenerationStateChanged += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.GridPowerGenerationStateChanged -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.GridPowerGenerationStateChanged, DummyCallback);

            GridPowerGenerationStateChangedEvent DummyCallback = (a, b, c) => { };
        }

        class Check_ItemSpawned : CheckForEventBase
        {
            public Check_ItemSpawned(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.ItemSpawned))
            {
                PreCheck(MyVisualScriptLogicProvider.ItemSpawned);
                MyVisualScriptLogicProvider.ItemSpawned += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.ItemSpawned -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.ItemSpawned, DummyCallback);

            ItemSpawnedEvent DummyCallback = (a, b, c, d, e) => { };
        }

        class Check_LandingGearUnlocked : CheckForEventBase
        {
            public Check_LandingGearUnlocked(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.LandingGearUnlocked))
            {
                PreCheck(MyVisualScriptLogicProvider.LandingGearUnlocked);
                MyVisualScriptLogicProvider.LandingGearUnlocked += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.LandingGearUnlocked -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.LandingGearUnlocked, DummyCallback);

            LandingGearUnlockedEvent DummyCallback = (a, b, c, d) => { };
        }

        class Check_MatchStateChanged : CheckForEventBase
        {
            public Check_MatchStateChanged(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.MatchStateChanged))
            {
                PreCheck(MyVisualScriptLogicProvider.MatchStateChanged);
                MyVisualScriptLogicProvider.MatchStateChanged += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.MatchStateChanged -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.MatchStateChanged, DummyCallback);

            MatchStateChangedEvent DummyCallback = (a, b) => { };
        }

        class Check_MatchStateEnded : CheckForEventBase
        {
            public Check_MatchStateEnded(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.MatchStateEnded))
            {
                PreCheck(MyVisualScriptLogicProvider.MatchStateEnded);
                MyVisualScriptLogicProvider.MatchStateEnded += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.MatchStateEnded -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.MatchStateEnded, DummyCallback);

            MatchStateEndedEvent DummyCallback = (a) => { };
        }

        class Check_MatchStateEnding : CheckForEventBase
        {
            public Check_MatchStateEnding(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.MatchStateEnding))
            {
                PreCheck(MyVisualScriptLogicProvider.MatchStateEnding);
                MyVisualScriptLogicProvider.MatchStateEnding += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.MatchStateEnding -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.MatchStateEnding, DummyCallback);

            MatchStateEndingEvent DummyCallback = (string a, ref bool b) => { };
        }

        class Check_MatchStateStarted : CheckForEventBase
        {
            public Check_MatchStateStarted(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.MatchStateStarted))
            {
                PreCheck(MyVisualScriptLogicProvider.MatchStateStarted);
                MyVisualScriptLogicProvider.MatchStateStarted += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.MatchStateStarted -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.MatchStateStarted, DummyCallback);

            MatchStateStartedEvent DummyCallback = (a) => { };
        }

        class Check_NewItemBuilt : CheckForEventBase
        {
            public Check_NewItemBuilt(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.NewItemBuilt))
            {
                PreCheck(MyVisualScriptLogicProvider.NewItemBuilt);
                MyVisualScriptLogicProvider.NewItemBuilt += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.NewItemBuilt -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.NewItemBuilt, DummyCallback);

            NewBuiltItemEvent DummyCallback = (a, b, c, d, e, f, g) => { };
        }

        class Check_PlayerHealthRecharging : CheckForEventBase
        {
            public Check_PlayerHealthRecharging(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.PlayerHealthRecharging))
            {
                PreCheck(MyVisualScriptLogicProvider.PlayerHealthRecharging);
                MyVisualScriptLogicProvider.PlayerHealthRecharging += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.PlayerHealthRecharging -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.PlayerHealthRecharging, DummyCallback);

            PlayerHealthRechargeEvent DummyCallback = (a, b, c) => { };
        }

        class Check_PlayerDropped : CheckForEventBase
        {
            public Check_PlayerDropped(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.PlayerDropped))
            {
                PreCheck(MyVisualScriptLogicProvider.PlayerDropped);
                MyVisualScriptLogicProvider.PlayerDropped += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.PlayerDropped -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.PlayerDropped, DummyCallback);

            PlayerItemEvent DummyCallback = (a, b, c, d) => { };
        }

        class Check_PlayerSuitRecharging : CheckForEventBase
        {
            public Check_PlayerSuitRecharging(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.PlayerSuitRecharging))
            {
                PreCheck(MyVisualScriptLogicProvider.PlayerSuitRecharging);
                MyVisualScriptLogicProvider.PlayerSuitRecharging += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.PlayerSuitRecharging -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.PlayerSuitRecharging, DummyCallback);

            PlayerSuitRechargeEvent DummyCallback = (a, b) => { };
        }

        class Check_PrefabSpawnedDetailed : CheckForEventBase
        {
            public Check_PrefabSpawnedDetailed(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.PrefabSpawnedDetailed))
            {
                PreCheck(MyVisualScriptLogicProvider.PrefabSpawnedDetailed);
                MyVisualScriptLogicProvider.PrefabSpawnedDetailed += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.PrefabSpawnedDetailed -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.PrefabSpawnedDetailed, DummyCallback);

            PrefabSpawnedEvent DummyCallback = (a, b) => { };
        }

        class Check_RemoteControlChanged : CheckForEventBase
        {
            public Check_RemoteControlChanged(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.RemoteControlChanged))
            {
                PreCheck(MyVisualScriptLogicProvider.RemoteControlChanged);
                MyVisualScriptLogicProvider.RemoteControlChanged += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.RemoteControlChanged -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.RemoteControlChanged, DummyCallback);

            RemoteControlChangedEvent DummyCallback = (a, b, c, d, e, f) => { };
        }

        class Check_RespawnShipSpawned : CheckForEventBase
        {
            public Check_RespawnShipSpawned(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.RespawnShipSpawned))
            {
                PreCheck(MyVisualScriptLogicProvider.RespawnShipSpawned);
                MyVisualScriptLogicProvider.RespawnShipSpawned += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.RespawnShipSpawned -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.RespawnShipSpawned, DummyCallback);

            RespawnShipSpawnedEvent DummyCallback = (a, b, c) => { };
        }

        class Check_RoomFullyPressurized : CheckForEventBase
        {
            public Check_RoomFullyPressurized(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.RoomFullyPressurized))
            {
                PreCheck(MyVisualScriptLogicProvider.RoomFullyPressurized);
                MyVisualScriptLogicProvider.RoomFullyPressurized += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.RoomFullyPressurized -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.RoomFullyPressurized, DummyCallback);

            RoomFullyPressurizedEvent DummyCallback = (a, b, c, d) => { };
        }

        class Check_ScreenAdded : CheckForEventBase
        {
            public Check_ScreenAdded(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.ScreenAdded))
            {
                PreCheck(MyVisualScriptLogicProvider.ScreenAdded);
                MyVisualScriptLogicProvider.ScreenAdded += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.ScreenAdded -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.ScreenAdded, DummyCallback);

            ScreenManagerEvent DummyCallback = (a) => { };
        }

        class Check_ScreenRemoved : CheckForEventBase
        {
            public Check_ScreenRemoved(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.ScreenRemoved))
            {
                PreCheck(MyVisualScriptLogicProvider.ScreenRemoved);
                MyVisualScriptLogicProvider.ScreenRemoved += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.ScreenRemoved -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.ScreenRemoved, DummyCallback);

            ScreenManagerEvent DummyCallback = (a) => { };
        }

        class Check_ShipDrillCollected : CheckForEventBase
        {
            public Check_ShipDrillCollected(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.ShipDrillCollected))
            {
                PreCheck(MyVisualScriptLogicProvider.ShipDrillCollected);
                MyVisualScriptLogicProvider.ShipDrillCollected += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.ShipDrillCollected -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.ShipDrillCollected, DummyCallback);

            ShipDrillCollectedEvent DummyCallback = (a, b, c, d, e, f, g) => { };
        }

        class Check_NPCDied : CheckForEventBase
        {
            public Check_NPCDied(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.NPCDied))
            {
                PreCheck(MyVisualScriptLogicProvider.NPCDied);
                MyVisualScriptLogicProvider.NPCDied += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.NPCDied -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.NPCDied, DummyCallback);

            SingleKeyEntityNameEvent DummyCallback = (a) => { };
        }

        class Check_PrefabSpawned : CheckForEventBase
        {
            public Check_PrefabSpawned(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.PrefabSpawned))
            {
                PreCheck(MyVisualScriptLogicProvider.PrefabSpawned);
                MyVisualScriptLogicProvider.PrefabSpawned += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.PrefabSpawned -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.PrefabSpawned, DummyCallback);

            SingleKeyEntityNameEvent DummyCallback = (a) => { };
        }

        class Check_GridSpawned : CheckForEventBase
        {
            public Check_GridSpawned(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.GridSpawned))
            {
                PreCheck(MyVisualScriptLogicProvider.GridSpawned);
                MyVisualScriptLogicProvider.GridSpawned += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.GridSpawned -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.GridSpawned, DummyCallback);

            SingleKeyEntityNameEvent DummyCallback = (a) => { };
        }

        class Check_TimerBlockTriggered : CheckForEventBase
        {
            public Check_TimerBlockTriggered(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.TimerBlockTriggered))
            {
                PreCheck(MyVisualScriptLogicProvider.TimerBlockTriggered);
                MyVisualScriptLogicProvider.TimerBlockTriggered += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.TimerBlockTriggered -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.TimerBlockTriggered, DummyCallback);

            SingleKeyEntityNameGridNameEvent DummyCallback = (a, b, c, d) => { };
        }

        class Check_TimerBlockTriggeredEntityName : CheckForEventBase
        {
            public Check_TimerBlockTriggeredEntityName(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.TimerBlockTriggeredEntityName))
            {
                PreCheck(MyVisualScriptLogicProvider.TimerBlockTriggeredEntityName);
                MyVisualScriptLogicProvider.TimerBlockTriggeredEntityName += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.TimerBlockTriggeredEntityName -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.TimerBlockTriggeredEntityName, DummyCallback);

            SingleKeyEntityNameGridNameEvent DummyCallback = (a, b, c, d) => { };
        }

        class Check_BlockDestroyed : CheckForEventBase
        {
            public Check_BlockDestroyed(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.BlockDestroyed))
            {
                PreCheck(MyVisualScriptLogicProvider.BlockDestroyed);
                MyVisualScriptLogicProvider.BlockDestroyed += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.BlockDestroyed -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.BlockDestroyed, DummyCallback);

            SingleKeyEntityNameGridNameEvent DummyCallback = (a, b, c, d) => { };
        }

        class Check_BlockIntegrityChanged : CheckForEventBase
        {
            public Check_BlockIntegrityChanged(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.BlockIntegrityChanged))
            {
                PreCheck(MyVisualScriptLogicProvider.BlockIntegrityChanged);
                MyVisualScriptLogicProvider.BlockIntegrityChanged += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.BlockIntegrityChanged -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.BlockIntegrityChanged, DummyCallback);

            SingleKeyEntityNameGridNameEvent DummyCallback = (a, b, c, d) => { };
        }

        class Check_PlayerSpawned : CheckForEventBase
        {
            public Check_PlayerSpawned(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.PlayerSpawned))
            {
                PreCheck(MyVisualScriptLogicProvider.PlayerSpawned);
                MyVisualScriptLogicProvider.PlayerSpawned += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.PlayerSpawned -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.PlayerSpawned, DummyCallback);

            SingleKeyPlayerEvent DummyCallback = (a) => { };
        }

        class Check_PlayerRequestsRespawn : CheckForEventBase
        {
            public Check_PlayerRequestsRespawn(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.PlayerRequestsRespawn))
            {
                PreCheck(MyVisualScriptLogicProvider.PlayerRequestsRespawn);
                MyVisualScriptLogicProvider.PlayerRequestsRespawn += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.PlayerRequestsRespawn -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.PlayerRequestsRespawn, DummyCallback);

            SingleKeyPlayerEvent DummyCallback = (a) => { };
        }

        class Check_PlayerDied : CheckForEventBase
        {
            public Check_PlayerDied(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.PlayerDied))
            {
                PreCheck(MyVisualScriptLogicProvider.PlayerDied);
                MyVisualScriptLogicProvider.PlayerDied += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.PlayerDied -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.PlayerDied, DummyCallback);

            SingleKeyPlayerEvent DummyCallback = (a) => { };
        }

        class Check_PlayerConnected : CheckForEventBase
        {
            public Check_PlayerConnected(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.PlayerConnected))
            {
                PreCheck(MyVisualScriptLogicProvider.PlayerConnected);
                MyVisualScriptLogicProvider.PlayerConnected += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.PlayerConnected -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.PlayerConnected, DummyCallback);

            SingleKeyPlayerEvent DummyCallback = (a) => { };
        }

        class Check_PlayerDisconnected : CheckForEventBase
        {
            public Check_PlayerDisconnected(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.PlayerDisconnected))
            {
                PreCheck(MyVisualScriptLogicProvider.PlayerDisconnected);
                MyVisualScriptLogicProvider.PlayerDisconnected += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.PlayerDisconnected -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.PlayerDisconnected, DummyCallback);

            SingleKeyPlayerEvent DummyCallback = (a) => { };
        }

        class Check_PlayerRespawnRequest : CheckForEventBase
        {
            public Check_PlayerRespawnRequest(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.PlayerRespawnRequest))
            {
                PreCheck(MyVisualScriptLogicProvider.PlayerRespawnRequest);
                MyVisualScriptLogicProvider.PlayerRespawnRequest += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.PlayerRespawnRequest -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.PlayerRespawnRequest, DummyCallback);

            SingleKeyPlayerEvent DummyCallback = (a) => { };
        }

        class Check_TeamBalancerPlayerSorted : CheckForEventBase
        {
            public Check_TeamBalancerPlayerSorted(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.TeamBalancerPlayerSorted))
            {
                PreCheck(MyVisualScriptLogicProvider.TeamBalancerPlayerSorted);
                MyVisualScriptLogicProvider.TeamBalancerPlayerSorted += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.TeamBalancerPlayerSorted -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.TeamBalancerPlayerSorted, DummyCallback);

            TeamBalancerSortEvent DummyCallback = (a, b) => { };
        }

        class Check_ToolbarItemChanged : CheckForEventBase
        {
            public Check_ToolbarItemChanged(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.ToolbarItemChanged))
            {
                PreCheck(MyVisualScriptLogicProvider.ToolbarItemChanged);
                MyVisualScriptLogicProvider.ToolbarItemChanged += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.ToolbarItemChanged -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.ToolbarItemChanged, DummyCallback);

            ToolbarItemChangedEvent DummyCallback = (a, b, c, d, e) => { };
        }

        class Check_ToolEquipped : CheckForEventBase
        {
            public Check_ToolEquipped(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.ToolEquipped))
            {
                PreCheck(MyVisualScriptLogicProvider.ToolEquipped);
                MyVisualScriptLogicProvider.ToolEquipped += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.ToolEquipped -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.ToolEquipped, DummyCallback);

            ToolEquipedEvent DummyCallback = (a, b, c) => { };
        }

        class Check_WeaponBlockActivated : CheckForEventBase
        {
            public Check_WeaponBlockActivated(CheckContainer container) : base(container, nameof(MyVisualScriptLogicProvider), nameof(MyVisualScriptLogicProvider.WeaponBlockActivated))
            {
                PreCheck(MyVisualScriptLogicProvider.WeaponBlockActivated);
                MyVisualScriptLogicProvider.WeaponBlockActivated += DummyCallback;
            }

            public override void Unhook()
            {
                MyVisualScriptLogicProvider.WeaponBlockActivated -= DummyCallback;
            }

            public override string Check() => CheckEvent(MyVisualScriptLogicProvider.WeaponBlockActivated, DummyCallback);

            WeaponBlockActivatedEvent DummyCallback = (a, b, c, d, e, f) => { };
        }
        #endregion
    }
}