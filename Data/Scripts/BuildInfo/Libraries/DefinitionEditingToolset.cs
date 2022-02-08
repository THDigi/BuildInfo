using System;
using VRage.Game;

namespace Digi
{
    public interface IDefinitionEdit
    {
        void Restore();
    }

    public static class DefinitionEdit
    {
        public static DefinitionEdit<TDef, TVal> Create<TDef, TVal>(TDef definition, Action<TDef, TVal> setter, TVal originalValue, TVal newValue)
               where TDef : MyDefinitionBase
        {
            return new DefinitionEdit<TDef, TVal>(definition, setter, originalValue, newValue);
        }
    }

    public class DefinitionEdit<TDef, TVal> : IDefinitionEdit where TDef : MyDefinitionBase
    {
        readonly TDef Definition;
        readonly TVal OriginalValue;
        readonly Action<TDef, TVal> Setter;

        public DefinitionEdit(TDef definition, Action<TDef, TVal> setter, TVal originalValue, TVal newValue)
        {
            Definition = definition;
            Setter = setter;
            OriginalValue = originalValue;
            Setter.Invoke(Definition, newValue);
        }

        public void Restore()
        {
            Setter.Invoke(Definition, OriginalValue);
        }
    }
}
