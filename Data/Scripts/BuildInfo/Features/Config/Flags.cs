﻿using System;

namespace Digi.BuildInfo.Features.Config
{
    [Flags]
    public enum AimInfoFlags
    {
        None = 0,
        All = int.MaxValue,
        TerminalName = (1 << 0),
        Mass = (1 << 1),
        Integrity = (1 << 2),
        DamageMultiplier = (1 << 3),
        ToolUseTime = (1 << 4),
        Ownership = (1 << 5),
        GrindChangeWarning = (1 << 6),
        GridMoving = (1 << 7),
        ShipGrinderImpulse = (1 << 8),
        GrindGridSplit = (1 << 9),
        AddedByMod = (1 << 10),
        OverlayHint = (1 << 11),
        Projected = (1 << 12),
        RequiresDLC = (1 << 13),
        ComponentsVolume = (1 << 14),
    }

    [Flags]
    public enum PlaceInfoFlags
    {
        None = 0,
        All = int.MaxValue,
        BlockName = (1 << 0),
        Line1 = (1 << 1),
        Line2 = (1 << 2),
        Airtight = (1 << 3),
        GrindChangeWarning = (1 << 4),
        Mirroring = (1 << 5),
        AddedByMod = (1 << 6),
        OverlayHint = (1 << 7),
        ExtraInfo = (1 << 8),
        PartStats = (1 << 9),
        PowerStats = (1 << 10),
        ResourcePriorities = (1 << 11),
        InventoryStats = (1 << 12),
        InventoryVolumeMultiplied = (1 << 13),
        InventoryExtras = (1 << 14),
        Production = (1 << 15),
        ItemInputs = (1 << 16),
        AmmoDetails = (1 << 17),
        Warnings = (1 << 18),
        RequiresDLC = (1 << 19),
        ComponentsVolume = (1 << 20),
    }

    [Flags]
    public enum TextAlignFlags
    {
        Top = 1,
        Bottom = 2,
        Left = 4,
        Right = 8,
    }

    public enum CubeBuilderSelectionInfo
    {
        Off = 0,
        AlwaysOn = 1,
        ShowOnPress = 2,
        HudHints = 3,
    }

    [Flags]
    public enum OverlayLabelsFlags
    {
        None = 0,
        All = int.MaxValue,
        Axis = (1 << 0),
        Other = (1 << 1),
    }

    public enum ToolbarLabelsMode
    {
        Off = 0,
        AlwaysOn = 1,
        ShowOnPress = 2,
        HudHints = 3,
    }

    public enum ToolbarNameMode
    {
        Off = 0,
        AlwaysShow = 1,
        GroupsOnly = 2,
        InMenuOnly = 3,
    }

    public enum ToolbarStyle
    {
        SingleList = 0,
        TwoColumns = 1,
    }

    public enum ActionIconsMode
    {
        Original = 0,
        Custom = 1,
        Hidden = 2,
    }

    public enum MassFormat
    {
        Vanilla = 0,
        CustomKg = 1,
        CustomMetric = 2,
        CustomSI = 3,
    }

    public enum ForceControllerMode
    {
        Off = 0,
        KeyboardMouseOnly,
        ControllerOnly,
    }
}
