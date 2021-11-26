using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.ToolbarInfo;
using Digi.ComponentLib;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features
{
    /// <summary>
    /// Tracks built blocks and add top actions to color+skin the top part block like the base block.
    /// 
    /// Edge cases where it can fail:
    /// - timer/etc triggers add top action while player is offline or not have it streamed, won't color it (because this code only runs player-side).
    /// </summary>
    public class TopPartColor : ModComponent
    {
        readonly Queue<QueueItem> BlockQueue = new Queue<QueueItem>();

        struct QueueItem
        {
            public readonly IMyMechanicalConnectionBlock Block;
            public readonly int CheckAtTick;

            public QueueItem(IMyMechanicalConnectionBlock block)
            {
                Block = block;
                CheckAtTick = BuildInfoMod.Instance.Tick + 30;
            }
        }

        readonly HashSet<IMyTerminalControl> ModifiedTerminalControls = new HashSet<IMyTerminalControl>();

        public TopPartColor(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            MyVisualScriptLogicProvider.BlockBuilt += BlockBuilt;
            MyAPIGateway.TerminalControls.CustomControlGetter += TerminalControlGetter;
            Main.ToolbarOverride.ActionCollected += ActionCollected;
        }

        public override void UnregisterComponent()
        {
            MyVisualScriptLogicProvider.BlockBuilt -= BlockBuilt;
            MyAPIGateway.TerminalControls.CustomControlGetter -= TerminalControlGetter;

            if(!Main.ComponentsRegistered)
                return;

            Main.ToolbarOverride.ActionCollected -= ActionCollected;
        }

        void BlockBuilt(string typeId, string subtypeId, string gridName, long blockId)
        {
            try
            {
                IMyCubeBlock block = MyEntities.GetEntityById(blockId) as IMyCubeBlock;
                if(block != null)
                    HandleBlock(block);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void TerminalControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            try
            {
                if(block as IMyMechanicalConnectionBlock == null)
                    return;

                foreach(IMyTerminalControl control in controls)
                {
                    IMyTerminalControlButton button = control as IMyTerminalControlButton;
                    if(button == null)
                        continue;

                    switch(control.Id)
                    {
                        case "AddRotorTopPart":
                        case "AddHingeTopPart":
                        case "AddSmallRotorTopPart":
                        case "AddSmallHingeTopPart":
                        case "Add Top Part":
                            if(ModifiedTerminalControls.Add(control)) // didn't exist in the set
                                button.Action += TerminalAddTopAction; // append to the existing delegate(s)
                            break;
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void ActionCollected(ActionWrapper wrapper)
        {
            switch(wrapper.Action.Id)
            {
                case "AddRotorTopPart":
                case "AddHingeTopPart":
                case "AddSmallRotorTopPart":
                case "AddSmallHingeTopPart":
                case "Add Top Part":
                    wrapper.Action.Action += TerminalAddTopAction; // append to the existing delegate(s)
                    break;
            }
        }

        void TerminalAddTopAction(IMyTerminalBlock block)
        {
            try
            {
                HandleBlock(block);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void HandleBlock(IMyCubeBlock block)
        {
            // HACK: player can be null for MP clients that just joined (and updated ~4 ticks), ignore those cases...
            IMyPlayer player = MyAPIGateway.Session.Player;
            if(player == null)
                return;

            IMyMechanicalConnectionBlock mechanicalBlock = block as IMyMechanicalConnectionBlock;
            if(mechanicalBlock != null && mechanicalBlock.SlimBlock.BuiltBy == player.IdentityId)
            {
                BlockQueue.Enqueue(new QueueItem(mechanicalBlock));
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
            }
        }

        public override void UpdateAfterSim(int tick)
        {
            while(BlockQueue.Count > 0)
            {
                QueueItem item = BlockQueue.Peek();
                if(item.CheckAtTick > tick)
                    break;

                BlockQueue.Dequeue();
                IMyMechanicalConnectionBlock block = item.Block;
                if(block.Top != null)
                {
                    // gets synchronized automatically
                    block.TopGrid.SkinBlocks(block.Top.Min, block.Top.Min, block.SlimBlock.ColorMaskHSV, block.SlimBlock.SkinSubtypeId.String);
                }
            }

            if(BlockQueue.Count == 0)
            {
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
            }
        }
    }
}
