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
		/// When each village's vikings on this client last found food to forage or prey to hunt, or when that was first
		/// looked at, by its ZDO, in ZNet time ticks
		/// </summary>
		private static readonly Dictionary<ZDOID, long> foodFoundAt = new Dictionary<ZDOID, long>();

		/// <summary>
		/// Notes that the viking found food to forage or prey to hunt around its village
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
			int food = 0;
			foreach (Container chest in VillageChests(village))
			{
				foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
				{
					food += (VikingFood.IsFood(item) ? item.m_stack : 0);
				}
			}
			return(!FoodEnough(food, village.Vikings().Count));
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
		/// less than 0 when it doesn't, and when the quest set out, in ZNet time ticks
		/// </summary>
		private float homecomingAt = -1f;
		private long questSetOutAt;

		/// <summary>
		/// What the viking still has to say on coming home to a village whose vikings all died, and when it says the next
		/// line, in Time.time seconds
		/// </summary>
		private readonly Queue<string> homecomingLines = new Queue<string>();
		private float nextHomecomingLine;

		/// <summary>
		/// When a viking of each village on this client last came home from a quest to find it empty, by its ZDO, in
		/// Time.time seconds - so only the first of them cries out
		/// </summary>
		private static readonly Dictionary<ZDOID, float> foundEmptyAt = new Dictionary<ZDOID, float>();

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
			QuestOutcome story = leader.RecentStory();
			if (story != null && Random.value < QuestSpeech.BackTalkChance)
			{
				return(QuestSpeech.BackTalk(story.Kind, story.Companions));
			}
			QuestRecord record = leader.QuestOut();
			if (record != null && Random.value < QuestSpeech.AwayTalkChance)
			{
				return(QuestSpeech.AwayTalk(record.Kind, record.Names, VillageQuests.DuePhrase(record)));
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
			QuestOutcome story = RecentStory();
			if (story != null && Random.value < QuestSpeech.BackAnswerChance)
			{
				return(QuestSpeech.BackAnswer(story.Kind, story.Companions, player.GetPlayerName()));
			}
			QuestRecord record = QuestOut();
			if (record != null && Random.value < QuestSpeech.AwayAnswerChance)
			{
				return(QuestSpeech.AwayAnswer(record.Kind, record.Names, VillageQuests.DuePhrase(record), player.GetPlayerName()));
			}
			return(null);
		}

		/// <summary>
		/// Looks around a viking just home from a quest for the rest of its village, a few seconds after it comes in -
		/// and finding every other viking of it dead, the first of those home on this client cries out and mourns by name
		/// those who died while it was gone, a line at a time - call it from the viking's Idle
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
			if (others || (foundEmptyAt.TryGetValue(id, out float found) && Time.time - found < CameHomeTogether))
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
					life.Interrupt();
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
		/// QuestGame.OutcomeFor), the village it left and the house it calls home on it, writing its inventory to its ZDO
		/// and handing it to the server to keep until the quest is due back (see VillageTravel)
		/// </summary>
		/// <param name="record">The quest</param>
		private void LeaveOnQuest(QuestRecord record)
		{
			ZDO zdo = nview.GetZDO();
			string me = VikingIdentity.Get(zdo);
			List<ItemDrop.ItemData> carried = humanoid.GetInventory().GetAllItems().FindAll((ItemDrop.ItemData item) => IsCarriedFor(item, CarryQuest) && item.m_dropPrefab != null);
			QuestOutcome outcome = QuestGame.OutcomeFor(humanoid, viking, record, me, carried, VillageNames.Get(village));
			zdo.Set(VillageQuests.QuestOutcomeKey, outcome.Encode());
			zdo.Set(VillageQuests.QuestIdKey, record.Id);
			zdo.Set(VillageQuests.QuestGoneKey, true);
			zdo.Set(VillageQuests.QuestVillageKey, village.Farm);
			zdo.Set(VillageQuests.QuestHomeKey, zdo.GetVec3(Village.HomeKey, Vector3.zero));
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
			entry.QuestId = record.Id;
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
		/// anything else, even at night. What happened is kept on it to talk about (see VillageQuests.StoryKey)
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
			if (outcome != null)
			{
				PlayOut(outcome);
				zdo.Set(VillageQuests.StoryKey, outcome.Encode());
				zdo.Set(VillageQuests.BackAtKey, NetTime.Now());
				zdo.Set(VillageQuests.QuestOutcomeKey, new byte[0]);
				homecomingAt = Time.time + HomecomingDelay;
				questSetOutAt = outcome.SetOutAt;
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
			humanoid.SetHealth(humanoid.GetMaxHealth() * Mathf.Clamp01(outcome.HealthShare));
			foreach (KeyValuePair<int, float> skill in outcome.Skills)
			{
				RaiseSkill((Skills.SkillType)skill.Key, skill.Value);
			}
			Plugin.Log.LogInfo(TestLog.Name(nview.GetZDO()) + " came home from " + outcome.Kind + " at " + (outcome.HealthShare * 100f).ToString("F0") + "% health with " + outcome.Loot.Count + " kinds of loot, having used " + outcome.Used.Count + " kinds of supplies");
		}

		/// <summary>
		/// Finds the job putting away what the viking brought home from a quest, which goes on through dusk and night
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
			return((Time.time >= dumpAgainAt) ? new LootJob(this, null, true) : null);
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
			private int foodWanted;
			private int potionsWanted;
			private readonly Dictionary<string, int> spendWanted = new Dictionary<string, int>();
			private readonly HashSet<Container> searched = new HashSet<Container>();
			private bool announced;
			private float actAt = -1f;

			/// <summary>
			/// Makes the job, working out what the viking takes along - a full set of foods for as long as the quest is
			/// gone (see QuestSim.MealsWanted), up to QuestGame.FoodMax, up to QuestGame.PotionMax potions, and what it
			/// spends there, like treasure to sell (see MemberPlan.Spend) - less what it already carries for the quest
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="record">The quest</param>
			/// <param name="member">The viking's GUID</param>
			public QuestLeaveJob(VillageLife owner, QuestRecord record, string member) : base(owner)
			{
				questId = record.Id;
				tool = record.ToolOf(member);
				leader = (record.Members.Count > 0 && record.Members[0] == member);
				exit = record.Exit;
				leaveBy = Time.time + QuestRules.LeaveTimeout;
				toolDone = (tool.Length == 0);
				float seconds = (record.Due - record.SetOutAt) / (float)NetTime.Ticks(1f);
				int food = 0;
				int potions = 0;
				foreach (ItemDrop.ItemData item in owner.humanoid.GetInventory().GetAllItems())
				{
					if (!IsCarriedFor(item, CarryQuest))
					{
						continue;
					}
					food += (VikingFood.IsFood(item) ? item.m_stack : 0);
					potions += (QuestGame.IsPotion(item) ? item.m_stack : 0);
				}
				foodWanted = Mathf.Max(0, Mathf.Min(QuestGame.FoodMax, QuestSim.MealsWanted(seconds, QuestSim.DefaultBurn)) - food);
				potionsWanted = Mathf.Max(0, QuestGame.PotionMax - potions);
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
				suppliesDone = (foodWanted == 0 && potionsWanted == 0 && spendWanted.Count == 0);
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
			/// still wants, and takes what it wants of them - of food and potions no more than half of each stack, so the
			/// village keeps some - going with what it has once no chest holds any more
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has packed</returns>
			private bool FetchSupplies(float dt)
			{
				if (chest == null || !life.Usable(chest))
				{
					chest = life.Nearest(life.UsableChests().FindAll((Container candidate) => !searched.Contains(candidate) && !life.Avoided(candidate.m_nview.GetZDO().m_uid) && candidate.GetInventory().GetAllItems().Exists(Wanted)));
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
						int wanted = ((spend > 0) ? Mathf.Min(spend, item.m_stack) : Mathf.Min((food ? foodWanted : potionsWanted), Mathf.Max(1, item.m_stack / 2)));
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
							foodWanted -= took;
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
				if (foodWanted > 0 || potionsWanted > 0)
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
				return(SpendLeft(item.m_dropPrefab.name) > 0 || (foodWanted > 0 && VikingFood.IsFood(item)) || (potionsWanted > 0 && QuestGame.IsPotion(item)));
			}

			/// <summary>
			/// Walks to the chest and reaches into it, the way a player opens one - leaving a chest it can't get to alone
			/// for a while
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has reached into it</returns>
			private bool ReachChest(float dt)
			{
				if (Utils.DistanceXZ(chest.transform.position, life.transform.position) > ChestReach)
				{
					life.Go(dt, chest.transform.position, ChestReach, false);
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
