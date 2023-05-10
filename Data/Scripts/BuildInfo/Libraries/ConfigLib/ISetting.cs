using System.Text;

namespace Digi.ConfigLib
{
    public interface ISetting
    {
        string Name { get; }

        bool IsMultiLine { get; }

        void ResetToDefault();

        void ReadValue(string valueString, out string error);

        void WriteValue(StringBuilder output);

        void WriteDefaultValue(StringBuilder output);

        void SaveSetting(StringBuilder output);

        /// <summary>
        /// The comment without comment prefix
        /// </summary>
        string GetDescription();

        /// <summary>
        /// For internal use!
        /// </summary>
        void TriggerValueSetEvent();
    }
}
