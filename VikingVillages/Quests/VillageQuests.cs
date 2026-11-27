using System.Collections.Generic;
using System.Text;
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
		/// On a PC viking, the quest it set out on, that it was taken out of the world for it - so one that comes
		/// back knows it's back - and the village it set out from and the house there it calls home, which it's
		/// brought back to
		/// </summary>
		public static readonly int QuestIdKey = "kriona.VikingVillages.vikingQuest".GetStableHashCode();
		public static readonly int QuestGoneKey = "kriona.VikingVillages.vikingQuestGone".GetStableHashCode();
		public static readonly int QuestVillageKey = "kriona.VikingVillages.vikingQuestVillage".GetStableHashCode();
		public static readonly int QuestHomeKey = "kriona.VikingVillages.vikingQuestHome".GetStableHashCode();

		/// <summary>
		/// On a PC viking, how its quest goes for it (see QuestOutcome), noted as it leaves and played out as it comes
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
		/// A question this client asked the server about a quest's road - its ID (see TravelPlanner.AskQuestRoute) and when
		/// it was asked, in Time.time seconds
		/// </summary>
		private struct RouteAsk
		{
			public long Id;
			public float At;
		}

		/// <summary>
		/// The roads to quests this client has asked the server for and not heard back about, by the key they're noted at
		/// on their village (see RouteKeys) mixed with the village's ZDO
		/// </summary>
		private static readonly Dictionary<int, RouteAsk> routesAsked = new Dictionary<int, RouteAsk>();

		/// <summary>
		/// How long a road to a quest is kept on its village before the server is asked for it again, in game days - so
		/// portals built or broken since are taken into account
		/// </summary>
		private const double RouteKeepDays = 1.0;

		/// <summary>
		/// Gets the keys how long the road to a quest takes from a village is noted at on the village's ZDO, once the
		/// server has worked it out - there and back, in seconds, when it was worked out, in ZNet time ticks, and the biome
		/// it goes to, by VikingGear's order
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <param name="there">The key of the time there</param>
		/// <param name="back">The key of the time back</param>
		/// <param name="at">The key of when it was worked out</param>
		/// <param name="biome">The key of the biome it goes to</param>
		private static void RouteKeys(QuestKind kind, string item, out int there, out int back, out int at, out int biome)
		{
			string name = RouteName(kind, item);
			there = (name + ".there").GetStableHashCode();
			back = (name + ".back").GetStableHashCode();
			at = (name + ".at").GetStableHashCode();
			biome = (name + ".biome").GetStableHashCode();
		}

		/// <summary>
		/// Gets the name a quest's road is noted under on its village's generator
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <returns>The name</returns>
		private static string RouteName(QuestKind kind, string item)
		{
			return("kriona.VikingVillages.villageQuestRoad." + kind + "." + item);
		}

		/// <summary>
		/// Gets the key of the sea a quest's road sets out across from its village (see BoatRules.QuestSea)
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <returns>The key</returns>
		private static int SeaKey(QuestKind kind, string item)
		{
			return((RouteName(kind, item) + ".sea").GetStableHashCode());
		}

		/// <summary>
		/// Gets the sea a quest's road sets out across from a village, as the server last worked it out
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <returns>The sea, with no waypoints when the road goes on foot from the village</returns>
		private static TravelRoad SeaOf(Village village, QuestKind kind, string item)
		{
			return(TravelRoad.Decode(village.Zdo.GetByteArray(SeaKey(kind, item))));
		}

		/// <summary>
		/// Gets what the vikings call the trader a village sells its treasure to - the one whose camp its road goes to
		/// </summary>
		/// <param name="village">The village, or null</param>
		/// <returns>The trader's name, like "Hildir"</returns>
		internal static string TraderOf(Village village)
		{
			if (village == null)
			{
				return(QuestTargets.TraderName(-1, ResourceBook.Meadows));
			}
			return(QuestTargets.TraderName(RouteBiome(village, QuestKind.SellTreasure, ""), VillageCooking.VillageBiome(village)));
		}

		/// <summary>
		/// Gets the biome the road to a quest goes to from a village, as the server last worked it out
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <returns>The biome, by VikingGear's order, or -1 when it isn't known</returns>
		private static int RouteBiome(Village village, QuestKind kind, string item)
		{
			RouteKeys(kind, item, out int _, out int _, out int _, out int biomeKey);
			return(village.Zdo.GetInt(biomeKey, -1));
		}

		/// <summary>
		/// Gets how long the road to a quest takes from a village, there and back - asking the server the first time, and
		/// again once what it said is RouteKeepDays old, going by what it said before until it answers, or once it's heard
		/// nothing back for QuestRules.RouteAnswerWait
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <param name="back">Set to the time back, in seconds, or less than 0 while the server hasn't answered</param>
		/// <returns>The time there, in seconds, or less than 0 while the server hasn't answered</returns>
		private static float RouteSeconds(Village village, QuestKind kind, string item, out float back)
		{
			RouteKeys(kind, item, out int thereKey, out int backKey, out int atKey, out int biomeKey);
			float there = village.Zdo.GetFloat(thereKey, -1f);
			back = village.Zdo.GetFloat(backKey, -1f);
			long at = village.Zdo.GetLong(atKey, 0L);
			bool fresh = (there >= 0f && EnvMan.instance != null && NetTime.Now() - at < NetTime.Ticks((float)(EnvMan.instance.m_dayLengthSec * RouteKeepDays)));
			// Asked once per village and road until the server answers, or until the answer looks lost
			int asked = thereKey ^ village.Zdo.m_uid.GetHashCode();
			bool waiting = routesAsked.TryGetValue(asked, out RouteAsk ask);
			if (!QuestRules.AskForRoute(fresh, waiting, ask.At, Time.time))
			{
				return(there);
			}
			if (waiting)
			{
				TravelPlanner.ForgetQuestRoute(ask.Id);
				Plugin.Log.LogInfo("Quest: the village at " + village.Farm.ToString("F0") + " heard nothing back about the road to " + kind + (!string.IsNullOrEmpty(item) ? " for " + item : "") + " in " + (Time.time - ask.At).ToString("F0") + "s, so it asks the server again");
				TestLog.Count("Quests", "roads asked again");
			}
			// Noted before asking, since a host with nowhere for the quest to go answers before AskQuestRoute returns
			routesAsked[asked] = new RouteAsk { At = Time.time };
			ZDOID id = village.Zdo.m_uid;
			int seaKey = SeaKey(kind, item);
			long askId = TravelPlanner.AskQuestRoute(village.Farm, VillageCooking.VillageBiome(village), kind, item, (float answerThere, float answerBack, int answerBiome, TravelRoad answerSea) =>
			{
				routesAsked.Remove(asked);
				ZDO zdo = ((ZDOMan.instance != null) ? ZDOMan.instance.GetZDO(id) : null);
				if (zdo != null && zdo.IsOwner())
				{
					zdo.Set(thereKey, answerThere);
					zdo.Set(backKey, answerBack);
					zdo.Set(atKey, NetTime.Now());
					zdo.Set(biomeKey, answerBiome);
					zdo.Set(seaKey, answerSea.Encode());
				}
			});
			if (routesAsked.ContainsKey(asked))
			{
				routesAsked[asked] = new RouteAsk { Id = askId, At = Time.time };
			}
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
					if (CanSetOutNow(village, record) && village.FindEnemy(EnemyReach) == null)
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
		/// Checks whether a quest can set out now - by day, after the vikings wake and before StartCutoff, and for one
		/// that's home by night, like a hunt, with daylight enough for the least work it's worth going for (see
		/// QuestRules.LeastWork)
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <param name="record">The quest</param>
		/// <returns>True when it can</returns>
		private static bool CanSetOutNow(Village village, QuestRecord record)
		{
			float dayFraction = EnvMan.instance.GetDayFraction();
			if (EnvMan.IsNight() || !QuestRules.CanSetOut(dayFraction))
			{
				return(false);
			}
			if (!QuestRules.HomeByNight(record.Kind))
			{
				return(true);
			}
			float there = RouteSeconds(village, record.Kind, record.Item, out float back);
			there = ((there >= 0f) ? there : QuestRules.DefaultTravel);
			back = ((back >= 0f) ? back : there);
			return(QuestRules.WorkBeforeNight(QuestRules.SecondsToNight(dayFraction, EnvMan.instance.m_dayLengthSec), there, back) >= QuestRules.LeastWork(record.Kind));
		}

		/// <summary>
		/// On a village's ZDO, whether its last hunting or foraging quest was a hunt, so they take turns
		/// </summary>
		public static readonly int LastHuntKey = "kriona.VikingVillages.villageLastHunt".GetStableHashCode();

		/// <summary>
		/// Gets the key on a village's ZDO of when it last sent vikings to buy something from Haldor, in ZNet time ticks
		/// </summary>
		/// <param name="kind">The quest that buys it</param>
		/// <returns>The key</returns>
		private static int LastBoughtKey(QuestKind kind)
		{
			return(("kriona.VikingVillages.villageLastBought." + kind).GetStableHashCode());
		}

		/// <summary>
		/// What the treasure in a village's chests has to be worth before it's taken to Haldor to sell, in coins, how many
		/// game days apart the village buys each thing from him, and how much wood a quest cutting trees is after
		/// </summary>
		public const int SellAt = 100;
		private const float BuyDays = 7f;
		private const int WoodWanted = 50;

		/// <summary>
		/// How many of its item a quest started with the kri_vv_quest command is after
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

			/// <summary>
			/// For a trip to Haldor, what it buys, the first first (see QuestRecord.Buys)
			/// </summary>
			public List<KeyValuePair<string, int>> Buys = new List<KeyValuePair<string, int>>();
		}

		/// <summary>
		/// Picks a quest for a village once it has rested long enough since its last and no enemy is near - the first it
		/// can go on and spare the vikings for, of hunting or foraging when its chests hold less than a food for each of
		/// its vikings, the next step of its goal, hunting or foraging when its chests are low on food and it has found
		/// none around it for a while or it's trapped, cutting trees when it's out of wood,
		/// selling its treasure to Haldor, and buying from him, each at most once a week, a Dverger circlet for a viking
		/// still using torches, a strength belt for one without, or wider or deeper pockets for one without (see
		/// PurchaseChoice) - setting out at once by day, or waiting for the morning
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
			QuestRecord record = Plan(village, choice.Kind, choice.Item, choice.Amount, choice.Source, choice.Buys);
			if (record == null)
			{
				return;
			}
			if (choice.Kind == QuestKind.Hunt || choice.Kind == QuestKind.Forage)
			{
				village.Zdo.Set(LastHuntKey, choice.Kind == QuestKind.Hunt);
			}
			// Each thing the trip buys waits BuyDays before it's bought again
			foreach (KeyValuePair<string, int> bought in record.Buys)
			{
				QuestKind purchase = ResourceBook.PurchaseOf(bought.Key);
				if (purchase != QuestKind.None)
				{
					village.Zdo.Set(LastBoughtKey(purchase), now);
				}
			}
			Plugin.Log.LogInfo("Quest: the village at " + village.Farm.ToString("F0") + " picked " + record.Kind + (!string.IsNullOrEmpty(record.Item) ? " for " + record.Item : "") + " with " + string.Join(", ", record.Names) + (CanSetOutNow(village, record) ? "" : ", waiting for the morning"));
			if (CanSetOutNow(village, record))
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
		/// Finds a quest for food a village could go on now - hunting and foraging taking turns, the other when the one
		/// whose turn it is can't go
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="book">Every way to get each item</param>
		/// <param name="holdings">What the village has</param>
		/// <returns>The quest, or null when neither can go</returns>
		private static Choice FoodChoice(Village village, ResourceBook book, VillageHoldings holdings)
		{
			QuestKind kind = (village.Zdo.GetBool(LastHuntKey) ? QuestKind.Forage : QuestKind.Hunt);
			return(Possible(book, holdings, kind, "", 0) ?? Possible(book, holdings, ((kind == QuestKind.Hunt) ? QuestKind.Forage : QuestKind.Hunt), "", 0));
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
			// A village nearly out of food goes for more ahead of its goal, so its wounded have something to heal on
			if (VillageLife.FoodScarce(village))
			{
				Choice scarce = FoodChoice(village, book, holdings);
				if (scarce != null)
				{
					return(scarce);
				}
			}
			PlanStep step = VillageLife.CurrentGoalStep(village);
			if (step != null && step.Kind == StepKind.Quest && step.Source != null)
			{
				return(new Choice { Kind = step.Quest, Item = (step.Item ?? ""), Amount = step.Amount, Source = step.Source });
			}
			bool foodLow = VillageLife.FoodLow(village);
			bool woodLow = (VillageLife.ChestWoodOf(village) < VillageLife.WoodLow);
			bool trapped = ((foodLow || woodLow) && IsTrapped(village));
			if (foodLow && (trapped || VillageLife.FoodGone(village)))
			{
				Choice food = FoodChoice(village, book, holdings);
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
			Choice gear = GearChoice(village, book, holdings);
			if (gear != null)
			{
				return(gear);
			}
			foreach (QuestKind kind in new[] { QuestKind.BuyWiderPockets, QuestKind.BuyDeeperPockets })
			{
				Choice pockets = PocketsChoice(village, book, holdings, kind);
				if (pockets != null)
				{
					return(pockets);
				}
			}
			return(null);
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
		/// Counts how many of something from Haldor a village wants (see ResourceBook.Bought) - one for each of its vikings
		/// that lacks it (see Lacks), less the spare strength belts its chests hold, when the village's biome is the
		/// quest's or harder, it has the coins and treasure to pay for one, and it hasn't bought it in the last BuyDays
		/// days
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="book">Every way to get each item</param>
		/// <param name="holdings">What the village has</param>
		/// <param name="kind">The quest that buys it</param>
		/// <returns>How many it wants, or 0 when it shouldn't buy any</returns>
		private static int Wanting(Village village, ResourceBook book, VillageHoldings holdings, QuestKind kind)
		{
			long last = village.Zdo.GetLong(LastBoughtKey(kind), 0L);
			if (last > 0L && NetTime.Now() < last + NetTime.Ticks(EnvMan.instance.m_dayLengthSec * BuyDays))
			{
				return(0);
			}
			string item = ResourceBook.BoughtItem(kind);
			if (Possible(book, holdings, kind, (item ?? ""), 1) == null)
			{
				return(0);
			}
			int lacking = village.Vikings().FindAll((Character viking) => viking is Humanoid humanoid && Lacks(kind, humanoid)).Count;
			if (kind == QuestKind.BuyBelt)
			{
				lacking -= holdings.Count(ResourceBook.Belt);
			}
			return(Mathf.Max(0, lacking));
		}

		/// <summary>
		/// Gets what something a quest buys costs at Haldor's (see QuestRule.Coins)
		/// </summary>
		/// <param name="book">Every way to get each item</param>
		/// <param name="kind">The quest that buys it</param>
		/// <returns>The price in coins, or 0 when it isn't known</returns>
		private static int PriceOf(ResourceBook book, QuestKind kind)
		{
			QuestRule rule = book.RuleOf(kind);
			return((rule != null) ? rule.Coins : 0);
		}

		/// <summary>
		/// Finds a trip to Haldor to buy the Dverger circlets and strength belts a village wants (see Wanting), all at
		/// once - as many as its coins and treasure pay for, circlets first (see HaldorTrade.Shopping) - which the one
		/// carrying the purse hands out to the vikings who lack them once it's home (see VillageLife.HandOutBought)
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="book">Every way to get each item</param>
		/// <param name="holdings">What the village has</param>
		/// <returns>The quest - buying a circlet when it buys any, else a strength belt - or null when it buys neither</returns>
		private static Choice GearChoice(Village village, ResourceBook book, VillageHoldings holdings)
		{
			List<Purchase> wanted = new List<Purchase>();
			foreach (QuestKind kind in new[] { QuestKind.BuyCirclet, QuestKind.BuyBelt })
			{
				int count = Wanting(village, book, holdings, kind);
				if (count > 0)
				{
					wanted.Add(new Purchase { Item = ResourceBook.Bought(kind), Count = count, Price = PriceOf(book, kind) });
				}
			}
			List<Purchase> bought = HaldorTrade.Shopping(wanted, holdings.Coins);
			if (bought.Count == 0)
			{
				return(null);
			}
			QuestKind trip = ResourceBook.PurchaseOf(bought[0].Item);
			int total = 0;
			foreach (Purchase purchase in bought)
			{
				total += purchase.Count;
			}
			Choice choice = Possible(book, holdings, trip, bought[0].Item, total);
			if (choice != null)
			{
				choice.Buys = bought.ConvertAll((Purchase purchase) => new KeyValuePair<string, int>(purchase.Item, purchase.Count));
			}
			return(choice);
		}

		/// <summary>
		/// Finds a trip to Haldor for the wider or deeper pockets a village's vikings lack (see Wanting), as many as its
		/// coins and treasure pay for - each viking who's to have them goes along, since they're sewn into its own clothes
		/// there (see Plan)
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="book">Every way to get each item</param>
		/// <param name="holdings">What the village has</param>
		/// <param name="kind">The quest - wider or deeper pockets</param>
		/// <returns>The quest, or null when it buys none</returns>
		private static Choice PocketsChoice(Village village, ResourceBook book, VillageHoldings holdings, QuestKind kind)
		{
			int count = Wanting(village, book, holdings, kind);
			int price = PriceOf(book, kind);
			int paid = ((price > 0) ? Mathf.Min(count, holdings.Coins / price) : 0);
			if (paid <= 0)
			{
				return(null);
			}
			Choice choice = Possible(book, holdings, kind, "", paid);
			if (choice != null)
			{
				choice.Buys = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>(ResourceBook.Bought(kind), paid) };
			}
			return(choice);
		}

		/// <summary>
		/// Checks whether a viking lacks what a quest to Haldor buys - a Dverger circlet or strength belt of its own, or
		/// the wider or deeper pockets it could be sewn (see VikingUpgrades.CanTakePockets)
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="humanoid">The viking</param>
		/// <returns>True when it lacks it, false for a quest that buys nothing</returns>
		internal static bool Lacks(QuestKind kind, Humanoid humanoid)
		{
			switch (kind)
			{
				case QuestKind.BuyCirclet:
				case QuestKind.BuyBelt:
					return(!HasOwn(humanoid, ResourceBook.Bought(kind)));
				case QuestKind.BuyWiderPockets:
				case QuestKind.BuyDeeperPockets:
					VikingCore core = humanoid.GetComponent<VikingCore>();
					return(core != null && VikingUpgrades.CanTakePockets(kind, core.InventoryRows()));
				default:
					return(false);
			}
		}

		/// <summary>
		/// Checks whether a viking has an item of its own, worn or not, rather than carrying it for its village
		/// </summary>
		/// <param name="humanoid">The viking</param>
		/// <param name="prefab">The item's prefab name</param>
		/// <returns>True when it has</returns>
		private static bool HasOwn(Humanoid humanoid, string prefab)
		{
			return(humanoid.GetInventory().GetAllItems().Exists((ItemDrop.ItemData item) => item.m_dropPrefab != null && item.m_dropPrefab.name == prefab && !VikingItems.IsCarried(item)));
		}

		/// <summary>
		/// Gets the biome a viking's gear comes from (see VikingCore.BiomeIndex)
		/// </summary>
		/// <param name="humanoid">The viking</param>
		/// <param name="fallback">The biome for one with no VikingCore</param>
		/// <returns>The biome's index in VikingGear's tables</returns>
		private static int BiomeOf(Humanoid humanoid, int fallback)
		{
			VikingCore core = humanoid.GetComponent<VikingCore>();
			return((core != null) ? core.BiomeIndex() : fallback);
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
		/// Picks the vikings for a quest - those fit to go, leaving home any whose gear is so far behind the quest's biome
		/// that it would be expected to lose all its health there (see QuestSim.ExpectedToDie), or on a quest that
		/// kills creatures, in the fight (see QuestFightGame.ExpectedToDie), for something bought from Haldor the ones
		/// that lack it first (see Lacks), then the ones with a tool good enough for it, then on a quest that kills
		/// creatures the ones of the hardest biome, then the healthiest - and the tool each takes, as many as the village
		/// can spare (see QuestRules.GroupSize), and on a trip for pockets, every viking it buys them for (see
		/// QuestRules.PocketsGroup). Who goes of NPC and PC vikings is picked by NPCQuests.Pick. A quest that takes a tool
		/// needs at least one
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for</param>
		/// <param name="amount">How many of it the village wants</param>
		/// <param name="source">The way the quest gets it, with the tool it takes</param>
		/// <param name="buys">For a trip to Haldor for circlets and belts, what it buys (see GearChoice), or null for one
		/// of what the quest buys</param>
		/// <returns>The quest, not noted yet, or null when the village can't spare the vikings</returns>
		internal static QuestRecord Plan(Village village, QuestKind kind, string item, int amount, ResourceSource source, List<KeyValuePair<string, int>> buys = null)
		{
			ResourceBook book = ResourceBookGame.Get();
			QuestRule rule = ((book != null) ? book.RuleOf(kind) : null);
			if (rule == null)
			{
				return(null);
			}
			List<Character> vikings = village.Vikings().FindAll((Character viking) => viking != null && !viking.IsDead());
			int biome = VillageCooking.VillageBiome(village);
			int questBiome = QuestGame.QuestBiomeOf(kind, (item ?? ""), biome, RouteBiome(village, kind, (item ?? "")), Mathf.Max(rule.MinBiome, source.MinBiome));
			List<Humanoid> fit = new List<Humanoid>();
			foreach (Character character in vikings)
			{
				Humanoid humanoid = character as Humanoid;
				if (humanoid == null || humanoid.GetHealthPercentage() < 0.75f || VikingIdentity.Get(humanoid.m_nview.GetZDO()).Length == 0)
				{
					continue;
				}
				// An NPC viking only goes on the quests that don't fight or take a weapon to the work (see NPCQuests.Allows)
				if (VikingCore.IsNPCViking(humanoid) && !NPCQuests.Allows(kind))
				{
					continue;
				}
				// One whose gear is so far behind the quest that it would be expected to lose all its health stays home
				VikingCore core = humanoid.GetComponent<VikingCore>();
				if (QuestFightGame.ExpectedToDie(kind, humanoid, rule.VikingsFor(biome)) ?? QuestSim.ExpectedToDie(kind, questBiome - ((core != null) ? core.BiomeIndex() : biome)))
				{
					continue;
				}
				fit.Add(humanoid);
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
			// A quest that fights takes the strongest first - those of the hardest biome - so the weaker stay home
			bool fights = QuestLoot.Fights(kind);
			fit.Sort((Humanoid a, Humanoid b) =>
			{
				int lacking = Lacks(kind, b).CompareTo(Lacks(kind, a));
				if (lacking != 0)
				{
					return(lacking);
				}
				int tools = CarriesTool(b, source).CompareTo(CarriesTool(a, source));
				if (tools != 0)
				{
					return(tools);
				}
				int strength = (fights ? BiomeOf(b, biome).CompareTo(BiomeOf(a, biome)) : 0);
				return((strength != 0) ? strength : b.GetHealthPercentage().CompareTo(a.GetHealthPercentage()));
			});
			int carried = fit.FindAll((Humanoid humanoid) => CarriesTool(humanoid, source)).Count;
			if (source.Tool != ToolKind.None && carried + chestTools.Count == 0)
			{
				return(null);
			}
			int size = QuestRules.GroupSize(rule, biome, ((source.Tool != ToolKind.None) ? carried + chestTools.Count : 0), fit.Count, vikings.Count);
			if (size <= 0)
			{
				return(null);
			}
			// Pockets are sewn into the buyer's own clothes, so every viking the trip buys them for goes along, as many as
			// the village can spare
			bool pockets = ResourceBook.BuysPockets(kind);
			if (pockets)
			{
				List<Humanoid> lacking = fit.FindAll((Humanoid viking) => Lacks(kind, viking));
				int buyers = Mathf.Min(Mathf.Max(1, amount), lacking.Count);
				bool pcBuys = lacking.Exists((Humanoid viking) => !VikingCore.IsNPCViking(viking));
				size = QuestRules.PocketsGroup(size, buyers, pcBuys, fit.Count, vikings.Count);
			}
			QuestRecord record = new QuestRecord();
			record.Id = System.Guid.NewGuid().ToString("N");
			record.Kind = kind;
			record.Item = (item ?? "");
			record.Amount = amount;
			// NPC vikings forage and find bees first, and a trip to a trader takes one PC viking with NPC vikings - waiting
			// for a PC viking fit to go
			List<Humanoid> going = NPCQuests.Pick(kind, fit, (Humanoid viking) => VikingCore.IsNPCViking(viking), (Humanoid viking) => Lacks(kind, viking), size);
			if (going.Count == 0)
			{
				return(null);
			}
			if (pockets)
			{
				// It buys pockets for each of those going who lacks them
				int buying = going.FindAll((Humanoid viking) => Lacks(kind, viking)).Count;
				record.Amount = buying;
				record.Buys = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>(ResourceBook.Bought(kind), buying) };
			}
			else if (ResourceBook.IsPurchase(kind))
			{
				record.Buys = (((buys != null) && (buys.Count > 0)) ? new List<KeyValuePair<string, int>>(buys) : new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>(ResourceBook.Bought(kind), 1) });
			}
			foreach (Humanoid member in going)
			{
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
		/// QuestGame.Prepare), and for one whose road takes to the sea by the village, the boats it sails in and
		/// QuestRules.SailOffSeconds more to set out (see PlanBoats) - noting it, and stopping what its vikings on this
		/// client are doing, so they get ready to go. A quest one of whose vikings isn't here any more is given up on, for
		/// another to be picked
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
			// A quest that sails from the village leaves the cart at home
			bool cart = (VillageCarts.AtHome(village) && (exit.Trapped || SeaOf(village, record.Kind, record.Item).Count < 2));
			if (!QuestGame.Prepare(village, record, source, travel, back, RouteBiome(village, record.Kind, record.Item), cart, out string why))
			{
				Plugin.Log.LogInfo("Quest: " + record.Kind + " from the village at " + village.Farm.ToString("F0") + " was given up on, as " + why);
				TestLog.Count("Quests", record.Kind + " given up on");
				Write(village, null);
				// A quest that isn't needed counts as the last, so the village rests before picking one again rather than
				// picking it again at once
				if (why == QuestGame.NothingWanted)
				{
					village.Zdo.Set(LastQuestKey, NetTime.Now());
				}
				return;
			}
			if (!record.Trapped && PlanBoats(village, record, SeaOf(village, record.Kind, record.Item)))
			{
				record.Due += NetTime.Ticks(QuestRules.SailOffSeconds);
			}
			Write(village, record);
			VillageLife.CallToQuest(village, record);
			TestLog.Count("Quests", record.Kind + " set out");
			float seconds = (record.Due - record.SetOutAt) / (float)NetTime.Ticks(1f);
			Plugin.Log.LogInfo("Quest: " + record.Kind + " from the village at " + village.Farm.ToString("F0") + " sets out from " + record.Exit.ToString("F0") + (record.Trapped ? ", inside the village, as it's trapped" : "") + ", " + travel.ToString("F0") + "s there, " + back.ToString("F0") + "s back and " + record.ActiveSeconds.ToString("F0") + "s there, due back in " + seconds.ToString("F0") + "s");
		}

		/// <summary>
		/// How far past a village's edge its own boats can be moored to sail a quest from it, in meters - as far out as a
		/// village's boat is moored (see VillageBoatSite)
		/// </summary>
		private const float OwnBoatReach = 80f;

		/// <summary>
		/// Plans the boats a quest sails in from its village, when its road sets out across the sea there (see
		/// BoatRules.QuestSea) - the village's own boats, no player's, that are loaded with nobody on board, the biggest
		/// first and only those that hold up in the sea it crosses, as many as its vikings fill, each taken over for the
		/// quest (see VikingBoat.Enlist), and new boats of the best kind the village's biome has, or a drakkar on a sea
		/// that boils, for the rest, which its first viking builds at the shore (see BoatRules.QuestCrews)
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <param name="record">The quest, its vikings picked, given its sea and boats</param>
		/// <param name="sea">The sea its road sets out across, with no waypoints when it goes on foot</param>
		/// <returns>True when it sails</returns>
		private static bool PlanBoats(Village village, QuestRecord record, TravelRoad sea)
		{
			if (sea.Count < 2 || ZNetScene.instance == null)
			{
				return(false);
			}
			bool burning = SeaBurns(sea);
			int biome = VillageCooking.VillageBiome(village);
			if (burning && !TravelTime.BoatSurvivesAshlands(biome))
			{
				biome = TravelTime.BiomeOfBoat("drakkar");
			}
			string built = TravelTime.BoatPrefab(biome);
			if (ZNetScene.instance.GetPrefab(built) == null)
			{
				return(false);
			}
			List<Ship> own = OwnBoats(village, burning);
			List<List<string>> builtCrews = BoatRules.QuestCrews(record.Members, own.ConvertAll((Ship ship) => TravelTime.BoatCrew(TravelTime.BiomeOfBoat(Utils.GetPrefabName(ship.gameObject)))), TravelTime.BoatCrew(biome), out List<List<string>> ownCrews);
			record.Sea = sea;
			record.Boats.Clear();
			Vector3 land = sea.Points[sea.Count - 1];
			for (int i = 0; i < ownCrews.Count; i++)
			{
				record.Boats.Add(new QuestBoat { Prefab = Utils.GetPrefabName(own[i].gameObject), Owned = true, Crew = ownCrews[i] });
				TravelRoad route = new TravelRoad();
				route.Add(own[i].transform.position, TravelLeg.Foot);
				for (int p = 1; p < sea.Count; p++)
				{
					if (sea.SeaLeg(p))
					{
						route.Add(sea.Points[p], TravelLeg.Sea);
					}
				}
				VikingBoat.Enlist(own[i], record.BoatGroup, i, ownCrews[i], route, land, record.Id);
			}
			foreach (List<string> crew in builtCrews)
			{
				record.Boats.Add(new QuestBoat { Prefab = built, Owned = false, Crew = crew });
			}
			Plugin.Log.LogInfo("Quest: " + record.Kind + " from the village at " + village.Farm.ToString("F0") + " sails from " + sea.Points[0].ToString("F0") + " in " + ownCrews.Count + " of the village's boats and " + builtCrews.Count + " new " + TravelTime.BoatName(biome) + ((builtCrews.Count == 1) ? "" : "s"));
			return(true);
		}

		/// <summary>
		/// Finds a village's own boats a quest can sail in - ships of the vikings' kinds no player built, within
		/// OwnBoatReach of its edge, loaded with nobody on board and not out with other vikings, and on a sea that boils
		/// only those that hold up in it - the biggest first
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="burning">Whether the sea the quest crosses boils</param>
		/// <returns>The boats</returns>
		private static List<Ship> OwnBoats(Village village, bool burning)
		{
			HashSet<int> kinds = new HashSet<int>();
			foreach (string prefab in TravelTime.BoatPrefabNames())
			{
				kinds.Add(prefab.GetStableHashCode());
			}
			List<ZDO> near = new List<ZDO>();
			VillageRespawn.ZdosNear(village.Middle, village.Radius + OwnBoatReach, near);
			List<Ship> found = new List<Ship>();
			foreach (ZDO zdo in near)
			{
				if (!kinds.Contains(zdo.GetPrefab()) || zdo.GetLong(ZDOVars.s_creator, 0L) != 0L || zdo.GetString(VikingBoat.GroupKey, "").Length > 0 || village.DistanceOutside(zdo.GetPosition()) > OwnBoatReach)
				{
					continue;
				}
				ZNetView instance = ZNetScene.instance.FindInstance(zdo);
				Ship ship = ((instance != null) ? instance.GetComponent<Ship>() : null);
				if (ship == null || ship.m_players.Count > 0 || VikingBoat.Of(ship) != null)
				{
					continue;
				}
				int boatBiome = TravelTime.BiomeOfBoat(Utils.GetPrefabName(ship.gameObject));
				if (burning && !TravelTime.BoatSurvivesAshlands(boatBiome))
				{
					continue;
				}
				found.Add(ship);
			}
			found.Sort((Ship a, Ship b) => TravelTime.BoatCrew(TravelTime.BiomeOfBoat(Utils.GetPrefabName(b.gameObject))).CompareTo(TravelTime.BoatCrew(TravelTime.BiomeOfBoat(Utils.GetPrefabName(a.gameObject)))));
			return(found);
		}

		/// <summary>
		/// Checks whether a quest's sea boils anywhere along it, burning a boat not made for it (see
		/// TravelPlanner.Sample)
		/// </summary>
		/// <param name="sea">The sea</param>
		/// <returns>True when it does</returns>
		private static bool SeaBurns(TravelRoad sea)
		{
			for (int i = 1; i < sea.Count; i++)
			{
				int samples = Mathf.Max(1, Mathf.CeilToInt(TravelRoad.Flat(sea.Points[i - 1], sea.Points[i]) / (TravelRoute.CellSize / 2f)));
				for (int s = 1; s <= samples; s++)
				{
					Vector3 point = Vector3.Lerp(sea.Points[i - 1], sea.Points[i], (float)s / samples);
					if (WorldGenerator.GetAshlandsOceanGradient(point.x, point.z) > 0f)
					{
						return(true);
					}
				}
			}
			return(false);
		}

		/// <summary>
		/// How near the shore a quest set sail from a player has to be for its vikings to sail home where they can be
		/// seen, in meters, rather than come home with their boats moored
		/// </summary>
		private const float SailHomeWatch = BoatRules.QuestSailIn + 60f;

		/// <summary>
		/// Brings a quest that sailed from its village home by boat while a player is near enough to see - each of its
		/// boats with any of its crew still living back out at sea (see BoatRules.SailInFrom), with them on board, sailing
		/// in to the shore it set sail from, where it's left for the village or broken up (see KeepsBoat). A boat whose
		/// crew all died is lost with them
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="vikings">Its PC vikings' ZDOs, added to</param>
		/// <param name="entry">The quest's vikings coming home, those brought in on board taken out of it</param>
		/// <param name="living">The snapshots of those of them still living</param>
		/// <returns>True when they sail home, and false when they come home the usual way</returns>
		internal static bool SailHome(Village village, List<ZDO> vikings, TravelEntry entry, List<byte[]> living)
		{
			QuestRecord record = Read(village);
			if (record == null || record.Id != entry.QuestId || !record.BySea || living.Count == 0 || ZNetScene.instance == null)
			{
				return(false);
			}
			Vector3 shore = record.Sea.Points[0];
			if (!Player.IsPlayerInRange(shore, SailHomeWatch))
			{
				return(false);
			}
			Dictionary<string, byte[]> alive = LivingById(living);
			Vector3 start = BoatRules.SailInFrom(record.Sea, out TravelRoad home);
			Vector3 facing = ((home.Count > 1) ? home.Points[1] : shore) - start;
			facing.y = 0f;
			Vector3 side = Vector3.Cross(Vector3.up, (facing.sqrMagnitude > 0.01f) ? facing.normalized : Vector3.forward);
			HashSet<string> keptKinds = new HashSet<string>();
			string group = "home" + record.BoatGroup;
			int placed = 0;
			for (int i = 0; i < record.Boats.Count; i++)
			{
				QuestBoat plan = record.Boats[i];
				List<string> crew = plan.Crew.FindAll((string member) => alive.ContainsKey(member));
				GameObject prefab = ZNetScene.instance.GetPrefab(plan.Prefab);
				if (crew.Count == 0 || prefab == null)
				{
					continue;
				}
				float offset = ((placed + 1) / 2) * ((placed % 2 == 1) ? 1f : -1f) * VikingBoat.SailInSpacing;
				Vector3 spot = start + side * offset;
				TravelRoad route = new TravelRoad();
				route.Add(spot, TravelLeg.Foot);
				for (int p = 1; p < home.Count; p++)
				{
					route.Add(home.Points[p], TravelLeg.Sea);
				}
				VikingBoat boat = VikingBoat.Build(prefab, spot, facing, group, i, crew, route, shore, 0, 0);
				if (boat == null)
				{
					continue;
				}
				placed++;
				boat.MarkQuest(record.Id, true, plan.Owned);
				boat.SetKept(KeepsBoat(village, plan, keptKinds));
				for (int seat = 0; seat < crew.Count; seat++)
				{
					byte[] snapshot = alive[crew[seat]];
					ZDO zdo = VillageRespawn.SpawnArrivalAt(village, vikings, snapshot, boat.Seat(seat).position, false);
					if (zdo != null)
					{
						zdo.Set(VikingBoat.BoatKey, boat.Key);
						zdo.Set(VikingBoat.BoardKey, (int)Crossing.Aboard);
						entry.Vikings.Remove(snapshot);
					}
				}
				boat.StartHomeward();
			}
			Plugin.Log.LogInfo("Quest: " + record.Kind + " sails home to the village at " + village.Farm.ToString("F0") + " in " + placed + " boats, from " + start.ToString("F0"));
			TestLog.Count("Quests", record.Kind + " sailed home");
			return(placed > 0);
		}

		/// <summary>
		/// Moors the boats a quest that sailed from its village comes home in while no player is near to see them sail in
		/// - each with any of its crew still living that's left for the village (see KeepsBoat). A boat whose crew all died
		/// is lost with them
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="entry">The quest's vikings coming home</param>
		/// <param name="living">The snapshots of those of them still living</param>
		internal static void MoorQuestBoats(Village village, TravelEntry entry, List<byte[]> living)
		{
			QuestRecord record = Read(village);
			if (record == null || record.Id != entry.QuestId || !record.BySea)
			{
				return;
			}
			Dictionary<string, byte[]> alive = LivingById(living);
			HashSet<string> keptKinds = new HashSet<string>();
			foreach (QuestBoat plan in record.Boats)
			{
				if (plan.Crew.Exists((string member) => alive.ContainsKey(member)) && KeepsBoat(village, plan, keptKinds))
				{
					VillageShoreBoat.MoorAnother(village, plan.Prefab);
				}
			}
		}

		/// <summary>
		/// Checks whether a quest's boat is left for its village once its vikings are home - the village's own always, and
		/// a new one when the village has no boat of its kind yet, nor another of the quest's boats of that kind kept
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="plan">The boat</param>
		/// <param name="keptKinds">The kinds of the quest's boats kept so far, added to</param>
		/// <returns>True when it's left</returns>
		private static bool KeepsBoat(Village village, QuestBoat plan, HashSet<string> keptKinds)
		{
			bool kept = (plan.Owned || (!keptKinds.Contains(plan.Prefab) && !VikingBoat.HasSuchBoat(village, plan.Prefab.GetStableHashCode(), null)));
			if (kept)
			{
				keptKinds.Add(plan.Prefab);
			}
			return(kept);
		}

		/// <summary>
		/// Finds the PC vikings of a quest coming home by their GUIDs
		/// </summary>
		/// <param name="living">Their snapshots</param>
		/// <returns>Each snapshot, by the viking's GUID</returns>
		private static Dictionary<string, byte[]> LivingById(List<byte[]> living)
		{
			Dictionary<string, byte[]> alive = new Dictionary<string, byte[]>();
			foreach (byte[] snapshot in living)
			{
				string id = VikingIdentity.OfSnapshot(snapshot);
				if (id.Length > 0)
				{
					alive[id] = snapshot;
				}
			}
			return(alive);
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
			List<string> failed = new List<string>();
			bool noSpot = false;
			for (int i = 0; i < TrappedTries && Pathfinding.instance != null; i++)
			{
				if (!village.ArrivalSpot(out Vector3 spot))
				{
					noSpot = true;
					TestLog.Count("Trapped villages, by why a way out failed", "no spot outside found");
					break;
				}
				lastTried = spot;
				if (Pathfinding.instance.GetPath(from, spot, path, Pathfinding.AgentType.Humanoid, true))
				{
					exit.Trapped = false;
					exit.Spot = spot;
					break;
				}
				failed.Add(spot.ToString("F0") + ", " + PathFailure(from, spot, path));
			}
			if (exit.Trapped)
			{
				// The farthest it can get toward a spot outside is as near the edge as it gets
				Vector3 toward = ((lastTried != Vector3.zero) ? lastTried : village.Farm + Vector3.forward * (village.Radius + 10f));
				string fallback = "no navmesh";
				if (Pathfinding.instance != null)
				{
					fallback = "no path at all toward " + toward.ToString("F0");
					if (Pathfinding.instance.GetPath(from, toward, path, Pathfinding.AgentType.Humanoid, false) && path.Count > 0)
					{
						exit.Spot = path[path.Count - 1];
						fallback = "the farthest it gets toward " + toward.ToString("F0") + " is " + Utils.DistanceXZ(exit.Spot, toward).ToString("F1") + "m short of it";
					}
				}
				string why = ((failed.Count > 0) ? failed.Count + " of " + TrappedTries + " spots outside it tried from " + from.ToString("F0") + " had no whole path there - " + string.Join("; ", failed) : "no spot outside it tried from " + from.ToString("F0")) + (noSpot ? ", then no open spot outside it was found" : "");
				Plugin.Log.LogInfo("Quest: the village at " + village.Farm.ToString("F0") + " is trapped, so its vikings leave from " + exit.Spot.ToString("F0") + " - " + why + ", and " + fallback);
			}
			exits[id] = exit;
			return(exit);
		}

		/// <summary>
		/// Works out why there's no whole path between two spots, for the log and counted - one of them not snapping to
		/// the navmesh the way the game's path finding snaps them, only a partial path, ending how far short, or no path
		/// at all
		/// </summary>
		/// <param name="from">Where the path starts</param>
		/// <param name="to">Where it ends</param>
		/// <param name="path">A list for the path's corners, cleared</param>
		/// <returns>Why, like "only a partial path, ending 12.0m short of it"</returns>
		private static string PathFailure(Vector3 from, Vector3 to, List<Vector3> path)
		{
			Pathfinding pathfinding = Pathfinding.instance;
			Pathfinding.AgentSettings settings = pathfinding.GetSettings(Pathfinding.AgentType.Humanoid);
			string why;
			string kind;
			Vector3 start = from;
			Vector3 end = to;
			if (!pathfinding.SnapToNavMesh(ref start, true, settings))
			{
				why = "the start off the navmesh";
				kind = why;
			}
			else if (!pathfinding.SnapToNavMesh(ref end, true, settings))
			{
				why = "the spot off the navmesh";
				kind = why;
			}
			else if (pathfinding.GetPath(from, to, path, Pathfinding.AgentType.Humanoid, false) && path.Count > 0)
			{
				why = "only a partial path, ending " + Utils.DistanceXZ(path[path.Count - 1], to).ToString("F1") + "m short of it";
				kind = "only a partial path";
			}
			else
			{
				why = "no path at all";
				kind = why;
			}
			path.Clear();
			TestLog.Count("Trapped villages, by why a way out failed", kind);
			return(why);
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
		/// <param name="vikings">Its PC vikings' ZDOs</param>
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
		/// Checks whether a viking coming in from the road died on the quest it was out on (see QuestOutcome.Died), from
		/// the values it was kept as
		/// </summary>
		/// <param name="snapshot">The viking's values (see VillageDeparted.SnapshotOf)</param>
		/// <returns>True when it did</returns>
		public static bool DiedOnQuest(byte[] snapshot)
		{
			VillageDeparted.VikingValues values = VillageDeparted.Decode(snapshot);
			int at = ((values != null) ? values.Bytes.FindIndex((KeyValuePair<int, byte[]> entry) => entry.Key == QuestOutcomeKey) : -1);
			QuestOutcome outcome = ((at >= 0) ? QuestOutcome.Decode(values.Bytes[at].Value) : null);
			return(outcome != null && outcome.Died);
		}

		/// <summary>
		/// Notes the death of a viking that died on a quest of a village, as the others come home without it - the way a
		/// viking dying at home is noted, so another comes in its place in time, the village remembers it by name and the
		/// neighbors hear of it
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="snapshot">The viking's values (see VillageDeparted.SnapshotOf)</param>
		public static void FellOnQuest(Village village, byte[] snapshot)
		{
			if (ZRoutedRpc.instance == null)
			{
				return;
			}
			VillageDeparted.VikingValues values = VillageDeparted.Decode(snapshot);
			string name = "";
			string author = "";
			if (values != null)
			{
				VikingNames.ReadRaw(values, out name, out author);
			}
			long now = NetTime.Now();
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, VillageRespawn.DeathRpc, village.Farm, now, false);
			if (name.Length > 0)
			{
				ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, VillageFallen.FallenRpc, village.Farm, name, author, now);
			}
			string shown = VikingNames.Filter(name, author).RemoveRichTextTags().Trim();
			NeighborNews.Report(NeighborEvent.Death, village.Farm, VillageNames.Get(village), shown);
			TestLog.Count("Quests", "vikings who died on one");
			Plugin.Log.LogInfo("Quest: " + ((shown.Length > 0) ? shown : "a viking") + " of the village at " + village.Farm.ToString("F0") + " died on it and didn't come home");
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
		/// Describes everything behind a village's quests, for the kri_vv_quest command - its goal and the next step
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
				lines.Add(Headline(record, NetTime.Now(), DuePhrase(record)) + " - " + string.Join(", ", record.Names) + ", " + record.Amount + " wanted, " + record.ActiveSeconds.ToString("F0") + "s of work, quest biome " + record.QuestBiome + ", " + record.Wanted + " vikings wanted, leaving from " + record.Exit.ToString("F0"));
				for (int i = 0; i < record.Members.Count; i++)
				{
					lines.Add("  " + MemberLine(record, i));
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
						RouteKeys(kind, item, out int thereKey, out int backKey, out int atKey, out int biomeKey);
						float there = village.Zdo.GetFloat(thereKey, -1f);
						if (there >= 0f)
						{
							float back = village.Zdo.GetFloat(backKey, there);
							float age = (NetTime.Now() - village.Zdo.GetLong(atKey, 0L)) / (float)NetTime.Ticks(1f);
							int biome = village.Zdo.GetInt(biomeKey, -1);
							roads.Add(kind + ((item.Length > 0) ? " " + item : "") + " " + there.ToString("F0") + "s" + ((Mathf.Abs(back - there) >= 1f) ? "/" + back.ToString("F0") + "s back" : "") + ((biome >= 0) ? ", to biome " + biome : "") + " (" + age.ToString("F0") + "s old)");
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
		/// Starts a quest of a kind for a village at once, for the kri_vv_quest command - whatever the village wants,
		/// however long since its last and whatever the time of day - for the item given, by its prefab name in any case,
		/// or else the first a quest of that kind brings back that it's allowed to go for
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or "" for the first it can go for</param>
		/// <param name="roadAsked">Set to true when it waits for the server to plan the road, to be tried again</param>
		/// <returns>What happened</returns>
		public static string Force(Village village, QuestKind kind, string item, out bool roadAsked)
		{
			roadAsked = false;
			ResourceBook book = ResourceBookGame.Get();
			if (book == null || !village.IsOwner())
			{
				return("Only the client running the village can start its quests, once the game has loaded");
			}
			if (Read(village) != null)
			{
				return("The village is already on a quest - kri_vv_quest due brings it home");
			}
			List<string> routeItems = RouteItems(book, kind);
			if (item.Length > 0)
			{
				string named = routeItems.Find((string candidate) => candidate.Length > 0 && string.Equals(candidate, item, System.StringComparison.OrdinalIgnoreCase));
				if (named == null)
				{
					List<string> brought = routeItems.FindAll((string candidate) => candidate.Length > 0);
					return(kind + " doesn't bring back " + item + ((brought.Count > 0) ? " - it can go for " + string.Join(", ", brought) : " - it isn't for any item in particular"));
				}
				item = named;
			}
			VillageHoldings holdings = VillageLife.HoldingsOf(village);
			List<string> items = ((item.Length > 0) ? new List<string> { item } : routeItems);
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
				roadAsked = true;
				return("Asked the server for the road to " + kind);
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
		/// Brings a village's quest home now, for the kri_vv_quest command - on the server, which keeps its vikings
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
		/// Describes a village's quest for its debug summary (see VillageDebug) - the quest it's on and when it's due back,
		/// counting down, where its vikings are, and a line for each of them - or with none, how long it rests before it
		/// looks for one
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="arrivals">The groups on their way to it (see VillageArrivals.Of), or null while the server hasn't
		/// answered</param>
		/// <param name="now">The time now, in ZNet time ticks</param>
		/// <returns>The lines, ending in a line break</returns>
		public static string Describe(Village village, List<VillageArrivals.Arrival> arrivals, long now)
		{
			QuestRecord record = Read(village);
			if (record == null)
			{
				return("Quest: none - " + Resting(village, now) + "\n");
			}
			StringBuilder text = new StringBuilder(Headline(record, now, DuePhrase(record)) + "\n");
			if (record.Phase != QuestPhase.Waiting)
			{
				VillageArrivals.Arrival group = ((arrivals != null) ? arrivals.Find((VillageArrivals.Arrival arrival) => arrival.QuestId == record.Id) : null);
				if (arrivals == null)
				{
					text.Append("  Where they are: asking the server\n");
				}
				else if (group != null)
				{
					text.Append("  " + group.Count + " of " + record.Members.Count + " left the village, " + group.Where + "\n");
				}
				else
				{
					text.Append("  " + ((record.Phase == QuestPhase.Leaving) ? "None of them has left the village yet" : "None of them is on the road") + "\n");
				}
			}
			for (int i = 0; i < record.Members.Count; i++)
			{
				text.Append("  " + MemberLine(record, i) + "\n");
			}
			return(text.ToString());
		}

		/// <summary>
		/// Describes a quest on a line of its own - what it's for, where it's at and when it's due back, counting down,
		/// with the game's time it's due back by, and whether the village is trapped
		/// </summary>
		/// <param name="record">The quest</param>
		/// <param name="now">The time now, in ZNet time ticks</param>
		/// <param name="gameTime">When it's due back in the game's time (see DuePhrase), or ""</param>
		/// <returns>The line</returns>
		internal static string Headline(QuestRecord record, long now, string gameTime)
		{
			string due = "waiting for the morning";
			if (record.Phase != QuestPhase.Waiting)
			{
				due = "due back " + VillageArrivals.Countdown(record.Due, now) + ((gameTime.Length > 0 && gameTime != "now") ? " (game time: " + gameTime + ")" : "");
			}
			return("Quest: " + record.Kind + ((record.Item.Length > 0) ? " for " + record.Item : "") + ", " + record.Phase.ToString().ToLowerInvariant() + ", " + due + (record.Trapped ? ", trapped" : ""));
		}

		/// <summary>
		/// Describes what a viking does on a quest - the tool it takes from a chest, and once the quest has set out, the
		/// tool it works with, the enemies it kills, the health the quest's fight harms it by, what it brings home, what
		/// the village's cart it pulls brings home, and what it spends
		/// </summary>
		/// <param name="record">The quest</param>
		/// <param name="index">The viking's index among its members</param>
		/// <returns>The line</returns>
		internal static string MemberLine(QuestRecord record, int index)
		{
			MemberPlan plan = ((index < record.Plans.Count) ? record.Plans[index] : new MemberPlan());
			string name = ((index < record.Names.Count && record.Names[index].Length > 0) ? record.Names[index] : record.Members[index]);
			string tool = ((index < record.Tools.Count) ? record.Tools[index] : "");
			string loot = string.Join(", ", plan.Loot.ConvertAll((KeyValuePair<string, int> entry) => entry.Value + " " + entry.Key));
			string spend = string.Join(", ", plan.Spend.ConvertAll((KeyValuePair<string, int> entry) => entry.Value + " " + entry.Key));
			string cart = string.Join(", ", plan.CartLoot.ConvertAll((KeyValuePair<string, int> entry) => entry.Value + " " + entry.Key));
			return(name + ((tool.Length > 0) ? ", taking " + tool + " from a chest" : "") + ((plan.Tool.Length > 0) ? ", working with " + plan.Tool + " (" + plan.Hits + " hits, " + plan.ToolLeft.ToString("F0") + " durability left)" : "") + ((plan.Kills > 0) ? ", " + plan.Kills + " kills" : "") + ((plan.FightHarm >= 0f) ? ", harmed by " + plan.FightHarm.ToString("F0") + " health in the fight" : "") + ", bringing home " + ((loot.Length > 0) ? loot : "nothing") + ((cart.Length > 0) ? ", and pulling the village's cart home with " + cart : "") + ((spend.Length > 0) ? ", spending " + spend : ""));
		}

		/// <summary>
		/// Says when a village with no quest looks for one - not until its vikings move in, once it has rested a while
		/// since its last came back (see QuestRules.RestBetween), counting down, or every LookInterval once it has
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="now">The time now, in ZNet time ticks</param>
		/// <returns>The phrase</returns>
		private static string Resting(Village village, long now)
		{
			if (!village.Zdo.GetBool(VillageRespawn.PopulatedKey) && !village.IsFounded())
			{
				return("not looking for one until its vikings move in");
			}
			long last = village.Zdo.GetLong(LastQuestKey, 0L);
			string back = ((last > 0L) ? "the last came back " + VillageArrivals.Countdown(last, now) + ", " : "");
			long ready = ((last > 0L && EnvMan.instance != null) ? last + NetTime.Ticks(EnvMan.instance.m_dayLengthSec * QuestRules.RestBetween) : 0L);
			if (now < ready)
			{
				return(back + "resting, looking for one " + VillageArrivals.Countdown(ready, now));
			}
			return(back + "looking for one every " + LookInterval.ToString("F0") + "s");
		}
	}
}
