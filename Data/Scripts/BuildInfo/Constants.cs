using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.Game;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo
{
    public class Constants : ModComponent
    {
        public const int MOD_VERSION = 2; // notifies player of notable changes and links them to workshop's changelog.

        public readonly Vector2 BLOCKINFO_SIZE = new Vector2(0.02164f, 0.00076f);
        public const float ASPECT_RATIO_54_FIX = 0.938f;
        public const float BLOCKINFO_TEXT_PADDING = 0.001f;

        public const int TICKS_PER_SECOND = 60;

        public readonly MyDefinitionId COMPUTER_COMPONENT_ID = new MyDefinitionId(typeof(MyObjectBuilder_Component), MyStringHash.GetOrCompute("Computer")); // HACK: this is what the game uses for determining if a block can have ownership

        public const bool BLOCKPICKER_IN_MP = true;
        public const string BLOCKPICKER_DISABLED_CONFIG = "NOTE: This feature is disabled in MP because of issues, see: https://support.keenswh.com/spaceengineers/general/topic/187-2-modapi-settoolbarslottoitem-causes-everyone-in-server-to-disconnect";
        public const string BLOCKPICKER_DISABLED_CHAT = "Pick block feature disabled in MP because of issues, see workshop page for details.";

        public static bool EXPORT_VANILLA_BLOCKS = false; // used for exporting vanilla block IDs for AnalyseShip's hardcoded list.

        public readonly HashSet<MyObjectBuilderType> DEFAULT_ALLOWED_TYPES = new HashSet<MyObjectBuilderType>(MyObjectBuilderType.Comparer) // used in inventory formatting if type argument is null
        {
            typeof(MyObjectBuilder_Ore),
            typeof(MyObjectBuilder_Ingot),
            typeof(MyObjectBuilder_Component)
        };

        public readonly MyStringId[] CONTROL_SLOTS = new MyStringId[]
        {
            MyControlsSpace.SLOT0,
            MyControlsSpace.SLOT1,
            MyControlsSpace.SLOT2,
            MyControlsSpace.SLOT3,
            MyControlsSpace.SLOT4,
            MyControlsSpace.SLOT5,
            MyControlsSpace.SLOT6,
            MyControlsSpace.SLOT7,
            MyControlsSpace.SLOT8,
            MyControlsSpace.SLOT9,
        };

        public Constants(BuildInfoMod main) : base(main)
        {
            ComputeCharacterSizes();
            ComputeResourceGroups();
        }

        protected override void RegisterComponent()
        {
        }

        protected override void UnregisterComponent()
        {
        }

        #region Resource group priorities
        public int resourceSinkGroups = 0;
        public int resourceSourceGroups = 0;
        public readonly Dictionary<MyStringHash, ResourceGroupData> resourceGroupPriority
                  = new Dictionary<MyStringHash, ResourceGroupData>(MyStringHash.Comparer);

        private void ComputeResourceGroups()
        {
            resourceGroupPriority.Clear();
            resourceSourceGroups = 0;
            resourceSinkGroups = 0;

            // from MyResourceDistributorComponent.InitializeMappings()
            var groupDefs = MyDefinitionManager.Static.GetDefinitionsOfType<MyResourceDistributionGroupDefinition>();
            var orderedGroupsEnumerable = groupDefs.OrderBy((def) => def.Priority);

            // compact priorities into an ordered number.
            foreach(var group in orderedGroupsEnumerable)
            {
                int priority = 0;

                if(group.IsSource)
                {
                    resourceSourceGroups++;
                    priority = resourceSourceGroups;
                }
                else
                {
                    resourceSinkGroups++;
                    priority = resourceSinkGroups;
                }

                resourceGroupPriority.Add(group.Id.SubtypeId, new ResourceGroupData(group, priority));
            }
        }

        public struct ResourceGroupData
        {
            public readonly MyResourceDistributionGroupDefinition Def;
            public readonly int Priority;

            public ResourceGroupData(MyResourceDistributionGroupDefinition def, int priority)
            {
                Def = def;
                Priority = priority;
            }
        }
        #endregion Resource group priorities

        #region Character sizes for padding HUD notifications
        public readonly Dictionary<char, int> CharSize = new Dictionary<char, int>();

        void ComputeCharacterSizes()
        {
            //ParseFonts();

            CharSize.Clear();

            AddCharsSize(0, "\n\r");
            AddCharsSize(2 * 8, "\t"); // 2x space

            // generated from fonts/white_shadow/FontDataPA.xml+FontDataCH.xml
            AddCharsSize(6, "'|¦ˉ‘’‚");
            AddCharsSize(7, "ј");
            AddCharsSize(8, " !I`ijl ¡¨¯´¸ÌÍÎÏìíîïĨĩĪīĮįİıĵĺļľłˆˇ˘˙˚˛˜˝ІЇії‹›∙！");
            AddCharsSize(9, "(),.1:;[]ft{}·ţťŧț（）：《》，。、；【】");
            AddCharsSize(10, "\"-rª­ºŀŕŗř");
            AddCharsSize(11, "*²³¹");
            AddCharsSize(12, "\\°“”„");
            AddCharsSize(13, "ґ");
            AddCharsSize(14, "/ĳтэє");
            AddCharsSize(15, "L_vx«»ĹĻĽĿŁГгзлхчҐ–•");
            AddCharsSize(16, "7?Jcz¢¿çćĉċčĴźżžЃЈЧавийнопсъьѓѕќ？");
            AddCharsSize(17, "3FKTabdeghknopqsuy£µÝàáâãäåèéêëðñòóôõöøùúûüýþÿāăąďđēĕėęěĝğġģĥħĶķńņňŉōŏőśŝşšŢŤŦũūŭůűųŶŷŸșȚЎЗКЛбдекруцяёђћўџ");
            AddCharsSize(18, "+<=>E^~¬±¶ÈÉÊË×÷ĒĔĖĘĚЄЏЕНЭ−");
            AddCharsSize(19, "#0245689CXZ¤¥ÇßĆĈĊČŹŻŽƒЁЌАБВДИЙПРСТУХЬ€");
            AddCharsSize(20, "$&GHPUVY§ÙÚÛÜÞĀĜĞĠĢĤĦŨŪŬŮŰŲОФЦЪЯжы†‡￥");
            AddCharsSize(21, "ABDNOQRSÀÁÂÃÄÅÐÑÒÓÔÕÖØĂĄĎĐŃŅŇŌŎŐŔŖŘŚŜŞŠȘЅЊЖф□");
            AddCharsSize(22, "љ");
            AddCharsSize(23, "ю");
            AddCharsSize(24, "%ĲЫ");
            AddCharsSize(25, "@©®мшњ");
            AddCharsSize(26, "MМШ");
            AddCharsSize(27, "mw¼ŵЮщ");
            AddCharsSize(28, "¾æœЉ");
            AddCharsSize(29, "½Щ");
            AddCharsSize(30, "™");
            AddCharsSize(31, "WÆŒŴ—…‰");
            AddCharsSize(32, "");
            AddCharsSize(33, "一丁七万丈三上下丌不与丐丑专且丕世丘丙业丛东丝丞丢两严丧丨个丫丬中丰串临丶丸丹为主丽举丿乃久乇么义之乌乍乎乏乐乒乓乔乖乘乙乜九乞也习乡书乩买乱乳乾了予争事二亍于亏云互亓五井亘亚些亟亠亡亢交亥亦产亨亩享京亭亮亲亳亵人亻亿什仁仂仃仄仅仆仇仉今介仍从仑仓仔仕他仗付仙仝仞仟仡代令以仨仪仫们仰仲仳仵件价任份仿企伉伊伍伎伏伐休众优伙会伛伞伟传伢伤伥伦伧伪伫伯估伲伴伶伸伺似伽佃但位低住佐佑体何佗佘余佚佛作佝佞佟你佣佤佥佧佩佬佯佰佳佴佶佻佼佾使侃侄侈侉例侍侏侑侔侗供依侠侣侥侦侧侨侩侪侬侮侯侵便促俄俅俊俎俏俐俑俗俘俚俜保俞俟信俣俦俨俩俪俭修俯俱俳俸俺俾倌倍倏倒倔倘候倚倜借倡倥倦倨倩倪倬倭倮债值倾偃假偈偌偎偏偕做停健偬偶偷偻偾偿傀傅傈傍傣傥傧储傩催傲傺傻像僖僚僦僧僬僭僮僳僵僻儆儇儋儒儡儿兀允元兄充兆先光克免兑兔兕兖党兜兢入全八公六兮兰共关兴兵其具典兹养兼兽冀冁冂内冈冉册再冒冕冖冗写军农冠冢冤冥冫冬冯冰冱冲决况冶冷冻冼冽净凄准凇凉凋凌减凑凛凝几凡凤凫凭凯凰凳凵凶凸凹出击凼函凿刀刁刂刃分切刈刊刍刎刑划刖列刘则刚创初删判刨利别刭刮到刳制刷券刹刺刻刽刿剀剁剂剃削剌前剐剑剔剖剜剞剡剥剧剩剪副割剽剿劁劂劈劐劓力劝办功加务劢劣动助努劫劬劭励劲劳劾势勃勇勉勋勐勒勖勘募勤勰勹勺勾勿匀包匆匈匍匏匐匕化北匙匚匝匠匡匣匦匪匮匹区医匾匿十千卅升午卉半华协卑卒卓单卖南博卜卞卟占卡卢卣卤卦卧卩卫卮卯印危即却卵卷卸卺卿厂厄厅历厉压厌厍厕厘厚厝原厢厣厥厦厨厩厮厶去县叁参又叉及友双反发叔取受变叙叛叟叠口古句另叨叩只叫召叭叮可台叱史右叵叶号司叹叻叼叽吁吃各吆合吉吊同名后吏吐向吒吓吕吖吗君吝吞吟吠吡吣否吧吨吩含听吭吮启吱吲吴吵吸吹吻吼吾呀呃外夕夔夏复备处夂壹壶壳声壮壬士壤壕壑壅壁墼墩墨墟增墚墙墓墒墉墅境墁墀塾塬填塥塞塘塔塑塍塌塄堵堰堪堤堡堠堞堙堕堑堍堋堇堆堂堀埽基培埸埴埯埭埤埠域埝埚埙埘埕埔埒埏城埋埃埂垸垴垲垮垭垫垩垧垦垤垣垢垡垠垛垓垒垌型垆垅垄垃垂坼坻坷坶坳坯坭坫坪坩坨坦坤坡坠坟坞坝坜坛坚块坑坐坏坎坍坌坊均坂址圾圻场圹圳地圯圮圭圬圪圩在圣土圜圊圉圈圆圄圃囿图国固囹囵围囱困园囫囤团囡因囟回囝四囚囗囔囊嚼嚷嚯嚣嚓嚏嚎嚆嚅噼噻噶噱噬噫噪噩器噤噢噜噙噘噗噔噎噍噌嘿嘻嘹嘶嘴嘲嘱嘭嘬嘧嘤嘣嘟嘞嘛嘘嘏嘎嘌嘉嘈嘁嘀嗾嗽嗷嗵嗳嗲嗯嗬嗫嗪嗨嗦嗥嗤嗣嗡嗟嗝嗜嗖嗔嗓嗒嗑嗍嗌嗉嗅嗄喾喽喻喹喷喵喳喱喧喟喝喜喙喘喔喑喏喋喊喉喈喇善喃喂喁喀啾啼啻啸啷啶啵啮啭啬啪啧啦啥啤啡啜啖啕啐啊啉商啄啃啁唿唾唼唷唳唱唰唯售唬唪唧唤唣唢唠唛唔唑唐唏唉唇唆唁哿哽哼哺哳哲哮哭哪哩哨哧哦哥哟哞哝哜哚哙哗哕哔哓哒哑哐哏哎响哌哉哈哇哆哄哂品哀咿咽咻咸咴咳咱咯咭咬咫咪咩咨咧咦咤咣咝咛咚咙咖咕咔咒咐咏咎和咋咆咄咂咀命呼呻呸呷呶呵味呲呱周呦呤呢呜呛呙员呗呖呕呔呓呒呐呋告呈呆悻悸悴悲悱悯悭悬悫您悦患悠悟悝悛悚悖悔悒悍悌悉悄悃恿恽恼恻恺恹恸恶恳恰息恭恬恫恪恩恨恧恤恣恢恝恚恙恕恒恐恍恋恃恂恁怿怼总怵怯怫怪怩怨性怦急怡怠思怜怛怙怖怕怔怒怏怎怍怊怆怅怄怃怂态怀忿忾忽忻忸念忱忮忭快忪忧忤忡忠忝忙忘志忖忒忑忐忏忍忌忉忆必忄心徽徼德徵微徭循徨御徜徙徘得徕徒徐後律徊徉很徇待径徂征往彼彻役彷彳影彰彭彬彪彩彦彤形彡彝彘彗彖录当归彐彀弼强弹弱弯弭弪弩弧弦弥张弟弛弘弗引弓弑式弋弊弈弄弃异弁开廿廾建廷延廴廪廨廛廖廓廒廑廊廉庾庹庸康庶庵庳庭座度庥庠废庞府庚庙店庖底应库庑庐序庋床庇庆庄庀广幽幼幻幺幸并年平干幢幡幞幛幕幔幌幅幄幂帽帼帻常帷帱帮席帧带帝帜帛帚帙帘帖帕帔帑帐帏希师帆帅布市币巾巽巷巴巳已己巯差巫巩巨巧左工巢巡州川巛巍巅嶷嶝嶙嶂嵴嵯嵬嵫嵩嵝嵛嵘嵌嵋嵊嵇崾崽崴崮崭崩崧崦崤崞崛崖崔崎崇崆崃崂峻峰峭峪峨峦峥峤峡峙峒峋峄峁岿岽岸岷岵岳岱岭岬岫岩岣岢岜岛岚岙岘岗岖岔岑岐岍岌岈岂岁屿屺屹山屯屮屦履屣屡屠属屙展屑屐屏屎屋届屉屈居层屁局尿尾尽尼尻尺尹尸尴就尬尧尥尤尢尝尜尚尘尖尕尔少小尊尉将射封寿导寻寺对寸寰寮寨寥寤寡察寞寝寓寒寐富寇密寅寄寂宿宾宽容宸家宵宴害宰宫宪宦宥室宣客审宠实宝宜宛定宙官宗宕宓宏完宋安守宇宅宄它宁宀孽孺孵孳孱孰孬孪孩学孥孤季孢孟孝孜孛孚孙存字孕孔孓孑子孀嬷嬴嬲嬗嬖嬉嫱嫫嫩嫦嫣嫡嫠嫜嫘嫖嫔嫒嫌嫉嫂嫁媾媸媵媳媲媪媛媚媒婿婺婷婶婵婴婪婧婢婚婕婊婉婆婀娼娶娴娲娱娩娥娣娠娟娜娘娓娑娌娉娈娇娆娅娄娃威姿姻姹姬姨姥姣姝姜姚姘姗委姓姒姑姐始姊姆妾妻妹妲妯妮妫妪妩妨妥妤妣妞妙妗妖妓妒妍妊妈妇妆妄妃如妁好她奸奶奴女奥奢奠奚奘套奖奕奔契奏奎奋奉奈奇奄奂奁夼夺夹夸夷头失夯央夭夫太天大夥夤够夜多夙栾栽格根核样栳栲株栩校栝栗栖栓树栏栎栌栋栊栉栈标栅栀柿柽柴柳柱柰柯柬柩查柢柠柞柝柜柚柙柘柔染柒柑某柏柄柃柁枸枷架枵枳枰枯枭枫枪枨枧枥枣枢枞枝果枚枘林枕析枋枉枇构极板松杼杷杵杳杲杰杯杭杪杩杨来条杠束杞杜杖杓村材杏李杌杉杈杆权杂杀朽机朵朴朱术札本末未木朦期朝望朗朕朔朐服朋朊有月最替曾曼曹曷更曳曲曰曩曦曝曜曛曙暾暹暴暮暨暧暝暗暖暑暌暇暄暂晾智晷晶晴晰景普晨晦晤晡晟晚晗晖晕晔晓晒晏晌晋晃晁显昼昶昵昴昱是昭昨昧春映星昝昙昕昔易昏明昌昊昆昃昂昀旺旷时旱旰旯旮旭旬早旨旧旦日既无旗旖旒族旎旌旋旆旅旄旃旁施於方新斯断斫斩斧斥斤斡斟斜斛料斗斓斑斐斌斋文敷整敲数敬敫敦散敢敞敝敛教敖敕救敏敌敉效故政放攻改攸收攵攴支攮攫攥攘攒攉攀擦擤擢擞擘擗擒擐擎操擅擂擀撼撺撸撷撵撰撮播撬撩撤撞撙撖撕撒撑撇撅撄撂摺摹摸摭摩摧摞摘摔摒摊摈摇摆摅摄摁搿搽携搴搭搬搪搦搡搠搞搜搛搔搓搐搏搌搋搅搂搁搀揿揽揸揶援揲揭揪揩揣握揠揞揖插提描揎揍揉揆揄掾掼掺掸掷掴掳掰掮掭掬措掩推控接掣探掠掘掖排掐掏掎掌掊掉授掇掂掀捻捺捷捶捱据捭捩捧捣换捡损捞捕捐捏捎捍捌捋捉捆捅捃捂挽挺挹挲振挫挪挨挥挤挣挢挡挠挟挞挝挛挚挖挑挎按挈指挂持拿拾拽拼拷拶拴拳拱拯拮拭括择拨拧拦拥拣拢拟拜招拚拙拘拗拖拔拓拒拐拎拍拌拊拉拈拇拆担拄拂抿抽押抻抹抵抱抬披抨报护抢抡抠抟抛抚折抗抖投抓抒抑把抉抄技承找扼批扶扳扰扯扮扭扬扫扪扩执扦扣扛托扔打扒扑扎才扌手扉扈扇扃扁所房戾戽户戴戳戮戬截戥戤戢戡戟戛戚战戗或戕戒我成戏戎戍戌戋戊戈戆懿懵懦懔懒懑懋懊懈懂憾憷憬憩憨憧憝憔憎憋慷慵慰慨慧慢慝慕慑慎慌慊慈愿愫愧愦愤愣愠感愚愕意愎愍愉愈愆愁愀惺惹惶惴想惰惯惮惭惬惫惩惨惧惦惠惟惝惜惚惘惕惑惋惊惆情悼煊煅然焱焰焯焦焚焙焘焖焕焓焐焊焉烽烹烷烯热烬烫烩烨烧烦烤烟烛烙烘烊烈烃烂烁烀炽炼炻点炸炷炳炱炯炮炭炬炫炝炜炙炖炕炔炒炎炊炉炅炀灿灾灼灸灶灵灰灯灭灬火灞灏灌瀹瀵瀣瀛瀚瀑濯濮濡濠濞濒濑濉濂激澹澶澳澧澡澜澎澍澌澉澈澄潼潺潸潴潲潮潭潦潢潞潜潘潍潋潇潆漾漶漳漱漯漭漫漪漩漤漠漕演漓漏漉漆漂滹滴滩滨滦滥滤滢满滠滟滞滚滗滕滔滓滑滏滋滇滂滁溽溻溺溷溶溴溲溱溯溪溧溥溢溟溜溘源溏溉溆溅溃湿湾湮湫湟湛湘湖湔湓湎湍湄湃渺游渴渲港渭渫温渥渤渣渡渠渝渚渗渖渔渑渐渎渍渌渊清淼添淹混淳深淮淬淫淦淤淡淠淞淝淙淘淖淑淌淋淇淆淅淄淀涿涸涵液涯涮涫涪涩涨涧润涤涣涡涠涟涞涝涛涕涔涓涑涎涌涉消涅涂浼浸海浴浯浮浪浩浦浣浠浞浜浚浙浔浓浒浑浏济浍测浊浈浇浆浅浃流派洽洼活洹洵洳洲洱洮洫洪洧津洞洛洚洙洗洒洎洌洋洇洄洁泾泽泼泻泺泸泷泶泵泳泱泰泯泮泫泪注泥泣波泡泠泞泛泗泖法泔泓泐泌泊泉泅泄沿沾沽沼治油沸河沲沱沮沭沫沪沩沧沦沥沤沣没沟沛沙沔沓沐沏沌沉沈沆沅沃沂沁汾汽汹汶汴汲汰汪汩汨汤污池江汞汝汜汛汗汕汔汐汊汉汇汆求汁汀氽永氵水氲氰氯氮氪氩氨氧氦氤氢氡氟氛氚氙氘氖氕气氓民氐氏氍氇氆氅毽毹毵毳毯毫毪毡毛毙毗毖毕比毓毒每母毋毅毂毁殿殷段殴殳殪殡殛殚殖殓殒殍残殊殉殇殆殄殃殂殁歼死歹歪歧武步此正止歙歌歉歇歆歃款欺欹欷欲欧欤欣欢次欠檬檫檩檠檗檑檐檎檄檀橼橹橱橥橡橛橙橘橐橇橄樾樽樵樱樯横樨模樟樘樗樊槿槽槲槭槠槟槛槔槐槎槌槊槁榻榷榴榱榭榫榨榧榜榛榘榕榔榍榉榈榇榆榄概榀楼楹楸楷楱楮楫楦楣楠楞楝楚楗楔楂椿椽椹椴椰椭椤椠椟椒椐椎植椋椅椁棼棺棹棵棱棰森棣棠棚棘棕棒棍棋棉棂检梵梳械梯梭梨梧梦梢梗梓梏梆梅梃梁桷桶桴桫桩桨桧桦桥桤档桢桡桠桕桔桓桑桐桎桌桊桉案框桅桄桃桂桁桀秽移秸称积秭秫秩秧秦秤秣租秘秕秒科种秋秉秆秃私秀禾禽离禺禹禳禧禚福禊禅禄禁禀祺祸祷祯祭票祧祥祢祠祟神祝祜祛祚祗祖祓祉祈祆祁祀社礼礻示礴礤礞礓礅礁磺磷磴磲磬磨磙磕磔磐磋磊磉磅磁碾碹碴碳碲碱碰碧碥碣碡碟碜碛碚碘碗碓碑碎碍碌碉碇硼硷确硭硬硫硪硝硗硖硕硒硐硎硌硇硅础砾砼砻砺砹砸砷破砰砭砬砩砧砦砥砣砟砝砜砚砘砗砖研砒砑砍砌砉砂码砀矿矾矽矸矶石矮短矬矫矩矧知矣矢矜矛矗矍瞿瞽瞻瞵瞳瞰瞬瞪瞩瞧瞥瞢瞠瞟瞒瞑瞎瞍瞌瞅瞄瞀睿睾睽睹睬睫睨睦睥督睢睡睛睚睑睐睇睃睁着眼眺眸眷眶眵眯眭眩眨眦眢眠真眚眙眍看眉眈眇眄省盾盼盹相直盲盱盯目盥盟盛盘盗盖盔盒监盐盏盎盍益盈盆盅盂皿皴皲皱皮皤皙皖皓皑皎皋皈皇皆的皂百白登癸癯癫癣癞癜癖癔癍癌癃癀瘿瘾瘼瘸瘵瘴瘳瘰瘭瘫瘪瘩瘦瘥瘤瘢瘠瘟瘛瘙瘘瘗瘕瘐瘌瘊瘅瘃瘁瘀痿痼痹痴痱痰痫痪痨痧痦痤痣痢痞痛痘痖痕痔痒痍痊痉痈症病痄痃痂疾疽疼疹疸疵疴疳疲疱疰疯疮疬疫疥疤疣疡疠疟疝疚疙疗疖疔疒疑疏疋疆疃畿畹畸畴畲番畦略畜畛畚留畔畏畎界畋畈畅畀甾画町甸男电申甲由田甯甭甬甫甩用甥生甜甚甙甘甓甑甏甍甄瓿瓷瓶瓴瓯瓮瓦瓤瓣瓢瓠瓞瓜瓒璺璩璨璧璞璜璐璎璋璇璃璁璀瑾瑷瑶瑰瑭瑟瑞瑜瑛瑚瑙瑗瑕瑁琼琶琵琴琳琰琮琬琪琨琦琥琢琛琚琐琏琊琉理琅球珲班珩珧珥珠珞珙珑珐珏珍珊珉珈珂珀玻玺玷玳玲现环玮玫玩玢玟玛玖玑玎王玉率玄獾獯獭獬獠獗獒獐獍猿猾猹猸猷猴猱献猬猫猪猩猥猢猡猞猝猜猛猗猖猕猓猎猊猃猁狼狻狺狸狷狴狳狲狱狰狯狮狭独狩狨狡狠狞狙狗狒狐狎狍狈狄狃狂狁犹犸犷状犴犰犯犭犬犟犒犏犍犋犊犄犁犀牿牾牺特牵牲牯牮物牧牦牢牡牟牝牛牙牖牒牍牌版片爿爽爻爹爸爷父爵爱爰爬爪爨爝爆燹燮燧燥燠燕燔燎燃熹熵熳熬熨熠熟熙熘熔熏熊熄煽煺煸煳煲煮煨照煦煤煞煜煎煌艹艴艳色艰良艮艨艟艚艘艏艋艉艇艄舾舻船舸舷舶舵舴舳舱舰舯舭般舫航舨舣舢舡舟舞舜舛舔舒舐舍舌舆舅舄舂舁舀臾臼臻致至臭臬自臧臣臌臊臆臃臂臁臀膻膺膳膪膨膦膣膝膜膛膘膑膏膊膈膂膀腿腾腽腼腻腺腹腴腱腰腮腭腩腧腥腠腚腙腕腔腓腑腐腌腋腊腈腆脾脸脶脲脱脯脬脞脚脘脖脔脓脒脑脐脏脎脍脊脉脆脂能胼胺胸胶胴胳胲胱胰胯胭胬胫胪胩胨胧胥胤胡胞胝胜胛胚胙胗胖胎胍背胆胄胃胂胁胀肿肾肽肼肺肷肴育肱肯肮肭肫肪肩肥肤肢股肠肟肝肜肛肚肘肖肓肌肋肉肇肆肄肃肀聿聱聪聩聚聘联聒聍职聋聊聆聃聂耿耽耻耸耷耶耵耳耱耪耩耨耧耦耥耢耠耜耙耘耗耖耕耔耒耐耍而耋耆者耄考老耀翼翻翳翱翰翮翩翦翥翡翠翟翘翕翔翎翌翊翅翁羿羽羼羹羸羲羰羯羧群羡羟羞羝羚羔美羌羊羁罾罹罴署罱置罪罩罨罢罡罟罚罘罗罕罔网罐罅罄罂缺缸缶缵缴缳缲缱缰缯缮缭缬缫缪缩缨缧缦缥缤缣缢缡缠缟缝缜缛缚缙缘缗编缕缔缓缒缑缏缎缍缌缋缉缈缇缆缅缄缃缂缁缀绿绾绽综绻绺绸绷绶绵维绳绲绱绰绯绮续绫绪绩绨继绦绥绣绢绡绠统绞绝络绛绚给绘绗绕绔结绒绑绐经绎绍绌绋绊绉终织细绅组练绂绁绀线纾纽纺纹纸纷纶纵纳纲纱纰纯纭纬纫纪纩纨级约纥纤纣红纡纠纟纛纂繇繁縻綮綦絷絮累紫紧索素紊系糸糯糨糠糟糜糙糗糖糕糍糌糊糈糇糅糁精粽粼粹粳粲粱粮粪粥粤粢粟粞粝粜粘粗粕粒粑粉籽籼类籴米籍籁籀簿簸簪簧簦簟簖簏簌簋簇篾篼篷篱篮篪篦篥篡篝篚篙篓篑篌篇篆篁箸箴箱箭箬箫箪箩箨箧箦箢管箝箜算箕箔箐箍箅简签筻筹筷筵筲筱筮筢筠筝筛筚筘策答筒筑筐筏筌筋等筇筅笾笼笺笸笳笱笮第笫笪笨符笥笤笠笞笛笙笕笔笑笏笋笊笈笆笄笃竿竽竺竹端竭竦童竣章竟竞站竖立窿窳窭窬窨窦窥窠窟窝窜窘窗窖窕窒窑窍窈窆窄窃突窀穿空穹穸穷究穴穰穗穑穆稿稽稼稻稹稷稳稣稠稞稚稗稔税稍程稆稃稂稀诬诫诩诨诧详该诤诣询诡诠诟诞话诜诛诚诙诘诗诖试诔诓诒译诏诎词诌诋诊诉诈识诅评诃诂证诀访设讽讼论讹许讷讶讵讴讳讲记讯议训讫讪让讨讧讦讥认讣订计讠譬警謦謇誓誊誉詹詈訾訇言觳觯觫触觥解觞觜觚觖角觑觐觏觎觌觋觊觉览觇视觅规观见覆覃要西襻襦襟襞襄襁褶褴褰褫褪褥褡褛褚褙褓褒褐褊褂裾裼裹裸裴裳裱裰裨裥裤裣裢裟裙裘裕裔裒裎裉裆装裂裁袼袷袱袭被袤袢袜袖袒袍袋袈袅袄袂袁衿衾衽衷衲衰衮衬衫衩表补衤衣衢衡衙街衔衍行衅衄血蠼蠹蠲蠢蠡蠛蠖蠕蠓蠊蠃蟾蟹蟮蟪蟥蟠蟛蟓蟒蟑蟋蟊蟆蟀螽螺螵螳螯螭螬螫螨螟螗螓融螋螈螅螃螂蝾蝽蝼蝻蝶蝴蝰蝮蝥蝤蝣蝠蝙蝗蝓蝎蝌蝉蝈蝇蜿蜾蜻蜷蜴蜱蜮蜩蜥蜣蜢蜡蜞蜜蜚蜘蜗蜕蜓蜒蜍蜊蜉蜈蜇蜃蜂蜀蛾蛹蛸蛴蛳蛲蛱蛰蛮蛭蛩蛤蛟蛞蛛蛙蛘蛔蛑蛐蛏蛎蛋蛊蛉蛇蛆蛄蛀蚺蚶蚵蚴蚱蚰蚯蚬蚪蚩蚨蚧蚤蚣蚝蚜蚕蚓蚍蚌蚋蚊蚂蚁蚀虿虾虽虼虻虺虹虱虮虬虫虢虞虚虔虑虐虏虎虍蘼蘸蘩蘧蘖蘑蘅藿藻藩藤藜藕藓藐藏藉藁薹薷薰薯薮薪薨薤薜薛薏薇薅薄蕾蕻蕺蕹蕴蕲蕨蕤蕞蕙蕖蕊蕉蕈蕃蔽蔼蔻蔺蔹蔸蔷蔬蔫蔡蔟蔚蔗蔓蔑蔌蓿蓼蓰蓬蓦蓥蓣蓠蓟蓝蓖蓓蓑蓐蓍蓊蓉蓄蓁蒿蒽蒺蒹蒸蒴蒲蒯蒡蒜蒙蒗蒎蒌蒋蒉蒈蒇蒂葺葸葶葵葳葱葭葬葫葩董葡葜葛葚葙著葑葆落萼萸萱萨萧萦营萤萝萜萘萑萏萎萍萌萋萆萄萃萁菽菹菸菲菱菰菪菩菥菡菠菟菝菜菘菖菔菏菌菊菇菅菁菀莽莼莺莹莸获莶莴莳莲莱莰莫莪莩莨莠莞莜莛莘莓莒莎莉莆莅荽荼荻荸荷药荮荭荬荫荪荩荨荧荦荥荤荣荡荠荟荞荜荛荚荔荒荑荐荏草荇荆荃荀茼茺茹茸茶茵茴茳茱茯茭茬茫茨茧茜茛茚茗茕茔茑茏茎茌茉茈茇茆茅茄范茂茁苻苹苷苴英苯苫苦若苤苣苡苠苟苞苜苛苘苗苕苔苓苒苑苏苎苍苌苋苊苈苇苄苁芾芽芹芸芷芴芳花芰芯芮芭芬芫芪芩芨芦芥芤芡芟芝芜芙芘芗芒芑芏芎芍芋芊芈芄节艿艾艽艺锊锉锈锇锆锅锄锃锂锁销铿链铽铼铺铹铸铷银铵铴铳铲铱铰铯铮铭铬铫铪铩铨铧铥铤铣铢铡铠铟铞铝铜铛铙铘铗铖铕铒铑铐铎铍铌铋铊铉铈铆铅铄铃铂铁铀钿钾钽钼钻钺钹钸钷钶钵钴钳钲钱钰钯钮钭钬钫钪钩钨钧钦钥钤钣钢钡钠钟钞钝钜钛钚钙钗钕钔钓钒钐钏钎钍钌钋钊钉针钇钆钅鑫鐾鏖鏊鎏鍪錾鋈銮銎鉴釜金量野重里释釉采醺醵醴醯醮醭醪醣醢醛醚醒醑醐醍醌醋醉醇醅酿酾酽酹酸酷酶酵酴酲酱酰酯酮酬酪酩酥酤酣酢酡酞酝酚酗酒酐酏酎配酌酋酊酉酆酃鄹鄱鄯鄣鄢鄞鄙鄄鄂郾都郸郴郯郭郫部郧郦郢郡郝郜郛郗郓郑郐郏郎郊郇郅郄郁邾邻邺邹邸邶邵邴邳邱邰邯邮邬邪邦那邢邡邝邛邙邗邕邓邑邋邈邃邂邀避遽遵遴遮遭遨遥遣遢遛遘遗道遒遑遐遏遍遇遄遂遁逾逼逻逸逶逵逯逮逭逦逢逡造速逞逝逛通逗逖途递逑逐透逍逋逊选逆逅逄逃适送退追迹迸迷迳述迮迭迫迪迩迨迦迥迤迢迟连违远进这还迕返迓近运迎迈过迅迄迂迁达辽边辶辱辰辫辩辨辣辟辞辜辛辚辙辘辗辖辕辔输辑辐辏辎辍辋辊辉辈辇辆辅辄较辂辁轿轾载轼轻轺轹轸轷轶轵轴轳轲轱轰软轮轭转轫轩轨轧车軎躺躲躯躬身躞躜躔躐躏躇躅躁蹿蹼蹶蹴蹲蹰蹯蹭蹬蹩蹦蹙蹒蹑蹋蹊蹉蹈蹇蹄蹂蹁蹀踽踺踹踵踱踯踮踬踪踩踣踢踟踞踝踔踏踌踊踉踅跽跻跺跹跸跷践跳路跬跫跪跨跤跣跟跞距跛跚跗跖跑跏跎跌跋跆跄跃趿趾趼趺趸趵趴足趱趣趟趔趑趋越超趄趁起赶赵赴赳走赭赫赧赦赤赣赢赡赠赞赝赜赛赚赙赘赖赕赔赓赐赏赎赍赌赋赊赉赈赇赆赅资赃赂赁赀贿贾贽贼贻贺费贸贷贶贵贴贳贲贱贰贯贮购贬贫贪贩质货账败贤责财贡负贞贝貘貔貌貊貉貅貂豺豹豸豳豫豪豢象豚豕豌豉豇豆豁谷谶谵谴谳谲谱谰谯谮谭谬谫谪谩谨谧谦谥谤谣谢谡谠谟谝谜谛谚谙谘谗谖谕谔谓谒谑谐谏谎谍谌谋谊谈谇谆谅谄调谂谁谀诿课诽诼读诺诹诸请诶诵说诳诲诱诰误诮语龠龟龛龚龙龌龋龊龉龈龇龆龅龄龃龀齿齑齐齄鼾鼽鼻鼹鼷鼯鼬鼢鼠鼙鼗鼓鼐鼎鼍鼋黾黼黻黹黯黪黩黧黥黢黠黟黝黜黛默黔黑黏黎黍黉黄麾麽麻麸麴麦麟麝麓麒麋麈麇麂鹿鹾鹳鹱鹰鹭鹬鹫鹪鹩鹨鹧鹦鹤鹣鹞鹜鹛鹚鹘鹗鹕鹑鹏鹎鹌鹋鹊鹉鹈鹇鹆鹅鹄鹃鹂鹁鸿鸾鸽鸺鸹鸸鸷鸶鸵鸳鸲鸱鸯鸭鸬鸫鸪鸩鸨鸦鸥鸣鸢鸡鸠鸟鳢鳟鳞鳝鳜鳙鳘鳗鳖鳕鳔鳓鳐鳏鳎鳍鳌鳋鳊鳇鳆鳅鳄鳃鲽鲼鲻鲺鲸鲷鲶鲵鲴鲳鲲鲱鲰鲮鲭鲫鲩鲨鲧鲦鲥鲤鲣鲢鲡鲠鲟鲞鲜鲛鲚鲕鲔鲒鲑鲐鲎鲍鲋鲈鲇鲆鲅鲂鲁鱿鱼魔魑魏魍魉魈魇魅魄魃魂魁鬼鬻鬲鬯鬣鬟鬓鬏鬈鬃髻髹髯髭髫髦髡髟高髓髑髌髋髅髂髁髀骼骺骸骷骶骱骰骨骧骥骤骣骢骡骠骟骞骝骜骛骚骘骗骖骓骒骑骐骏验骋骊骈骇骆骅骄骂骁骀驿驾驽驼驻驺驹驸驷驶驵驴驳驱驰驯驮驭马馨馥香馘馗首馕馔馓馒馑馐馏馍馋馊馈馇馆馅馄馁馀饿饽饼饺饷饶饵饴饲饱饰饯饮饭饬饫饪饩饨饧饥饣饕饔餮餐餍飨飧食飞飚飙飘飕飓飒飑风颧颦颥颤颢颡颠颟颞额颜颛颚题颗颖颔颓频颐颏颍颌颊颉颈颇领颅预颃颂颁颀顿顾顽顼须顺项顸顷顶页韶韵音韭韬韫韪韩韧韦鞴鞲鞯鞭鞫鞣鞠鞘鞔鞒鞑鞍鞋鞅靼靶靴靳革靥面靡靠非靛静靖靓青霾霹霸露霰霭霪霞霜霖霓霏霎霍霉霈震霆霄霁需雾雹雷零雳雯雪雩雨雠雕雒雏雎雍雌雉雇集雅雄雁雀难隽隼隹隶隳隰隧障隙隘隗隔隐随隍隋隈隆隅陷陶陵陴陲陬陪险陨陧除院陡陟陛陕陔限降陌陋陉陈陇陆际附陂陀阿阽阼阻阶阵阴阳防阱阮阪阢阡队阝阜阚阙阗阖阕阔阒阑阐阏阎阍阌阋阊阉阈阆阅阄阃阂阁阀闾闽闼闻闺闹闸闷闶闵间闳闲闱闰闯问闭闫闪闩门长镶镳镲镱镰镯镭镬镫镪镩镨镧镦镥镤镣镢镡镟镞镝镜镛镙镘镗镖镔镓镒镑镐镏镎镍镌镊镉镇镆镅镄镂镁镀锿锾锼锻锺锹锸锷锶锵锴锲锱锰锯键锭锬锫锪锩锨锦锥锤锣锢锡锟锞锝锛锚错锘锗锖锕锔锓锒锑锐锏锎锍锌锋");
            AddCharsSize(34, "");
            AddCharsSize(37, "");
            AddCharsSize(40, "");
            AddCharsSize(41, "");
            AddCharsSize(45, "");
            AddCharsSize(46, "");
            AddCharsSize(57, "");
        }

        void AddCharsSize(int size, string chars)
        {
            for(int i = 0; i < chars.Length; i++)
            {
                var chr = chars[i];

                int existingSize;
                if(CharSize.TryGetValue(chr, out existingSize))
                {
                    Log.Error($"Character '{chr.ToString()}' already exists for size: {existingSize.ToString()} --- line: AddCharsSize({size.ToString()}, {chars})");
                    continue;
                }

                CharSize.Add(chr, size);
            }
        }

        // parsing the game font files, for dev use only.
#if false
        private void ParseFonts()
        {
            Dictionary<int, HashSet<char>> charsBySize = new Dictionary<int, HashSet<char>>();

            if(!ParseFontFile("FontDataPA.xml", charsBySize) | !ParseFontFile("FontDataCH.xml", charsBySize))
                return;

            var sizes = charsBySize.Keys.ToList();
            sizes.Sort();

            var sb = new System.Text.StringBuilder();

            foreach(var size in sizes)
            {
                sb.Append("AddCharsSize(").Append(size).Append(", \"");

                var characters = charsBySize[size];

                foreach(var chr in characters)
                {
                    // escape characters used in code for simpler paste
                    if(chr == '\\')
                        sb.Append("\\\\");
                    else if(chr == '"')
                        sb.Append("\\\"");
                    else
                        sb.Append(chr);
                }

                sb.Append("\");").AppendLine();
            }

            using(var writer = Sandbox.ModAPI.MyAPIGateway.Utilities.WriteFileInLocalStorage("FontSizes.txt", typeof(Constants)))
            {
                writer.Write(sb);
            }
        }

        private bool ParseFontFile(string file, Dictionary<int, HashSet<char>> addTo)
        {
            if(!Sandbox.ModAPI.MyAPIGateway.Utilities.FileExistsInLocalStorage(file, typeof(Constants)))
                return false;

            using(var reader = Sandbox.ModAPI.MyAPIGateway.Utilities.ReadFileInLocalStorage(file, typeof(Constants)))
            {
                string line;

                while((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    if(line.Equals("</glyphs>"))
                        break;

                    if(!line.StartsWith("<glyph "))
                        continue;

                    var ch = GetInBetween(line, "ch=\"", "\"");
                    var aw = GetInBetween(line, "aw=\"", "\"");

                    ch = unescape.GetValueOrDefault(ch, ch); // stuff like &lt; to be converted to <, etc.

                    var character = ch[0]; // this is how SE is doing it too; some of their ch="" have 2 characters...
                    var width = int.Parse(aw);

                    HashSet<char> set;
                    if(!addTo.TryGetValue(width, out set))
                    {
                        set = new HashSet<char>();
                        addTo.Add(width, set);
                    }
                    set.Add(character);
                }
            }

            return true;
        }

        private string GetInBetween(string content, string start, string end, int startFrom = 0)
        {
            int startIndex = content.IndexOf(start, startFrom);

            if(startIndex == -1)
                throw new System.Exception($"Couldn't find '{start}' after {startFrom.ToString()} in line: {content}");

            startIndex += start.Length;
            int endIndex = content.IndexOf(end, startIndex);

            if(endIndex == -1)
                throw new System.Exception($"Couldn't find '{end}' after {startIndex.ToString()} in line: {content}");

            return content.Substring(startIndex, (endIndex - startIndex));
        }

        // workaround for HttpUtility.HtmlDecode() not being available
        Dictionary<string, string> unescape = new Dictionary<string, string>()
        {
            ["&lt;"] = "<",
            ["&gt;"] = ">",
            ["&quot;"] = "\"",
            ["&amp;"] = "&",
            ["&apos;"] = "'",
        };
#endif
        #endregion Character sizes for padding HUD notifications
    }
}
