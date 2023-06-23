using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features;
using Digi.ComponentLib;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.GUI.TextPanel;
using VRage.Utils;
using VRageMath;
using PB_TextSurfaceProvider = Sandbox.ModAPI.Ingame.IMyTextSurfaceProvider; // HACK: because modAPI one isn't implemented

namespace Digi.BuildInfo.Systems
{
    public struct SurfaceInfo
    {
        public readonly int Index;
        public readonly IMyTextSurface Surface;

        public SurfaceInfo(int index, IMyTextSurface surface)
        {
            Index = index;
            Surface = surface;
        }
    }

    public struct SurfaceCache
    {
        public readonly int Index;
        public readonly IMyTextSurface Surface;
        public readonly int CacheExpiresAt;

        public SurfaceCache(int index, IMyTextSurface surface)
        {
            Surface = surface;
            Index = index;
            CacheExpiresAt = BuildInfoMod.Instance.Tick + SelectedLCD.CacheLiveTicks;
        }
    }

    //public class SurfaceCache
    //{
    //    public readonly IMyTerminalBlock Block;
    //    public int Index;
    //    public IMyTextSurface Surface;
    //    public int CacheExpiresAt;
    //
    //    public SurfaceCache(IMyTerminalBlock block, int index, IMyTextSurface surface)
    //    {
    //        Block = block;
    //        //Block.PropertiesChanged += BlockPropertiesChanged;
    //
    //        ChangeSelection(index, surface);
    //    }
    //
    //    public void ChangeSelection(int index, IMyTextSurface surface)
    //    {
    //        Surface = surface;
    //        Index = index;
    //        CacheExpiresAt = BuildInfoMod.Instance.Tick + SelectedLCD.CacheLiveTicks;
    //    }
    //
    //    //void BlockPropertiesChanged(IMyTerminalBlock block)
    //    //{
    //    //    // something changed on the block, make this cache expire
    //    //    CacheExpiresAt = 0;
    //    //}
    //
    //    internal void Dispose()
    //    {
    //        //Block.PropertiesChanged -= BlockPropertiesChanged;
    //    }
    //}

    public class SelectedLCD : ModComponent
    {
        public const int CacheLiveTicks = Constants.TicksPerSecond * 10; // needs to be low enough to detect changes from other players

        Dictionary<PB_TextSurfaceProvider, SurfaceCache> SelectedCache = new Dictionary<PB_TextSurfaceProvider, SurfaceCache>();
        List<PB_TextSurfaceProvider> CacheToRemove = new List<PB_TextSurfaceProvider>();

        List<SurfaceInfo> TempMatches = new List<SurfaceInfo>();

        public SelectedLCD(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.TerminalInfo.SelectedChanged += TerminalSelectionChanged;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.TerminalInfo.SelectedChanged -= TerminalSelectionChanged;
        }

        void TerminalSelectionChanged()
        {
            bool selectionHasMultiLCDs = false;

            foreach(IMyTerminalBlock block in Main.TerminalInfo.SelectedInTerminal)
            {
                PB_TextSurfaceProvider surfaceProvider = block as PB_TextSurfaceProvider;
                if(surfaceProvider != null && surfaceProvider.SurfaceCount > 1)
                {
                    selectionHasMultiLCDs = true;
                }
            }

            SetUpdateMethods(UpdateFlags.UPDATE_INPUT, selectionHasMultiLCDs);
        }

        public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(!paused && inMenu && anyKeyOrMouse && MyAPIGateway.Input.IsNewLeftMousePressed())
            {
                foreach(IMyTerminalBlock block in Main.TerminalInfo.SelectedInTerminal)
                {
                    PB_TextSurfaceProvider surfaceProvider = block as PB_TextSurfaceProvider;
                    if(surfaceProvider != null)
                    {
                        // cache.Dispose()
                        SelectedCache.Remove(surfaceProvider);
                    }
                }
            }
        }

