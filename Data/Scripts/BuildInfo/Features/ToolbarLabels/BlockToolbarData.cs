using System.Collections.Generic;

namespace Digi.BuildInfo.Features.ToolbarLabels
{
    public class BlockToolbarData
    {
        public readonly Dictionary<int, string> CustomLabels = new Dictionary<int, string>();
        public readonly List<string> ParseErrors = new List<string>();

        public string GetCustomLabel(int index) => CustomLabels.GetValueOrDefault(index, null);

        public void SetLabel(int index, string value) => CustomLabels[index] = value;

        public void AddError(string message) => ParseErrors.Add(message);

        public void StartParse()
        {
            CustomLabels.Clear();
            ParseErrors.Clear();
        }
    }
}