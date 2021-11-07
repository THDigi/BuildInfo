using System.Collections.Generic;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_ButtonPanel : BData_Base
    {
        public readonly List<ButtonInfo> Buttons = new List<ButtonInfo>();
        public readonly Dictionary<string, ButtonInfo> ButtonInfoByDummyName = new Dictionary<string, ButtonInfo>();

        public class ButtonInfo
        {
            public readonly Matrix LocalMatrix;
            public readonly int Index;

            public ButtonInfo(Matrix matrix, int index)
            {
                LocalMatrix = matrix;
                Index = index;
            }
        }

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            MyButtonPanelDefinition buttonDef = def as MyButtonPanelDefinition;
            if(buttonDef == null)
            {
                Log.Error($"Block '{def.Id}' is not {nameof(MyButtonPanelDefinition)}, probably missing `<Definition xsi:type=\"MyObjectBuilder_ButtonPanelDefinition\">` in its definition?");
                return base.IsValid(block, def) || false;
            }

            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            foreach(KeyValuePair<string, IMyModelDummy> dummy in dummies)
            {
                // from MyUseObjectPanelButton.ctor()
                if(dummy.Key.StartsWith(Hardcoded.ButtonPanel_DummyName))
                {
                    string[] splits = dummy.Key.Split('_');

                    int buttonIndex;
                    if(splits.Length > 1 && int.TryParse(splits[splits.Length - 1], out buttonIndex))
                    {
                        buttonIndex -= 1;

                        if(buttonIndex >= buttonDef.ButtonCount)
                        {
                            // game would already log about this, maybe I should attract more attention to it?
                            buttonIndex = buttonDef.ButtonCount - 1;
                        }

                        ButtonInfo info = new ButtonInfo(dummy.Value.Matrix, buttonIndex);
                        Buttons.Add(info);
                        ButtonInfoByDummyName[dummy.Value.Name] = info;
                    }
                }
            }

            dummies.Clear();
            return base.IsValid(block, def) || true;
        }
    }
}
