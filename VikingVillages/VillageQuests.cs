using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The quests a village sends its vikings on, out of every player's sight, for what it can't get at home - picked
	/// by the client that runs the village and noted on its ZDO (see QuestRecord), so it carries on wherever the village
	/// is run from
	/// </summary>
	/// <remarks>
	/// A quest is picked from the village's goal (see VillageLife.CurrentGoalStep) when its next step is a quest the
	/// village can go on, with the vikings it can spare. Picked too late in the day, it waits for the morning. As it
	/// sets out, its vikings get what they take and leave the village (see VillageLife.QuestLeaveJob), each taken out of
	/// the world as it goes and handed to the server, which keeps them on the road (see VillageTravel) until they're due
	/// back, when they come in from where they left, carrying what they found. While they're gone, the village counts
	/// them as its own, so none come in their place (see AwayCount). A village that can't get out - walled in, or ringed
	/// by water - sends them from the spot nearest its edge they can reach, and they come back there (see Trapped)
	/// </remarks>
	internal static class VillageQuests
	{
		/// <summary>
		/// On a village's ZDO, its quest (see QuestRecord), and when its last quest came back, in ZNet time ticks
		/// </summary>
		public static readonly int QuestKey = "kriona.VikingVillages.villageQuest".GetStableHashCode();
		public static readonly int LastQuestKey = "kriona.VikingVillages.villageLastQuest".GetStableHashCode();

		/// <summary>
		/// On a living viking, the quest it set out on, that it was taken out of the world for it - so one that comes
		/// back knows it's back - and the village it set out from and the house there it calls home, which it's
		/// brought back to
		/// </summary>
		public static readonly int QuestIdKey = "kriona.VikingVillages.vikingQuest".GetStableHashCode();
		public static readonly int QuestGoneKey = "kriona.VikingVillages.vikingQuestGone".GetStableHashCode();
		public static readonly int QuestVillageKey = "kriona.VikingVillages.vikingQuestVillage".GetStableHashCode();
		public static readonly int QuestHomeKey = "kriona.VikingVillages.vikingQuestHome".GetStableHashCode();

		/// <summary>
		/// On a living viking, how its quest goes for it (see QuestOutcome), noted as it leaves and played out as it comes
		/// back; then how its last quest went, to talk about, and when it came back from it, in ZNet time ticks
		/// </summary>
		public static readonly int QuestOutcomeKey = "kriona.VikingVillages.vikingQuestOutcome".GetStableHashCode();
		public static readonly int StoryKey = "kriona.VikingVillages.vikingQuestStory".GetStableHashCode();
		public static readonly int BackAtKey = "kriona.VikingVillages.vikingQuestBackAt".GetStableHashCode();

		/// <summary>
		/// Seconds between looks at each village's quest this client runs
		/// </summary>
		private const float LookInterval = 10f;

		/// <summary>
		/// How many game days past when it was due a quest that hasn't come back is given up on
		/// </summary>
		private const float LostDays = 2f;

		/// <summary>
		/// How long whether a village is trapped is kept before it's looked at again, in seconds, and how many spots
		/// outside it are tried
		/// </summary>
		private const float TrappedKeep = 600f;
		private const int TrappedTries = 4;

		/// <summary>
		/// How far an enemy keeps a quest from setting out, in meters
		/// </summary>
		private const float EnemyReach = 30f;

		/// <summary>
		/// How far from where it stood a village's farm can be noted on a viking and still be it, in meters
		/// </summary>
		private const float FarmMatch = 16f;

		/// <summary>
		/// When this client next looks at each village's quest, by its ZDO, in Time.time seconds
		/// </summary>
		private static readonly Dictionary<ZDOID, float> nextLook = new Dictionary<ZDOID, float>();

		/// <summary>
		/// The roads to quests this client has asked the server for and not heard back about, by the key they're noted at
		/// on their village (see RouteKey) mixed with the village's ZDO
		/// </summary>
		private static readonly HashSet<int> routesAsked = new HashSet<int>();

		/// <summary>
		/// How long a road to a quest is kept on its village before the server is asked for it again, in game days - so
		/// portals built or broken since are taken into account
		/// </summary>
		private const double RouteKeepDays = 1.0;

		/// <summary>
		/// Gets the keys how long the road to a quest takes from a village is noted at on the village's ZDO, once the
		/// server has worked it out - there and back, in seconds, and when it was worked out, in ZNet time ticks
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <param name="there">The key of the time there</param>
		/// <param name="back">The key of the time back</param>
		/// <param name="at">The key of when it was worked out</param>
		private static void RouteKeys(QuestKind kind, string item, out int there, out int back, out int at)
		{
			string name = "kriona.VikingVillages.villageQuestRoad." + kind + "." + item;
			there = (name + ".there").GetStableHashCode();
			back = (name + ".back").GetStableHashCode();
			at = (name + ".at").GetStableHashCode();
		}

		/// <summary>
		/// Gets how long the road to a quest takes from a village, there and back - asking the server the first time, and
		/// again once what it said is RouteKeepDays old, going by what it said before until it answers
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <param name="back">Set to the time back, in seconds, or less than 0 while the server hasn't answered</param>
		/// <returns>The time there, in seconds, or less than 0 while the server hasn't answered</returns>
		private static float RouteSeconds(Village village, QuestKind kind, string item, out float back)
		{
			RouteKeys(kind, item, out int thereKey, out int backKey, out int atKey);
			float there = village.Zdo.GetFloat(thereKey, -1f);
			back = village.Zdo.GetFloat(backKey, -1f);
			long at = village.Zdo.GetLong(atKey, 0L);
			bool fresh = (there >= 0f && EnvMan.instance != null && NetTime.Now() - at < NetTime.Ticks((float)(EnvMan.instance.m_dayLengthSec * RouteKeepDays)));
			// Asked once per village and road until the server answers
			int asked = thereKey ^ village.Zdo.m_uid.GetHashCode();
			if (fresh || routesAsked.Contains(asked))
			{
				return(there);
			}
			routesAsked.Add(asked);
			ZDOID id = village.Zdo.m_uid;
			TravelPlanner.AskQuestRoute(village.Farm, VillageCooking.VillageBiome(village), kind, item, (float answerThere, float answerBack) =>
			{
				routesAsked.Remove(asked);
				ZDO zdo = ((ZDOMan.instance != null) ? ZDOMan.instance.GetZDO(id) : null);
				if (zdo != null && zdo.IsOwner())
				{
					zdo.Set(thereKey, answerThere);
					zdo.Set(backKey, answerBack);
					zdo.Set(atKey, NetTime.Now());
				}
			});
			return(there);
		}

		/// <summary>
		/// Gets how long the road to a quest takes from a village, there - asking the server the first time
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <returns>The time, in seconds, or less than 0 while the server hasn't answered</returns>
		private static float RouteSeconds(Village village, QuestKind kind, string item)
		{
			return(RouteSeconds(village, kind, item, out float _));
		}

		/// <summary>
		/// Whether each village is trapped, the spot its vikings leave from, and when that was worked out, by its ZDO
		/// </summary>
		private struct Exit
		{
			public bool Trapped;
			public Vector3 Spot;
			public float At;
		}
		private static readonly Dictionary<ZDOID, Exit> exits = new Dictionary<ZDOID, Exit>();

		/// <summary>
		/// Reads a village's quest
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The quest, or null when it has none</returns>
		public static QuestRecord Read(Village village)
		{
			return((village != null && village.Zdo != null) ? QuestRecord.Decode(village.Zdo.GetByteArray(QuestKey)) : null);
		}

		/// <summary>
		/// Notes a village's quest, or that it has none
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <param name="record">The quest, or null</param>
		private static void Write(Village village, QuestRecord record)
		{
			village.Zdo.Set(QuestKey, ((record != null) ? record.Encode() : new byte[0]));
		}

		/// <summary>
		/// Looks at the quest of each village this client runs, every LookInterval seconds - call it from the plugin's
		/// Update
		/// </summary>
		public static void Update()
		{
			if (ZNet.instance == null || ZNetScene.instance == null || EnvMan.instance == null || !ServerCheck.Active)
			{
				return;
			}
			foreach (Village village in Village.Loaded())
			{
				if (village.Zdo == null || !village.IsOwner() || (!village.Zdo.GetBool(VillageRespawn.PopulatedKey) && !village.IsFounded()))
				{
					continue;
				}
				ZDOID id = village.Zdo.m_uid;
				if (nextLook.TryGetValue(id, out float next) && Time.time < next)
				{
					continue;
				}
				nextLook[id] = Time.time + LookInterval;
				Look(village);
			}
		}

		/// <summary>
		/// Forgets what was worked out for the world being left
		/// </summary>
		public static void Forget()
		{
			nextLook.Clear();
			exits.Clear();
			routesAsked.Clear();
			VillageLife.ForgetFood();
		}

		/// <summary>
		/// Moves a village's quest along - setting out one waiting for the morning, noting it gone once its vikings have
		/// all left, giving up on one long overdue - or picks one when it has none
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		private static void Look(Village village)
		{
			QuestRecord record = Read(village);
			long now = NetTime.Now();
			if (record == null)
			{
				Pick(village);
				return;
			}
			switch (record.Phase)
			{
				case QuestPhase.Waiting:
					if (CanSetOutNow() && village.FindEnemy(EnemyReach) == null)
					{
						SetOut(village, record);
					}
					break;
				case QuestPhase.Leaving:
					if (!record.Members.Exists((string member) => FindMember(village, member) != null))
					{
						record.Phase = QuestPhase.Away;
						Write(village, record);
						Plugin.Log.LogInfo("Quest: " + record.Kind + " from the village at " + village.Farm.ToString("F0") + " has set out, due back " + DuePhrase(record));
					}
					break;
				case QuestPhase.Away:
					if (EnvMan.instance != null && now > record.Due + NetTime.Ticks(EnvMan.instance.m_dayLengthSec * LostDays))
					{
						Plugin.Log.LogWarning("Quest: " + record.Kind + " from the village at " + village.Farm.ToString("F0") + " never came back, so the village no longer waits for it");
						TestLog.Count("Quests", record.Kind + " never came back");
						Write(village, null);
						village.Zdo.Set(LastQuestKey, now);
					}
					break;
				default:
					Write(village, null);
					break;
			}
		}

		/// <summary>
		/// Checks whether a quest can set out now - by day, after the vikings wake and before StartCutoff
		/// </summary>
		/// <returns>True when it can</returns>
		private static bool CanSetOutNow()
		{
			return(!EnvMan.IsNight() && QuestRules.CanSetOut(EnvMan.instance.GetDayFraction()));
		}

		/// <summary>
		/// On a village's ZDO, whether its last hunting or foraging quest was a hunt, so they take turns, and when it last
		/// sent vikings to buy a circlet, in ZNet time ticks
		/// </summary>
		public static readonly int LastHuntKey = "kriona.VikingVillages.villageLastHunt".GetStableHashCode();
		public static readonly int LastCircletKey = "kriona.VikingVillages.villageLastCirclet".GetStableHashCode();

		/// <summary>
		/// What the treasure in a village's chests has to be worth before it's taken to Haldor to sell, in coins, how many
		/// game days apart the village buys circlets, and how much wood a quest cutting trees is after
		/// </summary>
		public const int SellAt = 100;
		private const float CircletDays = 7f;
		private const int WoodWanted = 50;

		/// <summary>
		/// How many of its item a quest started with the kri_vv_testlog command is after
		/// </summary>
		private const int ForcedAmount = 20;

		/// <summary>
		/// A quest a village could go on - which, what for and how many, and the way it gets that
		/// </summary>
		private class Choice
		{
			public QuestKind Kind;
			public string Item = "";
			public int Amount;
			public ResourceSource Source;
		}

		/// <summary>
		/// Picks a quest for a village once it has rested long enough since its last and no enemy is near - the first it
		/// can go on and spare the vikings for, of the next step of its goal, hunting or foraging when its chests are low
		/// on food and it has found none around it for a while, it's trapped, or its chests hold less than a food for each
		/// of its vikings, cutting trees when it's out of wood,
		/// selling its treasure to Haldor, and buying a Dverger circlet once a week for a viking still using torches -
		/// setting out at once by day, or waiting for the morning
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		private static void Pick(Village village)
		{
			long now = NetTime.Now();
			long last = village.Zdo.GetLong(LastQuestKey, 0L);
			if (last > 0L && now < last + NetTime.Ticks(EnvMan.instance.m_dayLengthSec * QuestRules.RestBetween))
			{
				return;
			}
			if (village.FindEnemy(EnemyReach) != null)
			{
				return;
			}
			Choice choice = Choose(village);
			if (choice == null)
			{
				return;
			}
			// The road is known before the quest is picked, so it never waits on the server once it's setting out
			if (RouteSeconds(village, choice.Kind, choice.Item) < 0f)
			{
				return;
			}
			QuestRecord record = Plan(village, choice.Kind, choice.Item, choice.Amount, choice.Source);
			if (record == null)
			{
				return;
			}
			if (choice.Kind == QuestKind.Hunt || choice.Kind == QuestKind.Forage)
			{
				village.Zdo.Set(LastHuntKey, choice.Kind == QuestKind.Hunt);
			}
			if (choice.Kind == QuestKind.BuyCirclet)
			{
				village.Zdo.Set(LastCircletKey, now);
			}
			Plugin.Log.LogInfo("Quest: the village at " + village.Farm.ToString("F0") + " picked " + record.Kind + (!string.IsNullOrEmpty(record.Item) ? " for " + record.Item : "") + " with " + string.Join(", ", record.Names) + (CanSetOutNow() ? "" : ", waiting for the morning"));
			if (CanSetOutNow())
			{
				SetOut(village, record);
			}
			else
			{
				record.Phase = QuestPhase.Waiting;
				Write(village, record);
			}
		}

		/// <summary>
		/// Finds the first quest a village could go on now (see Pick)
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <returns>The quest, or null when there's none</returns>
		private static Choice Choose(Village village)
		{
			ResourceBook book = ResourceBookGame.Get();
			if (book == null)
			{
				return(null);
			}
			VillageHoldings holdings = VillageLife.HoldingsOf(village);
			PlanStep step = VillageLife.CurrentGoalStep(village);
			if (step != null && step.Kind == StepKind.Quest && step.Source != null)
			{
				return(new Choice { Kind = step.Quest, Item = (step.Item ?? ""), Amount = step.Amount, Source = step.Source });
			}
			bool foodLow = VillageLife.FoodLow(village);
			bool woodLow = (VillageLife.ChestWoodOf(village) < VillageLife.WoodLow);
			bool trapped = ((foodLow || woodLow) && IsTrapped(village));
			if (foodLow && (trapped || VillageLife.FoodGone(village) || VillageLife.FoodScarce(village)))
			{
				QuestKind kind = (village.Zdo.GetBool(LastHuntKey) ? QuestKind.Forage : QuestKind.Hunt);
				Choice food = Possible(book, holdings, kind, "", 0) ?? Possible(book, holdings, ((kind == QuestKind.Hunt) ? QuestKind.Forage : QuestKind.Hunt), "", 0);
				if (food != null)
				{
					return(food);
				}
			}
			if ((woodLow && trapped) || VillageLife.OutOfWood(village))
			{
				Choice wood = Possible(book, holdings, QuestKind.ChopTrees, "Wood", WoodWanted);
				if (wood != null)
				{
					return(wood);
				}
			}
			if (TreasureValue(holdings) >= SellAt)
			{
				Choice sell = Possible(book, holdings, QuestKind.SellTreasure, "", 0);
				if (sell != null)
				{
					return(sell);
				}
			}
			return(CircletChoice(village, book, holdings));
		}

		/// <summary>
		/// Works out what the treasure a village has sells for (see ResourceBook.Treasure)
		/// </summary>
		/// <param name="holdings">What the village has</param>
		/// <returns>The coins</returns>
		private static int TreasureValue(VillageHoldings holdings)
		{
			return(holdings.Coins - holdings.Count(ResourceBook.Coins));
		}

		/// <summary>
		/// Finds a quest to buy a Dverger circlet, for a village of the Mountains or harder that bought none in the last
		/// CircletDays days, with a viking still using torches, and the coins and treasure to pay for it
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="book">Every way to get each item</param>
		/// <param name="holdings">What the village has</param>
		/// <returns>The quest, or null when it shouldn't buy one</returns>
		private static Choice CircletChoice(Village village, ResourceBook book, VillageHoldings holdings)
		{
			long last = village.Zdo.GetLong(LastCircletKey, 0L);
			if (holdings.Biome < ResourceBook.Mountain || (last > 0L && NetTime.Now() < last + NetTime.Ticks(EnvMan.instance.m_dayLengthSec * CircletDays)))
			{
				return(null);
			}
			if (!village.Vikings().Exists((Character viking) => viking is Humanoid humanoid && !HasCirclet(humanoid)))
			{
				return(null);
			}
			return(Possible(book, holdings, QuestKind.BuyCirclet, ResourceBook.Circlet, 1));
		}

		/// <summary>
		/// Checks whether a viking has a Dverger circlet of its own
		/// </summary>
		/// <param name="humanoid">The viking</param>
		/// <returns>True when it has</returns>
		private static bool HasCirclet(Humanoid humanoid)
		{
			return(humanoid.GetInventory().GetAllItems().Exists((ItemDrop.ItemData item) => item.m_dropPrefab != null && item.m_dropPrefab.name == ResourceBook.Circlet && !VikingItems.IsCarried(item)));
		}

		/// <summary>
		/// Checks whether a village can go on a quest - its biome isn't easier than the quest's or the item's, it has the
		/// stations the quest takes, and the coins it costs (see ResourceBook.AddQuests)
		/// </summary>
		/// <param name="book">Every way to get each item</param>
		/// <param name="holdings">What the village has</param>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <param name="amount">How many</param>
		/// <returns>The quest, or null when the village can't go on it</returns>
		private static Choice Possible(ResourceBook book, VillageHoldings holdings, QuestKind kind, string item, int amount)
		{
			QuestRule rule = book.RuleOf(kind);
			ResourceSource source = QuestGame.SourceFor(book, kind, item);
			if (rule == null || holdings.Biome < Mathf.Max(rule.MinBiome, source.MinBiome) || holdings.Coins < rule.Coins)
			{
				return(null);
			}
			foreach (string station in rule.Stations)
			{
				if (!holdings.HasStation(station, 1))
				{
					return(null);
				}
			}
			return(new Choice { Kind = kind, Item = item, Amount = amount, Source = source });
		}

		/// <summary>
		/// Picks the vikings for a quest - those fit to go, for a circlet the one still using torches first, then the
		/// ones with a tool good enough for it, then the healthiest - and the tool each takes, as many as the village can
		/// spare (see QuestRules.GroupSize). A quest that takes a tool needs at least one
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for</param>
		/// <param name="amount">How many of it the village wants</param>
		/// <param name="source">The way the quest gets it, with the tool it takes</param>
		/// <returns>The quest, not noted yet, or null when the village can't spare the vikings</returns>
		internal static QuestRecord Plan(Village village, QuestKind kind, string item, int amount, ResourceSource source)
		{
			ResourceBook book = ResourceBookGame.Get();
			QuestRule rule = ((book != null) ? book.RuleOf(kind) : null);
			if (rule == null)
			{
				return(null);
			}
			List<Character> vikings = village.Vikings().FindAll((Character viking) => viking != null && !viking.IsDead());
			List<Humanoid> fit = new List<Humanoid>();
			foreach (Character character in vikings)
			{
				Humanoid humanoid = character as Humanoid;
				if (humanoid != null && humanoid.GetHealthPercentage() >= 0.75f && VikingIdentity.Get(humanoid.m_nview.GetZDO()).Length > 0)
				{
					fit.Add(humanoid);
				}
			}
			List<string> chestTools = new List<string>();
			if (source.Tool != ToolKind.None)
			{
				foreach (Container chest in VillageLife.VillageChests(village))
				{
					foreach (ItemDrop.ItemData stored in chest.GetInventory().GetAllItems())
					{
						if (!VikingItems.IsCarried(stored) && GoodTool(stored, source))
						{
							chestTools.Add(stored.m_dropPrefab.name);
						}
					}
				}
				chestTools.Sort((string a, string b) => ToolTier(b).CompareTo(ToolTier(a)));
			}
			fit.Sort((Humanoid a, Humanoid b) =>
			{
				int circlet = ((kind == QuestKind.BuyCirclet) ? HasCirclet(a).CompareTo(HasCirclet(b)) : 0);
				if (circlet != 0)
				{
					return(circlet);
				}
				int tools = CarriesTool(b, source).CompareTo(CarriesTool(a, source));
				return((tools != 0) ? tools : b.GetHealthPercentage().CompareTo(a.GetHealthPercentage()));
			});
			int carried = fit.FindAll((Humanoid humanoid) => CarriesTool(humanoid, source)).Count;
			if (source.Tool != ToolKind.None && carried + chestTools.Count == 0)
			{
				return(null);
			}
			int biome = VillageCooking.VillageBiome(village);
			int size = QuestRules.GroupSize(rule, biome, ((source.Tool != ToolKind.None) ? carried + chestTools.Count : 0), fit.Count, vikings.Count);
			if (size <= 0)
			{
				return(null);
			}
			QuestRecord record = new QuestRecord();
			record.Id = System.Guid.NewGuid().ToString("N");
			record.Kind = kind;
			record.Item = (item ?? "");
			record.Amount = amount;
			for (int i = 0; i < size; i++)
			{
				Humanoid member = fit[i];
				record.Members.Add(VikingIdentity.Get(member.m_nview.GetZDO()));
				record.Names.Add(VikingNames.Get(member));
				string tool = "";
				if (source.Tool != ToolKind.None && !CarriesTool(member, source) && chestTools.Count > 0)
				{
					tool = chestTools[0];
					chestTools.RemoveAt(0);
				}
				record.Tools.Add(tool);
			}
			return(record);
		}

		/// <summary>
		/// Checks whether an item is a tool good enough for a quest's way to get its item
		/// </summary>
		/// <param name="item">The item</param>
		/// <param name="source">The way, with the tool it takes</param>
		/// <returns>True when it is</returns>
		internal static bool GoodTool(ItemDrop.ItemData item, ResourceSource source)
		{
			if (item == null || item.m_dropPrefab == null || VikingItems.IsProp(item) || item.m_shared.m_toolTier < source.ToolTier || item.m_durability <= 0f)
			{
				return(false);
			}
			if (source.Tool == ToolKind.Pickaxe)
			{
				return(item.m_shared.m_skillType == Skills.SkillType.Pickaxes && item.m_shared.m_damages.m_pickaxe > 0f);
			}
			return(source.Tool == ToolKind.Axe && item.IsWeapon() && item.m_shared.m_damages.m_chop > 0f);
		}

		/// <summary>
		/// Checks whether a viking carries a tool good enough for a quest's way to get its item
		/// </summary>
		/// <param name="humanoid">The viking</param>
		/// <param name="source">The way</param>
		/// <returns>True when it does</returns>
		private static bool CarriesTool(Humanoid humanoid, ResourceSource source)
		{
			return(source.Tool != ToolKind.None && humanoid.GetInventory().GetAllItems().Exists((ItemDrop.ItemData item) => GoodTool(item, source)));
		}

		/// <summary>
		/// Gets the tier of a tool by its prefab name
		/// </summary>
		/// <param name="prefab">The tool's prefab name</param>
		/// <returns>The tier, or 0 when it isn't known</returns>
		private static int ToolTier(string prefab)
		{
			GameObject found = ((ObjectDB.instance != null) ? ObjectDB.instance.GetItemPrefab(prefab) : null);
			ItemDrop drop = ((found != null) ? found.GetComponent<ItemDrop>() : null);
			return((drop != null) ? drop.m_itemData.m_shared.m_toolTier : 0);
		}

		/// <summary>
		/// Sets a quest out - working out the spot its vikings leave from, what each does and when they're due back (see
		/// QuestGame.Prepare), noting it, and stopping what its vikings on this client are doing, so they get ready to go.
		/// A quest one of whose vikings isn't here any more is given up on, for another to be picked
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <param name="record">The quest</param>
		private static void SetOut(Village village, QuestRecord record)
		{
			Exit exit = ExitOf(village);
			record.Exit = exit.Spot;
			record.Trapped = exit.Trapped;
			record.Phase = QuestPhase.Leaving;
			record.SetOutAt = NetTime.Now();
			float travel = RouteSeconds(village, record.Kind, record.Item, out float back);
			travel = ((travel >= 0f) ? travel : QuestRules.DefaultTravel);
			back = ((back >= 0f) ? back : travel);
			ResourceSource source = QuestGame.SourceFor(ResourceBookGame.Get(), record.Kind, record.Item);
			if (!QuestGame.Prepare(village, record, source, travel, back))
			{
				Plugin.Log.LogInfo("Quest: " + record.Kind + " from the village at " + village.Farm.ToString("F0") + " was given up on, as its vikings aren't all here");
				TestLog.Count("Quests", record.Kind + " given up on");
				Write(village, null);
				return;
			}
			Write(village, record);
			VillageLife.CallToQuest(village, record);
			TestLog.Count("Quests", record.Kind + " set out");
			float seconds = (record.Due - record.SetOutAt) / (float)NetTime.Ticks(1f);
			Plugin.Log.LogInfo("Quest: " + record.Kind + " from the village at " + village.Farm.ToString("F0") + " sets out from " + record.Exit.ToString("F0") + (record.Trapped ? ", inside the village, as it's trapped" : "") + ", " + travel.ToString("F0") + "s there, " + back.ToString("F0") + "s back and " + record.ActiveSeconds.ToString("F0") + "s there, due back in " + seconds.ToString("F0") + "s");
		}

		/// <summary>
		/// Says roughly when a quest is due back
		/// </summary>
		/// <param name="record">The quest</param>
		/// <returns>The phrase, like "tomorrow" (see TravelTime.Phrase)</returns>
		public static string DuePhrase(QuestRecord record)
		{
			if (ZNet.instance == null || EnvMan.instance == null)
			{
				return("");
			}
			double now = ZNet.instance.GetTimeSeconds();
			double due = now + (record.Due - NetTime.Now()) / (double)NetTime.Ticks(1f);
			return(TravelTime.Phrase(now, due, EnvMan.instance.m_dayLengthSec));
		}

		/// <summary>
		/// Works out whether a village is trapped and the spot its vikings leave from - a spot outside it they can walk to
		/// from its middle, or when there's none, the spot nearest its edge they can reach - kept for TrappedKeep
		/// seconds
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The spot, and whether the village is trapped</returns>
		private static Exit ExitOf(Village village)
		{
			ZDOID id = village.Zdo.m_uid;
			if (exits.TryGetValue(id, out Exit known) && Time.time - known.At < TrappedKeep)
			{
				return(known);
			}
			Exit exit = new Exit { At = Time.time, Spot = village.RallyPoint(), Trapped = true };
			Vector3 from = village.RallyPoint();
			Vector3 lastTried = Vector3.zero;
			List<Vector3> path = new List<Vector3>();
			for (int i = 0; i < TrappedTries && Pathfinding.instance != null; i++)
			{
				if (!village.ArrivalSpot(out Vector3 spot))
				{
					break;
				}
				lastTried = spot;
				if (Pathfinding.instance.GetPath(from, spot, path, Pathfinding.AgentType.Humanoid, true))
				{
					exit.Trapped = false;
					exit.Spot = spot;
					break;
				}
			}
			if (exit.Trapped)
			{
				// The farthest it can get toward a spot outside is as near the edge as it gets
				Vector3 toward = ((lastTried != Vector3.zero) ? lastTried : village.Farm + Vector3.forward * (village.Radius + 10f));
				if (Pathfinding.instance != null && Pathfinding.instance.GetPath(from, toward, path, Pathfinding.AgentType.Humanoid, false) && path.Count > 0)
				{
					exit.Spot = path[path.Count - 1];
				}
				Plugin.Log.LogInfo("Quest: the village at " + village.Farm.ToString("F0") + " is trapped, so its vikings leave from " + exit.Spot.ToString("F0"));
			}
			exits[id] = exit;
			return(exit);
		}

		/// <summary>
		/// Checks whether a village was found trapped when its last quest set out, so its vikings come back inside it
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="spot">Set to the spot they come back to</param>
		/// <returns>True when it was</returns>
		public static bool TrappedSpot(Village village, out Vector3 spot)
		{
			spot = Vector3.zero;
			if (village == null || village.Zdo == null || !exits.TryGetValue(village.Zdo.m_uid, out Exit exit) || !exit.Trapped)
			{
				return(false);
			}
			spot = exit.Spot;
			return(true);
		}

		/// <summary>
		/// Checks whether a village is trapped - walled in or ringed by water, with no way out its vikings can walk
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>True when it is</returns>
		public static bool IsTrapped(Village village)
		{
			return(ExitOf(village).Trapped);
		}

		/// <summary>
		/// Finds a loaded viking of a village by its GUID
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="member">The viking's GUID</param>
		/// <returns>The viking, or null when it isn't loaded here</returns>
		private static Character FindMember(Village village, string member)
		{
			return(village.Vikings().Find((Character viking) => viking != null && viking.m_nview != null && viking.m_nview.IsValid() && VikingIdentity.Get(viking.m_nview.GetZDO()) == member));
		}

		/// <summary>
		/// Counts a village's vikings out on its quest - those on it that aren't among the vikings it has - so it doesn't
		/// bring in others in their place
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="vikings">Its living vikings' ZDOs</param>
		/// <returns>How many are away</returns>
		public static int AwayCount(Village village, List<ZDO> vikings)
		{
			QuestRecord record = Read(village);
			if (record == null || (record.Phase != QuestPhase.Leaving && record.Phase != QuestPhase.Away))
			{
				return(0);
			}
			HashSet<string> here = new HashSet<string>();
			foreach (ZDO zdo in vikings)
			{
				here.Add(VikingIdentity.Get(zdo));
			}
			return(record.Members.FindAll((string member) => !here.Contains(member)).Count);
		}

		/// <summary>
		/// Checks whether a viking coming in from the road is coming home from a quest of a village - it set out from
		/// there - from the values it was kept as
		/// </summary>
		/// <param name="snapshot">The viking's values (see VillageDeparted.SnapshotOf)</param>
		/// <param name="village">The village</param>
		/// <returns>True when it is</returns>
		public static bool ComingHome(byte[] snapshot, Village village)
		{
			VillageDeparted.VikingValues values = VillageDeparted.Decode(snapshot);
			if (values == null)
			{
				return(false);
			}
			// A bool is kept as an int
			bool gone = values.Ints.Exists((KeyValuePair<int, int> entry) => entry.Key == QuestGoneKey && entry.Value != 0);
			KeyValuePair<int, Vector3> from = values.Vec3s.Find((KeyValuePair<int, Vector3> entry) => entry.Key == QuestVillageKey);
			return(gone && from.Key == QuestVillageKey && Utils.DistanceXZ(from.Value, village.Farm) <= FarmMatch);
		}

		/// <summary>
		/// Notes that a village's quest has come back, as the first of its vikings come in, so it can pick another once
		/// it has rested
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <param name="questId">The quest's ID</param>
		public static void Returned(Village village, string questId)
		{
			QuestRecord record = Read(village);
			if (record == null || record.Id != questId)
			{
				return;
			}
			Write(village, null);
			village.Zdo.Set(LastQuestKey, NetTime.Now());
			if (QuestRules.WorthTalkingAbout(record.Kind))
			{
				NeighborNews.Report(NeighborEvent.QuestBack, village.Farm, VillageNames.Get(village), record.Kind + "|" + record.Item);
			}
			TestLog.Count("Quests", record.Kind + " came back");
			Plugin.Log.LogInfo("Quest: " + record.Kind + " came back to the village at " + village.Farm.ToString("F0"));
		}

		/// <summary>
		/// Notes when a village's quest is now due back, as the server worked it out from the trip its vikings make (see
		/// VillageTravel.PlanQuest) - so what the village says of when they're back follows the trip, not its guess as it
		/// sent them - on the client that runs the village
		/// </summary>
		/// <param name="farm">Where the village stands</param>
		/// <param name="questId">The quest's ID</param>
		/// <param name="due">When it's due back, in ZNet time ticks</param>
		public static void DueChanged(Vector3 farm, string questId, long due)
		{
			Village village = Village.ForFarm(farm);
			if (village == null || !village.IsOwner())
			{
				return;
			}
			QuestRecord record = Read(village);
			if (record == null || record.Id != questId || record.Due == due)
			{
				return;
			}
			Plugin.Log.LogInfo("Quest: " + record.Kind + " from the village at " + village.Farm.ToString("F0") + " is now due back in " + ((due - NetTime.Now()) / (float)NetTime.Ticks(1f)).ToString("F0") + "s, " + ((due - record.Due) / (float)NetTime.Ticks(1f)).ToString("+0;-0") + "s from before");
			record.Due = due;
			Write(village, record);
		}

		/// <summary>
		/// Describes everything behind a village's quests, for the kri_vv_testlog command - its goal and the next step
		/// toward it, the quest it's on and what each viking does on it, what would set off its other quests, and the
		/// roads to quests it knows
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The lines</returns>
		public static List<string> Report(Village village)
		{
			List<string> lines = new List<string>();
			ResourceBook book = ResourceBookGame.Get();
			string goal = village.Zdo.GetString(VillageLife.GoalKey, "");
			PlanStep step = VillageLife.CurrentGoalStep(village);
			lines.Add("Village at " + village.Farm.ToString("F0") + (village.IsOwner() ? ", run by this client" : ", run by another client"));
			lines.Add("Goal: " + ((goal.Length > 0) ? goal + " at quality " + village.Zdo.GetInt(VillageLife.GoalQualityKey, 1) : "none") + " - next step: " + ((step != null) ? step.Describe() : "none"));
			QuestRecord record = Read(village);
			if (record == null)
			{
				lines.Add("Quest: none");
			}
			else
			{
				lines.Add(Describe(village).TrimEnd('\n') + ", " + record.Amount + " wanted, " + record.ActiveSeconds.ToString("F0") + "s of work, quest biome " + record.QuestBiome + ", " + record.Wanted + " vikings wanted, leaving from " + record.Exit.ToString("F0"));
				for (int i = 0; i < record.Members.Count; i++)
				{
					MemberPlan plan = ((i < record.Plans.Count) ? record.Plans[i] : new MemberPlan());
					string loot = string.Join(", ", plan.Loot.ConvertAll((KeyValuePair<string, int> entry) => entry.Value + " " + entry.Key));
					string spend = string.Join(", ", plan.Spend.ConvertAll((KeyValuePair<string, int> entry) => entry.Value + " " + entry.Key));
					lines.Add("  " + ((record.Names[i].Length > 0) ? record.Names[i] : record.Members[i]) + ((record.Tools[i].Length > 0) ? ", taking " + record.Tools[i] + " from a chest" : "") + ((plan.Tool.Length > 0) ? ", working with " + plan.Tool + " (" + plan.Hits + " hits, " + plan.ToolLeft.ToString("F0") + " durability left)" : "") + ((plan.Kills > 0) ? ", " + plan.Kills + " kills" : "") + ", bringing home " + ((loot.Length > 0) ? loot : "nothing") + ((spend.Length > 0) ? ", spending " + spend : ""));
				}
			}
			if (book != null)
			{
				VillageHoldings holdings = VillageLife.HoldingsOf(village);
				lines.Add("Food: " + (VillageLife.FoodLow(village) ? "low" : "enough") + (VillageLife.FoodGone(village) ? ", none found around it lately" : ", found around it lately") + " - wood in the chests: " + VillageLife.ChestWoodOf(village) + (VillageLife.OutOfWood(village) ? ", out of wood" : ""));
				lines.Add("Treasure worth " + TreasureValue(holdings) + " coins, " + holdings.Count(ResourceBook.Coins) + " coins - sells at " + SellAt + " - biome " + holdings.Biome + ", stations: " + string.Join(", ", new List<string>(holdings.Stations.Keys)));
				lines.Add("Trapped: " + (IsTrapped(village) ? "yes, leaving from " + ExitOf(village).Spot.ToString("F0") : "no"));
				List<string> roads = new List<string>();
				foreach (QuestKind kind in System.Enum.GetValues(typeof(QuestKind)))
				{
					foreach (string item in RouteItems(book, kind))
					{
						RouteKeys(kind, item, out int thereKey, out int backKey, out int atKey);
						float there = village.Zdo.GetFloat(thereKey, -1f);
						if (there >= 0f)
						{
							float back = village.Zdo.GetFloat(backKey, there);
							float age = (NetTime.Now() - village.Zdo.GetLong(atKey, 0L)) / (float)NetTime.Ticks(1f);
							roads.Add(kind + ((item.Length > 0) ? " " + item : "") + " " + there.ToString("F0") + "s" + ((Mathf.Abs(back - there) >= 1f) ? "/" + back.ToString("F0") + "s back" : "") + " (" + age.ToString("F0") + "s old)");
						}
					}
				}
				lines.Add("Roads known: " + ((roads.Count > 0) ? string.Join(", ", roads) : "none"));
			}
			long last = village.Zdo.GetLong(LastQuestKey, 0L);
			lines.Add("Last quest back " + ((last > 0L) ? ((NetTime.Now() - last) / (double)NetTime.Ticks(1f)).ToString("F0") + "s ago" : "never"));
			return(lines);
		}

		/// <summary>
		/// Lists what a quest can be for - each item a quest of its kind brings back, or "" for a quest for nothing in
		/// particular
		/// </summary>
		/// <param name="book">Every way to get each item</param>
		/// <param name="kind">The quest</param>
		/// <returns>The items</returns>
		private static List<string> RouteItems(ResourceBook book, QuestKind kind)
		{
			List<string> items = new List<string> { "" };
			foreach (KeyValuePair<string, List<ResourceSource>> entry in book.Sources)
			{
				if (entry.Value.Exists((ResourceSource source) => source.Kind == SourceKind.Quest && source.Quest == kind))
				{
					items.Add(entry.Key);
				}
			}
			return(items);
		}

		/// <summary>
		/// Starts a quest of a kind for a village at once, for the kri_vv_testlog command - whatever the village wants,
		/// however long since its last and whatever the time of day - for the item given, or else the first a quest of
		/// that kind brings back that it's allowed to go for
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or "" for the first it can go for</param>
		/// <returns>What happened</returns>
		public static string Force(Village village, QuestKind kind, string item)
		{
			ResourceBook book = ResourceBookGame.Get();
			if (book == null || !village.IsOwner())
			{
				return("Only the client running the village can start its quests, once the game has loaded");
			}
			if (Read(village) != null)
			{
				return("The village is already on a quest - kri_vv_testlog quest due brings it home");
			}
			VillageHoldings holdings = VillageLife.HoldingsOf(village);
			List<string> items = ((item.Length > 0) ? new List<string> { item } : RouteItems(book, kind));
			// An item the quest brings back is tried before going for nothing in particular
			items.Sort((string a, string b) => (a.Length == 0).CompareTo(b.Length == 0));
			Choice choice = null;
			foreach (string candidate in items)
			{
				choice = Possible(book, holdings, kind, candidate, ((candidate.Length > 0) ? ForcedAmount : 0));
				if (choice != null)
				{
					break;
				}
			}
			if (choice == null)
			{
				return("The village can't go on " + kind + " - its biome is too easy, or it's missing a station or the coins");
			}
			if (RouteSeconds(village, choice.Kind, choice.Item) < 0f)
			{
				return("Asked the server for the road to " + kind + " - run the command again in a moment");
			}
			QuestRecord record = Plan(village, choice.Kind, choice.Item, choice.Amount, choice.Source);
			if (record == null)
			{
				return("The village can't spare the vikings for " + kind + ", or has no tool for it");
			}
			SetOut(village, record);
			record = Read(village);
			return((record != null) ? "Started " + kind + ((choice.Item.Length > 0) ? " for " + choice.Item : "") + " with " + string.Join(", ", record.Names) + ", due back " + DuePhrase(record) : "The quest's vikings aren't all loaded, so it was given up on");
		}

		/// <summary>
		/// Brings a village's quest home now, for the kri_vv_testlog command - on the server, which keeps its vikings
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>What happened</returns>
		public static string DueNow(Village village)
		{
			QuestRecord record = Read(village);
			if (record == null)
			{
				return("The village isn't on a quest");
			}
			if (ZNet.instance == null || !ZNet.instance.IsServer())
			{
				return("Only the server keeps the vikings out on quests - run this as the host");
			}
			int count = VillageTravel.DueNow(record.Id);
			if (village.IsOwner())
			{
				record.Due = NetTime.Now();
				Write(village, record);
			}
			return((count > 0) ? count + " vikings on " + record.Kind + " are due back now" : "None of the quest's vikings have left the village yet");
		}

		/// <summary>
		/// Describes a village's quest for the debug hover text of its fire
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The line, or "" when it has none</returns>
		public static string Describe(Village village)
		{
			QuestRecord record = Read(village);
			if (record == null)
			{
				return("");
			}
			string due = ((record.Phase == QuestPhase.Waiting) ? "waiting for the morning" : "due back " + DuePhrase(record));
			return("Quest: " + record.Kind + ((record.Item.Length > 0) ? " for " + record.Item : "") + ", " + record.Phase.ToString().ToLowerInvariant() + ", " + due + " - " + string.Join(", ", record.Names) + (record.Trapped ? " (trapped)" : "") + "\n");
		}
	}
}
