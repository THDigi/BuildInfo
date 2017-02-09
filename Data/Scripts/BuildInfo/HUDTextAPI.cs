using Sandbox.ModAPI;
using System;
using VRageMath;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using VRage.Game.ModAPI;

namespace Draygo.API
{
    public class HUDTextAPI
    {
        #region Data
        public Dictionary<char, int> FontDict = new Dictionary<char, int>()
        {
            {' ',15},
            {'!',24},
            {'"',25},
            {'#',35},
            {'$',36},
            {'%',39},
            {'&',35},
            {'\'',22},
            {'(',24},
            {')',24},
            {'*',26},
            {'+',34},
            {',',25},
            {'-',25},
            {'.',25},
            {'/',30},
            {'0',35},
            {'1',24},
            {'2',34},
            {'3',33},
            {'4',34},
            {'5',35},
            {'6',35},
            {'7',31},
            {'8',35},
            {'9',35},
            {':',25},
            {';',25},
            {'<',34},
            {'=',34},
            {'>',34},
            {'?',31},
            {'@',40},
            {'A',37},
            {'B',37},
            {'C',35},
            {'D',37},
            {'E',34},
            {'F',32},
            {'G',36},
            {'H',35},
            {'I',24},
            {'J',31},
            {'K',33},
            {'L',30},
            {'M',42},
            {'N',37},
            {'O',37},
            {'P',35},
            {'Q',37},
            {'R',37},
            {'S',37},
            {'T',32},
            {'U',36},
            {'V',35},
            {'W',47},
            {'X',35},
            {'Y',36},
            {'Z',35},
            {'[',25},
            {'\\',28},
            {']',25},
            {'^',34},
            {'_',31},
            {'`',23},
            {'a',33},
            {'b',33},
            {'c',32},
            {'d',33},
            {'e',33},
            {'f',24},
            {'g',33},
            {'h',33},
            {'i',23},
            {'j',23},
            {'k',32},
            {'l',23},
            {'m',42},
            {'n',33},
            {'o',33},
            {'p',33},
            {'q',33},
            {'r',25},
            {'s',33},
            {'t',25},
            {'u',33},
            {'v',30},
            {'w',42},
            {'x',31},
            {'y',33},
            {'z',31},
            {'{',25},
            {'|',22},
            {'}',25},
            {'~',34},
            {'¡',24},
            {'¢',32},
            {'£',33},
            {'¤',35},
            {'¥',35},
            {'¦',22},
            {'§',36},
            {'¨',23},
            {'©',40},
            {'ª',26},
            {'«',30},
            {'¬',34},
            {'®',40},
            {'¯',23},
            {'°',27},
            {'±',34},
            {'²',27},
            {'³',27},
            {'´',23},
            {'µ',33},
            {'¶',34},
            {'·',25},
            {'¸',23},
            {'¹',27},
            {'º',26},
            {'»',30},
            {'¼',43},
            {'½',45},
            {'¾',43},
            {'¿',31},
            {'À',37},
            {'Á',37},
            {'Â',37},
            {'Ã',37},
            {'Ä',37},
            {'Å',37},
            {'Æ',47},
            {'Ç',35},
            {'È',34},
            {'É',34},
            {'Ê',34},
            {'Ë',34},
            {'Ì',24},
            {'Í',24},
            {'Î',24},
            {'Ï',24},
            {'Ð',37},
            {'Ñ',37},
            {'Ò',37},
            {'Ó',37},
            {'Ô',37},
            {'Õ',37},
            {'Ö',37},
            {'×',34},
            {'Ø',37},
            {'Ù',36},
            {'Ú',36},
            {'Û',36},
            {'Ü',36},
            {'Ý',33},
            {'Þ',35},
            {'ß',34},
            {'à',33},
            {'á',33},
            {'â',33},
            {'ã',33},
            {'ä',33},
            {'å',33},
            {'æ',44},
            {'ç',32},
            {'è',33},
            {'é',33},
            {'ê',33},
            {'ë',33},
            {'ì',23},
            {'í',23},
            {'î',23},
            {'ï',23},
            {'ð',33},
            {'ñ',33},
            {'ò',33},
            {'ó',33},
            {'ô',33},
            {'õ',33},
            {'ö',33},
            {'÷',34},
            {'ø',33},
            {'ù',33},
            {'ú',33},
            {'û',33},
            {'ü',33},
            {'ý',33},
            {'þ',33},
            {'ÿ',33},
            {'Ā',35},
            {'ā',33},
            {'Ă',37},
            {'ă',33},
            {'Ą',37},
            {'ą',33},
            {'Ć',35},
            {'ć',32},
            {'Ĉ',35},
            {'ĉ',32},
            {'Ċ',35},
            {'ċ',32},
            {'Č',35},
            {'č',32},
            {'Ď',37},
            {'ď',33},
            {'Đ',37},
            {'đ',33},
            {'Ē',34},
            {'ē',33},
            {'Ĕ',34},
            {'ĕ',33},
            {'Ė',34},
            {'ė',33},
            {'Ę',34},
            {'ę',33},
            {'Ě',34},
            {'ě',33},
            {'Ĝ',36},
            {'ĝ',33},
            {'Ğ',36},
            {'ğ',33},
            {'Ġ',36},
            {'ġ',33},
            {'Ģ',36},
            {'ģ',33},
            {'Ĥ',35},
            {'ĥ',33},
            {'Ħ',35},
            {'ħ',33},
            {'Ĩ',24},
            {'ĩ',23},
            {'Ī',24},
            {'ī',23},
            {'Į',24},
            {'į',23},
            {'İ',24},
            {'ı',23},
            {'Ĳ',40},
            {'ĳ',29},
            {'Ĵ',31},
            {'ĵ',23},
            {'Ķ',33},
            {'ķ',32},
            {'Ĺ',30},
            {'ĺ',23},
            {'Ļ',30},
            {'ļ',23},
            {'Ľ',30},
            {'ľ',23},
            {'Ŀ',30},
            {'ŀ',26},
            {'Ł',30},
            {'ł',23},
            {'Ń',37},
            {'ń',33},
            {'Ņ',37},
            {'ņ',33},
            {'Ň',37},
            {'ň',33},
            {'ŉ',33},
            {'Ō',37},
            {'ō',33},
            {'Ŏ',37},
            {'ŏ',33},
            {'Ő',37},
            {'ő',33},
            {'Œ',47},
            {'œ',44},
            {'Ŕ',37},
            {'ŕ',25},
            {'Ŗ',37},
            {'ŗ',25},
            {'Ř',37},
            {'ř',25},
            {'Ś',37},
            {'ś',33},
            {'Ŝ',37},
            {'ŝ',33},
            {'Ş',37},
            {'ş',33},
            {'Š',37},
            {'š',33},
            {'Ţ',32},
            {'ţ',25},
            {'Ť',32},
            {'ť',25},
            {'Ŧ',32},
            {'ŧ',25},
            {'Ũ',36},
            {'ũ',33},
            {'Ū',36},
            {'ū',33},
            {'Ŭ',36},
            {'ŭ',33},
            {'Ů',36},
            {'ů',33},
            {'Ű',36},
            {'ű',33},
            {'Ų',36},
            {'ų',33},
            {'Ŵ',47},
            {'ŵ',42},
            {'Ŷ',33},
            {'ŷ',33},
            {'Ÿ',33},
            {'Ź',35},
            {'ź',31},
            {'Ż',35},
            {'ż',31},
            {'Ž',35},
            {'ž',31},
            {'ƒ',35},
            {'Ș',37},
            {'ș',33},
            {'Ț',32},
            {'ț',25},
            {'ˆ',23},
            {'ˇ',23},
            {'ˉ',22},
            {'˘',23},
            {'˙',23},
            {'˚',23},
            {'˛',23},
            {'˜',23},
            {'˝',23},
            {'Ё',34},
            {'Ѓ',32},
            {'Є',34},
            {'Ѕ',37},
            {'І',24},
            {'Ї',24},
            {'Ј',31},
            {'Љ',43},
            {'Њ',37},
            {'Ќ',34},
            {'Ў',32},
            {'Џ',33},
            {'А',35},
            {'Б',35},
            {'В',35},
            {'Г',30},
            {'Д',35},
            {'Е',34},
            {'Ж',37},
            {'З',33},
            {'И',35},
            {'Й',34},
            {'К',33},
            {'Л',33},
            {'М',42},
            {'Н',33},
            {'О',35},
            {'П',34},
            {'Р',34},
            {'С',35},
            {'Т',35},
            {'У',35},
            {'Ф',36},
            {'Х',35},
            {'Ц',36},
            {'Ч',32},
            {'Ш',42},
            {'Щ',45},
            {'Ъ',35},
            {'Ы',40},
            {'Ь',34},
            {'Э',34},
            {'Ю',42},
            {'Я',35},
            {'а',32},
            {'б',33},
            {'в',31},
            {'г',30},
            {'д',33},
            {'е',33},
            {'ж',36},
            {'з',30},
            {'и',32},
            {'й',32},
            {'к',32},
            {'л',30},
            {'м',41},
            {'н',31},
            {'о',32},
            {'п',32},
            {'р',32},
            {'с',32},
            {'т',30},
            {'у',33},
            {'ф',37},
            {'х',31},
            {'ц',33},
            {'ч',31},
            {'ш',41},
            {'щ',42},
            {'ъ',31},
            {'ы',36},
            {'ь',31},
            {'э',30},
            {'ю',39},
            {'я',32},
            {'ё',33},
            {'ђ',33},
            {'ѓ',31},
            {'є',30},
            {'ѕ',32},
            {'і',23},
            {'ї',23},
            {'ј',22},
            {'љ',38},
            {'њ',41},
            {'ћ',33},
            {'ќ',31},
            {'ў',33},
            {'џ',33},
            {'Ґ',30},
            {'ґ',29},
            {'–',30},
            {'—',46},
            {'‘',22},
            {'’',22},
            {'‚',22},
            {'“',27},
            {'”',27},
            {'„',27},
            {'†',36},
            {'‡',36},
            {'•',31},
            {'…',47},
            {'‰',47},
            {'‹',24},
            {'›',24},
            {'€',35},
            {'™',46},
            {'−',34},
            {'∙',24},
            {'□',37}
        };
        #endregion

