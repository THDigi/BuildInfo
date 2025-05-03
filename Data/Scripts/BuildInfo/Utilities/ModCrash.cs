using System;
using System.IO;
using Digi.BuildInfo;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;

namespace BuildInfo.Utilities
{
    internal class ModCrash
    {
        /// <summary>
        /// Crash the game with a custom message.
        /// </summary>
        /// <param name="innerException">The actual error.</param>
        /// <param name="schedule">Uses <see cref="IMyUtilities.InvokeOnGameThread(Action, string, int, int)"/> to ensure crashing, useful for loading screen or threads.</param>
        /// <exception cref="ModCrashedException"></exception>
        public static void Throw(Exception innerException, bool schedule = false)
        {
            IMyModContext mod = BuildInfoMod.Instance.Session.ModContext;

            FakeContext fakeContext = new FakeContext();

            // {0} in the localized string
            fakeContext.ModName = $"{mod.ModName} ({mod.ModServiceName}:{mod.ModItem.PublishedFileId})";

            // {1}
            fakeContext.ModServiceName = "Please send the SE log file to the author." +
                                       "\nEither on the workshop page or on discord: @m_digi" +
                                       "\nThe log file: ";

            // {2}
            fakeContext.ModId = "log";

            // and {3} is hardcoded to "log".

            // now override the localization of that with a better formatted message (but only english).

            string folder = Path.Combine(mod.ModPathData, "Localization");

            // this method loads all MyCommonTexts/MyCoreTexts/MyTexts prefixed files from the given folder.
            // if culture is not null it would also load the same prefixed files with `Prefix.Culture.resx`
            // if culture and subculture are not null, aside from loading the culture one it also loads `Prefix.Culture-Subculture.resx`.
            MyTexts.LoadTexts(folder, cultureName: "override", subcultureName: null);

            if(schedule)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    throw new ModCrashedException(innerException, fakeContext);
                });
            }
            else
            {
                throw new ModCrashedException(innerException, fakeContext);
            }
        }

        class FakeContext : IMyModContext
        {
            public string ModName { get; set; }
            public string ModId { get; set; }
            public string ModServiceName { get; set; }
            public string ModPath { get; set; } = string.Empty;
            public string ModPathData { get; set; } = string.Empty;
            public bool IsBaseGame { get; set; } = false;
            public MyObjectBuilder_Checkpoint.ModItem ModItem { get; set; }
        }
    }
}