        static Nullable<T> GetProp<T>(IMyTerminalBlock block, string propId) where T : struct
        {
            ITerminalProperty<T> prop = block.GetProperty(propId)?.As<T>();
            if(prop == null)
            {
                //throw new Exception($"Couldn't find terminal property '{propId}' or not of type {typeof(T)}");
                return null;
            }
            return prop.GetValue(block);
        }

        /// <summary>
        /// Attempt to detect which LCD is selected.
        /// Has fast shortcuts for 0, 1 or cockpits.
        /// If <paramref name="useCache"/> is true, uses a dictionary cache per block instance that expries in 3 seconds.
        /// If <paramref name="useToggleDetect"/> is used it can toggle an LCD property to always identify, otherwise it can fail and return false if it has multiple matches.
        /// </summary>
        public bool TryGetSelectedSurface(PB_TextSurfaceProvider surfaceProvider, out SurfaceInfo surfaceInfo, bool useCache = true, bool useToggleDetect = true)
        {
            surfaceInfo = default(SurfaceInfo);

            if(surfaceProvider.SurfaceCount <= 0)
                return false;

            if(surfaceProvider.SurfaceCount == 1)
            {
                surfaceInfo = new SurfaceInfo(0, (IMyTextSurface)surfaceProvider.GetSurface(0));
                return true;
            }

            // HACK: faster alternative only for cockpits
            MyCockpit cockpit = surfaceProvider as MyCockpit;
            if(cockpit != null)
            {
                if(cockpit.PanelComponent == null)
                    return false;

                for(int i = 0; i < surfaceProvider.SurfaceCount; i++)
                {
                    IMyTextSurface surface = (IMyTextSurface)surfaceProvider.GetSurface(i);
                    if(surface == cockpit.PanelComponent)
                    {
                        surfaceInfo = new SurfaceInfo(i, surface);
                        return true;
                    }
                }
            }

            if(useCache)
            {
                SurfaceCache cache;
                if(SelectedCache.TryGetValue(surfaceProvider, out cache))
                {
                    surfaceInfo = new SurfaceInfo(cache.Index, cache.Surface);
                    return true;
                }
            }

            TempMatches.Clear();
            IMyTerminalBlock block = (IMyTerminalBlock)surfaceProvider;

            bool? preserveAspectRatio;
            try
            {
                preserveAspectRatio = GetProp<bool>(block, "PreserveAspectRatio");

                Color? scriptFgColor = GetProp<Color>(block, "ScriptForegroundColor");
                Color? scriptBgColor = GetProp<Color>(block, "ScriptBackgroundColor");
                float? fontSize = GetProp<float>(block, "FontSize");
                Color? fontColor = GetProp<Color>(block, "FontColor");
                float? textPadding = GetProp<float>(block, "TextPaddingSlider");
                Color? bgColor = GetProp<Color>(block, "BackgroundColor");
                float? bgChangeInterval = GetProp<float>(block, "ChangeIntervalSlider");

                TextAlignment? alignment = null;
                long? textAlignmentRaw = GetProp<long>(block, "alignment");
                if(textAlignmentRaw != null)
                    alignment = (TextAlignment)textAlignmentRaw.Value;

                ContentType? contentType = null;
                long? contentTypeRaw = GetProp<long>(block, "Content");
                if(contentTypeRaw != null)
                    contentType = (ContentType)contentTypeRaw.Value;

                long? fontProp = GetProp<long>(block, "Font");
                string font = null;
                if(fontProp != null)
                {
                    MyStringHash stringHash = MyStringHash.TryGet((int)fontProp.Value);
                    if(stringHash != MyStringHash.NullOrEmpty)
                        font = stringHash.String;
                }

                for(int i = 0; i < surfaceProvider.SurfaceCount; i++)
                {
                    IMyTextSurface surface = (IMyTextSurface)surfaceProvider.GetSurface(i);

                    // each prop is optional, in case it doesn't exist anymore
                    if(contentType != null && contentType.Value != surface.ContentType) continue;
                    if(scriptFgColor != null && scriptFgColor.Value != surface.ScriptForegroundColor) continue;
                    if(scriptBgColor != null && scriptBgColor.Value != surface.ScriptBackgroundColor) continue;
                    if(font != null && font != surface.Font) continue;
                    if(fontSize != null && fontSize.Value != surface.FontSize) continue;
                    if(fontColor != null && fontColor.Value != surface.FontColor) continue;
                    if(alignment != null && alignment.Value != surface.Alignment) continue;
                    if(textPadding != null && textPadding.Value != surface.TextPadding) continue;
                    if(bgColor != null && bgColor.Value != surface.BackgroundColor) continue;
                    if(bgChangeInterval != null && bgChangeInterval.Value != surface.ChangeInterval) continue;
                    if(preserveAspectRatio != null && preserveAspectRatio.Value != surface.PreserveAspectRatio) continue;

                    //if(surface.ContentType == contentType
                    //&& surface.ScriptForegroundColor == scriptFgColor
                    //&& surface.ScriptBackgroundColor == scriptBgColor
                    //&& surface.Font == font
                    //&& surface.FontSize == fontSize
                    //&& surface.FontColor == fontColor
                    //&& surface.Alignment == alignment
                    //&& surface.TextPadding == textPadding
                    //&& surface.BackgroundColor == bgColor
                    //&& surface.ChangeInterval == bgChangeInterval
                    //&& surface.PreserveAspectRatio == preserveAspectRatio)

                    TempMatches.Add(new SurfaceInfo(i, surface));
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
                return false;
            }

            bool result = false;

            if(TempMatches.Count <= 0)
            {
                //Log.Error("SelectedLCD could not find any selected LCD :o");
            }
            else if(TempMatches.Count == 1)
            {
                surfaceInfo = TempMatches[0];
                result = true;
            }
            else if(useToggleDetect)
            {
                if(preserveAspectRatio == null)
                {
                    Log.Error($"'PreserveAspectRatio' prop no longer exists for '{block.BlockDefinition}'. Can't identify selected LCD because of this.");
                }
                else
                {
                    try
                    {
                        block.SetValue<bool>("PreserveAspectRatio", !preserveAspectRatio.Value); // NOTE: synchronized

                        foreach(SurfaceInfo si in TempMatches)
                        {
                            if(si.Surface.PreserveAspectRatio == !preserveAspectRatio.Value)
                            {
                                surfaceInfo = si;
                                result = true;
                                break; // can't be more than one
                            }
                        }
                    }
                    finally
                    {
                        block.SetValue<bool>("PreserveAspectRatio", preserveAspectRatio.Value);
                    }
                }
            }

            TempMatches.Clear();

            if(useCache)
            {
                //SurfaceCache cache;
                //if(SelectedCache.TryGetValue(surfaceProvider, out cache))
                //{
                //    cache.ChangeSelection(surfaceInfo.Index, surfaceInfo.Surface);
                //}
                //else
                {
                    SelectedCache[surfaceProvider] = new SurfaceCache(surfaceInfo.Index, surfaceInfo.Surface);
                }

                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
            }

            return result;
        }

        public override void UpdateAfterSim(int tick)
        {
            if(SelectedCache.Count == 0)
            {
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                return;
            }

            if(tick % 30 == 0)
                return;

            foreach(KeyValuePair<PB_TextSurfaceProvider, SurfaceCache> kv in SelectedCache)
            {
                SurfaceCache cache = kv.Value;
                if(tick >= cache.CacheExpiresAt)
                {
                    //cache.Dispose();
                    CacheToRemove.Add(kv.Key);
                }
            }

            foreach(PB_TextSurfaceProvider key in CacheToRemove)
            {
                SelectedCache.Remove(key);
            }

            CacheToRemove.Clear();

            if(SelectedCache.Count == 0)
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
        }
    }
}