        public enum TextOrientation : byte
        {
            ltr = 1,
            center = 2,
            rtl = 3
        }
        [Flags]
        public enum Options : byte
        {
            None = 0x0,
            HideHud = 0x1,
            Shadowing = 0x2,
        }
        public struct HUDMessage
        {
            public long id;
            public int ttl;
            public Vector2D origin;
            public string message;
            public Options options;
            public Vector4 shadowcolor;
            public double scale;

            /// <summary>
            /// Data to transport for HUD Text Display
            /// </summary>
            /// <param name="messageid">Message id is automatically generated if set to 0. Resend a message with the same ID to overwrite previously sent messages</param>
            /// <param name="timetolive">How many frames a message will live</param>
            /// <param name="Origin">Vector 2D, middle of screen is (0,0) up is positive, down is negative, right is positive, left is negative.</param>
            /// <param name="message">Actual message you want to send. &lt;color=colorname&gt; to change the color of the text.</param>
            public HUDMessage(long messageid, int timetolive, Vector2D Origin, string message)
            {
                id = messageid;
                ttl = timetolive;
                origin = Origin;
                options = Options.None;
                var blackshadow = Color.Black;
                shadowcolor = blackshadow.ToVector4();
                scale = 1;
                this.message = message;

            }
            /// <summary>
            /// Data to transport for HUD Text Display
            /// </summary>
            /// <param name="messageid">Message id is automatically generated if set to 0. Resend a message with the same ID to overwrite previously sent messages</param>
            /// <param name="timetolive">How many frames a message will live</param>
            /// <param name="Origin">Vector 2D, middle of screen is (0,0) up is positive, down is negative, right is positive, left is negative. up to +1 and -1</param>
            /// <param name="Scale">Scale multiplier for text, 0.5 is half as big. 2.0 twice as big.</param>
            /// <param name="HideHud">Automatically hide this HUD element when player hides their HUD</param>
            /// <param name="Shadowing">Enables text shadowing</param>
            /// <param name="Shadowcolor">Specifies Text Shadow Color</param>
            /// <param name="message">Message string that you want to send. &lt;color=colorname&gt; to change the color of the text. </param>
            public HUDMessage(long messageid, int timetolive, Vector2D Origin, double Scale, bool HideHud, bool Shadowing, Color Shadowcolor, string message)
            {
                options = Options.None;
                if(HideHud)
                    options |= Options.HideHud;
                if(Shadowing)
                    options |= Options.Shadowing;
                scale = Scale;
                id = messageid;
                ttl = timetolive;
                shadowcolor = Shadowcolor.ToVector4();
                origin = Origin;
                this.message = message;
            }

        }
        public struct BillBoardHUDMessage
        {
            public long id;
            public int ttl;
            public Vector2D origin;
            public string material;
            public Options options;
            public Vector4 bbcolor;
            public double scale;
            public float rotation;

