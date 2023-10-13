using System.Text;
using Digi.BuildInfo.Utilities;
using VRage.Game;
using VRageMath;

namespace Digi.BuildInfo.Features.Toolbars.FakeAPI.Items
{
    interface IToolbarItem
    {
        bool Validate(MyObjectBuilder_ToolbarItem data);
    }

    public abstract class ToolbarItem : IToolbarItem
    {
        public bool IsValid { get; private set; }
        //public string DisplayName { get; protected set; }
        //public string[] Icons { get; protected set; }

        public string DebugName { get; private set; }

        bool IToolbarItem.Validate(MyObjectBuilder_ToolbarItem data) => (IsValid = Init(data));

        protected virtual bool Init(MyObjectBuilder_ToolbarItem data)
        {
            DebugName = data?.GetType().Name ?? "null";
            return true;
        }

        public override string ToString() => GetType().Name;

        public abstract void AppendFancyRender(StringBuilder sb, float opacity);
    }

    public class ToolbarItemUnknown : ToolbarItem
    {
        public override void AppendFancyRender(StringBuilder sb, float opacity)
        {
            sb.Color(Color.Red).Append("(Unknown:").Append(DebugName).Append(")");
        }
    }
}
