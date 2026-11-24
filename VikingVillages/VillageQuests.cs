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
		/// Gets the key how long the road to a quest takes from a village is noted at on the village's ZDO, in seconds
		/// each way, once the server has worked it out
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <returns>The key</returns>
		private static int RouteKey(QuestKind kind, string item)
		{
			return(("kriona.VikingVillages.villageQuestRoute." + kind + "." + item).GetStableHashCode());
		}

		/// <summary>
		/// Gets how long the road to a quest takes from a village, each way - asking the server the first time
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <returns>The time, in seconds, or less than 0 while the server hasn't answered</returns>
		private static float RouteSeconds(Village village, QuestKind kind, string item)
		{
			int key = RouteKey(kind, item);
			float seconds = village.Zdo.GetFloat(key, -1f);
			// Asked once per village and road until the server answers
			int asked = key ^ village.Zdo.m_uid.GetHashCode();
			if (seconds >= 0f || routesAsked.Contains(asked))
			{
				return(seconds);
			}
			routesAsked.Add(asked);
			ZDOID id = village.Zdo.m_uid;
			VillageTravel.AskQuestRoute(village.Farm, VillageCooking.VillageBiome(village), kind, item, (float answer) =>
			{
				routesAsked.Remove(asked);
				ZDO zdo = ((ZDOMan.instance != null) ? ZDOMan.instance.GetZDO(id) : null);
				if (zdo != null && zdo.IsOwner())
				{
					zdo.Set(key, answer);
				}
			});
			return(-1f);
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
		/// Picks a quest for a village when its goal's next step is one it can go on, it has rested long enough since
		/// its last, no enemy is near and it can spare the vikings - setting out at once by day, or waiting for the
		/// morning
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
			PlanStep step = VillageLife.CurrentGoalStep(village);
			if (step == null || step.Kind != StepKind.Quest || step.Source == null)
			{
				return;
			}
			// The road is known before the quest is picked, so it never waits on the server once it's setting out
			if (RouteSeconds(village, step.Quest, step.Item ?? "") < 0f)
			{
				return;
			}
			QuestRecord record = Plan(village, step.Quest, step.Item, step.Amount, step.Source);
			if (record == null)
			{
				return;
			}
			Plugin.Log.LogInfo("Quest: the village at " + village.Farm.ToString("F0") + " picked " + record.Kind + " for " + record.Item + " with " + string.Join(", ", record.Names) + (CanSetOutNow() ? "" : ", waiting for the morning"));
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
		/// Picks the vikings for a quest - those fit to go, the ones with a tool good enough for it first, then the
		/// healthiest - and the tool each takes, as many as the village can spare (see QuestRules.GroupSize)
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
				int tools = CarriesTool(b, source).CompareTo(CarriesTool(a, source));
				return((tools != 0) ? tools : b.GetHealthPercentage().CompareTo(a.GetHealthPercentage()));
			});
			int carried = fit.FindAll((Humanoid humanoid) => CarriesTool(humanoid, source)).Count;
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
			float travel = RouteSeconds(village, record.Kind, record.Item);
			ResourceSource source = QuestGame.SourceFor(ResourceBookGame.Get(), record.Kind, record.Item);
			if (!QuestGame.Prepare(village, record, source, ((travel >= 0f) ? travel : QuestRules.DefaultTravel)))
			{
				Plugin.Log.LogInfo("Quest: " + record.Kind + " from the village at " + village.Farm.ToString("F0") + " was given up on, as its vikings aren't all here");
				Write(village, null);
				return;
			}
			Write(village, record);
			VillageLife.CallToQuest(village, record);
			float seconds = (record.Due - record.SetOutAt) / (float)NetTime.Ticks(1f);
			Plugin.Log.LogInfo("Quest: " + record.Kind + " from the village at " + village.Farm.ToString("F0") + " sets out from " + record.Exit.ToString("F0") + (record.Trapped ? ", inside the village, as it's trapped" : "") + ", " + travel.ToString("F0") + "s each way and " + record.ActiveSeconds.ToString("F0") + "s there, due back in " + seconds.ToString("F0") + "s");
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
			Plugin.Log.LogInfo("Quest: " + record.Kind + " came back to the village at " + village.Farm.ToString("F0"));
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
