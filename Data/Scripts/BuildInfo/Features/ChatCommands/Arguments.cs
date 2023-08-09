using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace Digi.BuildInfo.Features.ChatCommands
{
    /// <summary>
    /// An implementation of <see cref="MyCommandLine"/> without the switches (because it messes with negative numbers).
    /// Also added IndexOffset feature for simpler indexing in commands.
    /// </summary>
    public class Arguments
    {
        /// <summary>
        /// Number of available arguments.
        /// Affected by <see cref="IndexOffset"/>.
        /// </summary>
        public int Count => (Args.Count - IndexOffset);

        /// <summary>
        /// Offsets index for <see cref="Get(int)"/>, used before giving to commands so they can get their arguments from 0.
        /// </summary>
        public int IndexOffset { get; set; }

        readonly List<StringSegment> Args = new List<StringSegment>();

        /// <summary>
        /// Gets argument at specified index.
        /// Start from 0, index is automatically adjusted by <see cref="IndexOffset"/>.
        /// </summary>
        public string Get(int index)
        {
            index += IndexOffset;

            if(index < 0 || index >= Args.Count)
                return null;

            return Args[index].ToString();
        }

        /// <summary>
        /// Gets the remaining arguments at specified index (0 being first arg) as a single string.
        /// Returns null if no args are at or after that index.
        /// </summary>
        public string GetRestAsText(int index)
        {
            index += IndexOffset;

            if(index < 0 || index >= Args.Count)
                return null;

            // this works because each segment references the entire string command with indexes as segment pointers
            StringSegment segment = Args[index];
            return segment.Text.Substring(segment.Start);
        }

        public bool TryParse(string message)
        {
            IndexOffset = 0;
            Args.Clear();

            if(string.IsNullOrEmpty(message))
                return false;

            TextPtr ptr = new TextPtr(message);

            while(true)
            {
                ptr = ptr.SkipWhitespace();

                if(ptr.IsOutOfBounds())
                    break;

                StringSegment arg = ParseQuoted(ref ptr);
                Args.Add(arg);
            }

            return Args.Count > 0;
        }

        StringSegment ParseQuoted(ref TextPtr ptr)
        {
            TextPtr textPtr = ptr;

            bool quoted = textPtr.Char == '"';

            if(quoted)
                textPtr = ++textPtr;

            TextPtr textPtr2 = textPtr;
            TextPtr textPtr3;

            while(!textPtr2.IsOutOfBounds())
            {
                if(textPtr2.Char == '"')
                    quoted = !quoted;

                if(!quoted && char.IsWhiteSpace(textPtr2.Char))
                {
                    ptr = textPtr2;
                    textPtr3 = textPtr2 - 1;

                    if(textPtr3.Char == '"')
                        textPtr2 = textPtr3;

                    return new StringSegment(textPtr.Content, textPtr.Index, textPtr2.Index - textPtr.Index);
                }

                textPtr2 = ++textPtr2;
            }

            textPtr2 = (ptr = new TextPtr(ptr.Content, ptr.Content.Length));
            textPtr3 = textPtr2 - 1;

            if(textPtr3.Char == '"')
                textPtr2 = textPtr3;

            return new StringSegment(textPtr.Content, textPtr.Index, textPtr2.Index - textPtr.Index);
        }
    }
}