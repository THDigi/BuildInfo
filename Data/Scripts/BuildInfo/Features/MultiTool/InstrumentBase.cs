using System;
using System.Text;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Utils;

namespace Digi.BuildInfo.Features.MultiTool
{
    public class DescriptionBuilder
    {
        public string Text { get; set; }

        public readonly StringBuilder Builder = new StringBuilder(512);

        public DescriptionBuilder()
        {
        }

        public void UpdateFromBuilder()
        {
            Text = Builder.ToString();
        }
    }

    public abstract class InstrumentBase
    {
        public string DisplayName { get; protected set; }
        public string DisplayNameHUD { get; protected set; }
        public MyStringId BillboardIcon { get; protected set; }
        public string HUDIcon { get; protected set; }
        public readonly DescriptionBuilder Description = new DescriptionBuilder();

        public char IconChar;

        internal readonly BuildInfoMod Main;
        internal readonly MultiToolHandler MultiTool;

        public InstrumentBase(string displayName, MyStringId icon)
        {
            Main = BuildInfoMod.Instance;
            MultiTool = Main.MultiTool;

            DisplayName = displayName;
            DisplayNameHUD = displayName;

            MyTransparentMaterialDefinition matDef;
            if(!MyDefinitionManager.Static.TryGetDefinition(new MyDefinitionId(typeof(MyObjectBuilder_TransparentMaterialDefinition), icon.String), out matDef))
            {
                Log.Error($"Couldn't find transparent material definition '{icon.String}'");

                BillboardIcon = Constants.MatUI_SquareHollow;
                HUDIcon = MultiToolHandler.MissingIcon;
            }
            else
            {
                BillboardIcon = icon;
                HUDIcon = matDef.Texture;
            }
        }

        public abstract void Dispose();

        public abstract void Selected();

        public abstract void Deselected();

        public virtual void Update(bool inputReadable)
        {
        }

        public virtual void Draw()
        {
        }
    }
}
