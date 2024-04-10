using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using DoorStatus = Sandbox.ModAPI.Ingame.DoorStatus;
using PistonStatus = Sandbox.ModAPI.Ingame.PistonStatus;

namespace Digi.BuildInfo.Features.Terminal
{
    // TODO: blocks with upgrade modules attached? to quickly check if they all have all slots used or something

    public class MultiDetailInfo : ModComponent
    {
        const string TextFont = FontsHandler.BI_SEOutlined;

        public readonly StringBuilder InfoText = new StringBuilder(512);

        bool RefreshNext;
        TextAPI.TextPackage Label;
        TextAPI.TextPackage Hint;
        HudAPIv2.BillBoardHUDMessage MoveIcon;

        BoxDragging Drag;

        delegate void MultiInfoDelegate(StringBuilder info, List<IMyTerminalBlock> blocks, bool allSameType, bool allSameId);
        readonly Dictionary<MyObjectBuilderType, MultiInfoDelegate> MultiInfoPerType = new Dictionary<MyObjectBuilderType, MultiInfoDelegate>(MyObjectBuilderType.Comparer);

        readonly Dictionary<MyObjectBuilderType, HashSet<MyObjectBuilderType>> SameType = new Dictionary<MyObjectBuilderType, HashSet<MyObjectBuilderType>>(MyObjectBuilderType.Comparer);
        readonly HashSet<MyObjectBuilderType> TempTypeSet = new HashSet<MyObjectBuilderType>();

        readonly Vector3D[] TempCorners = new Vector3D[8];

