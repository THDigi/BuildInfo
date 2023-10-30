using System;
using Sandbox.ModAPI;

namespace Digi
{
    public class TerminalAppendCondition
    {
        /// <summary>
        /// For appending conditions to terminal controls' Enabled or Visual.
        /// <para>Returns the delegate to assign to the original delegate (not +=).</para>
        /// <para>Example usage:</para>
        /// <para>c.Visible = TerminalAppendCondition.Create(c.Visible, (b) =&gt; b.GameLogic.GetAs&lt;MyLogic&gt;() == null);</para>
        /// <para>Which will hide it for blocks that have the MyLogic gamelogic comp.</para>
        /// </summary>
        /// <param name="originalFunc">The existing delegate</param> 
        /// <param name="appendFunc">The condition to append</param>
        /// <param name="and">Whether the original is checked with AND against appended, otherwise uses OR.</param>
        public static Func<IMyTerminalBlock, bool> Create(Func<IMyTerminalBlock, bool> originalFunc, Func<IMyTerminalBlock, bool> appendFunc, bool and = true)
        {
            return new TerminalAppendCondition(originalFunc, appendFunc, and).ResultFunc;
        }

        readonly Func<IMyTerminalBlock, bool> OriginalFunc;
        readonly Func<IMyTerminalBlock, bool> AppendFunc;
        readonly bool AND;

        TerminalAppendCondition(Func<IMyTerminalBlock, bool> originalFunc, Func<IMyTerminalBlock, bool> customFunc, bool and)
        {
            OriginalFunc = originalFunc;
            AppendFunc = customFunc;
            AND = and;
        }

        bool ResultFunc(IMyTerminalBlock block)
        {
            if(block?.CubeGrid == null)
                return false;

            bool originalCondition = OriginalFunc?.Invoke(block) ?? true;
            bool appendedCondition = AppendFunc?.Invoke(block) ?? true;

            if(AND)
                return originalCondition && appendedCondition;
            else
                return originalCondition || appendedCondition;
        }
    }
}
