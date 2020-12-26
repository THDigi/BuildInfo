using Sandbox.Common.ObjectBuilders;

namespace Digi.BuildInfo.Features.ToolbarLabels
{
    public struct ToolbarItemData
    {
        public readonly int Index;
        public readonly string Label;
        public readonly string GroupName;
        public readonly string PBRunArgument;

        public ToolbarItemData(int index, string label, string group, MyObjectBuilder_ToolbarItemTerminalBlock blockItem)
        {
            Index = index;
            Label = GetWrappedText(label);
            GroupName = GetWrappedText(group);
            PBRunArgument = null;

            // HACK major assumptions here, but there's no other use case and some stuff is prohibited so just w/e
            if(blockItem?.Parameters != null && blockItem.Parameters.Count > 0 && blockItem._Action == "Run")
            {
                string arg = blockItem.Parameters[0]?.Value;
                if(arg != null)
                {
                    const string Label = "Run:\n";
                    PBRunArgument = GetWrappedText(Label + arg, Label.Length + ToolbarCustomNames.CustomLabelMaxLength);
                }
            }
        }

        private static string GetWrappedText(string text, int maxLength = ToolbarCustomNames.CustomLabelMaxLength)
        {
            if(text == null)
                return null;

            if(text == string.Empty)
                return string.Empty;

            var sb = BuildInfoMod.Instance.Caches.SB;
            sb.Clear();
            ActionWriterOverride.AppendWordWrapped(sb, text, maxLength);
            return sb.ToString();
        }
    }
}
