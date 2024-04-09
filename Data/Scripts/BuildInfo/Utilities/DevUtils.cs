using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Input;

namespace Digi
{
    public static class Dev
    {
        static Dictionary<string, Stored> Values = new Dictionary<string, Stored>();

        static readonly char[] Separator = new char[] { 'e' };

        /// <summary>
        /// This needs to be called every tick to be effective.
        /// Hold <paramref name="modifier"/> key in game to adjust number.
        /// Hold Shift+<paramref name="modifier"/> to change exponent.
        /// <para>There's also <see cref="GetValueScroll(string)"/> to just get it in various places without risking duplicated inputs.</para>
        /// </summary>
        /// <param name="id"></param>
        /// <param name="initial"></param>
        /// <param name="modifier"></param>
        /// <param name="notifyTime"></param>
        /// <returns></returns>
        public static double GetValueScroll(string id, double initial, MyKeys modifier = MyKeys.None, int notifyTime = 16)
        {
            Stored stored;
            if(!Values.TryGetValue(id, out stored))
            {
                stored = new Stored();
                stored.Initial = initial;
                stored.Value = initial;
                stored.Step = GetStep(initial);
                Values.Add(id, stored);
            }

            if(modifier != MyKeys.None && !MyAPIGateway.Input.IsKeyPress(modifier))
                return stored.Value;

            if(MyAPIGateway.Input.IsNewKeyPressed(MyKeys.R))
            {
                stored.Value = stored.Initial;
                stored.Step = GetStep(stored.Value);
                return stored.Value;
            }

            int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
            if(scroll != 0)
            {
                if(MyAPIGateway.Input.IsAnyShiftKeyPressed()) // adjust exponent
                {
                    if(scroll > 0)
                        stored.Value *= 10d;
                    else
                        stored.Value /= 10d;

                    stored.Step = GetStep(stored.Value);
                }
                else // adjust number
                {
                    if(scroll > 0)
                        stored.Value += stored.Step;
                    else
                        stored.Value -= stored.Step;
                }
            }

            if(notifyTime > 0)
            {
                const string format = "###,###,###,###,###,###,##0.##########";
                MyAPIGateway.Utilities.ShowNotification($"[{id}] = [{stored.Value.ToString(format)}] ({stored.Value:0e0}; step={stored.Step.ToString(format)})", notifyTime);
            }

            return stored.Value;
        }

        public static double GetValueScroll(string id)
        {
            Stored stored;
            if(Values.TryGetValue(id, out stored))
                return stored.Value;

            return 0;
        }

        static double GetStep(double value)
        {
            if(value == 0)
                return 0.1;

            int exponnent = 0;
            string valueStr = Math.Abs(value).ToString("0e0");
            if(valueStr.Length > 0 && char.IsDigit(valueStr[0]))
            {
                string[] split = valueStr.Split(Separator);
                if(split.Length == 2)
                {
                    string exponentStr = split[1];
                    int.TryParse(exponentStr, out exponnent);
                }
            }

            // step would be one exponent lower
            //exponnent -= 1;

            if(exponnent == 0)
                return 1;

            return Math.Pow(10, exponnent);
        }

        class Stored
        {
            public double Initial;
            public double Value;
            public double Step;
        }

        [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
        class Unloader : MySessionComponentBase
        {
            protected override void UnloadData()
            {
                if(Values == null)
                    return;

                foreach(var kv in Values)
                {
                    Log.Info($"[DEV] GetValueScroll() exported: {kv.Key} = {kv.Value.Value} (initial: {kv.Value.Initial})");
                }

                Values = null;
            }
        }
    }
}