        public MultiDetailInfo(BuildInfoMod main) : base(main)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);
        }

        public override void RegisterComponent()
        {
            Main.Config.Handler.SettingsLoaded += RefreshPositions;
            Main.TerminalInfo.SelectedChanged += TerminalSelectedChanged;

            Drag = new BoxDragging(MyMouseButtonsEnum.Right);
            Drag.BoxSelected += () => MoveIcon.BillBoardColor = Color.Lime;
            Drag.BoxDeselected += () => MoveIcon.BillBoardColor = Color.White;
            Drag.Dragging += (newPos) =>
            {
                Main.Config.TerminalMultiDetailedInfoPosition.SetValue(newPos);

                // TODO: scale setting?
                //int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                //if(scroll != 0)
                //{
                //    ConfigLib.FloatSetting setting = Main.Config.TerminalMultiDetailedInfoScale???;
                //    float scale = setting.Value + (scroll > 0 ? 0.05f : -0.05f);
                //    scale = MathHelper.Clamp(scale, setting.Min, setting.Max);
                //    setting.SetValue((float)Math.Round(scale, 3));
                //}

                RefreshPositions();
            };
            Drag.FinishedDragging += (finalPos) =>
            {
                Main.Config.Save();
                Main.ConfigMenuHandler.RefreshAll();
            };

            SetupSimilarTypeFormatters();
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.Config.Handler.SettingsLoaded -= RefreshPositions;
            Main.TerminalInfo.SelectedChanged -= TerminalSelectedChanged;

            Drag = null;
        }

        void RefreshPositions()
        {
            if(Label == null)
                return;

            Vector2D pos = Main.Config.TerminalMultiDetailedInfoPosition.Value;
            const float scale = 1f;
            float textScale = scale * 1.3f;

            float iconHeight = 0.4f;
            float iconWidth = (float)(0.0025 / Main.GameConfig.AspectRatio);

            Label.Text.Scale = textScale;
            Label.Text.Origin = pos;

            if(Label.Shadow != null)
            {
                Label.Shadow.Scale = textScale;
                Label.Shadow.Origin = pos;
                Label.Shadow.Offset = Label.Text.Offset + new Vector2D(0.002, -0.002) * textScale;
            }

            MoveIcon.Scale = scale;
            MoveIcon.Origin = pos;
            MoveIcon.Width = iconWidth;
            MoveIcon.Height = iconHeight;
            MoveIcon.Offset = new Vector2D(-0.0363f, iconHeight / -2);
        }

        void TerminalSelectedChanged()
        {
            RefreshNext = true;
        }

        public override void UpdateDraw()
        {
            int selectedNum = Main.TerminalInfo.SelectedInTerminal.Count;
            if(selectedNum <= 1
            || !Main.TextAPI.IsEnabled
            || !MyAPIGateway.Gui.IsCursorVisible
            || MyAPIGateway.Gui.GetCurrentScreen != MyTerminalPageEnum.ControlPanel
            || Main.GUIMonitor.InAnyToolbarGUI
            || MyAPIGateway.Gui.ActiveGamePlayScreen != "MyGuiScreenTerminal")
                return;

            if(Label == null)
            {
                Label = new TextAPI.TextPackage(InfoText);
                Label.Font = TextFont;

                MoveIcon = TextAPI.CreateHUDTexture(MyStringId.GetOrCompute("BuildInfo_UI_Square"), Color.White, Vector2D.Zero, hideWithHud: false);

                Hint = new TextAPI.TextPackage(new StringBuilder("UI bg opacity too high to see multi-select info provided by buildinfo."));
                Hint.Text.Origin = new Vector2D(0, -0.98);
                Hint.Text.Scale = 0.6f;
                Hint.Text.Options &= ~HudAPIv2.Options.HideHud;
                Vector2D hintTextSize = Hint.Text.GetTextLength();
                Hint.Text.Offset = new Vector2D(hintTextSize.X / -2, 0);

                RefreshPositions();
            }

            if(Main.GameConfig.UIBackgroundOpacity >= 0.9f && Main.Config.TerminalMultiDetailedInfoPosition.Value == Main.Config.TerminalMultiDetailedInfoPosition.DefaultValue)
            {
                Hint.Text.Draw();
            }

            int skipEveryTicks;
            if(selectedNum > 1000)
                skipEveryTicks = 120;
            else if(selectedNum > 300)
                skipEveryTicks = 60;
            else if(selectedNum > 100)
                skipEveryTicks = 30;
            else
                skipEveryTicks = 15;

            if(RefreshNext || Main.Tick % skipEveryTicks == 0)
            {
                RefreshNext = false;
                UpdateText();
            }

            Vector2D center = MoveIcon.Origin + MoveIcon.Offset;
            Vector2D halfSize = new Vector2D(0.02, MoveIcon.Height) / 2;
            Drag.DragHitbox = new BoundingBox2D(center - halfSize, center + halfSize);
            Drag.Position = Label.Text.Origin;
            Drag.Update();

            Label.Shadow?.Draw();
            Label.Text.Draw();
            MoveIcon.Draw();
        }

        readonly Dictionary<MyStringHash, ResInfo> ResInput = new Dictionary<MyStringHash, ResInfo>(MyStringHash.Comparer);
        readonly Dictionary<MyStringHash, ResInfo> ResOutput = new Dictionary<MyStringHash, ResInfo>(MyStringHash.Comparer);
        readonly Dictionary<MyStringHash, ResInfo> ResStorage = new Dictionary<MyStringHash, ResInfo>(MyStringHash.Comparer);
        readonly Dictionary<long, int> OtherOwners = new Dictionary<long, int>();

        struct ResInfo
        {
            public float Current;
            public float Max;
            public int Blocks;
        }

        static void IncrementResInfo<TKey>(Dictionary<TKey, ResInfo> dict, TKey key, float addCurrent, float addMax)
        {
            ResInfo resInfo = dict.GetValueOrDefault(key);

            resInfo.Current += addCurrent;
            resInfo.Max += addMax;
            resInfo.Blocks++;

            dict[key] = resInfo;
        }

        void UpdateText()
        {
            if(MyAPIGateway.Session?.Player == null)
                return; // HACK: player can be null for first few frames, just ignore those...

            List<IMyTerminalBlock> selected = Main.TerminalInfo.SelectedInTerminal;
            int totalBlocks = selected.Count;
            if(totalBlocks <= 0)
                return;

            // NOTE: this isn't perfect because of FOV spring is ignored or something, but good enough for this purpose
            MatrixD viewProjectionMatrix = MyAPIGateway.Session.Camera.ViewMatrix * MyAPIGateway.Session.Camera.ProjectionMatrix;
            BoundingFrustumD cameraFrustum = new BoundingFrustumD(viewProjectionMatrix);

            ResInput.Clear();
            ResOutput.Clear();
            ResStorage.Clear();
            OtherOwners.Clear();

            float inventoryCurrentM3 = 0f;
            float inventoryMaxM3 = 0f;
            float inventoryMass = 0f;
            int inventoryBlocks = 0;

            int unfinished = 0;
            int damaged = 0;
            int broken = 0;

            long localIdentityId = MyAPIGateway.Session.Player.IdentityId;
            int unowned = 0;
            int ownedByMe = 0;
            int sharePrivate = 0;
            int shareFaction = 0;
            int shareAll = 0;

            int outsideView = 0;

            IMyTerminalBlock firstBlock = selected[0];
            bool allSameId = true;
            bool allSameType = true;
            bool allSimilarType = true;

            HashSet<MyObjectBuilderType> firstSimilarTo = SameType.GetValueOrDefault(firstBlock.BlockDefinition.TypeId, null);
            if(firstSimilarTo == null)
            {
                TempTypeSet.Clear();
                TempTypeSet.Add(firstBlock.BlockDefinition.TypeId);
                firstSimilarTo = TempTypeSet;
            }

            #region Block compute loop
            for(int blockIdx = 0; blockIdx < selected.Count; blockIdx++)
            {
                IMyTerminalBlock block = selected[blockIdx];

                #region Compute outside view
                {
                    BoundingBox modelBB = block.LocalAABB;
                    modelBB.Inflate(0.05f);
                    MyOrientedBoundingBoxD obb = new MyOrientedBoundingBoxD(modelBB, block.WorldMatrix);

                    bool anyCornerVisible = false;

                    obb.GetCorners(TempCorners, 0);
                    for(int i = 0; i < TempCorners.Length; i++)
                    {
                        Vector3D corner = TempCorners[i];

                        ContainmentType result;
                        cameraFrustum.Contains(ref corner, out result);
                        if(result == ContainmentType.Contains)
                        {
                            anyCornerVisible = true;
                            break;
                        }
                    }

                    if(!anyCornerVisible)
                        outsideView++;
                }
                #endregion

                #region Compute if all selected are similar or same
                if(blockIdx > 0)
                {
                    if(allSameType && block.BlockDefinition.TypeId != firstBlock.BlockDefinition.TypeId)
                        allSameType = false;

                    if(allSimilarType && !firstSimilarTo.Contains(block.BlockDefinition.TypeId))
                        allSimilarType = false;

                    if(allSameId && !block.SlimBlock.BlockDefinition.Id.Equals(firstBlock.SlimBlock.BlockDefinition.Id))
                        allSameId = false;
                }
                #endregion

                #region Compute unfinished/damaged/broken
                if(!block.IsFunctional)
                {
                    if(block.SlimBlock.ComponentStack.IsBuilt)
                        broken++;
                    else
                        unfinished++;
                }
                else
                {
                    if(!block.SlimBlock.IsFullIntegrity)
                        damaged++;
                }
                #endregion

                #region Compute ownership
                MyCubeBlock internalBlock = (MyCubeBlock)block;
                if(internalBlock.IDModule != null)
                {
                    long ownerId = block.OwnerId;
                    if(ownerId == 0)
                        unowned++;
                    else if(ownerId == localIdentityId)
                        ownedByMe++;
                    else
                        OtherOwners[ownerId] = OtherOwners.GetValueOrDefault(ownerId, 0) + 1;

                    switch(internalBlock.IDModule.ShareMode)
                    {
                        case MyOwnershipShareModeEnum.None: sharePrivate++; break;
                        case MyOwnershipShareModeEnum.Faction: shareFaction++; break;
                        case MyOwnershipShareModeEnum.All: shareAll++; break;
                    }
                }
                #endregion

                #region Compute resource input/output
                IMyThrust thrust = block as IMyThrust; // HACK: thrusters have shared sinks and not reliable to get
                IMyGyro gyro = (thrust == null ? block as IMyGyro : null); // HACK: gyro has no sink 

                if(thrust != null)
                {
                    Hardcoded.ThrustInfo thrustInfo = Hardcoded.Thrust_GetUsage(thrust);
                    MyStringHash key = thrustInfo.Fuel.SubtypeId;

                    IncrementResInfo(ResInput, key, thrustInfo.CurrentUsage, thrustInfo.MaxUsage);
                }
                else if(gyro != null)
                {
                    if(gyro.IsFunctional && gyro.Enabled)
                    {
                        MyStringHash key = MyResourceDistributorComponent.ElectricityId.SubtypeId;
                        MyGyro internalGyro = (MyGyro)gyro;
                        MyGyroDefinition gyroDef = (MyGyroDefinition)gyro.SlimBlock.BlockDefinition;

                        IncrementResInfo(ResInput, key, internalGyro.RequiredPowerInput, gyroDef.RequiredPowerInput * gyro.PowerConsumptionMultiplier);
                    }
                }
                else
                {
                    MyResourceSinkComponent sink = block.Components.Get<MyResourceSinkComponent>();
                    if(sink != null)
                    {
                        IMyJumpDrive jd = block as IMyJumpDrive;

                        foreach(MyDefinitionId resId in sink.AcceptedResources)
                        {
                            MyStringHash key = resId.SubtypeId;
                            IncrementResInfo(ResInput, key, sink.CurrentInputByType(resId), sink.MaxRequiredInputByType(resId));

                            if(jd != null && key == MyResourceDistributorComponent.ElectricityId.SubtypeId)
                            {
                                IncrementResInfo(ResStorage, key, jd.CurrentStoredPower, jd.MaxStoredPower);
                            }
                        }
                    }

                    MyResourceSourceComponent source = block.Components.Get<MyResourceSourceComponent>();
                    if(source != null)
                    {
                        bool isHydrogenEngine = (block.BlockDefinition.TypeId == typeof(MyObjectBuilder_HydrogenEngine));
                        IMyBatteryBlock battery = (!isHydrogenEngine ? block as IMyBatteryBlock : null);
                        IMyGasTank tank = (battery == null ? block as IMyGasTank : null);

                        foreach(MyDefinitionId resId in source.ResourceTypes)
                        {
                            MyStringHash key = resId.SubtypeId;

                            IncrementResInfo(ResOutput, key, source.CurrentOutputByType(resId), source.MaxOutputByType(resId));

                            if(key == MyResourceDistributorComponent.ElectricityId.SubtypeId)
                            {
                                if(battery != null)
                                {
                                    IncrementResInfo(ResStorage, key, battery.CurrentStoredPower, battery.MaxStoredPower);
                                }
                            }
                            else
                            {
                                if(tank != null)
                                {
                                    IncrementResInfo(ResStorage, key, (float)(tank.FilledRatio * tank.Capacity), tank.Capacity);
                                }
                            }
                        }

                        if(isHydrogenEngine)
                        {
                            MyHydrogenEngineDefinition engineDef = (MyHydrogenEngineDefinition)block.SlimBlock.BlockDefinition;
                            MyStringHash key = engineDef.Fuel.FuelId.SubtypeId;
                            float filled = source.RemainingCapacityByType(MyResourceDistributorComponent.ElectricityId);

                            IncrementResInfo(ResStorage, key, filled, engineDef.FuelCapacity);
                        }
                    }
                }
                #endregion

                #region Compute inventory
                if(block.HasInventory)
                {
                    for(int invIdx = 0; invIdx < block.InventoryCount; invIdx++)
                    {
                        IMyInventory inv = block.GetInventory(invIdx);

                        inventoryCurrentM3 += (float)inv.CurrentVolume;
                        inventoryMaxM3 += (float)inv.MaxVolume;
                        inventoryMass += (float)inv.CurrentMass;
                    }

                    inventoryBlocks++;
                }
                #endregion
            }
            #endregion Block compute loop

            // NOTE: the same SB is used by both the text and the shadow, therefore it can't use colors.
            // NOTE: this SB is also used on the copy detail info feature, if adding colors, must strip them there.
            StringBuilder info = InfoText.Clear();

            info.Append("--- ").Append(totalBlocks).Append("x ");

            if(allSameId)
            {
                info.AppendMaxLength(firstBlock.DefinitionDisplayNameText, 40);
            }
            else if(allSameType)
            {
                info.IdTypeFormat(firstBlock.BlockDefinition.TypeId).Append(" blocks");
            }
            else if(allSimilarType)
            {
                info.IdTypeFormat(firstSimilarTo.FirstElement()).Append(" blocks");
            }
            else
            {
                info.Append("mixed blocks");
            }

            info.Append(" ---\n");

            foreach(KeyValuePair<MyStringHash, ResInfo> kv in ResInput)
            {
                MyStringHash resource = kv.Key;
                ResInfo resInfo = kv.Value;
                AppendInputFormat(info, resource, resInfo);

                // get output for this resource too
                if(ResOutput.TryGetValue(resource, out resInfo))
                {
                    ResOutput.Remove(resource); // and consume it
                    AppendOutputFormat(info, resource, resInfo);
                }

                // and storage too!
                if(ResStorage.TryGetValue(resource, out resInfo))
                {
                    ResStorage.Remove(resource); // and consume it
                    AppendStorageFormat(info, resource, resInfo);
                }
            }

            // print leftover outputs that have no inputs
            foreach(KeyValuePair<MyStringHash, ResInfo> kv in ResOutput)
            {
                MyStringHash resource = kv.Key;
                ResInfo resInfo = kv.Value;
                AppendOutputFormat(info, resource, resInfo);

                // print storage for this resource
                if(ResStorage.TryGetValue(resource, out resInfo))
                {
                    ResStorage.Remove(resource); // and consume it
                    AppendStorageFormat(info, resource, resInfo);
                }
            }

            // print leftover storage
            foreach(KeyValuePair<MyStringHash, ResInfo> kv in ResStorage)
            {
                MyStringHash resource = kv.Key;
                ResInfo resInfo = kv.Value;
                AppendStorageFormat(info, resource, resInfo);
            }

            if(inventoryBlocks > 0)
            {
                info.Append(inventoryBlocks).Append("x Inventories: ").VolumeFormat(inventoryCurrentM3 * 1000).Append(" / ").VolumeFormat(inventoryMaxM3 * 1000).Append(" (").MassFormat(inventoryMass).Append(")\n");
            }

            // unfinished/damaged/broken
            {
                int lenBefore = info.Length;

                if(unfinished > 0)
                    info.Append(unfinished).Append(" unfinished, ");

                if(damaged > 0)
                    info.Append(damaged).Append(" damaged, ");

                if(broken > 0)
                    info.Append(broken).Append(" broken, ");

                // remove last comma+space and finish the line
                if(info.Length > lenBefore)
                {
                    info.Length -= 2;
                    info.Append('\n');
                }
            }

            // owners summary
            {
                int lenBefore = info.Length;

                if(unowned > 0)
                    info.Append(unowned).Append(" not owned, ");

                //if(ownedByMe > 0)
                //    info.Append(ownedByMe).Append(" mine, ");

                if(OtherOwners.Count > 0)
                {
                    int friendOwned = 0;
                    int enemyOwned = 0;

                    foreach(KeyValuePair<long, int> kv in OtherOwners)
                    {
                        if(MyAPIGateway.Session.Player.GetRelationTo(kv.Key).IsFriendly())
                        {
                            friendOwned += kv.Value;
                        }
                        else
                        {
                            enemyOwned += kv.Value;
                        }
                    }

                    if(enemyOwned > 0)
                        info.Append(enemyOwned).Append(" enemy-owned, ");

                    if(friendOwned > 0)
                        info.Append(friendOwned).Append(" friend-owned, ");
                }

                if(shareAll > 0)
                    info.Append(shareAll).Append(" shared all, ");

                if(sharePrivate > 0)
                    info.Append(sharePrivate).Append(" not shared, ");

                // remove last comma+space and finish the line
                if(info.Length > lenBefore)
                {
                    info.Length -= 2;
                    info.Append('\n');
                }
            }

            // outside of view
            if(outsideView > 0)
            {
                info.Append(outsideView).Append(" outside of view.\n");
            }

            if(allSimilarType)
            {
                info.Append('\n');

                MultiInfoPerType.GetValueOrDefault(firstBlock.BlockDefinition.TypeId, null)?.Invoke(info, selected, allSameType, allSameId);
                info.Append('\n');
            }
        }

        static void AppendInputFormat(StringBuilder info, MyStringHash resource, ResInfo resInfo)
        {
            if(resource == MyResourceDistributorComponent.ElectricityId.SubtypeId)
                info.Append(resInfo.Blocks).Append("x Power Input: ").PowerFormat(resInfo.Current);
            else
                info.Append(resInfo.Blocks).Append("x ").Append(resource.String).Append(" Input: ").VolumeFormat(resInfo.Current).Append("/s");
            info.Append('\n');
        }

        static void AppendOutputFormat(StringBuilder info, MyStringHash resource, ResInfo resInfo)
        {
            if(resource == MyResourceDistributorComponent.ElectricityId.SubtypeId)
                info.Append(resInfo.Blocks).Append("x Power Output: ").PowerFormat(resInfo.Current);
            else
                info.Append(resInfo.Blocks).Append("x ").Append(resource.String).Append(" Output: ").VolumeFormat(resInfo.Current).Append("/s");
            info.Append('\n');
        }

        static void AppendStorageFormat(StringBuilder info, MyStringHash resource, ResInfo resInfo)
        {
            if(resource == MyResourceDistributorComponent.ElectricityId.SubtypeId)
                info.Append(resInfo.Blocks).Append("x Power Storage: ").PowerStorageFormat(resInfo.Current).Append(" (max: ").PowerStorageFormat(resInfo.Max).Append(")");
            else
                info.Append(resInfo.Blocks).Append("x ").Append(resource.String).Append(" Storage: ").VolumeFormat(resInfo.Current).Append(" (max: ").VolumeFormat(resInfo.Max).Append(")");
            info.Append('\n');
        }

        #region Formatters for similar types
        void SetupSimilarTypeFormatters()
        {
            // first given type is also used for naming the kind of blocks selected

            // rotors & hinges, no suspensions
            AddFormatterAndPairTypes(Info_Rotors, typeof(MyObjectBuilder_MotorStator), typeof(MyObjectBuilder_MotorAdvancedStator));

            AddFormatterAndPairTypes(Info_Pistons, typeof(MyObjectBuilder_PistonBase), typeof(MyObjectBuilder_ExtendedPistonBase));

            AddFormatterAndPairTypes(Info_Doors, typeof(MyObjectBuilder_Door), typeof(MyObjectBuilder_AirtightSlideDoor), typeof(MyObjectBuilder_AirtightHangarDoor), typeof(MyObjectBuilder_AirtightDoorGeneric), typeof(MyObjectBuilder_AdvancedDoor));

            AddFormatterAndPairTypes(Info_PBs, typeof(MyObjectBuilder_MyProgrammableBlock));

            AddFormatterAndPairTypes(Info_Thrusters, typeof(MyObjectBuilder_Thrust));
        }

        /// <summary>
        /// Registers call to format specified types and marks given types as similar, causing them to trigger the formatter if all selected blocks are one of these types
        /// </summary>
        void AddFormatterAndPairTypes(MultiInfoDelegate call, params MyObjectBuilderType[] types)
        {
            AddFormatter(call, types);

            HashSet<MyObjectBuilderType> set = new HashSet<MyObjectBuilderType>(types);
            foreach(MyObjectBuilderType type in set)
            {
                SameType.Add(type, set);
            }
        }

        /// <summary>
        /// Registers call to format specified types only, requiring all selected blocks to all be ONE of the given types.
        /// </summary>
        void AddFormatter(MultiInfoDelegate call, params MyObjectBuilderType[] types)
        {
            foreach(MyObjectBuilderType type in types)
            {
                MultiInfoPerType.Add(type, call);
            }
        }

        void Info_Doors(StringBuilder info, List<IMyTerminalBlock> blocks, bool allSameType, bool allSameId)
        {
            int open = 0;
            int opening = 0;
            int closed = 0;
            int closing = 0;

            foreach(IMyDoor door in blocks)
            {
                switch(door.Status)
                {
                    case DoorStatus.Open: open++; break;
                    case DoorStatus.Opening: opening++; break;
                    case DoorStatus.Closed: closed++; break;
                    case DoorStatus.Closing: closing++; break;
                    default:
                        if(BuildInfoMod.IsDevMod)
                            throw new Exception($"door {door.BlockDefinition.ToString()} has new status: {door.Status.ToString()}");
                        break;
                }
            }

            info.Append("Status: ");

            int states = 0;
            if(open > 0) states++;
            if(opening > 0) states++;
            if(closed > 0) states++;
            if(closing > 0) states++;

            if(states > 2) // too many different states, combine some
            {
                info.Append(open + opening).Append(" open, ").Append(closed + closing).Append(" closed");
            }
            else
            {
                if(opening > 0) info.Append(opening).Append(" opening, ");
                if(open > 0) info.Append(open).Append(" open, ");
                if(closing > 0) info.Append(closing).Append(" closing, ");
                if(closed > 0) info.Append(closed).Append(" closed, ");

                info.Length -= 2; // remove last comma
            }

            info.Append('\n');
        }

        void Info_Rotors(StringBuilder info, List<IMyTerminalBlock> blocks, bool allSameType, bool allSameId)
        {
            float min = float.MaxValue;
            float max = float.MinValue;

            foreach(IMyMotorStator rotor in blocks)
            {
                float angle = rotor.Angle;
                min = Math.Min(angle, min);
                max = Math.Max(angle, max);
            }

            info.Append("Angle: ");
            if(Math.Abs(min - max) < (Math.PI / 180f)) // all rotors are roughly same angle
                info.AngleFormat(min);
            else
                info.AngleFormat(min).Append(" to ").AngleFormat(max);
            info.Append('\n');
        }

        void Info_Pistons(StringBuilder info, List<IMyTerminalBlock> blocks, bool allSameType, bool allSameId)
        {
            int extended = 0;
            int extending = 0;
            int retracted = 0;
            int retracting = 0;
            int stopped = 0;

            float min = float.MaxValue;
            float max = float.MinValue;

            foreach(IMyPistonBase piston in blocks)
            {
                float pos = piston.CurrentPosition;
                min = Math.Min(pos, min);
                max = Math.Max(pos, max);

                switch(piston.Status)
                {
                    case PistonStatus.Extended: extended++; break;
                    case PistonStatus.Extending: extending++; break;
                    case PistonStatus.Retracted: retracted++; break;
                    case PistonStatus.Retracting: retracting++; break;
                    case PistonStatus.Stopped: stopped++; break;
                    default:
                        if(BuildInfoMod.IsDevMod)
                            throw new Exception($"piston {piston.BlockDefinition.ToString()} has new status: {piston.Status.ToString()}");
                        break;
                }
            }

            info.Append("Position: ");
            if(Math.Abs(min - max) < 0.01f) // all pistons are roughly at the same position
                info.DistanceFormat(min);
            else
                info.DistanceRangeFormat(min, max);
            info.Append('\n');

            int states = 0;
            if(extended > 0) states++;
            if(extending > 0) states++;
            if(retracted > 0) states++;
            if(retracting > 0) states++;
            if(stopped > 0) states++;

            if(states > 2) // too many different states, combine some
            {
                info.Append(extended + extending).Append(" extended, ").Append(retracted + retracting).Append(" retracted, ");
            }
            else
            {
                if(extending > 0) info.Append(extending).Append(" extending, ");
                if(extended > 0) info.Append(extended).Append(" extended, ");
                if(retracting > 0) info.Append(retracting).Append(" retracting, ");
                if(retracted > 0) info.Append(retracted).Append(" retracted, ");
            }

            if(stopped > 0) info.Append(stopped).Append(" stopped, ");

            info.Length -= 2; // remove last comma
            info.Append('\n');
        }

        void Info_PBs(StringBuilder info, List<IMyTerminalBlock> blocks, bool allSameType, bool allSameId)
        {
            int haveScript = 0;

            foreach(IMyProgrammableBlock pb in blocks)
            {
                string script = pb.ProgramData;
                if(!string.IsNullOrWhiteSpace(script) && script != Hardcoded.ProgrammableBlock_DefaultScript)
                {
                    haveScript++;
                }
            }

            info.Append("Have script: ").Append(haveScript).Append('\n');
        }

        readonly Vector3[] ThrustPerSide = new Vector3[6];

        void Info_Thrusters(StringBuilder info, List<IMyTerminalBlock> blocks, bool allSameType, bool allSameId)
        {
            int connected = 0;
            float currentForce = 0;
            float effectiveMax = 0;
            float absoluteMax = 0;
            string fuelType = null;
            bool mixedFuels = false;
            bool canBeSubOptimal = false;
            bool allSameGrid = true;
            IMyCubeGrid testGrid = blocks[0].CubeGrid;

            for(int i = 0; i < ThrustPerSide.Length; i++)
            {
                ThrustPerSide[i] = Vector3.Zero;
            }

            foreach(MyThrust thrust in blocks)
            {
                if(thrust.CubeGrid != testGrid)
                    allSameGrid = false;

                if(thrust.IsPowered)
                    connected++;

                IMyThrust apiThrust = (IMyThrust)thrust;
                MyThrustDefinition def = thrust.BlockDefinition;
                float effMulMax = Math.Max(def.EffectivenessAtMinInfluence, def.EffectivenessAtMaxInfluence);
                float currentThrust = apiThrust.CurrentThrust;
                float maxEffectiveThrust = apiThrust.MaxEffectiveThrust;
                float maxThrust = apiThrust.MaxThrust * effMulMax;

                currentForce += currentThrust;
                effectiveMax += maxEffectiveThrust;
                absoluteMax += maxThrust;

                if(!canBeSubOptimal && def.EffectivenessAtMinInfluence != 1.0f || def.EffectivenessAtMaxInfluence != 1.0f)
                    canBeSubOptimal = true;

                if(!mixedFuels)
                {
                    string fuel = thrust.FuelDefinition?.Id.SubtypeName ?? MyResourceDistributorComponent.ElectricityId.SubtypeName;

                    if(fuelType == null)
                        fuelType = fuel;
                    else if(fuelType != fuel)
                        mixedFuels = true;
                }

                ThrustPerSide[(int)thrust.Orientation.Forward] += new Vector3(currentThrust, maxEffectiveThrust, maxThrust);
            }

            info.Append("Requires: ").Append(mixedFuels ? "Mixed" : fuelType).Append('\n');

            info.Append("Connected to fuel: ");
            if(connected >= blocks.Count)
                info.Append("All");
            else
                info.Append(connected).Append(" / ").Append(blocks.Count);
            info.Append('\n');

            info.Append("Current Thrust: ").ForceFormat(currentForce).Append('\n');

            if(canBeSubOptimal)
            {
                info.Append("Current Max Thrust: ").ForceFormat(effectiveMax).Append('\n');
                info.Append("Optimal Max Thrust: ").ForceFormat(absoluteMax).Append('\n');
            }
            else
            {
                info.Append("Max Thrust: ").ForceFormat(absoluteMax).Append('\n');
            }

            if(allSameGrid)
            {
                int activeSides = 0;

                for(int i = 0; i < ThrustPerSide.Length; i++)
                {
                    Vector3 data = ThrustPerSide[i];
                    if(data.Z > 0)
                        activeSides++;
                }

                if(activeSides > 1)
                {
                    var shipCtrl = testGrid.ControlSystem?.CurrentShipController?.Entity as IMyShipController;
                    if(shipCtrl != null)
                    {
                        info.Append("Relative to '").AppendMaxLength(shipCtrl.CustomName, 32).Append("':\n");

                        AppendThrustPerSide(info, Base6Directions.Direction.Backward, canBeSubOptimal, shipCtrl);
                        AppendThrustPerSide(info, Base6Directions.Direction.Forward, canBeSubOptimal, shipCtrl);
                        AppendThrustPerSide(info, Base6Directions.Direction.Down, canBeSubOptimal, shipCtrl);
                        AppendThrustPerSide(info, Base6Directions.Direction.Up, canBeSubOptimal, shipCtrl);
                        AppendThrustPerSide(info, Base6Directions.Direction.Left, canBeSubOptimal, shipCtrl);
                        AppendThrustPerSide(info, Base6Directions.Direction.Right, canBeSubOptimal, shipCtrl);
                    }
                    else
                    {
                        info.Append("(Control ship to see per-direction forces)\n");
                    }
                }
            }
        }

        void AppendThrustPerSide(StringBuilder info, Base6Directions.Direction dir, bool canBeSubOptimal, IMyShipController shipCtrl)
        {
            Base6Directions.Direction dataDir = dir;
            if(shipCtrl != null)
                dataDir = shipCtrl.Orientation.TransformDirection(dir); // aka "get direction"

            Vector3 data = ThrustPerSide[(int)dataDir];
            if(data.Z <= 0)
                return;

            string dirName;
            switch(dir) // shorter names with eyeballed padding
            {
                case Base6Directions.Direction.Forward: dirName = "Front  "; break;
                case Base6Directions.Direction.Backward: dirName = "Back  "; break;
                case Base6Directions.Direction.Left: dirName = "Left     "; break;
                case Base6Directions.Direction.Right: dirName = "Right  "; break;
                case Base6Directions.Direction.Up: dirName = "Up      "; break;
                case Base6Directions.Direction.Down: dirName = "Down"; break;
                default: throw new ArgumentException($"Invalid direction given: {dir}");
            }

            info.Append("•").Append(dirName).Append(": ");

            if(canBeSubOptimal)
                info.ForceFormat(data.Y).Append(" effective, ").ForceFormat(data.Z).Append(" max");
            else
                info.ForceFormat(data.Z).Append(" max");

            info.Append('\n');
        }
        #endregion
    }
}