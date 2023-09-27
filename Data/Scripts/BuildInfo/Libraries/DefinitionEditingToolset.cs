using System;
using System.Collections.Generic;
using VRage.Game;

namespace Digi
{
    public class DefinitionEdits
    {
        readonly List<IDefinitionEdit> Edits = new List<IDefinitionEdit>();

        /// <summary>
        /// Edits the given definition with the given <paramref name="newValue"/> using the supplied <paramref name="setter"/>.
        /// <para>It also stores the <paramref name="setter"/> and <paramref name="originalValue"/> which will be invoked upon <see cref="UndoAll"/>.</para>
        /// </summary>
        public void MakeEdit<TDef, TVal>(TDef definition, Action<TDef, TVal> setter, TVal originalValue, TVal newValue)
               where TDef : MyDefinitionBase
        {
            Edits.Add(new DefinitionEdit<TDef, TVal>(definition, setter, originalValue, newValue));
        }

        /// <summary>
        /// Reverts all edits made so far then removes them from the internal list.
        /// <para>Always call this before making new changes and when mod unloads.</para>
        /// </summary>
        public void UndoAll()
        {
            foreach(IDefinitionEdit edit in Edits)
            {
                edit.Restore();
            }

            Edits.Clear();
        }
    }

    public interface IDefinitionEdit
    {
        void Restore();
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
