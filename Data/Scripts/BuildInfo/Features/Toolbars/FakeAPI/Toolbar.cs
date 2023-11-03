using System;
using Digi.BuildInfo.Features.Toolbars.FakeAPI.Items;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.Toolbars.FakeAPI
{
    /// <summary>
    /// A fake API for toolbars that would be easier to replace if Keen ever adds an actual API
    /// </summary>
    public class Toolbar
    {
        public readonly MyEntity Owner;
        public readonly int SlotsPerPage;
        public readonly int PageCount;

        public MyToolbarType Type { get; private set; } = MyToolbarType.None;
        public int? SelectedSlot { get; private set; } = null;
        public int CurrentPageIndex { get; private set; } = 0;

        public readonly ToolbarItem[] Items;
        //public readonly List<ToolbarItem> ItemsGamepad;

        public event Action<int> PageChanged;

        readonly BuildInfoMod Main;

        public Toolbar(MyEntity owner, MyToolbarType type, int slotsPerPage = 9, int pages = 9)
        {
            Main = BuildInfoMod.Instance;

            Owner = owner;
            Type = type;
            SlotsPerPage = slotsPerPage;
            PageCount = pages;

            Items = new ToolbarItem[SlotsPerPage * PageCount];
            //ItemsGamepad = new List<ToolbarItem>();
        }

        public void Dispose()
        {
        }

        public void LoadFromOB(MyObjectBuilder_Toolbar ob)
        {
            if(ob == null) throw new ArgumentNullException("ob");

            Type = ob.ToolbarType;

            Clear();
            SelectedSlot = ob.SelectedSlot;

            if(ob.Slots != null)
            {
                foreach(MyObjectBuilder_Toolbar.Slot slot in ob.Slots)
                {
                    SetItemAtSerialized(slot.Index, slot.Item, slot.Data);
                }
            }

            if(ob.SlotsGamepad != null)
            {
                foreach(MyObjectBuilder_Toolbar.Slot item in ob.SlotsGamepad)
                {
                    SetItemAtSerialized(item.Index, item.Item, item.Data, gamepad: true);
                }
            }
        }

        /// <summary>
        /// Returns true if page changed
        /// </summary>
        public bool CheckPageInputs()
        {
            // NOTE: only designed with in-GUI toolbar in mind
            if(!MyAPIGateway.Gui.IsCursorVisible)
                return false;

            // most stuff like ones from MyToolbarComponent.HandleInput(), might need re-checking

            bool changes = false;

            MyStringId[] controlSlots = Main.Constants.ToolbarSlotControlIds;

            // 10 total, 1-9 and 0 last
            for(int i = 0; i < controlSlots.Length; ++i)
            {
                if(MyAPIGateway.Input.IsNewGameControlPressed(controlSlots[i]))
                {
                    if(!MyAPIGateway.Input.IsAnyCtrlKeyPressed())
                    {
                        // slot activated depending on GUI type
                    }
                    else if(i < PageCount)
                    {
                        SetToolbarPage(i);
                        changes = true;
                    }
                }
            }

            // HACK next/prev toolbar hotkeys don't work in the menu unless you click on the icons list... but I'm forcing toolbar to cycle regardless.
            // spectator condition is in game code because toolbar up/down is used for going between players.
            // also MUST be after the slot checks to match the vanilla code's behavior.
            //if(!inToolbarConfig && MySpectator.Static.SpectatorCameraMovement != MySpectatorCameraMovementEnum.ConstantDelta)
            if(!Main.GUIMonitor.InAnyDialogBox && MySpectator.Static.SpectatorCameraMovement != MySpectatorCameraMovementEnum.ConstantDelta)
            {
                if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOOLBAR_UP))
                {
                    AdjustToolbarPage(1);
                    changes = true;
                }
                // no 'else' because that's how the game handles it, meaning pressing both controls in same tick would do both actions.
                if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOOLBAR_DOWN))
                {
                    AdjustToolbarPage(-1);
                    changes = true;
                }
            }

            return changes;
        }

        void SetToolbarPage(int pageIndex)
        {
            pageIndex = MathHelper.Clamp(pageIndex, 0, PageCount - 1);

            if(CurrentPageIndex != pageIndex)
            {
                CurrentPageIndex = pageIndex;
                PageChanged?.Invoke(pageIndex);
            }
        }

        void AdjustToolbarPage(int relativeChange)
        {
            CurrentPageIndex += (relativeChange > 0 ? 1 : -1);

            // loop-around
            if(CurrentPageIndex >= PageCount)
                CurrentPageIndex = 0;
            else if(CurrentPageIndex < 0)
                CurrentPageIndex = PageCount - 1;

            PageChanged?.Invoke(CurrentPageIndex);

            // HACK: ensure the toolbar page is what the code expects, avoids toolbar page desync
            // HACK: needs to be delayed otherwise it jumps more than one page
            int copyPage = CurrentPageIndex;
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                MyVisualScriptLogicProvider.SetToolbarPageLocal(copyPage);
            });
        }

        void Clear()
        {
            //ItemsGamepad.Clear();

            for(int i = 0; i < Items.Length; i++)
            {
                Items[i] = null;
            }
        }

        void SetItemAtSerialized(int i, string serializedItem, MyObjectBuilder_ToolbarItem data, bool gamepad = false)
        {
            IToolbarItem item = CreateToolbarItem(data);
            if(!item.Validate(data))
                return;

            if(gamepad)
            {
                // TODO: gamepad
            }
            else
            {
                if(i >= Items.Length)
                    return;

                Items[i] = (ToolbarItem)item;
            }
        }

        IToolbarItem CreateToolbarItem(MyObjectBuilder_ToolbarItem data)
        {
            if(data is MyObjectBuilder_ToolbarItemTerminalBlock)
                return new ToolbarItemTerminalBlock();

            if(data is MyObjectBuilder_ToolbarItemTerminalGroup)
                return new ToolbarItemTerminalGroup();

            if(data is MyObjectBuilder_ToolbarItemDefinition)
                return new ToolbarItemWithDefinition();

            return new ToolbarItemUnknown();
        }
    }
}
