using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using VRage.Game;
using VRageMath;

namespace Digi.BuildInfo.Features.Toolbars.FakeAPI.Items
{
    public class ToolbarItemWithDefinition : ToolbarItem
    {
        public MyDefinitionBase Definition { get; private set; }

        bool IsWeapon;
        bool IsEmote;

        protected override bool Init(MyObjectBuilder_ToolbarItem data)
        {
            if(!base.Init(data))
                return false;

            var ob = (MyObjectBuilder_ToolbarItemDefinition)data;

            MyDefinitionBase def;
            if(!MyDefinitionManager.Static.TryGetDefinition(ob.DefinitionId, out def))
                return false;

            if(!def.Public)
                return false;

            Definition = def;
            //DisplayName = def.DisplayNameText;
            //Icons = def.Icons;
            IsWeapon = data is MyObjectBuilder_ToolbarItemWeapon;
            IsEmote = data is MyObjectBuilder_ToolbarItemEmote || data is MyObjectBuilder_ToolbarItemAnimation;
            return true;
        }

        public override string ToString()
        {
            return $"{GetType().Name}(''{Definition?.Id}'')";
        }

        public override void AppendFancyRender(StringBuilder sb, float opacity)
        {
            if(IsWeapon)
                sb.ColorA(ToolbarRender.WeaponColor * opacity);
            else
                sb.ColorA(ToolbarRender.OtherItemColor * opacity);

            if(IsEmote)
                sb.Append("Emote - ");

            sb.AppendMaxLength(Definition?.DisplayNameText, ToolbarRender.MaxNameLength);
        }
    }
}
