using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Input;
using VRageMath;

namespace Digi
{
    public static class Dev
    {
        static Dictionary<string, Stored> Values = new Dictionary<string, Stored>();
        static StringBuilder TempSB = new StringBuilder(256);

        // max lengths for MyFixedPoint
        const int MaxDecimals = 6;
        const int MaxIntegers = 13;
        const string ParsableFormat = "0000000000000.000000";
        const string DisplayFormat = "###,###,###,###,###,###,###,##0.000000";

        /// <summary>
        /// This needs to be called every tick to be effective.
        /// <para>Hold <paramref name="modifier"/> key in game to adjust number (NOTE: must not be any shift)</para>
        /// <para>Hold Shift+<paramref name="modifier"/> to change exponent.</para>
        /// <para>Use <see cref="GetValue(string)"/> to only read it.</para>
        /// </summary>
        public static double GetValueScroll(string id, double initial, MyKeys modifier = MyKeys.None, int notifyTime = 16)
        {
            Stored stored;
            if(!Values.TryGetValue(id, out stored))
            {
                stored = new Stored();
                stored.Initial = (MyFixedPoint)initial;
                stored.Value = stored.Initial;
                stored.Digit = -2;
                Values.Add(id, stored);
            }

            double returnValue = (double)stored.Value;

            if(modifier == MyKeys.Shift || modifier == MyKeys.LeftShift || modifier == MyKeys.RightShift)
            {
                throw new Exception("Can't use any shift for modifiers!");
            }

            if(modifier != MyKeys.None && !MyAPIGateway.Input.IsKeyPress(modifier))
            {
                if(MyAPIGateway.Input.IsAnyAltKeyPressed())
                {
                    string value = returnValue.ToString(DisplayFormat);
                    MyAPIGateway.Utilities.ShowNotification($"[{modifier}+scroll] - {id} = {value}", notifyTime);
                }

                return returnValue;
            }

            int tick = Session.Tick; // unaffected by pause
            if(stored.InputReadAt == tick)
                return returnValue;
            stored.InputReadAt = tick;

            if(MyAPIGateway.Input.IsNewKeyPressed(MyKeys.R))
            {
                stored.Value = stored.Initial;
                stored.Digit = -2;
                returnValue = (double)stored.Value;

                return returnValue;
            }

            int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
            if(scroll != 0)
            {
                if(MyAPIGateway.Input.IsAnyShiftKeyPressed()) // adjust exponent
                {
                    if(scroll > 0)
                        stored.Digit++;
                    else
                        stored.Digit--;

                    stored.Digit = MathHelper.Clamp(stored.Digit, -MaxDecimals, MaxIntegers - 1);
                }
                else // adjust number
                {
                    if(scroll > 0)
                        stored.Value += (MyFixedPoint)Math.Pow(10, stored.Digit);
                    else
                        stored.Value -= (MyFixedPoint)Math.Pow(10, stored.Digit);
                }
            }

            if(notifyTime > 0)
            {
                int digit = stored.Digit;

                string value = Math.Abs(returnValue).ToString(ParsableFormat);

                int markIndex = (digit >= 0 ? MaxIntegers - digit : MaxIntegers + 1 + (-digit)) - 1;

                TempSB.Clear();
                TempSB.Append(id).Append(" = ");

                if(returnValue < 0)
                    TempSB.Append('-');

                bool foundDot = false;
                int thousandsSeparator = MaxIntegers;

                for(int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    char next = ((i + 1) < value.Length ? value[i + 1] : '_');

                    thousandsSeparator--;

                    if(next == '.')
                        foundDot = true;

                    // strip leading zeros up to digit
                    //if(!foundDot && c == '0' && digit < i)
                    //    continue;

                    if(i == markIndex)
                        TempSB.Append('[');

                    TempSB.Append(c);

                    if(i == markIndex)
                        TempSB.Append(']');

                    if(!foundDot && thousandsSeparator != 0 && thousandsSeparator % 3 == 0)
                        TempSB.Append(',');
                }

                TempSB.Append("  (shift=select, r=reset)");

                if(stored.Notify == null)
                    stored.Notify = MyAPIGateway.Utilities.CreateNotification(string.Empty, notifyTime, MyFontEnum.Debug);

                stored.Notify.Hide();
                stored.Notify.Text = TempSB.ToString();
                stored.Notify.AliveTime = notifyTime;
                stored.Notify.Show();
            }

            return returnValue;
        }

        public static double GetValue(string id)
        {
            Stored stored;
            if(Values.TryGetValue(id, out stored))
                return (double)stored.Value;

            return 0;
        }

        class Stored
        {
            internal MyFixedPoint Initial;
            internal MyFixedPoint Value;
            internal int Digit;
            internal int InputReadAt = -1;
            internal IMyHudNotification Notify;
        }

        [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
        class Session : MySessionComponentBase
        {
            /// <summary>
            /// Tick unrestricted by game pause
            /// </summary>
            internal static int Tick;

            protected override void UnloadData()
            {
                if(Values == null)
                    return;

                foreach(var kv in Values)
                {
                    Log.Info($"[DEV] GetValueScroll() exported: {kv.Key} = {kv.Value.Value} (initial: {kv.Value.Initial})");
                }

                Values = null;
                TempSB = null;
            }

            public override void HandleInput()
            {
                Tick++;
            }
        }
    }
}