            /// <summary>
            /// Creates a BillBoardHUDMessage
            /// </summary>
            /// <param name="messageid">ID of message, resend a message with the same ID to overwrite</param>
            /// <param name="timetolive">How many frames the message will live.</param>
            /// <param name="Origin">Screen origin of the billboard, from -1 to 1. </param>
            /// <param name="BillBoardColor">Color of the billboard</param>
            /// <param name="Material">Material of the billboard</param>
            /// <param name="scale">Scale in size, defaults to 1</param>
            /// <param name="rotation">Rotation in radians</param>
            /// <param name="HideHud">Hide the billboard if the user hides his/her HUD, defaults to true</param>
            public BillBoardHUDMessage(long messageid, int timetolive, Vector2D Origin, Color BillBoardColor, string Material, double scale = 1, float rotation = 0, bool HideHud = true)
            {
                id = messageid;
                ttl = timetolive;
                origin = Origin;
                options = Options.None;
                if(HideHud)
                    options |= Options.HideHud;
                bbcolor = BillBoardColor.ToVector4();
                this.scale = scale;
                this.material = Material;
                this.rotation = rotation;

            }
        }
        public struct SpaceMessage
        {
            public long id;
            public int ttl;
            public Vector3D pos;
            public Vector3D up;
            public Vector3D left;
            public double scale;
            public string message;
            public TextOrientation t_orientation;
            /// <summary>
            /// Data to transport for Message in 3D space
            /// </summary>
            /// <param name="messageid">Message ID, if set to 0 it will be poplulated with an ID</param>
            /// <param name="timetolive">Time to live in frames</param>
            /// <param name="scale">Scale</param>
            /// <param name="position">World Position</param>
            /// <param name="Up">Up direction of text</param>
            /// <param name="Left">Left Direction of text</param>
            /// <param name="message">Actual message you want to send.&lt;color=colorname&gt; to change the color of the text.</param>
            /// <param name="orientation">left to right (ltr), center, and right to left (rtl) determines how text is laid out relative to pos.</param>
            public SpaceMessage(long messageid, int timetolive, double scale, Vector3D position, Vector3D Up, Vector3D Left, string message, TextOrientation orientation = TextOrientation.ltr)
            {
                id = messageid;
                ttl = timetolive;
                this.scale = scale;
                pos = position;
                up = Up;
                left = Left;
                t_orientation = orientation;
                this.message = message;
            }
        }
        public struct EntityMessage
        {
            public long id;
            public int ttl;
            public long entityid;
            public Vector3D rel;
            public Vector3D up;
            public Vector3D forward;
            public double scale;
            public string message;
            public TextOrientation t_orientation;
            public Vector2D max;
            /// <summary>
            /// Data to transport Entity attached message
            /// </summary>
            /// <param name="messageid">Message ID, if set to 0 this will be populated by the Send method</param>
            /// <param name="timetolive">Time to live in frames</param>
            /// <param name="scale">Scale</param>
            /// <param name="EntityId">Entity ID to attach to</param>
            /// <param name="localposition">Position relative to the entity ID</param>
            /// <param name="Up">Up direction relative to the entity</param>
            /// <param name="Forward">Forward direction relative to the entity</param>
            /// <param name="message">Actual message you want to send.&lt;color=colorname&gt; to change the color of the text.</param>
            /// <param name="max_x">maximum in the x direction the text can fill (to the left)  0 is unlimited</param>
            /// <param name="max_y">maximum in the y direction that the text can fill (down) 0 is unlimited</param>
            /// <param name="orientation">left to right (ltr), center, and right to left (rtl) determines how text is laid out relative to the Entity.</param>
            public EntityMessage(long messageid, int timetolive, double scale, long EntityId, Vector3D localposition, Vector3D Up, Vector3D Forward, string message, double max_x = 0, double max_y = 0, TextOrientation orientation = TextOrientation.ltr)
            {
                id = messageid;
                ttl = timetolive;
                this.scale = scale;
                this.entityid = EntityId;
                rel = localposition;
                up = Up;
                forward = Forward;
                max = new Vector2D(max_x, max_y);
                t_orientation = orientation;
                this.message = message;
            }
        }
        private bool m_heartbeat = false;
        protected long m_modId = 0;
        private long currentId = 1000;
        private readonly ushort HUDAPI_ADVMSG = 54019;
        private readonly ushort HUDAPI_RECEIVE = 54021;
        private readonly ushort MOD_VER = 1;

