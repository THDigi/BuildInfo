namespace Digi.ComponentLib
{
    public class ProfileUpdates
    {
        public readonly ProfileMeasure MeasuredInput = new ProfileMeasure();
        public readonly ProfileMeasure MeasuredBeforeSim = new ProfileMeasure();
        public readonly ProfileMeasure MeasuredAfterSim = new ProfileMeasure();
        public readonly ProfileMeasure MeasuredDraw = new ProfileMeasure();
    }

    public class ProfileMeasure
    {
        public double Min = double.MaxValue;
        public double Max = double.MinValue;
        public double MovingAvg = 0;
    }

    public struct ProfileData
    {
        public readonly string Name;
        public readonly double MeasuredMs;

        public ProfileData(string name, double measuredMs)
        {
            Name = name;
            MeasuredMs = measuredMs;
        }
    }
}
