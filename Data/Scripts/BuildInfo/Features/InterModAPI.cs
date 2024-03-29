﻿using System;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;

namespace Digi.BuildInfo.Features
{
    public class InterModAPI : ModComponent
    {
        public const long MOD_API_ID = 514062285; // API id for other mods to use, must not be changed if you want to support the API users; see "API Information"

        public InterModAPI(BuildInfoMod main) : base(main)
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(MOD_API_ID, ModMessageReceived);
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(MOD_API_ID, ModMessageReceived);
        }

        void ModMessageReceived(object obj)
        {
            try
            {
                if(obj is MyTuple<string, string, MyDefinitionId>)
                {
                    MyTuple<string, string, MyDefinitionId> data = (MyTuple<string, string, MyDefinitionId>)obj;

                    string modName = data.Item1;
                    string target = data.Item2;
                    MyDefinitionId defId = data.Item3;

                    if(Main.Tick > 10)
                    {
                        Log.Error($"Mod '{data.Item1}' sent {target} request too late (must be sent within first 10 ticks)");
                        return;
                    }

                    if(target == null)
                    {
                        Log.Error($"Mod '{data.Item1}' sent NULL target (2nd string); defId={defId.ToString()}");
                        return;
                    }

                    const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

                    if(target.Equals("All", Compare))
                    {
                        Main.TooltipHandler.IgnoreModItems.Add(defId);
                        Main.TerminalInfo.IgnoreModBlocks.Add(defId);
                        Main.BlockDescriptions.IgnoreBlockDefs.Add(defId);

                        if(Main.Config.InternalInfo.Value)
                            Log.Info($"Mod '{data.Item1}' asked BuildInfo to ignore all the things for {defId.ToString()}");

                        return;
                    }

                    if(target.Equals("NoDescriptionInfo", Compare))
                    {
                        Main.BlockDescriptions.IgnoreBlockDefs.Add(defId);

                        if(Main.Config.InternalInfo.Value)
                            Log.Info($"Mod '{data.Item1}' asked BuildInfo to not show extra Description info for {defId.ToString()}");

                        return;
                    }

                    if(target.Equals("NoDetailInfo", Compare))
                    {
                        Main.TerminalInfo.IgnoreModBlocks.Add(defId);

                        if(Main.Config.InternalInfo.Value)
                            Log.Info($"Mod '{data.Item1}' asked BuildInfo to not show extra DetailInfo for {defId.ToString()}");

                        return;
                    }

                    if(target.Equals("NoItemTooltip", Compare))
                    {
                        Main.TooltipHandler.IgnoreModItems.Add(defId);

                        if(Main.Config.InternalInfo.Value)
                            Log.Info($"Mod '{data.Item1}' asked BuildInfo to not show extra ItemTooltip for {defId.ToString()}");

                        return;
                    }

                    Log.Error($"Mod {data.Item1} sent an unknown target='{target}'; defId={defId.ToString()}");
                    return;
                }

                // backwards compatible
                if(obj is MyDefinitionId)
                {
                    if(Main.Tick > 10)
                    {
                        Log.Error($"A mod sent DetailInfo ignore request too late (must be sent within first 10 ticks)");
                        return;
                    }

                    MyDefinitionId id = (MyDefinitionId)obj;
                    Main.TerminalInfo.IgnoreModBlocks.Add(id);
                    return;
                }

                Log.Error($"A mod sent an unknwon mod message to this mod; type={obj?.GetType()?.FullName}; data='{obj}'");
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