        /// <summary>
        /// True if HUDApi is installed and initialized. Please wait a few seconds and try again if it isn't ready yet. 
        /// </summary>
        public bool Heartbeat
        {
            get
            {
                return m_heartbeat;
            }

            private set
            {
                m_heartbeat = value;
            }
        }
        /// <summary>
        /// True if HUD is visible, will also return true if Session is null
        /// </summary>
        public bool IsHudVisible
        {
            get
            {
                return (MyAPIGateway.Session?.Config?.MinimalHud == null || !MyAPIGateway.Session.Config.MinimalHud);
            }

        }
        public bool IsInMenu
        {
            get
            {
                if(MyAPIGateway.Gui.GetCurrentScreen != MyTerminalPageEnum.None)
                    return true;
                try
                {
                    if(MyAPIGateway.Gui?.ActiveGamePlayScreen != null)
                        if(MyAPIGateway.Gui.ActiveGamePlayScreen != "")
                            return true;
                }
                catch
                {
                    //LOL REXXAR
                }


                return false;
            }

        }

        /// <summary>
        /// You must specify a modId to avoid conflicts with other mods. Just pick a random number, it probably will be fine ;) Please call .Close() during the cleanup of your mod.
        /// </summary>
        /// <param name="modId">ID of your mod, it is recommended you choose a unique one for each mod.</param>
        public HUDTextAPI(long modId)
        {
            m_modId = modId;
            MyAPIGateway.Multiplayer.RegisterMessageHandler(HUDAPI_RECEIVE, callback);
        }

