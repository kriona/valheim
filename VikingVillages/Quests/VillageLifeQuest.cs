using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A viking's part in its village's quests (see VillageQuests) - getting ready and leaving the village when one sets
	/// out, and on coming back, putting away what it brought home before anything else
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// What a viking carries an item for on a quest - the tool it took from a chest - which goes back in a chest once
		/// it's home
		/// </summary>
		private const string CarryQuest = "quest";

		/// <summary>
		/// On a living viking, that it carries loot home from a quest, which it puts away before anything else, even at
		/// night
		/// </summary>
		public static readonly int QuestLootKey = "kriona.VikingVillages.vikingQuestLoot".GetStableHashCode();

		/// <summary>
		/// How near the spot a quest's vikings leave from one has to get before it goes, in meters
		/// </summary>
		private const float ExitReach = 3f;

		/// <summary>
		/// Whether the viking has looked at whether it just came back from a quest, as it first runs
		/// </summary>
		private bool questChecked;

		/// <summary>
		/// The wood in a village's chests it counts as running out at, and how long it goes without finding food to
		/// forage or prey to hunt before the food around it counts as gone, as a share of a day
		/// </summary>
		internal const int WoodLow = 10;
		private const float FoodGoneDays = 0.5f;

		/// <summary>
		/// When each village's vikings on this client last found food around it - picked food or honey, or went for prey
		/// that drops some - or when that was first looked at, by its ZDO, in ZNet time ticks
		/// </summary>
		private static readonly Dictionary<ZDOID, long> foodFoundAt = new Dictionary<ZDOID, long>();

		/// <summary>
		/// Notes that the viking found food around its village - picked food or honey, or went for prey that drops some
		/// </summary>
		private void NoteFoodFound()
		{
			if (village != null && village.Zdo != null)
			{
				foodFoundAt[village.Zdo.m_uid] = NetTime.Now();
			}
		}

		/// <summary>
		/// Checks whether a village's vikings have found nothing to forage or hunt around it for FoodGoneDays
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>True when they haven't</returns>
		internal static bool FoodGone(Village village)
		{
			ZDOID id = village.Zdo.m_uid;
			long now = NetTime.Now();
			if (!foodFoundAt.TryGetValue(id, out long at))
			{
				foodFoundAt[id] = now;
				return(false);
			}
			return(EnvMan.instance != null && now - at > NetTime.Ticks(EnvMan.instance.m_dayLengthSec * FoodGoneDays));
		}

		/// <summary>
		/// Checks whether a village's chests hold too little food for its vikings to keep their foods up (see
		/// FoodEnough)
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>True when they do</returns>
		internal static bool FoodLow(Village village)
		{
			return(!FoodEnough(ChestFoodOf(village), village.Vikings().Count));
		}

		/// <summary>
		/// Checks whether a village's chests hold so little food that it sends vikings out on a quest for more, rather
		/// than only foraging and hunting around it (see FoodScarceFor)
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>True when they do</returns>
		internal static bool FoodScarce(Village village)
		{
			return(FoodScarceFor(ChestFoodOf(village), village.Vikings().Count));
		}

		/// <summary>
		/// Checks whether there's so little food that a village sends vikings out for more - less than a food for each of
		/// its vikings
		/// </summary>
		/// <param name="food">How much food the chests hold</param>
		/// <param name="vikings">How many vikings the village has loaded</param>
		/// <returns>True when there is</returns>
		internal static bool FoodScarceFor(int food, int vikings)
		{
			return(food < Mathf.Max(1, vikings));
		}

		/// <summary>
		/// Counts the food in a village's chests
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>How many foods</returns>
		private static int ChestFoodOf(Village village)
		{
			int food = 0;
			foreach (Container chest in VillageChests(village))
			{
				foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
				{
					food += (VikingFood.IsFood(item) ? item.m_stack : 0);
				}
			}
			return(food);
		}

		/// <summary>
		/// Counts the wood in a village's chests
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The wood, of every kind</returns>
		internal static int ChestWoodOf(Village village)
		{
			int wood = 0;
			foreach (Container chest in VillageChests(village))
			{
				wood += ChestWood(chest);
			}
			return(wood);
		}

		/// <summary>
		/// Checks whether a village is out of wood - fewer than WoodLow in its chests, and a viking of it on this client
		/// found none to gather lately
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>True when it is</returns>
		internal static bool OutOfWood(Village village)
		{
			if (ChestWoodOf(village) >= WoodLow)
			{
				return(false);
			}
			foreach (VillageLife life in All)
			{
				if (life != null && life.village == village && Time.time < life.noWoodUntil)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Forgets when each village last found food and when its vikings home from a quest last found it empty, as the
		/// world is left
		/// </summary>
		internal static void ForgetFood()
		{
			foodFoundAt.Clear();
			foundEmptyAt.Clear();
			mournedCompanionsAt.Clear();
		}

		/// <summary>
		/// How long after coming home from a quest a viking looks around for the rest of its village, how long apart it
		/// says each line on finding them all dead, and the most of the dead it mourns by name, in seconds
		/// </summary>
		private const float HomecomingDelay = 6f;
		private const float HomecomingLineGap = 6f;
		private const int MournedMax = 4;

		/// <summary>
		/// How long the vikings of a quest count as coming home together, so one doesn't take another just back for the
		/// rest of the village, in seconds
		/// </summary>
		private const float CameHomeTogether = 300f;

		/// <summary>
		/// When the viking looks around for the rest of its village on coming home from a quest, in Time.time seconds, or
		/// less than 0 when it doesn't, when the quest set out, in ZNet time ticks, and the names of the others who went
		/// with it
		/// </summary>
		private float homecomingAt = -1f;
		private long questSetOutAt;
		private List<string> questCompanions = new List<string>();

		/// <summary>
		/// What the viking still has to say on coming home to a village whose vikings all died, and when it says the next
		/// line, in Time.time seconds
		/// </summary>
		private readonly Queue<string> homecomingLines = new Queue<string>();
		private float nextHomecomingLine;

		/// <summary>
		/// When a viking of each village on this client last came home from a quest to find it empty, and last mourned
		/// one of those it went with who died on it, by its ZDO, in Time.time seconds - so only the first of them speaks
		/// </summary>
		private static readonly Dictionary<ZDOID, float> foundEmptyAt = new Dictionary<ZDOID, float>();
		private static readonly Dictionary<ZDOID, float> mournedCompanionsAt = new Dictionary<ZDOID, float>();

		/// <summary>
		/// Gets how the viking's last quest went, while it's still talking about it - for BackDays after it came home,
		/// and only a quest worth talking about
		/// </summary>
		/// <returns>The quest's outcome, or null</returns>
		private QuestOutcome RecentStory()
		{
			ZDO zdo = nview.GetZDO();
			long back = zdo.GetLong(VillageQuests.BackAtKey, 0L);
			if (back == 0L || EnvMan.instance == null || NetTime.Now() - back > NetTime.Ticks(EnvMan.instance.m_dayLengthSec * QuestSpeech.BackDays))
			{
				return(null);
			}
			QuestOutcome story = QuestOutcome.Decode(zdo.GetByteArray(VillageQuests.StoryKey));
			return((story != null && QuestRules.WorthTalkingAbout(story.Kind)) ? story : null);
		}

		/// <summary>
		/// Gets the viking's last trip, while it's still talking about it - for TripStory.TalkHours game hours after it got
		/// there (see TripSpeech)
		/// </summary>
		/// <returns>The trip, or null</returns>
		private TripStory RecentTrip()
		{
			ZDO zdo = nview.GetZDO();
			if (EnvMan.instance == null || !TripStory.StillTalked(zdo.GetLong(TripStory.TripAtKey, 0L), NetTime.Now(), EnvMan.instance.m_dayLengthSec, NetTime.Ticks(1f)))
			{
				return(null);
			}
			return(TripStory.Of(zdo));
		}

		/// <summary>
		/// Gets the name of the village the viking came from, when it came from one lately - news from there it brought
		/// itself (see NeighborNews.Heard)
		/// </summary>
		/// <returns>The name, or ""</returns>
		private string CameFrom()
		{
			ZDO zdo = nview.GetZDO();
			TripStory trip = TripStory.Of(zdo);
			if (trip != null && trip.From.Length > 0 && !trip.TurnedHome)
			{
				return(trip.From);
			}
			return(VillageNewcomers.From(zdo));
		}

		/// <summary>
		/// Gets a biome's name as a viking says it
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <returns>The name</returns>
		private static string BiomeName(int biome)
		{
			return(VillagePortalSpeech.BiomeName(VikingGear.Get(biome).Biome));
		}

		/// <summary>
		/// Gets an item's name as a viking says it - the game's name for it, in the player's language
		/// </summary>
		/// <param name="prefab">The item's prefab name</param>
		/// <returns>The name, or the prefab name when the item can't be found</returns>
		private static string ItemName(string prefab)
		{
			GameObject item = ((ObjectDB.instance != null) ? ObjectDB.instance.GetItemPrefab(prefab) : null);
			ItemDrop drop = ((item != null) ? item.GetComponent<ItemDrop>() : null);
			if (drop == null || Localization.instance == null)
			{
				return(prefab);
			}
			return(Localization.instance.Localize(drop.m_itemData.m_shared.m_name).ToLowerInvariant());
		}

		/// <summary>
		/// Gets the village's quest that's out now, when it's worth talking about and the viking isn't on it
		/// </summary>
		/// <returns>The quest, or null</returns>
		private QuestRecord QuestOut()
		{
			QuestRecord record = VillageQuests.Read(village);
			if (record == null || record.Phase != QuestPhase.Away || !QuestRules.WorthTalkingAbout(record.Kind))
			{
				return(null);
			}
			return(record.Members.Contains(VikingIdentity.Get(nview.GetZDO())) ? null : record);
		}

		/// <summary>
		/// Picks a talk about a quest, now and then - the one the viking who started it just came back from, naming the
		/// others who went, or the one out now, naming those on it
		/// </summary>
		/// <param name="leader">The viking that started the talk</param>
		/// <returns>The talk's lines, or null for a talk about anything else</returns>
		private static string[] QuestTalk(VillageLife leader)
		{
			if (leader.village == null)
			{
				return(null);
			}
			TripStory trip = leader.RecentTrip();
			if (trip != null && Random.value < TripSpeech.TalkChance)
			{
				string[] tripTalk = TripSpeech.Talk(trip, leader.OwnBiome(), BiomeName);
				if (tripTalk != null)
				{
					return(tripTalk);
				}
			}
			NeighborEvent heard = NeighborNews.Heard(leader.village.Farm, leader.CameFrom(), out bool brought);
			if (heard != null && Random.value < NeighborSpeech.TalkChance)
			{
				return(NeighborSpeech.Talk(heard, brought, ItemName));
			}
			QuestOutcome story = leader.RecentStory();
			if (story != null && Random.value < QuestSpeech.BackTalkChance)
			{
				return(QuestSpeech.NameTrader(QuestSpeech.BackTalk(story.Kind, story.Companions), VillageQuests.TraderOf(leader.village)));
			}
			QuestRecord record = leader.QuestOut();
			if (record != null && Random.value < QuestSpeech.AwayTalkChance)
			{
				return(QuestSpeech.NameTrader(QuestSpeech.AwayTalk(record.Kind, record.Names, VillageQuests.DuePhrase(record)), VillageQuests.TraderOf(leader.village)));
			}
			return(null);
		}

		/// <summary>
		/// Picks what the viking says to a player about a quest, now and then - the one it just came back from, or the one
		/// out now
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>The line, or null for an answer about anything else</returns>
		private string QuestAnswer(Player player)
		{
			TripStory trip = RecentTrip();
			if (trip != null && Random.value < TripSpeech.AnswerChance)
			{
				string tripLine = TripSpeech.Answer(trip, OwnBiome(), player.GetPlayerName(), BiomeName);
				if (tripLine != null)
				{
					return(tripLine);
				}
			}
			bool brought = false;
			NeighborEvent heard = null;
			if (village != null)
			{
				heard = NeighborNews.Heard(village.Farm, CameFrom(), out brought);
			}
			if (heard != null && Random.value < NeighborSpeech.AnswerChance)
			{
				return(NeighborSpeech.Answer(heard, brought, ItemName));
			}
			QuestOutcome story = RecentStory();
			if (story != null && Random.value < QuestSpeech.BackAnswerChance)
			{
				return(QuestSpeech.NameTrader(QuestSpeech.BackAnswer(story.Kind, story.Companions, player.GetPlayerName()), VillageQuests.TraderOf(village)));
			}
			QuestRecord record = QuestOut();
			if (record != null && Random.value < QuestSpeech.AwayAnswerChance)
			{
				return(QuestSpeech.NameTrader(QuestSpeech.AwayAnswer(record.Kind, record.Names, VillageQuests.DuePhrase(record), player.GetPlayerName()), VillageQuests.TraderOf(village)));
			}
			return(null);
		}

		/// <summary>
		/// Looks around a viking just home from a quest for the rest of its village, a few seconds after it comes in -
		/// and finding every other viking of it dead, the first of those home on this client cries out and mourns by name
		/// those who died while it was gone, a line at a time, or else mourns by name those who went with it and died on
		/// the quest - call it from the viking's Idle
		/// </summary>
		private void UpdateHomecoming()
		{
			if (homecomingLines.Count > 0 && Time.time >= nextHomecomingLine)
			{
				nextHomecomingLine = Time.time + HomecomingLineGap;
				VikingSpeech.Say(humanoid, homecomingLines.Dequeue(), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
			}
			if (homecomingAt < 0f || Time.time < homecomingAt)
			{
				return;
			}
			homecomingAt = -1f;
			long together = NetTime.Now() - NetTime.Ticks(CameHomeTogether);
			bool others = village.Vikings().Exists((Character other) => other != null && other != humanoid && !other.IsDead() && other.m_nview != null && other.m_nview.IsValid() && other.m_nview.GetZDO().GetLong(VillageQuests.BackAtKey, 0L) < together);
			ZDOID id = village.Zdo.m_uid;
			if (others)
			{
				MournCompanions(id);
				return;
			}
			if (foundEmptyAt.TryGetValue(id, out float found) && Time.time - found < CameHomeTogether)
			{
				return;
			}
			foundEmptyAt[id] = Time.time;
			homecomingLines.Enqueue(QuestSpeech.Alone());
			List<string> fallen = VillageFallen.DiedSince(village, questSetOutAt);
			for (int i = 0; i < fallen.Count && i < MournedMax; i++)
			{
				homecomingLines.Enqueue(QuestSpeech.Mourn(fallen[i]));
			}
			nextHomecomingLine = Time.time;
			TestLog.Count("Quests", "came home to an empty village");
		}

		/// <summary>
		/// Has the viking, just home from a quest, mourn by name those who went with it and died on it - the first of
		/// those home on this client, a line at a time
		/// </summary>
		/// <param name="id">The village's ZDO</param>
		private void MournCompanions(ZDOID id)
		{
			if (mournedCompanionsAt.TryGetValue(id, out float mourned) && Time.time - mourned < CameHomeTogether)
			{
				return;
			}
			HashSet<string> went = new HashSet<string>(questCompanions, System.StringComparer.OrdinalIgnoreCase);
			List<string> fell = VillageFallen.DiedSince(village, questSetOutAt).FindAll((string name) => went.Contains(name));
			if (fell.Count == 0)
			{
				return;
			}
			mournedCompanionsAt[id] = Time.time;
			for (int i = 0; i < fell.Count && i < MournedMax; i++)
			{
				homecomingLines.Enqueue(QuestSpeech.MournCompanion(fell[i]));
			}
			nextHomecomingLine = Time.time;
		}

		/// <summary>
		/// Stops what the vikings of a quest on this client are doing, so they get ready to go
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="record">The quest</param>
		internal static void CallToQuest(Village village, QuestRecord record)
		{
			foreach (VillageLife life in All)
			{
				if (life != null && life.active && life.village == village && life.nview != null && life.nview.IsValid() && life.nview.IsOwner() && record.Members.Contains(VikingIdentity.Get(life.nview.GetZDO())))
				{
					life.Interrupt("called to a quest");
				}
			}
		}

		/// <summary>
		/// Finds the viking's part in a quest setting out from its village that it's on
		/// </summary>
		/// <returns>The job, or null when it isn't on one setting out</returns>
		private Job FindQuestWork()
		{
			QuestRecord record = VillageQuests.Read(village);
			if (record == null || record.Phase != QuestPhase.Leaving)
			{
				return(null);
			}
			string me = VikingIdentity.Get(nview.GetZDO());
			if (me.Length == 0 || !record.Members.Contains(me))
			{
				return(null);
			}
			return(new QuestLeaveJob(this, record, me));
		}

		/// <summary>
		/// Finds the nearest of the village's chests holding a tool
		/// </summary>
		/// <param name="prefab">The tool's prefab name</param>
		/// <returns>The chest, or null when none holds one</returns>
		private Container ChestWithTool(string prefab)
		{
			return(Nearest(UsableChests().FindAll((Container chest) => !Avoided(chest.m_nview.GetZDO().m_uid) && chest.GetInventory().GetAllItems().Exists((ItemDrop.ItemData item) => item.m_dropPrefab != null && item.m_dropPrefab.name == prefab && !VikingItems.IsCarried(item)))));
		}

		/// <summary>
		/// Takes the viking out of the world as it leaves on a quest - noting the quest, how it goes for the viking (see
		/// QuestGame.OutcomeFor), the foods it comes home having eaten, the village it left and the house it calls home on it, writing its inventory to its ZDO
		/// and handing it to the server to keep until the quest is due back (see VillageTravel)
		/// </summary>
		/// <param name="record">The quest</param>
		private void LeaveOnQuest(QuestRecord record)
		{
			ZDO zdo = nview.GetZDO();
			string me = VikingIdentity.Get(zdo);
			List<ItemDrop.ItemData> carried = humanoid.GetInventory().GetAllItems().FindAll((ItemDrop.ItemData item) => IsCarriedFor(item, CarryQuest) && item.m_dropPrefab != null);
			QuestOutcome outcome = QuestGame.OutcomeFor(humanoid, viking, record, me, carried, VillageNames.Get(village), out List<QuestMeal> lasting);
			VikingFood.Note(zdo, lasting, record.Due);
			zdo.Set(VillageQuests.QuestOutcomeKey, outcome.Encode());
			zdo.Set(VillageQuests.QuestIdKey, record.Id);
			zdo.Set(VillageQuests.QuestGoneKey, true);
			zdo.Set(VillageQuests.QuestVillageKey, village.Farm);
			zdo.Set(VillageQuests.QuestHomeKey, zdo.GetVec3(Village.HomeKey, Vector3.zero));
			TripStory.Begin(zdo, VillageNames.Get(village), village.Farm, "");
			VikingItems.RemoveProps(humanoid.GetInventory());
			ZPackage inventory = new ZPackage();
			humanoid.GetInventory().Save(inventory);
			zdo.Set(VikingCore.InventoryKey, inventory.GetArray());
			long now = NetTime.Now();
			TravelEntry entry = new TravelEntry();
			entry.Destination = village.Farm;
			entry.Name = VillageNames.Get(village);
			entry.Due = record.Due;
			entry.From = record.Exit;
			entry.Unloaded = now;
			entry.Remaining = Mathf.Max(0f, (record.Due - now) / (float)NetTime.Ticks(1f));
			entry.Biome = VillageCooking.VillageBiome(village);
			entry.BoatBiome = entry.Biome;
			entry.Unteleportable = !TravelPlanner.Teleportable(record.Item);
			entry.QuestId = record.Id;
			entry.QuestItem = record.Item;
			entry.QuestActive = record.ActiveSeconds;
			entry.QuestKind = (int)record.Kind;
			entry.QuestDue = record.Due;
			entry.QuestMembers = record.Members.Count;
			entry.Vikings.Add(VillageDeparted.SnapshotOf(zdo));
			VillageTravel.Queue(entry);
			TestLog.Count("Vikings", "left on a quest");
			Plugin.Log.LogInfo(TestLog.Name(zdo) + " left the village at " + village.Farm.ToString("F0") + " on a quest, " + record.Kind);
			nview.Destroy();
		}

		/// <summary>
		/// Plays out how a quest went for a viking that just came back from it (see QuestOutcome) - using up the food,
		/// potions and whatever it spent of what it carried, wearing its tool, giving it what it found, its health as it
		/// comes home and the skills it raised - and has it put away what it brought home, the tools it took too, before
		/// anything else, even at night. What happened is kept on it to talk about (see VillageQuests.StoryKey). One that
		/// died on the quest is noted as fallen and taken out of the world instead
		/// </summary>
		private void ReturnFromQuest()
		{
			ZDO zdo = nview.GetZDO();
			if (!zdo.GetBool(VillageQuests.QuestGoneKey))
			{
				return;
			}
			zdo.Set(VillageQuests.QuestGoneKey, false);
			zdo.Set(VillageQuests.QuestIdKey, "");
			Inventory inventory = humanoid.GetInventory();
			QuestOutcome outcome = QuestOutcome.Decode(zdo.GetByteArray(VillageQuests.QuestOutcomeKey));
			if (outcome != null && outcome.Died && village != null)
			{
				// One that died out there never comes home, wherever it came back into the world
				VillageQuests.FellOnQuest(village, VillageDeparted.SnapshotOf(zdo));
				nview.Destroy();
				return;
			}
			if (outcome != null)
			{
				PlayOut(outcome);
				zdo.Set(VillageQuests.StoryKey, outcome.Encode());
				zdo.Set(VillageQuests.BackAtKey, NetTime.Now());
				zdo.Set(VillageQuests.QuestOutcomeKey, new byte[0]);
				homecomingAt = Time.time + HomecomingDelay;
				questSetOutAt = outcome.SetOutAt;
				questCompanions = new List<string>(outcome.Companions);
			}
			bool loot = false;
			foreach (ItemDrop.ItemData item in inventory.GetAllItems())
			{
				if (IsCarriedFor(item, CarryQuest))
				{
					item.m_customData[VikingItems.CarryKey] = CarryLoot;
					loot = true;
				}
			}
			if (loot)
			{
				inventory.Changed();
				zdo.Set(QuestLootKey, true);
			}
		}

		/// <summary>
		/// Plays out how a quest went for the viking - what it ate, drank and spent goes from what it carried for the
		/// quest, its tool is worn to what it has left, what it found is carried to put away, its health is set and its
		/// skills rise
		/// </summary>
		/// <param name="outcome">How it went</param>
		private void PlayOut(QuestOutcome outcome)
		{
			Inventory inventory = humanoid.GetInventory();
			foreach (KeyValuePair<string, int> used in outcome.Used)
			{
				int left = used.Value;
				foreach (ItemDrop.ItemData item in inventory.m_inventory.FindAll((ItemDrop.ItemData carried) => IsCarriedFor(carried, CarryQuest) && carried.m_dropPrefab != null && carried.m_dropPrefab.name == used.Key))
				{
					if (left <= 0)
					{
						break;
					}
					int take = Mathf.Min(left, item.m_stack);
					inventory.RemoveItem(item, take);
					left -= take;
				}
			}
			if (outcome.Tool.Length > 0 && outcome.ToolLeft >= 0f)
			{
				List<ItemDrop.ItemData> tools = inventory.GetAllItems().FindAll((ItemDrop.ItemData item) => item.m_dropPrefab != null && item.m_dropPrefab.name == outcome.Tool);
				tools.Sort((ItemDrop.ItemData a, ItemDrop.ItemData b) => IsCarriedFor(b, CarryQuest).CompareTo(IsCarriedFor(a, CarryQuest)));
				if (tools.Count > 0)
				{
					tools[0].m_durability = Mathf.Min(tools[0].m_durability, outcome.ToolLeft);
				}
			}
			foreach (KeyValuePair<string, int> found in outcome.Loot)
			{
				// A circlet bought for one still using torches is theirs to wear, in place of a torch
				int count = found.Value;
				if (found.Key == ResourceBook.Circlet && OwnTool(ResourceBook.Circlet) == null && inventory.AddItem(ResourceBook.Circlet, 1, 1, 0, 0L, "", false) != null)
				{
					count--;
					TestLog.Count("Brought home from quests", found.Key);
				}
				ItemDrop.ItemData template = ItemOf(found.Key);
				int carried = ((template != null && count > 0) ? Carry(template, count, CarryLoot) : 0);
				TestLog.Count("Brought home from quests", found.Key, carried);
			}
			inventory.Changed();
			// Its max health follows the foods it came home with before its health is set as a share of it
			body.Food.UpdateMaxHealth();
			humanoid.SetHealth(humanoid.GetMaxHealth() * Mathf.Clamp01(outcome.HealthShare));
			foreach (KeyValuePair<int, float> skill in outcome.Skills)
			{
				RaiseSkill((Skills.SkillType)skill.Key, skill.Value);
			}
			Plugin.Log.LogInfo(TestLog.Name(nview.GetZDO()) + " came home from " + outcome.Kind + " at " + (outcome.HealthShare * 100f).ToString("F0") + "% health with " + outcome.Loot.Count + " kinds of loot, having used " + outcome.Used.Count + " kinds of supplies");
		}

		/// <summary>
		/// Finds the job putting away what the viking brought home from a quest, which goes on through dusk and night -
		/// cooking the raw food it brought first by day, when a cooking station has room for it, since this is picked
		/// ahead of the cooking work it would otherwise leave the raw food for
		/// </summary>
		/// <returns>The job, or null when it carries none home</returns>
		private Job FindQuestLootWork()
		{
			ZDO zdo = nview.GetZDO();
			if (!zdo.GetBool(QuestLootKey))
			{
				return(null);
			}
			if (CarriedLoot() == null)
			{
				zdo.Set(QuestLootKey, false);
				return(null);
			}
			if (!IsNight() && !IsDusk() && FindCookStation(true) != null)
			{
				Job cook = FindCookWork();
				if (cook != null)
				{
					return(cook);
				}
			}
			return((Time.time >= dumpAgainAt) ? new LootJob(this, null, true, false, false) : null);
		}

		/// <summary>
		/// Getting ready for a quest and leaving the village - taking the tool it takes from a chest, the first of the
		/// quest's vikings saying roughly when they'll be back, then running to the spot they leave from, where it's taken
		/// out of the world. One that hasn't got there in QuestRules.LeaveTimeout goes from wherever it is, so it still
		/// goes when it's stuck
		/// </summary>
		private class QuestLeaveJob : Job
		{
			private readonly string questId;
			private readonly string tool;
			private readonly bool leader;
			private readonly Vector3 exit;
			private readonly float leaveBy;
			private Container chest;
			private bool toolDone;
			private bool suppliesDone;
			private int potionsWanted;
			private readonly Dictionary<string, int> spendWanted = new Dictionary<string, int>();
			private readonly HashSet<Container> searched = new HashSet<Container>();
			private bool announced;
			private float actAt = -1f;

			/// <summary>
			/// For a quest that sails from the village (see SailOff), the viking's GUID, the water's edge it goes down to,
			/// and for its first viking, whether it has swung its hammer to build the quest's new boats, when the swing
			/// lands, and whether they're built
			/// </summary>
			private readonly string self;
			private Vector3 seaEdge;
			private bool hasSeaEdge;
			private bool swung;
			private float swingLands;
			private bool boatsBuilt;

			/// <summary>
			/// Whether the quest fights (see QuestLoot.Fights), so the viking packs its strongest food, how long it's gone,
			/// in seconds, and how many of each food it carries for the quest
			/// </summary>
			private readonly bool fights;
			private readonly float seconds;
			private readonly Dictionary<string, int> foodCarried = new Dictionary<string, int>();

			/// <summary>
			/// How many more of each food the viking takes from the chests it hasn't searched yet (see PlanFood)
			/// </summary>
			private Dictionary<string, int> foodPicks = new Dictionary<string, int>();

			/// <summary>
			/// Makes the job, working out what the viking takes along - the foods it would eat for as long as the quest is
			/// gone, up to QuestGame.FoodMax (see PlanFood), up to QuestGame.PotionMax potions, and what it spends there,
			/// like treasure to sell (see MemberPlan.Spend) - less what it already carries for the quest
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="record">The quest</param>
			/// <param name="member">The viking's GUID</param>
			public QuestLeaveJob(VillageLife owner, QuestRecord record, string member) : base(owner)
			{
				questId = record.Id;
				self = member;
				tool = record.ToolOf(member);
				leader = (record.Members.Count > 0 && record.Members[0] == member);
				exit = record.Exit;
				leaveBy = Time.time + QuestRules.LeaveTimeout + (record.BySea ? QuestRules.SailOffSeconds : 0f);
				toolDone = (tool.Length == 0);
				seconds = Mathf.Max(0f, (record.Due - record.SetOutAt) / (float)NetTime.Ticks(1f));
				fights = QuestLoot.Fights(record.Kind);
				int potions = 0;
				foreach (ItemDrop.ItemData item in owner.humanoid.GetInventory().GetAllItems())
				{
					if (!IsCarriedFor(item, CarryQuest))
					{
						continue;
					}
					if (VikingFood.IsFood(item))
					{
						QuestSim.Add(foodCarried, item.m_dropPrefab.name, item.m_stack);
					}
					potions += (QuestGame.IsPotion(item) ? item.m_stack : 0);
				}
				potionsWanted = Mathf.Max(0, QuestGame.PotionMax - potions);
				PlanFood(owner.UsableChests().FindAll((Container candidate) => !owner.Avoided(candidate.m_nview.GetZDO().m_uid)));
				foreach (KeyValuePair<string, int> spend in record.PlanOf(member).Spend)
				{
					int carried = 0;
					foreach (ItemDrop.ItemData item in owner.humanoid.GetInventory().GetAllItems())
					{
						if (IsCarriedFor(item, CarryQuest) && item.m_dropPrefab != null && item.m_dropPrefab.name == spend.Key)
						{
							carried += item.m_stack;
						}
					}
					if (spend.Value > carried)
					{
						spendWanted[spend.Key] = spend.Value - carried;
					}
				}
				suppliesDone = (FoodLeft() == 0 && potionsWanted == 0 && spendWanted.Count == 0);
			}

			/// <summary>
			/// Checks whether the viking still wants more of something it spends on the quest
			/// </summary>
			/// <param name="name">The item's prefab name</param>
			/// <returns>How many more, or 0</returns>
			private int SpendLeft(string name)
			{
				return(spendWanted.TryGetValue(name, out int left) ? left : 0);
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name</returns>
			public override string Describe()
			{
				return("Leaving on a quest" + (!toolDone ? ", getting a " + tool : (!suppliesDone ? ", packing food" : "")));
			}

			/// <summary>
			/// Checks whether the job has the viking heading out of the village
			/// </summary>
			/// <returns>True, as it's leaving</returns>
			public override bool Outing()
			{
				return(true);
			}

			/// <summary>
			/// Gets the tool, has the first of the vikings say when they'll be back, then runs to the spot it leaves from
			/// and leaves
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has left, or the quest is no longer setting out</returns>
			public override bool Tick(float dt)
			{
				QuestRecord record = VillageQuests.Read(life.village);
				if (record == null || record.Id != questId || record.Phase == QuestPhase.Waiting)
				{
					return(true);
				}
				if (Time.time >= leaveBy)
				{
					life.LeaveOnQuest(record);
					return(true);
				}
				if (!toolDone && !FetchTool(dt))
				{
					return(false);
				}
				if (!suppliesDone && !FetchSupplies(dt))
				{
					return(false);
				}
				if (leader && !announced)
				{
					announced = true;
					VikingSpeech.Say(life.humanoid, QuestSpeech.Departure(VillageQuests.DuePhrase(record)), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				}
				if (record.BySea && SailOff(dt, record))
				{
					return(false);
				}
				if (Utils.DistanceXZ(exit, life.transform.position) > ExitReach)
				{
					life.Go(dt, exit, ExitReach, true);
					if (!life.moveFailed)
					{
						return(false);
					}
					life.moveFailed = false;
				}
				life.LeaveOnQuest(record);
				return(true);
			}

			/// <summary>
			/// Sets off on a quest that sails from the village - walking down to the water's edge where the quest sets sail,
			/// the quest's first viking swinging its hammer there once to build the new boats it needs (see
			/// VillageQuests.PlanBoats), and once its own boat is there, getting on board (see StartQuestBoarding), from
			/// which its crossing takes over until it sails off on the quest (see SailOffOnQuest)
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="record">The quest</param>
			/// <returns>True while it's setting off by sea, and false when it sails in no boat and walks out instead</returns>
			private bool SailOff(float dt, QuestRecord record)
			{
				if (record.BoatOf(self) < 0)
				{
					return(false);
				}
				string group = record.BoatGroup;
				bool unbuilt = record.Boats.Exists((QuestBoat boat) => !boat.Owned && boat.Crew.Count > 0 && VikingBoat.FindFor(group, boat.Crew[0], out int _) == null);
				VikingBoat mine = VikingBoat.FindFor(group, self, out int seat);
				if (mine != null && !(leader && unbuilt && !boatsBuilt))
				{
					life.StartQuestBoarding(mine, seat);
					return(true);
				}
				if (!hasSeaEdge)
				{
					seaEdge = WaterEdge(record.Sea.Points[0], record.Sea.Points[1]);
					hasSeaEdge = true;
				}
				if (Utils.DistanceXZ(seaEdge, life.transform.position) > BuildReach)
				{
					life.mover = "walking down to the water to sail on a quest";
					if (!life.Go(dt, seaEdge, BuildReach / 2f, true))
					{
						return(true);
					}
					life.moveFailed = false;
					seaEdge = life.transform.position;
				}
				life.Face(record.Sea.Points[1]);
				if (!leader || !unbuilt || boatsBuilt)
				{
					life.mover = "waiting at the water for its boat";
					return(true);
				}
				life.mover = "building the quest's boats";
				life.HoldHammer(true);
				if (!swung)
				{
					swung = true;
					VikingPoses.Swing(life.humanoid);
					swingLands = Time.time + BuildSwingGap;
					return(true);
				}
				if (Time.time < swingLands)
				{
					return(true);
				}
				life.HoldHammer(false);
				boatsBuilt = true;
				BuildQuestBoats(record);
				return(true);
			}

			/// <summary>
			/// Builds the new boats a quest sails in from the village, side by side out from the water's edge the viking
			/// stands at - each with its crew and the quest's road across the sea
			/// </summary>
			/// <param name="record">The quest</param>
			private void BuildQuestBoats(QuestRecord record)
			{
				if (!FindLaunchToward(life.transform.position, record.Sea.Points[1], out Vector3 launch))
				{
					Plugin.Log.LogInfo("Quest: " + record.Kind + " found no water deep enough for a boat by " + life.transform.position.ToString("F0") + ", so its vikings walk out instead");
					return;
				}
				Vector3 facing = record.Sea.Points[1] - launch;
				facing.y = 0f;
				Vector3 side = Vector3.Cross(Vector3.up, (facing.sqrMagnitude > 0.01f) ? facing.normalized : life.transform.forward);
				Vector3 land = record.Sea.Points[record.Sea.Count - 1];
				string group = record.BoatGroup;
				int placed = 0;
				for (int i = 0; i < record.Boats.Count; i++)
				{
					QuestBoat plan = record.Boats[i];
					if (plan.Owned || plan.Crew.Count == 0 || VikingBoat.FindFor(group, plan.Crew[0], out int _) != null)
					{
						continue;
					}
					GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(plan.Prefab) : null);
					if (prefab == null)
					{
						continue;
					}
					// The first goes where the water was found, the rest to either side of it in turn
					float offset = ((placed + 1) / 2) * ((placed % 2 == 1) ? 1f : -1f) * BoatSpacing;
					Vector3 spot = launch + side * offset;
					TravelRoad route = new TravelRoad();
					route.Add(spot, TravelLeg.Foot);
					for (int p = 1; p < record.Sea.Count; p++)
					{
						if (record.Sea.SeaLeg(p))
						{
							route.Add(record.Sea.Points[p], TravelLeg.Sea);
						}
					}
					VikingBoat boat = VikingBoat.Build(prefab, spot, facing, group, i, plan.Crew, route, land, 0, 0);
					if (boat != null)
					{
						boat.MarkQuest(record.Id, false, false);
						placed++;
					}
				}
				Plugin.Log.LogInfo(life.LogName() + " built " + placed + " boats for the quest " + record.Kind + " at " + launch.ToString("F0"));
				TestLog.Count("Boats", "built for a quest", placed);
			}

			/// <summary>
			/// Walks to the nearest chest holding the tool and takes it, to carry on the quest - going without it when no
			/// chest holds one any more
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has the tool or goes without it</returns>
			private bool FetchTool(float dt)
			{
				if (chest == null || !life.Usable(chest))
				{
					chest = life.ChestWithTool(tool);
					actAt = -1f;
					if (chest == null)
					{
						toolDone = true;
						return(true);
					}
				}
				if (!ReachChest(dt))
				{
					return(false);
				}
				ItemDrop.ItemData got = life.TakeTool(chest, (ItemDrop.ItemData item) => item.m_dropPrefab != null && item.m_dropPrefab.name == tool);
				if (got != null)
				{
					got.m_customData[VikingItems.CarryKey] = CarryQuest;
					life.humanoid.GetInventory().Changed();
				}
				chest = null;
				toolDone = true;
				return(true);
			}

			/// <summary>
			/// Walks to the nearest chest it hasn't looked in yet holding food, a healing potion or something to spend it
			/// still wants, and takes what it wants of them - of food the foods it picked (see PlanFood), and of food and
			/// potions no more than half of each stack, so the village keeps some - going with what it has once no chest
			/// holds any more
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has packed</returns>
			private bool FetchSupplies(float dt)
			{
				if (chest == null || !life.Usable(chest))
				{
					List<Container> left = life.UsableChests().FindAll((Container candidate) => !searched.Contains(candidate) && !life.Avoided(candidate.m_nview.GetZDO().m_uid));
					PlanFood(left);
					chest = life.Nearest(left.FindAll((Container candidate) => candidate.GetInventory().GetAllItems().Exists(Wanted)));
					actAt = -1f;
					if (chest == null)
					{
						suppliesDone = true;
						return(true);
					}
				}
				if (!ReachChest(dt))
				{
					return(false);
				}
				searched.Add(chest);
				if (life.OpenChest(chest))
				{
					Inventory store = chest.GetInventory();
					foreach (ItemDrop.ItemData item in store.m_inventory.FindAll(Wanted))
					{
						string name = item.m_dropPrefab.name;
						int spend = SpendLeft(name);
						bool food = VikingFood.IsFood(item);
						// What it spends it takes all of, and food and potions only half a stack
						int wanted = ((spend > 0) ? Mathf.Min(spend, item.m_stack) : Mathf.Min((food ? FoodPicked(name) : potionsWanted), Mathf.Max(1, item.m_stack / 2)));
						int took = life.Carry(item, wanted, CarryQuest);
						if (took <= 0)
						{
							continue;
						}
						store.RemoveItem(item, took);
						if (spend > 0)
						{
							spendWanted[name] = spend - took;
						}
						else if (food)
						{
							foodPicks[name] = FoodPicked(name) - took;
							QuestSim.Add(foodCarried, name, took);
						}
						else
						{
							potionsWanted -= took;
						}
					}
				}
				chest = null;
				suppliesDone = AllSpent();
				return(suppliesDone);
			}

			/// <summary>
			/// Checks whether the viking has all it wants to take along
			/// </summary>
			/// <returns>True when it has</returns>
			private bool AllSpent()
			{
				if (FoodLeft() > 0 || potionsWanted > 0)
				{
					return(false);
				}
				foreach (int left in spendWanted.Values)
				{
					if (left > 0)
					{
						return(false);
					}
				}
				return(true);
			}

			/// <summary>
			/// Checks whether an item in a chest is food, a healing potion or something to spend the viking still wants to
			/// take along
			/// </summary>
			/// <param name="item">The item</param>
			/// <returns>True when it is</returns>
			private bool Wanted(ItemDrop.ItemData item)
			{
				if (item == null || item.m_dropPrefab == null || VikingItems.IsCarried(item))
				{
					return(false);
				}
				return(SpendLeft(item.m_dropPrefab.name) > 0 || (VikingFood.IsFood(item) && FoodPicked(item.m_dropPrefab.name) > 0) || (potionsWanted > 0 && QuestGame.IsPotion(item)));
			}

			/// <summary>
			/// Picks the foods the viking takes from chests it hasn't searched yet - what it would eat for as long as the
			/// quest is gone of those and what it already carries for it, half of each stack in the chests at most, in the
			/// order it wants them for the quest (see QuestSim.Rank), the strongest for a fight and otherwise the weakest
			/// that still does it good, going on with what it has eaten already, up to QuestGame.FoodMax (see
			/// QuestSim.Pack)
			/// </summary>
			/// <param name="chests">The chests it hasn't searched yet</param>
			private void PlanFood(List<Container> chests)
			{
				List<ItemDrop.ItemData> stacks = life.humanoid.GetInventory().GetAllItems().FindAll((ItemDrop.ItemData item) => IsCarriedFor(item, CarryQuest));
				foreach (Container candidate in chests)
				{
					stacks.AddRange(candidate.GetInventory().GetAllItems().FindAll((ItemDrop.ItemData item) => item.m_dropPrefab != null && !VikingItems.IsCarried(item)));
				}
				List<QuestFood> foods = QuestGame.FoodsOf(stacks, (ItemDrop.ItemData item) => (IsCarriedFor(item, CarryQuest) ? item.m_stack : Mathf.Max(1, item.m_stack / 2)));
				QuestSim.Rank(foods, fights, life.body.Food.Floor());
				foodPicks = QuestSim.Pack(life.body.Food.Eating(), foods, seconds, QuestGame.FoodMax, foodCarried);
			}

			/// <summary>
			/// Counts how many more foods the viking takes (see PlanFood)
			/// </summary>
			/// <returns>How many</returns>
			private int FoodLeft()
			{
				int left = 0;
				foreach (int picked in foodPicks.Values)
				{
					left += Mathf.Max(0, picked);
				}
				return(left);
			}

			/// <summary>
			/// Gets how many more of a food the viking takes (see PlanFood)
			/// </summary>
			/// <param name="name">The food's prefab name</param>
			/// <returns>How many, or 0</returns>
			private int FoodPicked(string name)
			{
				return(foodPicks.TryGetValue(name, out int picked) ? Mathf.Max(0, picked) : 0);
			}

			/// <summary>
			/// Walks to the chest and reaches into it, the way a player opens one - leaving a chest it can't get to alone
			/// for a while
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has reached into it</returns>
			private bool ReachChest(float dt)
			{
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(chest.transform.position);
					VikingPoses.Interact(life.humanoid);
					life.ShowChestOpen(chest);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				actAt = -1f;
				return(true);
			}
		}
	}
}
