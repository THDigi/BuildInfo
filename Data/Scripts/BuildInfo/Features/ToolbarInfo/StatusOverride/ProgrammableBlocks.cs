﻿using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using VRage;
using VRage.Utils;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class ProgrammableBlocks : StatusOverrideBase
    {
        string PBDIE_NoMain;
        string PBDIE_NoValidCtor;
        string PBDIE_NoAssembly;
        string PBDIE_OwnershipChanged;
        string PBDIE_NestedTooComplex;
        string PBDIE_TooComplex;
        string PBDIE_Caught;

        public ProgrammableBlocks(ToolbarStatusProcessor processor) : base(processor)
        {
            UpdatePBExceptionPrefixes();

            var type = typeof(MyObjectBuilder_MyProgrammableBlock);

            processor.AddStatus(type, Run, "Run", "RunWithDefaultArgument");

            processor.AddGroupStatus(type, GroupRun, "Run", "RunWithDefaultArgument");
        }

        bool Run(StringBuilder sb, ToolbarItem item)
        {
            if(!MyAPIGateway.Session.SessionSettings.EnableIngameScripts)
            {
                sb.Append("ERROR:\nNotAllowed");
                return true;
            }

            var pb = (IMyProgrammableBlock)item.Block;

            if(Processor.AnimFlip && !pb.Enabled)
            {
                Processor.AppendSingleStats(sb, item.Block);
            }
            else
            {
                if(string.IsNullOrEmpty(pb.ProgramData))
                {
                    sb.Append("ERROR:\nEmpty");
                    return true;
                }

                // HACK: MP clients only get PB detailed info when in terminal
                if(!MyAPIGateway.Multiplayer.IsServer)
                {
                    sb.Append("No data\nin MP");
                    return true;
                }

                string detailInfo = pb.DetailedInfo; // allocates a string so best to not call this unnecessarily

                if(string.IsNullOrEmpty(detailInfo))
                {
                    // grab last known echo text
                    PBData pbd;
                    if(BuildInfoMod.Instance.PBMonitor.PBData.TryGetValue(pb.EntityId, out pbd))
                    {
                        detailInfo = pbd.EchoText;
                    }
                }

                if(!string.IsNullOrEmpty(detailInfo))
                {
                    if(detailInfo.StartsWith(PBDIE_NoMain))
                    {
                        sb.Append("ERROR:\nNo Main()");
                    }
                    else if(detailInfo.StartsWith(PBDIE_NoValidCtor))
                    {
                        sb.Append("ERROR:\nInvalid");
                    }
                    else if(detailInfo.StartsWith(PBDIE_NoAssembly)
                         || detailInfo.StartsWith(PBDIE_OwnershipChanged))
                    {
                        sb.Append("ERROR:\nCompile");
                    }
                    else if(detailInfo.StartsWith(PBDIE_TooComplex)
                         || detailInfo.StartsWith(PBDIE_NestedTooComplex))
                    {
                        sb.Append("ERROR:\nTooComplex");
                    }
                    else if(detailInfo.StartsWith(PBDIE_Caught))
                    {
                        sb.Append("ERROR:\nException");
                    }
                    else
                    {
                        // append this max amount of lines from PB detailedinfo/echo
                        int allowedLines = 2;
                        int width = 0;

                        for(int i = 0; i < detailInfo.Length; ++i)
                        {
                            var chr = detailInfo[i];
                            if(chr == '\n')
                            {
                                width = 0;
                                if(--allowedLines == 0)
                                    break;
                            }

                            int chrSize = BuildInfoMod.Instance.FontsHandler.CharSize.GetValueOrDefault(chr, FontsHandler.DefaultCharSize);
                            width += chrSize;

                            // don't add characters beyond line width limit because it erases all lines below it
                            if(width <= ToolbarStatusProcessor.MaxLineSize)
                            {
                                sb.Append(chr);
                            }
                        }

                        if(sb.Length > 0 && sb[sb.Length - 1] == '\n')
                            sb.Length -= 1; // strip last newline
                    }
                }
                else
                {
                    // running or idle without any echo, nothing client can detect here
                }
            }

            return true;
        }

        bool GroupRun(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!MyAPIGateway.Session.SessionSettings.EnableIngameScripts)
            {
                sb.Append("ERROR:\nNotAllowed");
                return true;
            }

            // HACK: MP clients only get PB detailed info when in terminal
            if(!MyAPIGateway.Multiplayer.IsServer)
            {
                sb.Append("No data\nin MP");
                return true;
            }

            if(!groupData.GetGroupBlocks<IMyProgrammableBlock>())
                return false;

            int broken = 0;
            int off = 0;
            int errors = 0;
            int echo = 0;

            foreach(IMyProgrammableBlock pb in groupData.Blocks)
            {
                if(!pb.IsFunctional)
                    broken++;

                if(!pb.Enabled)
                    off++;

                string detailInfo = pb.DetailedInfo; // allocates a string so best to not call this unnecessarily

                if(!string.IsNullOrEmpty(detailInfo))
                {
                    if(detailInfo.StartsWith(PBDIE_NoMain)
                    || detailInfo.StartsWith(PBDIE_NoValidCtor)
                    || detailInfo.StartsWith(PBDIE_NoAssembly)
                    || detailInfo.StartsWith(PBDIE_OwnershipChanged)
                    || detailInfo.StartsWith(PBDIE_TooComplex)
                    || detailInfo.StartsWith(PBDIE_NestedTooComplex)
                    || detailInfo.StartsWith(PBDIE_Caught))
                    {
                        errors++;
                    }
                    else
                    {
                        echo++;
                    }
                }
            }

            Processor.AppendGroupStats(sb, broken, off);

            int total = groupData.Blocks.Count;

            if(errors == 0)
            {
                sb.NumberCapped(echo).Append(" msg");
            }
            else if(errors == total)
            {
                sb.Append("All error");
            }
            else
            {
                sb.NumberCapped(echo).Append(" msg\n");
                sb.NumberCapped(errors).Append(" error");
            }

            return true;
        }

        void UpdatePBExceptionPrefixes()
        {
            PBDIE_NoMain = GetTranslatedLimited(MySpaceTexts.ProgrammableBlock_Exception_NoMain);
            PBDIE_NoValidCtor = GetTranslatedLimited(MySpaceTexts.ProgrammableBlock_Exception_NoValidConstructor);
            PBDIE_NoAssembly = GetTranslatedLimited(MySpaceTexts.ProgrammableBlock_Exception_NoAssembly);
            PBDIE_OwnershipChanged = GetTranslatedLimited(MySpaceTexts.ProgrammableBlock_Exception_Ownershipchanged);
            PBDIE_NestedTooComplex = GetTranslatedLimited(MySpaceTexts.ProgrammableBlock_Exception_NestedTooComplex);
            PBDIE_TooComplex = GetTranslatedLimited(MySpaceTexts.ProgrammableBlock_Exception_TooComplex);
            PBDIE_Caught = GetTranslatedLimited(MySpaceTexts.ProgrammableBlock_Exception_ExceptionCaught);
        }

        string GetTranslatedLimited(MyStringId langKey, int maxLength = 10)
        {
            string text = MyTexts.GetString(langKey);
            return text.Substring(0, Math.Min(text.Length, maxLength));
        }
    }
}