        private void callback(byte[] obj)
        {
            m_heartbeat = true;
            return;
        }
        /// <summary>
        /// Gets the next ID for a HUDMessage, starts counting from 1000. Under 1000 is reserved for manual usage. 
        /// </summary>
        /// <returns>ID for a HUDMessage</returns>
        public long GetNextID()
        {
            return currentId++;
        }

        /// <summary>
        /// Returns the text length of text in meters. This method is not for HUDMessage. Returns a value at end of string or end of line ('\n')
        /// </summary>
        /// <param name="text">Text to measure</param>
        /// <param name="scale">Scale of text</param>
        /// <returns></returns>
        public double GetLineLength(string text, double scale = 1.0d)
        {
            double retval = 0;
            int value = 0;
            Regex ColorEncoder = new Regex("<color=(\\w*?|\\d*?\\,\\d*?\\,\\d*?|\\d*?,\\d*?,\\d*?,\\d*?)>", RegexOptions.IgnoreCase);
            var matches = ColorEncoder.Split(text);
            int i = 0;
            foreach(var match in matches)
            {
                i++;
                if(i % 2 == 0)
                {
                    continue;
                }
                foreach(var letter in match)
                {
                    if(letter == '\n')
                    {
                        return retval;
                    }
                    if(FontDict.TryGetValue(letter, out value))
                    {
                        retval += ((double)value / 45d) * scale * 1.2d;
                    }
                    else
                        retval += (15d / 45d) * scale * 1.2d;
                }
            }
            return retval;
        }
        /// <summary>
        /// Creates and Sends a HUDMessage
        /// </summary>
        /// <param name="timetolive">Time in frames until HUD element expires, not recommended to set this to 1 as it can lead to flicker.</param>
        /// <param name="origin">Vector2D between 1,1 and -1,-1. 0,0 is the middle of the screen.</param>
        /// <param name="message">Actual message you want to send.&lt;color=colorname&gt; to change the color of the text.</param>
        /// <returns>HUDMessage populated with a new ID that can be resent.</returns>
        public HUDMessage CreateAndSend(int timetolive, Vector2D origin, string message)
        {
            return CreateAndSend(GetNextID(), timetolive, origin, message);
        }
        /// <summary>
        /// Creates and Sends a HUDMessage
        /// </summary>
        /// <param name="id">ID of the HUDMessage, if 0 it will choose the next ID.</param>
        /// <param name="timetolive">Time in frames until HUD element expires, not recommended to set this to 1 as it can lead to flicker.</param>
        /// <param name="origin">Vector2D between 1,1 and -1,-1.</param>
        /// <param name="message">Actual message you want to send.&lt;color=colorname&gt; to change the color of the text.</param>
        /// <returns>HUDMessage that can be resent.</returns>
        public HUDMessage CreateAndSend(long id, int timetolive, Vector2D origin, string message)
        {
            HUDMessage Hmessage = new HUDMessage(id, timetolive, origin, message);
            return Send(Hmessage);
        }

