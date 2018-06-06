using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Timers;
using ParallelTasks;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace Digi
{
    /// <summary>
    /// <para>Standalone logger, does not require any setup.</para>
    /// <para>You can set the mod name to be shorter instead of being the workshop/folder name.</para>
    /// <para>Version 1.5 by Digi</para>
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate, priority: int.MaxValue)]
    public class Log : MySessionComponentBase
    {
        private static Handler handler;
        private static bool unloaded = false;

        public const string FILE = "info.log";
        public const int PRINT_TIME_INFO = 3000;
        public const int PRINT_TIME_ERROR = 10000;
        public const string PRINT_ERROR = "error";
        public const string PRINT_MSG = "msg";

        #region Handling of handler
        public override void LoadData()
        {
            EnsureHandlerCreated();
            handler.Init(this);
        }

        protected override void UnloadData()
        {
            // safety in case InvokeOnGameThread does not get called
            var timer = new Timer(5000);
            timer.Elapsed += EnsureUnloaded;
            timer.Start();

            // delaying the unload to allow the mod's session component(s) to unload first
            MyAPIGateway.Utilities.InvokeOnGameThread(Unload, "Digi.Log.Unload()");
        }

        private void EnsureUnloaded(object sender, ElapsedEventArgs e)
        {
            if(!unloaded)
                Unload();

            var timer = (Timer)sender;
            timer.Stop();
        }

        private void Unload()
        {
            unloaded = true;
            handler?.Close();
            handler = null;
        }

        private static void EnsureHandlerCreated()
        {
            if(unloaded)
                throw new Exception("Digi.Log accessed after it was unloaded!");

            if(handler == null)
                handler = new Handler();
        }
        #endregion

        #region Publicly accessible properties and methods
        /// <summary>
        /// Sets/gets the mod name.
        /// <para>This is optional as the mod name is generated from the folder/workshop name, but those can be weird or long.</para>
        /// </summary>
        public static string ModName
        {
            get
            {
                EnsureHandlerCreated();
                return handler.ModName;
            }
            set
            {
                EnsureHandlerCreated();
                handler.ModName = value;
            }
        }

        /// <summary>
        /// <para>Increases indentation by 1 tab character.</para>
        /// Each indent adds a tab character before each of the future messages.
        /// </summary>
        public static void IncreaseIndent()
        {
            EnsureHandlerCreated();
            handler.IncreaseIndent();
        }

        /// <summary>
        /// <para>Decreases indentation by 1 tab character down to 0 indentation.</para>
        /// See <seealso cref="IncreaseIndent"/>
        /// </summary>
        public static void DecreaseIndent()
        {
            EnsureHandlerCreated();
            handler.DecreaseIndent();
        }

        /// <summary>
        /// <para>Resets the indentation to 0.</para>
        /// See <seealso cref="IncreaseIndent"/>
        /// </summary>
        public static void ResetIndent()
        {
            EnsureHandlerCreated();
            handler.ResetIndent();
        }

        /// <summary>
        /// Writes an exception to custom log file, game's log file and by default writes a generic error message to player's HUD.
        /// </summary>
        /// <param name="exception">The exception to write to custom log and game's log.</param>
        /// <param name="printText">HUD notification text, can be set to null to disable, to "msg" to use the exception message, "error" to use the predefined error message, or any other custom string.</param>
        /// <param name="printTimeMs">How long to show the HUD notification for, in miliseconds.</param>
        public static void Error(Exception exception, string printText = PRINT_ERROR, int printTimeMs = PRINT_TIME_ERROR)
        {
            EnsureHandlerCreated();
            handler.Error(exception.ToString(), printText, printTimeMs);
        }

        /// <summary>
        /// Writes a message to custom log file, game's log file and by default writes a generic error message to player's HUD.
        /// </summary>
        /// <param name="message">The message printed to custom log and game log.</param>
        /// <param name="printText">HUD notification text, can be set to null to disable, to "msg" to use the message arg, "error" to use the predefined error message, or any other custom string.</param>
        /// <param name="printTimeMs">How long to show the HUD notification for, in miliseconds.</param>
        public static void Error(string message, string printText = PRINT_ERROR, int printTimeMs = PRINT_TIME_ERROR)
        {
            EnsureHandlerCreated();
            handler.Error(message, printText, printTimeMs);
        }

        /// <summary>
        /// Writes a message in the custom log file.
        /// <para>Optionally prints a different message (or same message) in player's HUD.</para>
        /// </summary>
        /// <param name="message">The text that's written to log.</param>
        /// <param name="printText">HUD notification text, can be set to null to disable, to "msg" to use the message arg or any other custom string.</param>
        /// <param name="printTimeMs">How long to show the HUD notification for, in miliseconds.</param>
        public static void Info(string message, string printText = null, int printTimeMs = PRINT_TIME_INFO)
        {
            EnsureHandlerCreated();
            handler.Info(message, printText, printTimeMs);
        }

        /// <summary>
        /// Iterates task errors and reports them, returns true if any errors were found.
        /// </summary>
        /// <param name="task">The task to check for errors.</param>
        /// <param name="taskName">Used in the reports.</param>
        /// <returns>true if errors found, false otherwise.</returns>
        public static bool TaskHasErrors(Task task, string taskName)
        {
            EnsureHandlerCreated();
            return handler.TaskHasErrors(task, taskName);
        }
        #endregion

        private class Handler
        {
            private Log sessionComp;
            private string modName = string.Empty;
            private ulong workshopId = 0;

            private TextWriter writer;
            private int indent = 0;
            private string errorPrintText;

            private IMyHudNotification notifyInfo;
            private IMyHudNotification notifyError;

            private StringBuilder sb = new StringBuilder(64);

            private List<string> preInitMessages;

            public string ModName
            {
                get
                {
                    return modName;
                }
                set
                {
                    modName = value;
                    ComputeErrorPrintText();
                }
            }

            public Handler()
            {
            }

            public void Init(Log sessionComp)
            {
                if(writer != null)
                    return; // already initialized

                if(MyAPIGateway.Utilities == null)
                {
                    Error("MyAPIGateway.Utilities is NULL !");
                    return;
                }

                this.sessionComp = sessionComp;

                if(string.IsNullOrWhiteSpace(ModName))
                    ModName = sessionComp.ModContext.ModName;

                workshopId = GetWorkshopID(sessionComp.ModContext.ModId);

                writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(FILE, typeof(Log));

                #region Pre-init messages
                if(preInitMessages != null)
                {
                    string warning = $"{modName} WARNING: there are log messages before the mod initialized!";

                    foreach(var msg in preInitMessages)
                    {
                        Error(msg, warning);
                    }

                    preInitMessages = null;
                }
                #endregion

                #region Init message
                sb.Clear();
                sb.Append("Initialized\nDS=").Append(MyAPIGateway.Utilities.IsDedicated);
                sb.Append("; defined=");

#if STABLE
            sb.Append("STABLE, ");
#endif

#if UNOFFICIAL
            sb.Append("UNOFFICIAL, ");
#endif

#if DEBUG
            sb.Append("DEBUG, ");
#endif

#if BRANCH_STABLE
            sb.Append("BRANCH_STABLE, ");
#endif

#if BRANCH_DEVELOP
            sb.Append("BRANCH_DEVELOP, ");
#endif

#if BRANCH_UNKNOWN
            sb.Append("BRANCH_UNKNOWN, ");
#endif

                Info(sb.ToString());
                sb.Clear();
                #endregion
            }

            public void Close()
            {
                if(writer != null)
                {
                    Info("Unloaded.");

                    writer.Flush();
                    writer.Close();
                    writer = null;
                }
            }

            private void ComputeErrorPrintText()
            {
                errorPrintText = $"[ {modName} ERROR, report contents of: %AppData%/SpaceEngineers/Storage/{MyAPIGateway.Utilities.GamePaths.ModScopeName}/{FILE} ]";
            }

            public void IncreaseIndent()
            {
                indent++;
            }

            public void DecreaseIndent()
            {
                if(indent > 0)
                    indent--;
            }

            public void ResetIndent()
            {
                indent = 0;
            }

            public void Error(string message, string printText = PRINT_ERROR, int printTime = PRINT_TIME_ERROR)
            {
                WriteMessage(message, "ERROR: "); // write to custom log

                MyLog.Default.WriteLineAndConsole(modName + " error/exception: " + message); // write to game's log

                if(printText != null) // printing to HUD is optional
                {
                    try
                    {
                        if(MyAPIGateway.Utilities != null && MyAPIGateway.Session?.Player != null) // print on screen if applicable
                        {
                            if(printText == PRINT_ERROR)
                                printText = errorPrintText;
                            else if(printText == PRINT_MSG)
                                printText = message;

                            if(notifyError == null)
                            {
                                notifyError = MyAPIGateway.Utilities.CreateNotification(printText, printTime, MyFontEnum.Red);
                            }
                            else
                            {
                                notifyError.Text = printText;
                                notifyError.AliveTime = printTime;
                                notifyError.ResetAliveTime();
                            }

                            notifyError.Show();
                        }
                    }
                    catch(Exception e)
                    {
                        Info("ERROR: Could not send notification to local client: " + e);
                        MyLog.Default.WriteLineAndConsole(modName + " logger error/exception: Could not send notification to local client: " + e);
                    }
                }
            }

            public void Info(string message, string printText = null, int printTime = PRINT_TIME_INFO)
            {
                WriteMessage(message); // write to custom log

                if(printText != null) // printing to HUD is optional
                {
                    try
                    {
                        if(MyAPIGateway.Utilities != null && MyAPIGateway.Session?.Player != null) // print on screen if applicable
                        {
                            if(printText == PRINT_MSG)
                                printText = message;

                            if(notifyInfo == null)
                            {
                                notifyInfo = MyAPIGateway.Utilities.CreateNotification(printText, printTime, MyFontEnum.White);
                            }
                            else
                            {
                                notifyInfo.Text = printText;
                                notifyInfo.AliveTime = printTime;
                                notifyInfo.ResetAliveTime();
                            }

                            notifyInfo.Show();
                        }
                    }
                    catch(Exception e)
                    {
                        Info("ERROR: Could not send notification to local client: " + e);
                        MyLog.Default.WriteLineAndConsole(modName + " logger error/exception: Could not send notification to local client: " + e);
                    }
                }
            }

            public bool TaskHasErrors(Task task, string taskName)
            {
                if(task.Exceptions != null && task.Exceptions.Length > 0)
                {
                    foreach(var e in task.Exceptions)
                    {
                        Error($"Error in {taskName} thread!\n{e}");
                    }

                    return true;
                }

                return false;
            }

            private void WriteMessage(string msg, string prefix = null)
            {
                try
                {
                    sb.Clear();
                    sb.Append(DateTime.Now.ToString("[HH:mm:ss] "));

                    if(writer == null)
                        sb.Append("(PRE-INIT) ");

                    for(int i = 0; i < indent; i++)
                        sb.Append('\t');

                    if(prefix != null)
                        sb.Append(prefix);

                    sb.Append(msg);

                    if(writer == null)
                    {
                        if(preInitMessages == null)
                            preInitMessages = new List<string>();

                        preInitMessages.Add(sb.ToString());
                    }
                    else
                    {
                        writer.WriteLine(sb);
                        writer.Flush();
                    }

                    sb.Clear();
                }
                catch(Exception e)
                {
                    MyLog.Default.WriteLineAndConsole($"{modName} had an error while logging message = '{msg}'\nLogger error: {e.Message}\n{e.StackTrace}");
                }
            }

            private ulong GetWorkshopID(string modId)
            {
                // HACK workaround for MyModContext not having the actual workshop ID number.
                foreach(var mod in MyAPIGateway.Session.Mods)
                {
                    if(mod.Name == modId)
                        return mod.PublishedFileId;
                }

                return 0;
            }
        }
    }
}