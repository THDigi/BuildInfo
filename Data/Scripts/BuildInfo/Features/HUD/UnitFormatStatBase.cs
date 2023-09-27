using System;
using Digi.BuildInfo.Utilities;

namespace Digi.BuildInfo.Features.HUD
{
    public abstract class UnitFormatStatBase : HudStatBase
    {
        public int MaxWidth { get; protected set; } = 4;
        public string UnitSymbol { get; protected set; } = "";
        public float UnitMultiplier { get; private set; } = 1f;
        public string UnitPrefix { get; private set; } = "";

        public UnitFormatStatBase(string id) : base(id)
        {
            OnValuesChanged();
            ValuesChanged += OnValuesChanged;
        }

        protected void OnValuesChanged()
        {
            foreach(Constants.UnitInfo unitInfo in Constants.UnitMulipliers)
            {
                if(MaxValue > unitInfo.Multiplier)
                {
                    UnitMultiplier = 1f / unitInfo.Multiplier;
                    UnitPrefix = unitInfo.Suffix;
                    break;
                }
            }
        }

        protected override string ValueAsString()
        {
            if(MaxValue == 0)
                return "N/A";

            float val = CurrentValue * UnitMultiplier;
            int rounded = (int)Math.Round(val, 0);
            int maxWidth = MaxWidth - UnitPrefix.Length - UnitSymbol.Length;
            int width = rounded.GetDigitCount();
            int round = Math.Max(maxWidth - width, 0);

            if(CurrentValue < 99999) // if it's this large then don't bother, also avoid overflow issues
            {
                // if "1234".Length <= "1.23k".Length then print first one
                int rawRounded = (int)Math.Round(CurrentValue, 0);
                int finalWidth = width + 1 + round;
                if(rawRounded.GetDigitCount() <= finalWidth)
                    return rawRounded.ToString() + UnitSymbol;
            }

            string[] formats = Constants.DigitFormats;
            round = Math.Min(round, formats.Length - 1);
            return val.ToString(formats[round]) + UnitPrefix + UnitSymbol;
        }
    }
}