        /// <summary>
        /// Sends an already constructed HUDMessage, if HUDMessage has an ID of 0 it will pick the next ID.
        /// </summary>
        /// <param name="message">HUDMessage being sent or resent.</param>
        /// <returns>Returns HUDMessage with populated ID</returns>
        public HUDMessage Send(HUDMessage message)
        {
            var msg = Encode(ref message);
            MyAPIGateway.Multiplayer.SendMessageTo(HUDAPI_ADVMSG, msg, MyAPIGateway.Multiplayer.MyId, true);
            return message;
        }
        /// <summary>
        /// Sends an already constructed BillBoardHUDMessage, if BillBoardHUDMessage has an ID of 0 it will pick the next ID.
        /// </summary>
        /// <param name="message">BillBoardHUDMessage being sent or resent.</param>
        /// <returns>Returns BillBoardHUDMessage with populated ID</returns>
        public BillBoardHUDMessage Send(BillBoardHUDMessage message)
        {
            var msg = Encode(ref message);
            MyAPIGateway.Multiplayer.SendMessageTo(HUDAPI_ADVMSG, msg, MyAPIGateway.Multiplayer.MyId, true);
            return message;
        }
        /// <summary>
        /// Sends an already constructed SpaceMessage, if Spacemessage has an ID of 0 it will pick the next id. 
        /// </summary>
        /// <param name="message">SpaceMessage being sent or resent</param>
        /// <returns>Returns SpaceMessage with populated ID</returns>
        public SpaceMessage Send(SpaceMessage message)
        {
            var msg = Encode(ref message);
            MyAPIGateway.Multiplayer.SendMessageTo(HUDAPI_ADVMSG, msg, MyAPIGateway.Multiplayer.MyId, true);
            return message;
        }
        /// <summary>
        /// Sends an Entity Attached Message. Entity Messages will stick to the assigned entity. 
        /// </summary>
        /// <param name="message">EntityMessage being sent or resent</param>
        /// <returns>EntityMessage with Populated ID</returns>
        public EntityMessage Send(EntityMessage message)
        {
            var msg = Encode(ref message);
            MyAPIGateway.Multiplayer.SendMessageTo(HUDAPI_ADVMSG, msg, MyAPIGateway.Multiplayer.MyId, true);
            return message;
        }
        /// <summary>
        /// Send already constructed HUDMessage to others. If the HUDMessage has an ID of 0 it will pick the next ID.
        /// </summary>
        /// <param name="message">HUDMessage being sent or resent.</param>
        /// <returns>Returns HUDMessage with populated ID</returns>
        public HUDMessage SendToOthers(HUDMessage message)
        {
            var msg = Encode(ref message);
            MyAPIGateway.Multiplayer.SendMessageToOthers(HUDAPI_ADVMSG, msg, true);
            return message;
        }
        /// <summary>
        /// Send already constructed BillBoardHUDMessage to others. If the BillBoardHUDMessage has an ID of 0 it will pick the next ID.
        /// </summary>
        /// <param name="message">BillBoardHUDMessage being sent or resent.</param>
        /// <returns>Returns BillBoardHUDMessage with populated ID</returns>
        public BillBoardHUDMessage SendToOthers(BillBoardHUDMessage message)
        {
            var msg = Encode(ref message);
            MyAPIGateway.Multiplayer.SendMessageToOthers(HUDAPI_ADVMSG, msg, true);
            return message;
        }
        /// <summary>
        /// Sends an already constructed SpaceMessage to others, if Spacemessage has an ID of 0 it will pick the next id. 
        /// </summary>
        /// <param name="message">SpaceMessage being sent or resent</param>
        /// <returns>Returns SpaceMessage with populated ID</returns>
        public SpaceMessage SendToOthers(SpaceMessage message)
        {
            var msg = Encode(ref message);
            MyAPIGateway.Multiplayer.SendMessageToOthers(HUDAPI_ADVMSG, msg, true);
            return message;
        }
        /// <summary>
        /// Sends an Entity Attached Message to others. Entity Messages will stick to the assigned entity. 
        /// </summary>
        /// <param name="message">EntityMessage being sent or resent</param>
        /// <returns>EntityMessage with Populated ID</returns>
        public EntityMessage SendToOthers(EntityMessage message)
        {
            var msg = Encode(ref message);
            MyAPIGateway.Multiplayer.SendMessageToOthers(HUDAPI_ADVMSG, msg, true);
            return message;
        }
        private byte[] Encode(ref HUDMessage message)
        {

            ushort msgtype = 3;
            if(message.id == 0)
            {

                message.id = GetNextID();
            }

            byte[] ver = BitConverter.GetBytes(MOD_VER);
            byte[] type = BitConverter.GetBytes(msgtype);
            byte[] modid = BitConverter.GetBytes(m_modId);
            byte[] mid = BitConverter.GetBytes(message.id);
            byte[] ttl = BitConverter.GetBytes(message.ttl);
            byte[] vx = BitConverter.GetBytes(message.origin.X);
            byte[] vy = BitConverter.GetBytes(message.origin.Y);
            byte[] options = new byte[1] { (byte)message.options };
            byte[] cx = BitConverter.GetBytes(message.shadowcolor.X);
            byte[] cy = BitConverter.GetBytes(message.shadowcolor.Y);
            byte[] cz = BitConverter.GetBytes(message.shadowcolor.Z);
            byte[] cw = BitConverter.GetBytes(message.shadowcolor.W);
            byte[] scale = BitConverter.GetBytes(message.scale);
            byte[] encode = Encoding.UTF8.GetBytes(message.message);
            byte[] msg = new byte[ver.Length + type.Length + cx.Length * 4 + scale.Length + modid.Length + mid.Length + ttl.Length + vx.Length + vy.Length + encode.Length + options.Length];
            int lth = 0;
            Copy(ref msg, ref ver, ref lth);
            Copy(ref msg, ref type, ref lth);
            Copy(ref msg, ref modid, ref lth);
            Copy(ref msg, ref mid, ref lth);
            Copy(ref msg, ref ttl, ref lth);
            Copy(ref msg, ref vx, ref lth);
            Copy(ref msg, ref vy, ref lth);
            Copy(ref msg, ref options, ref lth);
            Copy(ref msg, ref cx, ref lth);
            Copy(ref msg, ref cy, ref lth);
            Copy(ref msg, ref cz, ref lth);
            Copy(ref msg, ref cw, ref lth);
            Copy(ref msg, ref scale, ref lth);
            Copy(ref msg, ref encode, ref lth);
            return msg;
        }
        private byte[] Encode(ref BillBoardHUDMessage message)
        {

            ushort msgtype = 4;
            if(message.id == 0)
            {

                message.id = GetNextID();
            }

            byte[] ver = BitConverter.GetBytes(MOD_VER);
            byte[] type = BitConverter.GetBytes(msgtype);
            byte[] modid = BitConverter.GetBytes(m_modId);
            byte[] mid = BitConverter.GetBytes(message.id);
            byte[] ttl = BitConverter.GetBytes(message.ttl);
            byte[] vx = BitConverter.GetBytes(message.origin.X);
            byte[] vy = BitConverter.GetBytes(message.origin.Y);
            byte[] options = new byte[1] { (byte)message.options };
            byte[] cx = BitConverter.GetBytes(message.bbcolor.X);
            byte[] cy = BitConverter.GetBytes(message.bbcolor.Y);
            byte[] cz = BitConverter.GetBytes(message.bbcolor.Z);
            byte[] cw = BitConverter.GetBytes(message.bbcolor.W);
            byte[] scale = BitConverter.GetBytes(message.scale);
            byte[] rot = BitConverter.GetBytes(message.rotation);
            byte[] encode = Encoding.UTF8.GetBytes(message.material);
            byte[] msg = new byte[ver.Length + type.Length + cx.Length * 4 + scale.Length + modid.Length + mid.Length + ttl.Length + vx.Length + vy.Length + encode.Length + options.Length + rot.Length];
            int lth = 0;
            Copy(ref msg, ref ver, ref lth);
            Copy(ref msg, ref type, ref lth);
            Copy(ref msg, ref modid, ref lth);
            Copy(ref msg, ref mid, ref lth);
            Copy(ref msg, ref ttl, ref lth);
            Copy(ref msg, ref vx, ref lth);
            Copy(ref msg, ref vy, ref lth);
            Copy(ref msg, ref options, ref lth);
            Copy(ref msg, ref cx, ref lth);
            Copy(ref msg, ref cy, ref lth);
            Copy(ref msg, ref cz, ref lth);
            Copy(ref msg, ref cw, ref lth);
            Copy(ref msg, ref scale, ref lth);
            Copy(ref msg, ref rot, ref lth);
            Copy(ref msg, ref encode, ref lth);
            return msg;
        }
        private byte[] Encode(ref SpaceMessage message)
        {
            ushort msgtype = 1;
            if(message.id == 0)
            {

                message.id = GetNextID();
            }

            byte[] ver = BitConverter.GetBytes(MOD_VER);
            byte[] type = BitConverter.GetBytes(msgtype);
            byte[] modid = BitConverter.GetBytes(m_modId);
            byte[] mid = BitConverter.GetBytes(message.id);
            byte[] ttl = BitConverter.GetBytes(message.ttl);
            byte[] orient = new byte[1] { (byte)message.t_orientation };
            byte[] pos = Encode(message.pos);
            byte[] up = Encode(message.up);
            byte[] left = Encode(message.left);
            byte[] scale = BitConverter.GetBytes(message.scale);
            byte[] encode = Encoding.UTF8.GetBytes(message.message);
            byte[] msg = new byte[ver.Length + type.Length + modid.Length + mid.Length + ttl.Length + pos.Length + up.Length + left.Length + scale.Length + encode.Length + 1];
            int lth = 0;
            Copy(ref msg, ref ver, ref lth);
            Copy(ref msg, ref type, ref lth);
            Copy(ref msg, ref modid, ref lth);
            Copy(ref msg, ref mid, ref lth);
            Copy(ref msg, ref ttl, ref lth);
            Copy(ref msg, ref orient, ref lth);
            Copy(ref msg, ref pos, ref lth);
            Copy(ref msg, ref up, ref lth);
            Copy(ref msg, ref left, ref lth);
            Copy(ref msg, ref scale, ref lth);
            Copy(ref msg, ref encode, ref lth);
            return msg;
        }
        private byte[] Encode(ref EntityMessage message)
        {
            ushort msgtype = 2;
            if(message.id == 0)
            {

                message.id = GetNextID();
            }

            byte[] ver = BitConverter.GetBytes(MOD_VER);
            byte[] type = BitConverter.GetBytes(msgtype);
            byte[] modid = BitConverter.GetBytes(m_modId);
            byte[] mid = BitConverter.GetBytes(message.id);
            byte[] ttl = BitConverter.GetBytes(message.ttl);
            byte[] orient = new byte[1] { (byte)message.t_orientation };
            byte[] entity = BitConverter.GetBytes(message.entityid);
            byte[] mx = BitConverter.GetBytes(message.max.X);
            byte[] my = BitConverter.GetBytes(message.max.Y);
            byte[] rel = Encode(message.rel);
            byte[] up = Encode(message.up);
            byte[] forward = Encode(message.forward);
            byte[] scale = BitConverter.GetBytes(message.scale);
            byte[] encode = Encoding.UTF8.GetBytes(message.message);
            byte[] msg = new byte[ver.Length + type.Length + modid.Length + mid.Length + ttl.Length + entity.Length + rel.Length + up.Length + forward.Length + scale.Length + encode.Length + 1 + mx.Length + my.Length];

            int lth = 0;
            Copy(ref msg, ref ver, ref lth);
            Copy(ref msg, ref type, ref lth);
            Copy(ref msg, ref modid, ref lth);
            Copy(ref msg, ref mid, ref lth);
            Copy(ref msg, ref ttl, ref lth);
            Copy(ref msg, ref entity, ref lth);
            Copy(ref msg, ref orient, ref lth);
            Copy(ref msg, ref mx, ref lth);
            Copy(ref msg, ref my, ref lth);
            Copy(ref msg, ref rel, ref lth);
            Copy(ref msg, ref up, ref lth);
            Copy(ref msg, ref forward, ref lth);
            Copy(ref msg, ref scale, ref lth);
            Copy(ref msg, ref encode, ref lth);
            return msg;
        }
        private byte[] Encode(Vector3D vec)
        {
            byte[] x = BitConverter.GetBytes(vec.X);
            byte[] y = BitConverter.GetBytes(vec.Y);
            byte[] z = BitConverter.GetBytes(vec.Z);
            byte[] retval = new byte[x.Length + y.Length + z.Length];
            x.CopyTo(retval, 0);
            y.CopyTo(retval, x.Length);
            z.CopyTo(retval, x.Length + y.Length);
            return retval;
        }
        private void Copy(ref byte[] message, ref byte[] item, ref int lth)
        {
            item.CopyTo(message, lth);
            lth += item.Length;
        }
        /// <summary>
        /// Call when done.
        /// </summary>
        public void Close()
        {
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(HUDAPI_RECEIVE, callback);//remove
        }
    }
}