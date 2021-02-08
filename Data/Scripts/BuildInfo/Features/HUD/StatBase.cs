using System;
using Digi.BuildInfo.Utilities;
using VRage.ModAPI;
using VRage.Utils;

namespace Digi.BuildInfo.Features.HUD
{
    public abstract class StatBase : IMyHudStat
    {
        public MyStringHash Id { get; private set; }

        public const float CompareEpsilon = 0.00001f;

        private float _currentValue = -1;
        public float CurrentValue
        {
            get { return _currentValue; }
            set
            {
                try
                {
                    if(Math.Abs(_currentValue - value) < CompareEpsilon)
                        return;

                    _currentValue = value;
                    ValueStringCache = ValueAsString();
                }
                catch(Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public float MinValue { get; protected set; }

        private float _maxValue = -1;
        public float MaxValue
        {
            get { return _maxValue; }
            set
            {
                try
                {
                    if(Math.Abs(_maxValue - value) < CompareEpsilon)
                        return;

                    _maxValue = value;

                    foreach(var tuple in Constants.UnitMulipliers)
                    {
                        if(value > tuple.Item1)
                        {
                            UnitMultiplier = 1f / tuple.Item1;
                            UnitPrefix = tuple.Item2;
                            break;
                        }
                    }
                }
                catch(Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public float UnitMultiplier { get; private set; } = 1f;
        public string UnitPrefix { get; private set; } = "";
        public string UnitSymbol { get; protected set; } = "";
        public int ValueWidth { get; protected set; } = 3;
        public int UpdateTicks { get; protected set; } = 10;
        public string ValueStringCache { get; private set; } = "";

        public StatBase(string id)
        {
            Id = MyStringHash.GetOrCompute(id); // overwrites this stat's script
        }

        protected virtual string ValueAsString()
        {
            if(!BuildInfoMod.Instance.Config.HudStatOverrides.Value)
            {
                return (_currentValue * 100).ToString("0");
            }

            if(_maxValue == 0)
                return "N/A";

            float val = _currentValue * UnitMultiplier;
            int rounded = (int)Math.Round(val, 0);
            int maxWidth = 4 - UnitPrefix.Length - UnitSymbol.Length;
            int width = rounded.GetDigitCount();
            int round = Math.Max(maxWidth - width, 0);

            // if "1234".Length <= "1.23k".Length then print first one
            int rawRounded = (int)Math.Round(_currentValue, 0);
            int finalWidth = width + 1 + round;
            if(rawRounded.GetDigitCount() <= finalWidth)
                return rawRounded.ToString() + UnitSymbol;

            var formats = Constants.DigitFormats;
            round = Math.Min(round, formats.Length - 1);
            return val.ToString(formats[round]) + UnitPrefix + UnitSymbol;
        }

        protected abstract void Update(int tick);

        protected void UpdateString()
        {
            ValueStringCache = ValueAsString();
        }

        void IMyHudStat.Update()
        {
            try
            {
                int tick = BuildInfoMod.Instance.Tick;
                if(UpdateTicks <= 0 || tick % UpdateTicks == 0)
                    Update(tick);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        string IMyHudStat.GetValueString() => ValueStringCache;
    }
}
