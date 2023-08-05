using System;
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
        const int LimitCheckChars = 16;
        const string ErrorPrefix = "\ue104Error:\n"; // IconBad

        public ProgrammableBlocks(ToolbarStatusProcessor processor) : base(processor)
        {
            UpdatePBExceptionPrefixes();

            Type type = typeof(MyObjectBuilder_MyProgrammableBlock);

            processor.AddStatus(type, Run, "Run", "RunWithDefaultArgument");

            processor.AddGroupStatus(type, GroupRun, "Run", "RunWithDefaultArgument");
        }

        bool Run(StringBuilder sb, ToolbarItem item)
        {
            if(!MyAPIGateway.Session.SessionSettings.EnableIngameScripts)
            {
                sb.Append("ERROR:\nNotAllow");
                return true;
            }

            IMyProgrammableBlock pb = (IMyProgrammableBlock)item.Block;

            if(!Processor.AppendSingleStats(sb, item.Block))
            {
                if(string.IsNullOrEmpty(pb.ProgramData))
                {
                    sb.Append(ErrorPrefix).Append("Empty");
                    return true;
                }

                // HACK: MP clients only get PB detailed info when in terminal
                if(!MyAPIGateway.Multiplayer.IsServer)
                {
                    // TODO: split off into a separate mod, with networking
                    sb.Append("No data\nin MP");
                    return true;
                }

                if(MyAPIGateway.Multiplayer.IsServer && pb.HasCompileErrors)
                {
                    sb.Append(ErrorPrefix).Append("Syntax!");
                    return true;
                }

                string detailedInfo = null;

                StringBuilder detailedInfoSB = pb.GetDetailedInfo();
                if(detailedInfoSB != null && detailedInfoSB.Length > 0)
                {
                    // get a limited amount of characters for fast checking
                    detailedInfo = detailedInfoSB.ToString(0, Math.Min(detailedInfoSB.Length, LimitCheckChars));
                }
                else
                {
                    // grab last known echo text
                    PBEcho pbe;
                    if(BuildInfoMod.Instance.PBMonitor.PBEcho.TryGetValue(pb.EntityId, out pbe))
                    {
                        detailedInfo = pbe.EchoText;
                    }
                }

                if(!string.IsNullOrEmpty(detailedInfo))
                {
                    if(detailedInfo.StartsWith(PBDIE_NoMain))
                    {
                        sb.Append(ErrorPrefix).Append("No Main");
                    }
                    else if(detailedInfo.StartsWith(PBDIE_NoValidCtor))
                    {
                        sb.Append(ErrorPrefix).Append("Invalid");
                    }
                    else if(detailedInfo.StartsWith(PBDIE_NoAssembly)
                         || detailedInfo.StartsWith(PBDIE_OwnershipChanged))
                    {
                        sb.Append(ErrorPrefix).Append("Compile!");
                    }
                    else if(detailedInfo.StartsWith(PBDIE_TooComplex)
                         || detailedInfo.StartsWith(PBDIE_NestedTooComplex))
                    {
                        sb.Append(ErrorPrefix).Append("Complex!");
                    }
                    else if(detailedInfo.StartsWith(PBDIE_Caught))
                    {
                        sb.Append(ErrorPrefix).Append("Excep.");
                    }
                    else
                    {
                        // append this max amount of lines from PB detailedinfo/echo
                        int allowedLines = ToolbarStatusProcessor.MaxLines;
                        int lineLen = 0;

                        // get full echo for proper parsing
                        if(detailedInfoSB != null && detailedInfoSB.Length > 0)
                        {
                            for(int i = 0; i < detailedInfoSB.Length; ++i)
                            {
                                char chr = detailedInfoSB[i];
                                if(chr == '\n')
                                {
                                    lineLen = 0;
                                    if(--allowedLines == 0)
                                        break;
                                }
                                else
                                {
                                    lineLen++;
                                }

                                // don't add characters beyond line width limit because it erases all lines below it
                                if(lineLen <= ToolbarStatusProcessor.MaxChars)
                                {
                                    sb.Append(chr);
                                }
                            }
                        }
                        else
                        {
                            for(int i = 0; i < detailedInfo.Length; ++i)
                            {
                                char chr = detailedInfo[i];
                                if(chr == '\n')
                                {
                                    lineLen = 0;
                                    if(--allowedLines == 0)
                                        break;
                                }
                                else
                                {
                                    lineLen++;
                                }

                                // don't add characters beyond line width limit because it erases all lines below it
                                if(lineLen <= ToolbarStatusProcessor.MaxChars)
                                {
                                    sb.Append(chr);
                                }
                            }
                        }

                        sb.TrimEndWhitespace();
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
                sb.Append(ErrorPrefix).Append("NotAllow");
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

                if(string.IsNullOrEmpty(pb.ProgramData))
                {
                    errors++;
                    continue;
                }

                if(MyAPIGateway.Multiplayer.IsServer && pb.HasCompileErrors)
                {
                    errors++;
                    continue;
                }

                string detailedInfo = null;

                StringBuilder detailedInfoSB = pb.GetDetailedInfo();
                if(detailedInfoSB != null && detailedInfoSB.Length > 0)
                {
                    detailedInfo = detailedInfoSB.ToString(0, Math.Min(detailedInfoSB.Length, LimitCheckChars));
                }
                else
                {
                    // grab last known echo text
                    PBEcho pbe;
                    if(BuildInfoMod.Instance.PBMonitor.PBEcho.TryGetValue(pb.EntityId, out pbe))
                    {
                        detailedInfo = pbe.EchoText;
                    }
                }

                if(!string.IsNullOrEmpty(detailedInfo))
                {
                    if(detailedInfo.StartsWith(PBDIE_NoMain)
                    || detailedInfo.StartsWith(PBDIE_NoValidCtor)
                    || detailedInfo.StartsWith(PBDIE_NoAssembly)
                    || detailedInfo.StartsWith(PBDIE_OwnershipChanged)
                    || detailedInfo.StartsWith(PBDIE_TooComplex)
                    || detailedInfo.StartsWith(PBDIE_NestedTooComplex)
                    || detailedInfo.StartsWith(PBDIE_Caught))
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
                sb.NumberCappedSpaced(echo, MaxChars - 3).Append("msg");
            }
            else if(errors == total)
            {
                sb.Append("All error");
            }
            else
            {
                sb.NumberCappedSpaced(echo, MaxChars - 3).Append("msg\n");
                sb.NumberCappedSpaced(errors, MaxChars - 4).Append(IconBad).Append("err");
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

        string GetTranslatedLimited(MyStringId langKey, int maxLength = LimitCheckChars)
        {
            string text = MyTexts.GetString(langKey);
            return text.Substring(0, Math.Min(text.Length, maxLength));
        }
    }
}
