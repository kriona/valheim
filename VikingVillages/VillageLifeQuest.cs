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
				ItemDrop.ItemData template = ItemOf(found.Key);
				int carried = ((template != null) ? Carry(template, found.Value, CarryLoot) : 0);
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
			private readonly HashSet<Container> searched = new HashSet<Container>();
			private bool announced;
			private float actAt = -1f;

			/// <summary>
			/// Makes the job, working out the food and potions the viking takes along - a full set of foods for as long as
			/// the quest is gone (see QuestSim.MealsWanted), up to QuestGame.FoodMax, and up to QuestGame.PotionMax
			/// potions, less what it already carries for the quest
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
				suppliesDone = (foodWanted == 0 && potionsWanted == 0);
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
			/// Walks to the nearest chest it hasn't looked in yet holding food or a healing potion it still wants, and takes
			/// what it wants of them - no more than half of each stack, so the village keeps some - going with what it has
			/// once no chest holds any more
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
						bool food = VikingFood.IsFood(item);
						int wanted = Mathf.Min((food ? foodWanted : potionsWanted), Mathf.Max(1, item.m_stack / 2));
						int took = life.Carry(item, wanted, CarryQuest);
						if (took <= 0)
						{
							continue;
						}
						store.RemoveItem(item, took);
						if (food)
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
				suppliesDone = (foodWanted <= 0 && potionsWanted <= 0);
				return(suppliesDone);
			}

			/// <summary>
			/// Checks whether an item in a chest is food or a healing potion the viking still wants to take along
			/// </summary>
			/// <param name="item">The item</param>
			/// <returns>True when it is</returns>
			private bool Wanted(ItemDrop.ItemData item)
			{
				if (item == null || item.m_dropPrefab == null || VikingItems.IsCarried(item))
				{
					return(false);
				}
				return((foodWanted > 0 && VikingFood.IsFood(item)) || (potionsWanted > 0 && QuestGame.IsPotion(item)));
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
