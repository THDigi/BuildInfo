using System.Collections.Generic;
using System.Text;
using CoreSystems.Api;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using IMyControllableEntityModAPI = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class GenericFallback : StatusOverrideBase
    {
        public GenericFallback(ToolbarStatusProcessor processor) : base(processor)
        {
            ToolbarStatusProcessor.StatusDel funcShowIn = new ToolbarStatusProcessor.StatusDel(ShowIn);

            processor.AddFallback(OnOff, "OnOff", "OnOff_On", "OnOff_Off");
            processor.AddGroupFallback(GroupOnOff, "OnOff", "OnOff_On", "OnOff_Off");

            processor.AddFallback(UseConveyor, "UseConveyor");
            processor.AddGroupFallback(GroupUseConveyor, "UseConveyor");

            processor.AddFallback(funcShowIn, "ShowOnHUD", "ShowOnHUD_On", "ShowOnHUD_Off");
            // these don't seem to have actions
            //processor.AddFallback(funcShowIn, "ShowInTerminal", "ShowInTerminal_On", "ShowInTerminal_Off");
            //processor.AddFallback(funcShowIn, "ShowInInventory", "ShowInInventory_On", "ShowInInventory_Off");
            //processor.AddFallback(funcShowIn, "ShowInToolbarConfig", "ShowInToolbarConfig_On", "ShowInToolbarConfig_Off");

            processor.AddFallback(Control, "Control");
        }

        bool OnOff(StringBuilder sb, ToolbarItem item)
        {
            IMyFunctionalBlock block = item.Block as IMyFunctionalBlock;
            if(block == null)
                return false;

            // doesn't need IsFunctional check, slot grayed out

            // to differentiate at a glance between block on/off and other toggle.
            sb.Append(block.Enabled ? IconPowerOn : IconPowerOff).Append(block.Enabled ? "ON" : "OFF");
            return true;
        }

        bool GroupOnOff(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyFunctionalBlock>())
                return false;

            int on = 0;
            int broken = 0;

            foreach(IMyFunctionalBlock b in groupData.Blocks)
            {
                if(!b.IsFunctional)
                    broken++;
                else if(b.Enabled)
                    on++;
            }

            int total = groupData.Blocks.Count;

            if(Processor.AnimFlip)
            {
                sb.NumberCapped(on, MaxChars - 4).Append(IconBroken); //.Append("dmg");
                sb.Append('\n');
            }

            if(on == total)
            {
                sb.Append("All").Append(IconPowerOn); //.Append("on");
            }
            else if(on == 0)
            {
                sb.Append("All").Append(IconPowerOff); //.Append("off");
            }
            else
            {
                sb.NumberCapped(on, MaxChars - 3).Append(IconPowerOn); //.Append("on");
                sb.Append('\n');
                sb.NumberCapped(total - on, MaxChars - 3).Append(IconPowerOff); //.Append("off");
            }

            return true;
        }

        bool UseConveyor(StringBuilder sb, ToolbarItem item)
        {
            bool useConveyor = item.Block.GetValue<bool>("UseConveyor"); // TODO: replace when GetProperty() no longer necessary
            sb.Append(useConveyor ? "Share" : "Isolate");
            return true;
        }

        bool GroupUseConveyor(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyTerminalBlock>())
                return false;

            int useConveyor = 0;

            foreach(IMyTerminalBlock b in groupData.Blocks)
            {
                ITerminalProperty prop = b.GetProperty("UseConveyor"); // TODO: replace when GetProperty() no longer necessary
                if(prop != null)
                {
                    if(prop.AsBool().GetValue(b))
                        useConveyor++;
                }
            }

            int total = groupData.Blocks.Count;
            int noConveyor = (total - useConveyor);

            if(useConveyor == total)
            {
                sb.Append("All share");
            }
            else if(noConveyor == total)
            {
                sb.Append("All isolate");
            }
            else
            {
                sb.NumberCapped(noConveyor).Append(" iso\n");
                sb.NumberCapped(useConveyor).Append(" share"); // NOTE: this line is too wide with "99+ share", must be last.
            }

            return true;
        }

        bool ShowIn(StringBuilder sb, ToolbarItem item)
        {
            sb.Append(item.Block.ShowOnHUD ? "Shown" : "Hidden");
            return true;
        }

        readonly List<IMyPlayer> TempPlayers = new List<IMyPlayer>();

        public IMyPlayer GetPlayerFromIdentityId(long identityId)
        {
            TempPlayers.Clear();
            MyAPIGateway.Players.GetPlayers(TempPlayers);

            IMyPlayer returnPlayer = null;

            foreach(IMyPlayer player in TempPlayers)
            {
                if(player.IdentityId == identityId)
                {
                    returnPlayer = player;
                    break;
                }
            }

            TempPlayers.Clear();
            return returnPlayer;
        }

        bool Control(StringBuilder sb, ToolbarItem item)
        {
            {
                CoreSystemsAPIHandler CSHandler = Processor.Main.CoreSystemsAPIHandler;
                List<CoreSystemsDef.WeaponDefinition> csDefs;
                if(CSHandler.Weapons.TryGetValue(item.Block.BlockDefinition, out csDefs))
                {
                    long identityId = CSHandler.API.GetPlayerController(item.Block);

                    if(identityId == -1 || identityId == 0) // WC for some reason has -1 as no identity
                    {
                        MyTuple<bool, bool, bool, IMyEntity> targetInfo = CSHandler.API.GetWeaponTarget(item.Block);

                        //bool hasTarget = targetInfo.Item1;
                        //bool isTargetProjectile = targetInfo.Item2;
                        bool isManualOrPainter = targetInfo.Item3;

                        if(!isManualOrPainter)
                            sb.Append("AI");
                        else
                            sb.Append("Idle");
                    }
                    else
                    {
                        IMyPlayer player = GetPlayerFromIdentityId(identityId);
                        sb.Append("Control:\n");

                        if(player == null)
                            sb.Append("(Unk)");
                        else
                            sb.CleanPlayerName(player.DisplayName, MaxChars);
                    }

                    return true;
                }
            }

            {
                IMyPlayer player = MyAPIGateway.Players.GetPlayerControllingEntity(item.Block);
                if(player != null)
                {
                    sb.Append("Control:\n").CleanPlayerName(player.DisplayName, MaxChars);
                    return true;
                }
            }

            IMyControllableEntityModAPI controllable = item.Block as IMyControllableEntityModAPI;
            if(controllable != null)
            {
                //long identityId = controllable.ControllerInfo?.ControllingIdentityId ?? 0;
                //if(identityId != 0)
                //{
                //    IMyPlayer player = GetPlayerFromIdentityId(identityId);
                //    sb.Append("Ctrl by\n").Append(player == null ? "(Unk)" : player.DisplayName);
                //    return true;
                //}

                {
                    IMyRemoteControl rc = item.Block as IMyRemoteControl;
                    if(rc != null && rc.IsAutoPilotEnabled)
                    {
                        sb.Append("AutoPilot");
                        return true;
                    }
                }

                {
                    IMyLargeTurretBase turret = item.Block as IMyLargeTurretBase;
                    if(turret != null)
                    {
                        if(turret.AIEnabled) // set definition
                        {
                            sb.Append("AI");
                            return true;
                        }
                    }
                }

                {
                    IMyTurretControlBlock tcb = item.Block as IMyTurretControlBlock;
                    if(tcb != null)
                    {
                        if(tcb.AIEnabled)  // set in terminal
                        {
                            sb.Append("AI");
                            return true;
                        }
                    }
                }

                {
                    MySearchlightDefinition searchLightDef = item.Block.SlimBlock.BlockDefinition as MySearchlightDefinition;
                    if(searchLightDef != null)
                    {
                        if(searchLightDef.AiEnabled)  // set in definition
                        {
                            sb.Append("AI");
                            return true;
                        }
                    }
                }

                sb.Append("Idle");
                return true;
            }

            return false;
        }
    }
}
