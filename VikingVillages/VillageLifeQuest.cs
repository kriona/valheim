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
		/// Takes the viking out of the world as it leaves on a quest - noting the quest, the village it left and the house
		/// it calls home on it, writing its inventory to its ZDO and handing it to the server to keep until the quest is
		/// due back (see VillageTravel)
		/// </summary>
		/// <param name="record">The quest</param>
		private void LeaveOnQuest(QuestRecord record)
		{
			ZDO zdo = nview.GetZDO();
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
		/// Has a viking that just came back from a quest put away what it brought home - the tools it took and what it
		/// found - before anything else, even at night
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
			private bool announced;
			private float actAt = -1f;

			/// <summary>
			/// Makes the job
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
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name</returns>
			public override string Describe()
			{
				return("Leaving on a quest" + (toolDone ? "" : ", getting a " + tool));
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
				ItemDrop.ItemData got = life.TakeTool(chest, (ItemDrop.ItemData item) => item.m_dropPrefab != null && item.m_dropPrefab.name == tool);
				if (got != null)
				{
					got.m_customData[VikingItems.CarryKey] = CarryQuest;
					life.humanoid.GetInventory().Changed();
				}
				toolDone = true;
				return(true);
			}
		}
	}
}
