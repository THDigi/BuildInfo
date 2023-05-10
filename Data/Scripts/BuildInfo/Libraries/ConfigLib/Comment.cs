using System.Text;

namespace Digi.ConfigLib
{
    public class Comment : SettingBase<bool>
    {
        static int CommentSequentialId = 0;

        public Comment(ConfigHandler configInstance, params string[] commentLines)
            : base(configInstance, $"Comment#{CommentSequentialId++}", false, commentLines)
        {
            AddDefaultValueComment = false;
            AddValidRangeComment = false;
        }

        public override void ReadValue(string valueString, out string error)
        {
            error = null;
        }

        public override void WriteDefaultValue(StringBuilder output)
        {
        }

        public override void WriteValue(StringBuilder output)
        {
        }

        public override void SaveSetting(StringBuilder output)
        {
            AppendComments(output);
        }

        protected override void AppendDefaultValue(StringBuilder output)
        {
        }
    }
}
