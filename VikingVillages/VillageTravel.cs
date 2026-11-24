using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeps living vikings on the road while no player is near them - the server's side of the vikings' travels once
	/// they've set out on a road planned for them (see TravelPlanner), and the calls each client makes to it
	/// </summary>
	/// <remarks>
	/// A group on the road is taken out of the world together once they've all reached the sea and set sail or stepped
	/// into a portal on their road, or as soon as any one of them is out of every player's reach, so a group is never
	/// split - or, stepping into a portal whose far end a player is near, brought out there at once (see Emerge). The
	/// server keeps them in a record of its own, a ZDO far past the edge of the world, with the rest of their trip (see
	/// TravelJourney), which tells where they are - on foot at TravelTime.Speed, by sea at their boat's speed and through
	/// portals in TravelTime.PortalSeconds - planning the road again from there when a portal on it is broken (see
	/// CheckRoads). Once they're due at their village, they're given what they picked along the way (see TripForage) and
	/// handed to the client that runs the village, which brings them in from the side they came from (see Arrive). A
	/// village that's never loaded keeps them waiting. A group of a disbanded village that finds its village gone goes on
	/// to one picked at random, and one whose village was started again at its bonfire meanwhile turns around for home
	/// once it gets where it was going (see TurnHome). Vikings out on a quest are kept the same way, their trip there,
	/// the work and the trip back planned as they leave (see PlanQuest), due back at their own village when that's done,
	/// or at the village its vikings went to when it was disbanded meanwhile (see QuestsDisbanded)
	/// </remarks>
	internal static class VillageTravel
	{
		/// <summary>
		/// On a living viking, that it was spawned out in the wild with a console command and is still lost
		/// </summary>
		public static readonly int ConfusedKey = "kriona.VikingVillages.vikingConfused".GetStableHashCode();

		/// <summary>
		/// On a living viking on the road, where the village it's going to stands, its name and who gave it, its road (see
		/// TravelRoad.Encode), the index of the waypoint it's heading for, the group it set out with, or "", the easiest
		/// biome of its group, which the road keeps to, the hardest, which picks the boat they cross the sea in, whether
		/// any of them carries something a portal won't take, and that it has reached the sea and is setting sail, or
		/// reached a portal on its road and is stepping into it, so the server takes it off the road at once
		/// </summary>
		public static readonly int DestinationKey = "kriona.VikingVillages.vikingDestination".GetStableHashCode();
		public static readonly int DestinationNameKey = "kriona.VikingVillages.vikingDestinationName".GetStableHashCode();
		public static readonly int DestinationAuthorKey = "kriona.VikingVillages.vikingDestinationAuthor".GetStableHashCode();
		public static readonly int RouteKey = "kriona.VikingVillages.vikingRoute".GetStableHashCode();
		public static readonly int LegKey = "kriona.VikingVillages.vikingRouteLeg".GetStableHashCode();
		public static readonly int GroupKey = "kriona.VikingVillages.vikingTravelGroup".GetStableHashCode();
		public static readonly int BiomeKey = "kriona.VikingVillages.vikingTravelBiome".GetStableHashCode();
		public static readonly int BoatBiomeKey = "kriona.VikingVillages.vikingTravelBoatBiome".GetStableHashCode();
		public static readonly int UnteleportableKey = "kriona.VikingVillages.vikingTravelUnteleportable".GetStableHashCode();
		public static readonly int SailingKey = "kriona.VikingVillages.vikingSailing".GetStableHashCode();
		public static readonly int PortalKey = "kriona.VikingVillages.vikingPortal".GetStableHashCode();

		/// <summary>
		/// On a living viking that left a village a player disbanded, the village's name, kept until it joins another (see
		/// VillageNewcomers)
		/// </summary>
		public static readonly int FromNameKey = "kriona.VikingVillages.vikingFromVillage".GetStableHashCode();

		/// <summary>
		/// The routed RPCs - the server handing a group that's due to the client that runs its village, and that client
		/// handing it back when it can't bring it in; a client handing the server a group to keep on the road; and a client
		/// asking the server to call back the vikings that left a bonfire, and the server's answer
		/// </summary>
		private const string DeliverRpc = Plugin.PluginGuid + ".TravelDeliver";
		private const string HandBackRpc = Plugin.PluginGuid + ".TravelHandBack";
		private const string QueueRpc = Plugin.PluginGuid + ".TravelQueue";
		private const string RecallRpc = Plugin.PluginGuid + ".TravelRecall";
		private const string RecalledRpc = Plugin.PluginGuid + ".TravelRecalled";

		/// <summary>
		/// The routed RPC that tells the server a village was disbanded, so the vikings out on its quests go on to the
		/// village its vikings went to
		/// </summary>
		private const string QuestsDisbandedRpc = Plugin.PluginGuid + ".TravelQuestsDisbanded";

		/// <summary>
		/// The routed RPCs that hand a group stepping out of a portal near a player to that player's client, and that tell
		/// every client when a village's quest is now due back, for the one running the village to note
		/// </summary>
		private const string EmergeRpc = Plugin.PluginGuid + ".TravelEmerge";
		private const string QuestDueRpc = Plugin.PluginGuid + ".TravelQuestDue";

		/// <summary>
		/// Seconds between the server's looks at whether the portals on the roads of the groups it keeps still stand
		/// </summary>
		private const float RoadCheckInterval = 30f;

		/// <summary>
		/// When the server next looks at the portals on its groups' roads, in Time.time seconds, and the groups whose road
		/// it's planning again
		/// </summary>
		private static float nextRoadCheck;
		private static readonly HashSet<TravelEntry> replanning = new HashSet<TravelEntry>();

		/// <summary>
		/// The prefab hash of the record the server keeps the vikings on the road in - one no prefab has - where it
		/// stands, and the key they're written to
		/// </summary>
		private const string RecordPrefab = "kriona.VikingVillages.travelRecord";
		private static readonly Vector3 RecordPosition = new Vector3(-15000f, -1000f, -15100f);
		private static readonly int QueueKey = "kriona.VikingVillages.travelQueue".GetStableHashCode();

		/// <summary>
		/// How near a farm's planned spot its generator, or the spot a village is noted at, has to be to be it, in meters
		/// </summary>
		private const float FarmMatch = TravelPlanner.FarmMatch;

		/// <summary>
		/// Seconds between the server's looks for vikings on the road out of every player's reach and for groups due at
		/// their villages, how many of the world's sector batches it looks through each update, and how long a viking has
		/// to be out of reach to be taken out of the world, so one at the edge of a player's reach isn't taken and put
		/// back
		/// </summary>
		private const float SweepInterval = 3f;
		private const int SweepCalls = 25;
		private const float OutOfReachTime = 4f;

		/// <summary>
		/// Seconds before a group that found no village to go to looks again
		/// </summary>
		private const float LostRetry = 600f;

		/// <summary>
		/// Seconds past when it's due a group out on a quest waits for the rest of its vikings to leave the village - long
		/// enough for one that left late to catch up, as each leaves within QuestRules.LeaveTimeout of setting out
		/// </summary>
		private const float QuestGrace = QuestRules.LeaveTimeout * 2f;

		/// <summary>
		/// Seconds a client holds a group handed to it for a village it no longer runs before handing it back
		/// </summary>
		private const float HoldTime = 30f;

		/// <summary>
		/// On the server, the record and the groups on the road, the look for vikings out of reach going on - how far it
		/// has got and what it has found - when it next looks, and since when each viking on the road has been out of
		/// every player's reach, in Time.time seconds
		/// </summary>
		private static ZDO record;
		private static List<TravelEntry> queue = new List<TravelEntry>();
		private static int sweepIndex = -1;
		private static readonly List<ZDO> sweepFound = new List<ZDO>();
		private static float nextSweep;
		private static readonly Dictionary<ZDOID, float> outSince = new Dictionary<ZDOID, float>();

		/// <summary>
		/// On a client, the groups handed to it to bring into a village it runs, each with when it was handed over, in
		/// Time.time seconds
		/// </summary>
		private static readonly List<KeyValuePair<TravelEntry, float>> held = new List<KeyValuePair<TravelEntry, float>>();

		/// <summary>
		/// How many console commands are running right now, so a viking spawned by one is noted as lost (see NoteSpawned)
		/// </summary>
		public static int CommandsRunning;

		/// <summary>
		/// Registers the RPCs with a world's routed RPC, starting the world with nothing on the road
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance.Register<ZPackage>(DeliverRpc, OnDeliver);
			ZRoutedRpc.instance.Register<ZPackage>(HandBackRpc, OnHandBack);
			ZRoutedRpc.instance.Register<ZPackage>(QueueRpc, OnQueue);
			ZRoutedRpc.instance.Register<Vector3, Vector3>(RecallRpc, OnRecall);
			ZRoutedRpc.instance.Register<int, long>(RecalledRpc, OnRecalled);
			ZRoutedRpc.instance.Register<ZPackage>(QuestsDisbandedRpc, OnQuestsDisbanded);
			ZRoutedRpc.instance.Register<ZPackage>(EmergeRpc, OnEmerge);
			ZRoutedRpc.instance.Register<Vector3, string, long>(QuestDueRpc, OnQuestDue);
			TravelPlanner.Register();
			nextRoadCheck = 0f;
			replanning.Clear();
			record = null;
			queue = new List<TravelEntry>();
			sweepIndex = -1;
			sweepFound.Clear();
			nextSweep = 0f;
			outSince.Clear();
			held.Clear();
		}

		/// <summary>
		/// Notes a living viking just made as lost when a console command made it, so it stands about confused before it
		/// sets out for a village (see VillageLife.UpdateRoad)
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		public static void NoteSpawned(ZDO viking)
		{
			if (CommandsRunning > 0)
			{
				viking.Set(ConfusedKey, true);
			}
		}

		/// <summary>
		/// Checks whether a viking is on the road to a village
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>True when it is</returns>
		public static bool OnRoad(ZDO viking)
		{
			return(viking != null && viking.GetVec3(DestinationKey, Vector3.zero) != Vector3.zero);
		}

		/// <summary>
		/// Takes a viking off the road and out of being lost, as it joins a village or turns back to one
		/// </summary>
		/// <param name="viking">The viking's ZDO, owned by this client</param>
		public static void Clear(ZDO viking)
		{
			if (viking.GetBool(ConfusedKey))
			{
				viking.Set(ConfusedKey, false);
			}
			if (!OnRoad(viking))
			{
				return;
			}
			viking.Set(DestinationKey, Vector3.zero);
			viking.Set(DestinationNameKey, "");
			viking.Set(DestinationAuthorKey, "");
			viking.Set(RouteKey, new byte[0]);
			viking.Set(LegKey, 0);
			viking.Set(GroupKey, "");
			viking.Set(SailingKey, false);
			viking.Set(PortalKey, false);
		}

		/// <summary>
		/// Sets a viking out on the road to a village
		/// </summary>
		/// <param name="viking">The viking's ZDO, owned by this client</param>
		/// <param name="answer">The server's answer, with a village found</param>
		/// <param name="group">The group it sets out with, or ""</param>
		/// <param name="party">What its group can do on the road, so they all keep to the same road and boat</param>
		public static void SetOut(ZDO viking, TravelPlanner.Answer answer, string group, TravelParty party)
		{
			viking.Set(ConfusedKey, false);
			viking.Set(DestinationKey, answer.Destination);
			viking.Set(DestinationNameKey, answer.Name);
			viking.Set(DestinationAuthorKey, answer.Author);
			viking.Set(RouteKey, answer.Road.Encode());
			viking.Set(LegKey, 1);
			viking.Set(GroupKey, group);
			viking.Set(BiomeKey, party.Easiest);
			viking.Set(BoatBiomeKey, party.Hardest);
			viking.Set(UnteleportableKey, party.Unteleportable);
			viking.Set(SailingKey, false);
			viking.Set(PortalKey, false);
			TripStory.Begin(viking, answer.Spoken(), answer.Destination, viking.GetString(FromNameKey, ""));
		}

		/// <summary>
		/// Gets what a viking on the road's group can do on it, as it set out
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>The party</returns>
		public static TravelParty PartyOf(ZDO viking)
		{
			TravelParty party = new TravelParty();
			party.Easiest = Biome(viking);
			party.Hardest = BoatBiome(viking);
			party.Unteleportable = viking.GetBool(UnteleportableKey);
			return(party);
		}

		/// <summary>
		/// Notes a viking on the road as stepping into a portal, having reached one on its road, so the server takes it
		/// and its group off the road as soon as they all have (see TakeOffRoad) - the rest of the road taking as long as
		/// it takes from the portal's other end
		/// </summary>
		/// <param name="viking">The viking's ZDO, owned by this client</param>
		public static void EnterPortal(ZDO viking)
		{
			if (!viking.GetBool(PortalKey))
			{
				viking.Set(PortalKey, true);
			}
		}

		/// <summary>
		/// Checks whether a viking on the road is leaving the world for the rest of it - setting sail or stepping into a
		/// portal
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>True when it is</returns>
		private static bool Leaving(ZDO viking)
		{
			return(viking.GetBool(SailingKey) || viking.GetBool(PortalKey));
		}

		/// <summary>
		/// Notes a viking on the road as setting sail, having reached the sea on its road, so the server takes it and its
		/// group off the road as soon as they all have (see TakeOffRoad) - the rest of the road taking as long as it
		/// takes by boat
		/// </summary>
		/// <param name="viking">The viking's ZDO, owned by this client</param>
		public static void SetSail(ZDO viking)
		{
			if (!viking.GetBool(SailingKey))
			{
				viking.Set(SailingKey, true);
			}
		}

		/// <summary>
		/// Gets the biome a viking on the road keeps to - its group's easiest
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>The biome, by VikingGear's order</returns>
		public static int Biome(ZDO viking)
		{
			return(viking.GetInt(BiomeKey, 0));
		}

		/// <summary>
		/// Gets the biome a viking on the road picks its boat by - its group's hardest, or the one it keeps to when it set
		/// out before the two were told apart
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>The biome, by VikingGear's order</returns>
		public static int BoatBiome(ZDO viking)
		{
			return(viking.GetInt(BoatBiomeKey, Biome(viking)));
		}

		/// <summary>
		/// Gets the name of the village a viking on the road is going to, as it says it - filtered for its author's words
		/// and without rich text
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>The name, or "" when the village has none</returns>
		public static string DestinationName(ZDO viking)
		{
			return(VikingNames.Filter(viking.GetString(DestinationNameKey, ""), viking.GetString(DestinationAuthorKey, "")).RemoveRichTextTags().Trim());
		}

		/// <summary>
		/// Checks whether a village takes in a viking arriving from outside it - once it has had every viking it starts
		/// with, so one that comes as it's set up doesn't take a new viking's place, or always for a village a player
		/// started
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>True when it does</returns>
		public static bool Takes(Village village)
		{
			return(village != null && (village.IsFounded() || village.Zdo.GetBool(VillageRespawn.PopulatedKey)));
		}

		/// <summary>
		/// Checks whether a viking on the road is going to a village, so it joins that one and no other it passes through
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <param name="village">The village</param>
		/// <returns>True when it isn't on the road, or is going there</returns>
		public static bool Bound(ZDO viking, Village village)
		{
			if (!OnRoad(viking))
			{
				return(true);
			}
			return(Utils.DistanceXZ(viking.GetVec3(DestinationKey, Vector3.zero), village.Farm) <= FarmMatch);
		}

		/// <summary>
		/// Hands the server a group to keep on the road - the vikings of a disbanded village that were out of every
		/// player's reach as it was disbanded
		/// </summary>
		/// <param name="entry">The group</param>
		public static void Queue(TravelEntry entry)
		{
			if (ZRoutedRpc.instance == null || entry.Vikings.Count == 0)
			{
				return;
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), QueueRpc, new ZPackage(entry.Encode()));
		}

		/// <summary>
		/// Asks the server to call back the vikings on the road that left the village disbanded at a bonfire, to the
		/// village just started there - the server says how many and when they're due, which this client shows its player
		/// </summary>
		/// <param name="fire">Where the bonfire stands</param>
		/// <param name="anchor">Where the new village's anchor stands</param>
		public static void Recall(Vector3 fire, Vector3 anchor)
		{
			if (ZRoutedRpc.instance != null)
			{
				ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), RecallRpc, fire, anchor);
			}
		}

		/// <summary>
		/// Brings in the groups handed to this client for a village it runs, as the village is looked after (see
		/// VillageRespawn.Update) - those coming back to a village started again at their bonfire to its beds (see
		/// VillageDeparted.AddReturning), and the rest walking in from the side they came from, each as it was, joining the
		/// village as a newcomer, or coming home from a quest of the village's all at once, which ends its quest (see
		/// ArriveTogether)
		/// </summary>
		/// <param name="village">The village, whose generator this client owns, with every viking it starts with</param>
		/// <param name="vikings">Its living vikings' ZDOs, added to as they come</param>
		public static void Arrive(Village village, List<ZDO> vikings)
		{
			for (int i = held.Count - 1; i >= 0; i--)
			{
				TravelEntry entry = held[i].Key;
				if (Utils.DistanceXZ(entry.Destination, village.Farm) > FarmMatch)
				{
					continue;
				}
				if (entry.Returning)
				{
					VillageDeparted.AddReturning(village, entry.Vikings);
					Plugin.Log.LogInfo(entry.Vikings.Count + " vikings that left came back to the village at " + village.Farm.ToString("F0") + "'s beds");
					held.RemoveAt(i);
					continue;
				}
				if (entry.OnQuest)
				{
					ArriveTogether(village, vikings, entry);
				}
				while (!entry.OnQuest && entry.Vikings.Count > 0)
				{
					ZDO zdo = VillageRespawn.SpawnArrival(village, vikings, entry.Vikings[0], entry.From);
					if (zdo == null)
					{
						break;
					}
					entry.Vikings.RemoveAt(0);
				}
				if (entry.Vikings.Count == 0)
				{
					held.RemoveAt(i);
				}
				else
				{
					held[i] = new KeyValuePair<TravelEntry, float>(entry, Time.time);
				}
			}
		}

		/// <summary>
		/// Brings a group out on a quest into its village all at once, at the one spot found for the first of them - or
		/// none of them yet, when there's no spot for them, so they come home together - ending the village's quest. Those
		/// who died on it never come, their deaths noted as the rest come home (see VillageQuests.FellOnQuest)
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="vikings">Its living vikings' ZDOs, added to</param>
		/// <param name="entry">The group, emptied as they come</param>
		private static void ArriveTogether(Village village, List<ZDO> vikings, TravelEntry entry)
		{
			List<byte[]> fallen = entry.Vikings.FindAll((byte[] snapshot) => VillageQuests.DiedOnQuest(snapshot));
			List<byte[]> living = entry.Vikings.FindAll((byte[] snapshot) => !fallen.Contains(snapshot));
			Vector3 spot = Vector3.zero;
			bool outside = false;
			if (living.Count > 0 && !VillageRespawn.ArrivalSpotFor(village, living[0], entry.From, out spot, out outside))
			{
				return;
			}
			foreach (byte[] snapshot in fallen)
			{
				VillageQuests.FellOnQuest(village, snapshot);
				entry.Vikings.Remove(snapshot);
			}
			while (entry.Vikings.Count > 0)
			{
				if (VillageRespawn.SpawnArrivalAt(village, vikings, entry.Vikings[0], spot, outside) == null)
				{
					break;
				}
				entry.Vikings.RemoveAt(0);
			}
			VillageQuests.Returned(village, entry.QuestId);
		}

		/// <summary>
		/// Runs the server's side - its road searches (see TravelPlanner), its look for vikings out of reach and the groups
		/// due at their villages - and on a client hands back the groups it held too long - call it from the plugin's
		/// Update
		/// </summary>
		public static void Update()
		{
			if (ZNet.instance == null || ZDOMan.instance == null || ZRoutedRpc.instance == null || !ServerCheck.Active)
			{
				return;
			}
			UpdateHeld();
			// A world closing takes its players away first, which would take every group off the road and its boats
			// with it, so a player who logs out on a vikings' boat finds it there again
			if (!ZNet.instance.IsServer() || WorldGenerator.instance == null || (Game.instance != null && Game.instance.IsShuttingDown()))
			{
				return;
			}
			if (record == null)
			{
				if (!ZNet.instance.IsDedicated() && Player.m_localPlayer == null)
				{
					return;
				}
				Load();
			}
			TravelPlanner.Update();
			UpdateSweep();
		}

		/// <summary>
		/// Hands back to the server the groups this client has held for HoldTime without its village loaded here
		/// </summary>
		private static void UpdateHeld()
		{
			for (int i = held.Count - 1; i >= 0; i--)
			{
				if (Time.time - held[i].Value < HoldTime)
				{
					continue;
				}
				Village village = Village.ForFarm(held[i].Key.Destination);
				if (village != null && village.IsOwner())
				{
					held[i] = new KeyValuePair<TravelEntry, float>(held[i].Key, Time.time);
					continue;
				}
				ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), HandBackRpc, new ZPackage(held[i].Key.Encode()));
				held.RemoveAt(i);
			}
		}

		/// <summary>
		/// Finds the record the vikings on the road are kept in, or makes it, and reads them
		/// </summary>
		private static void Load()
		{
			List<ZDO> found = new List<ZDO>();
			int index = 0;
			while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(RecordPrefab, found, ref index))
			{
			}
			foreach (ZDO zdo in found)
			{
				if (record == null || zdo.GetByteArray(QueueKey) != null)
				{
					record = zdo;
				}
			}
			if (record == null)
			{
				int prefab = RecordPrefab.GetStableHashCode();
				record = ZDOMan.instance.CreateNewZDO(RecordPosition, prefab);
				record.SetPrefab(prefab);
				record.Persistent = true;
			}
			record.SetOwner(ZDOMan.GetSessionID());
			queue = TravelEntry.DecodeAll(record.GetByteArray(QueueKey));
			// A group whose search for another village was cut short by the world closing looks again
			foreach (TravelEntry entry in queue)
			{
				if (entry.Due == long.MaxValue)
				{
					entry.Due = NetTime.Now();
				}
			}
			if (queue.Count > 0)
			{
				Plugin.Log.LogInfo("Loaded " + queue.Count + " groups of vikings on the road");
			}
		}

		/// <summary>
		/// Writes the groups on the road to the record
		/// </summary>
		private static void Save()
		{
			if (record == null)
			{
				Load();
			}
			record.Set(QueueKey, TravelEntry.EncodeAll(queue));
		}

		/// <summary>
		/// Looks through the world for living vikings a few batches of sectors at a time, and once it has looked through
		/// all of them, takes the groups out of every player's reach off the road and hands over the groups that are due
		/// </summary>
		private static void UpdateSweep()
		{
			if (sweepIndex < 0)
			{
				if (Time.time < nextSweep)
				{
					return;
				}
				sweepIndex = 0;
				sweepFound.Clear();
			}
			for (int i = 0; i < SweepCalls; i++)
			{
				if (ZDOMan.instance.GetAllZDOsWithPrefabIterative(VikingCore.LivingPrefabName, sweepFound, ref sweepIndex))
				{
					sweepIndex = -1;
					nextSweep = Time.time + SweepInterval;
					TakeOffRoad(sweepFound.FindAll(OnRoad));
					Deliver();
					if (Time.time >= nextRoadCheck)
					{
						nextRoadCheck = Time.time + RoadCheckInterval;
						CheckRoads();
					}
					return;
				}
			}
		}

		/// <summary>
		/// Takes each group of vikings on the road off it once each of them has set sail or been out of every player's
		/// reach for OutOfReachTime, keeping it in the record until it's due at its village
		/// </summary>
		/// <param name="travelling">The vikings on the road</param>
		private static void TakeOffRoad(List<ZDO> travelling)
		{
			HashSet<ZDOID> seen = new HashSet<ZDOID>();
			Dictionary<string, List<ZDO>> groups = new Dictionary<string, List<ZDO>>();
			foreach (ZDO zdo in travelling)
			{
				seen.Add(zdo.m_uid);
				string group = zdo.GetString(GroupKey, "");
				if (group.Length == 0)
				{
					group = zdo.m_uid.ToString();
				}
				if (!groups.TryGetValue(group, out List<ZDO> members))
				{
					members = new List<ZDO>();
					groups[group] = members;
				}
				members.Add(zdo);
			}
			List<ZDOID> gone = new List<ZDOID>();
			foreach (ZDOID id in outSince.Keys)
			{
				if (!seen.Contains(id))
				{
					gone.Add(id);
				}
			}
			foreach (ZDOID id in gone)
			{
				outSince.Remove(id);
			}
			bool changed = false;
			foreach (List<ZDO> members in groups.Values)
			{
				if (!OutOfReach(members))
				{
					continue;
				}
				bool throughPortal = members.TrueForAll((ZDO zdo) => zdo.GetBool(PortalKey));
				TravelEntry entry = Pack(members);
				RemoveBoats(members);
				foreach (ZDO zdo in members)
				{
					outSince.Remove(zdo.m_uid);
					zdo.SetOwner(ZDOMan.GetSessionID());
					ZDOMan.instance.DestroyZDO(zdo);
				}
				changed = true;
				if (throughPortal && Emerge(entry))
				{
					continue;
				}
				queue.Add(entry);
			}
			if (changed)
			{
				Save();
			}
		}

		/// <summary>
		/// Takes the boats a group built to cross the sea out of the world with it (see VikingBoat), so no empty boat is
		/// left behind
		/// </summary>
		/// <param name="members">The group's vikings</param>
		private static void RemoveBoats(List<ZDO> members)
		{
			HashSet<string> groups = new HashSet<string>();
			List<Vector3> near = new List<Vector3>();
			foreach (ZDO zdo in members)
			{
				string boat = zdo.GetString(VikingBoat.BoatKey, "");
				int mark = boat.LastIndexOf('#');
				if (mark > 0)
				{
					groups.Add(boat.Substring(0, mark));
					near.Add(zdo.GetPosition());
				}
			}
			foreach (string group in groups)
			{
				int removed = VikingBoat.RemoveAll(group, near);
				if (removed > 0)
				{
					Plugin.Log.LogInfo("Travel: " + removed + " boats of " + group + " were taken out of the world with their crew");
				}
			}
		}

		/// <summary>
		/// Checks whether a group comes off the road together - once every one of them has set sail or stepped into a
		/// portal, or as soon as any one of them has been out of every player's reach for OutOfReachTime, so the group is
		/// never split between the world and the road
		/// </summary>
		/// <param name="members">The group's vikings</param>
		/// <returns>True when it does</returns>
		private static bool OutOfReach(List<ZDO> members)
		{
			bool allLeaving = true;
			bool anyGone = false;
			foreach (ZDO zdo in members)
			{
				if (Leaving(zdo))
				{
					continue;
				}
				allLeaving = false;
				if (InAnyReach(zdo.GetPosition()))
				{
					outSince.Remove(zdo.m_uid);
					continue;
				}
				if (!outSince.TryGetValue(zdo.m_uid, out float since))
				{
					outSince[zdo.m_uid] = Time.time;
				}
				else if (Time.time - since >= OutOfReachTime)
				{
					anyGone = true;
				}
			}
			return(allLeaving || anyGone);
		}

		/// <summary>
		/// Brings a group that stepped into a portal out of its far end at once when a player is near it - handing it to
		/// the client nearest there, which brings each of them out as they were, on the rest of their road (see OnEmerge)
		/// </summary>
		/// <param name="entry">The group, just taken off the road</param>
		/// <returns>True when a player is near the far end, false to keep the group on the road</returns>
		private static bool Emerge(TravelEntry entry)
		{
			TravelRoad road = ((entry.Journey != null && entry.Journey.Stages.Count > 0) ? entry.Journey.Stages[0].Road : null);
			if (road == null || road.Count < 2 || !road.PortalLeg(1))
			{
				return(false);
			}
			long peer = PeerNear(road.Points[1]);
			if (peer == 0L)
			{
				return(false);
			}
			Plugin.Log.LogInfo("Travel: " + entry.Vikings.Count + " vikings come out of the portal at " + road.Points[1].ToString("F0") + ", a player being near it");
			TestLog.Count("Travel", "groups out of a portal near a player");
			TravelJourney.Stage through = new TravelJourney.Stage { Road = new TravelRoad() };
			through.Road.Add(road.Points[0], TravelLeg.Foot);
			through.Road.Add(road.Points[1], TravelLeg.Portal);
			TravelJourney stepped = entry.Journey;
			entry.Journey = new TravelJourney { BoatSpeed = stepped.BoatSpeed };
			entry.Journey.Stages.Add(through);
			NoteTrip(entry, null, 0L);
			entry.Journey = stepped;
			ZRoutedRpc.instance.InvokeRoutedRPC(peer, EmergeRpc, new ZPackage(entry.Encode()));
			return(true);
		}

		/// <summary>
		/// Finds a client in whose reach a point is
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>The client's peer ID - this one's when its own player is - or 0 when it's in no one's reach</returns>
		private static long PeerNear(Vector3 point)
		{
			if (!ZNet.instance.IsDedicated() && ZNetScene.InActiveArea(point, ZNet.instance.GetReferencePosition()))
			{
				return(ZDOMan.GetSessionID());
			}
			foreach (ZNetPeer peer in ZNet.instance.GetPeers())
			{
				if (peer.IsReady() && ZNetScene.InActiveArea(point, peer.m_refPos))
				{
					return(peer.m_uid);
				}
			}
			return(0L);
		}

		/// <summary>
		/// Brings a group out of the far end of a portal on this client, each viking as it was, on the rest of its road
		/// together - or hands the group back to the server to keep on the road when they can't be brought out
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="pkg">The group</param>
		private static void OnEmerge(long sender, ZPackage pkg)
		{
			TravelEntry entry = TravelEntry.Decode(pkg.GetArray());
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(VikingCore.LivingPrefabHash) : null);
			TravelRoad road = ((entry != null && entry.Journey != null && entry.Journey.Stages.Count > 0) ? entry.Journey.Stages[0].Road : null);
			if (entry == null || road == null || road.Count < 2)
			{
				return;
			}
			if (prefab == null)
			{
				ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), HandBackRpc, new ZPackage(entry.Encode()));
				return;
			}
			TravelRoad rest = new TravelRoad();
			for (int i = 1; i < road.Count; i++)
			{
				rest.Add(road.Points[i], road.LegTo(i));
			}
			TravelPlanner.Answer answer = new TravelPlanner.Answer { Found = true, Destination = entry.Destination, Name = entry.Name, Road = rest };
			TravelParty party = new TravelParty { Easiest = entry.Biome, Hardest = entry.BoatBiome, Unteleportable = entry.Unteleportable };
			string group = ((entry.Vikings.Count > 1) ? "portal" + NetTime.Now() : "");
			foreach (byte[] snapshot in entry.Vikings)
			{
				Vector3 spot = road.Points[1] + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(0.5f, 2f);
				if (ZoneSystem.instance != null)
				{
					spot.y = ZoneSystem.instance.GetGroundHeight(spot);
				}
				GameObject instance = Object.Instantiate(prefab, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
				ZDO zdo = instance.GetComponent<ZNetView>().GetZDO();
				VillageDeparted.ApplySnapshot(snapshot, zdo);
				SetOut(zdo, answer, group, party);
				zdo.Set(FromNameKey, entry.FromName);
				VillageDeparted.NoteFirePosition(zdo, entry.OriginFire);
			}
			Plugin.Log.LogInfo("Travel: " + entry.Vikings.Count + " vikings came out of the portal at " + road.Points[1].ToString("F0") + " on their way to the village at " + entry.Destination.ToString("F0"));
		}

		/// <summary>
		/// Checks whether a point is in the area the game runs around any player (see ZNetScene.InActiveArea)
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>True when it is</returns>
		private static bool InAnyReach(Vector3 point)
		{
			if (!ZNet.instance.IsDedicated() && ZNetScene.InActiveArea(point, ZNet.instance.GetReferencePosition()))
			{
				return(true);
			}
			foreach (ZNetPeer peer in ZNet.instance.GetPeers())
			{
				if (peer.IsReady() && ZNetScene.InActiveArea(point, peer.m_refPos))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Packs a group taken off the road into an entry - due at its village once the farthest behind of them has
		/// covered the rest of its road, on foot, by sea in the boat of the group's hardest biome and through portals, and
		/// arriving from the way the road comes in
		/// </summary>
		/// <param name="members">The group's vikings</param>
		/// <returns>The entry</returns>
		private static TravelEntry Pack(List<ZDO> members)
		{
			ZDO first = members[0];
			TravelEntry entry = new TravelEntry();
			entry.Destination = first.GetVec3(DestinationKey, Vector3.zero);
			entry.Name = first.GetString(DestinationNameKey, "");
			entry.OriginFire = VillageDeparted.FirePosition(first);
			entry.FromName = first.GetString(FromNameKey, "");
			entry.Unloaded = NetTime.Now();
			TravelParty party = new TravelParty();
			for (int i = 0; i < members.Count; i++)
			{
				TravelParty own = PartyOf(members[i]);
				party.Add(own.Easiest, own.Unteleportable, (i == 0));
				party.Hardest = Mathf.Max(party.Hardest, own.Hardest);
			}
			entry.Biome = party.Easiest;
			entry.BoatBiome = party.Hardest;
			entry.Unteleportable = party.Unteleportable;
			float boat = TravelTime.BoatSpeed(entry.BoatBiome);
			float total = 0f;
			string how = "went out of every player's reach";
			string counted = "out of reach";
			TravelRoad behind = null;
			foreach (ZDO zdo in members)
			{
				TravelRoad road = TravelRoad.Decode(zdo.GetByteArray(RouteKey));
				int leg = zdo.GetInt(LegKey, 1);
				float left = road.Seconds(leg, zdo.GetPosition(), boat);
				if (left >= entry.Remaining)
				{
					entry.Remaining = left;
					total = road.TotalSeconds(boat);
					entry.From = ((road.Count > 0) ? road.Approach() : zdo.GetPosition());
					behind = Rest(road, leg, zdo.GetPosition());
				}
				if (zdo.GetBool(SailingKey))
				{
					how = "set sail in a " + TravelTime.BoatName(entry.BoatBiome);
					counted = "set sail";
				}
				else if (zdo.GetBool(PortalKey))
				{
					how = "stepped through a portal";
					counted = "through a portal";
				}
				entry.Vikings.Add(VillageDeparted.SnapshotOf(zdo));
			}
			entry.Spent = Mathf.Max(0f, total - entry.Remaining);
			if (behind != null && behind.Count > 1)
			{
				entry.Journey = TravelJourney.Along(behind, boat, entry.Unteleportable);
				entry.Remaining = entry.Journey.TotalSeconds();
			}
			entry.Due = entry.Unloaded + NetTime.Ticks(entry.Remaining);
			Plugin.Log.LogInfo("Travel: " + members.Count + " vikings (" + party.Describe() + ") " + how + " on their way to the village at " + entry.Destination.ToString("F0") + ", " + entry.Spent.ToString("F0") + "s into the road, due there in " + entry.Remaining.ToString("F0") + "s");
			TestLog.Count("Travel", "groups taken off the road, " + counted);
			return(entry);
		}

		/// <summary>
		/// Looks at the portals on the rest of each kept group's trip, and plans the road again from where the group is for
		/// the first stage with a portal that was broken, lost its other end, or no longer takes what they carry - the
		/// group due again once the new road is done
		/// </summary>
		private static void CheckRoads()
		{
			long now = NetTime.Now();
			double ticksPerSecond = NetTime.Ticks(1f);
			List<TravelRoute.Portal> any = null;
			List<TravelRoute.Portal> barred = null;
			foreach (TravelEntry entry in queue)
			{
				if (entry.Journey == null || entry.Due == long.MaxValue || replanning.Contains(entry))
				{
					continue;
				}
				TravelJourney rest = entry.Journey.From(entry.Elapsed(now, ticksPerSecond));
				any = (any ?? TravelPlanner.UsablePortals(new TravelParty()));
				barred = (barred ?? TravelPlanner.UsablePortals(new TravelParty { Unteleportable = true }));
				int broken = TravelPlanner.BrokenStage(rest, any, barred);
				if (broken >= 0)
				{
					Replan(entry, rest, broken);
				}
			}
		}

		/// <summary>
		/// Plans the road of a stage of a kept group's trip again, past a portal that's gone - from where the group is when
		/// it's the stage they're on, or from where the stage starts - starting the rest of the trip over from when the new
		/// road is found, or across open ground straight there when there's none
		/// </summary>
		/// <param name="entry">The group</param>
		/// <param name="rest">The rest of its trip from now</param>
		/// <param name="stage">The index in the rest of the trip of the stage to plan again</param>
		private static void Replan(TravelEntry entry, TravelJourney rest, int stage)
		{
			TravelRoad road = rest.Stages[stage].Road;
			Vector3 from = road.Points[0];
			Vector3 to = road.Points[road.Count - 1];
			TravelParty party = PartyOf(entry);
			party.Unteleportable = rest.Stages[stage].Unteleportable;
			float before = entry.Remaining - entry.Elapsed(NetTime.Now(), NetTime.Ticks(1f));
			replanning.Add(entry);
			string label = entry.Vikings.Count + " vikings whose road has a broken portal";
			TravelPlanner.PlanTo(from, party, new List<Vector3> { to }, label, (TravelPlanner.Answer answer) =>
			{
				replanning.Remove(entry);
				if (!queue.Contains(entry) || entry.Journey == null)
				{
					return;
				}
				long now = NetTime.Now();
				double ticksPerSecond = NetTime.Ticks(1f);
				// The group has gone on a little while the road was planned, which the new road doesn't count
				TravelJourney current = entry.Journey.From(entry.Elapsed(now, ticksPerSecond));
				int at = stage - (rest.Stages.Count - current.Stages.Count);
				if (at < 0 || at >= current.Stages.Count || current.Stages[at].Road == null)
				{
					return;
				}
				TravelRoad replaced = answer.Road;
				if (!answer.Found)
				{
					replaced = new TravelRoad();
					replaced.Add(from, TravelLeg.Foot);
					replaced.Add(to, TravelLeg.Foot);
				}
				current.Stages[at] = new TravelJourney.Stage { Road = replaced, Unteleportable = current.Stages[at].Unteleportable, Homeward = current.Stages[at].Homeward };
				entry.Events |= TravelEntry.ReroutedEvent;
				entry.Restart(current, now, ticksPerSecond, entry.OnQuest);
				Plugin.Log.LogInfo("Travel: " + label + " at " + from.ToString("F0") + " take " + (answer.Found ? "another road, by " + TravelPlanner.DescribeRoad(replaced) : "the long way straight there, finding no road") + " to " + to.ToString("F0") + " - " + entry.Remaining.ToString("F0") + "s left, " + (entry.Remaining - before).ToString("+0;-0") + "s from before");
				TestLog.Count("Travel", "groups rerouted past a broken portal");
				NoteQuestDue(entry);
				Save();
			});
		}

		/// <summary>
		/// Makes the rest of a viking's road from where it is - there, then the waypoint it's heading for and every one
		/// after
		/// </summary>
		/// <param name="road">Its road</param>
		/// <param name="leg">The index of the waypoint it's heading for</param>
		/// <param name="position">Where it is</param>
		/// <returns>The rest of the road</returns>
		internal static TravelRoad Rest(TravelRoad road, int leg, Vector3 position)
		{
			TravelRoad rest = new TravelRoad();
			rest.Add(position, TravelLeg.Foot);
			for (int i = Mathf.Max(1, leg); i < road.Count; i++)
			{
				rest.Add(road.Points[i], road.LegTo(i));
			}
			return(rest);
		}

		/// <summary>
		/// Hands each group that's due to the client running its village, once the village has had every viking it starts
		/// with - or finds it another village when it turned out to be a farm that stayed abandoned or a village since
		/// disbanded, one picked at random for the vikings of a disbanded village. The vikings of a disbanded village
		/// whose village was started again at its bonfire meanwhile turn around for home once they've got where they were
		/// going. A group due at a farm not generated or loaded yet waits for it, and a group out on a quest waits for
		/// the rest of its vikings to leave the village and join it, so they all come home together (see
		/// TravelEntry.QuestWaiting). Each group is given what it picked along its road as it's due (see TripForage)
		/// </summary>
		private static void Deliver()
		{
			long now = NetTime.Now();
			bool changed = false;
			for (int i = queue.Count - 1; i >= 0; i--)
			{
				TravelEntry entry = queue[i];
				if (entry.Due > now || TravelEntry.QuestWaiting(entry.Vikings.Count, entry.QuestMembers, now, entry.Due, NetTime.Ticks(QuestGrace)))
				{
					continue;
				}
				ZDO village = VillageRespawn.FindGenerator(entry.Destination);
				bool abandoned = (village != null && village.GetPrefab() == VillagePaths.GeneratorHash && village.GetBool(VillagePaths.AbandonedKey));
				if (abandoned || (village == null && !IsPlannedFarm(entry.Destination)))
				{
					Reroute(entry);
					continue;
				}
				if (!entry.Returning && TravelPlanner.HomeAnchor(entry.OriginFire, out ZDO home) && Utils.DistanceXZ(home.GetPosition(), entry.Destination) > FarmMatch)
				{
					TurnHome(entry, home.GetPosition());
					continue;
				}
				if (village == null)
				{
					continue;
				}
				bool ready = (VillageFounding.IsAnchor(village.GetPrefab()) || village.GetBool(VillageRespawn.PopulatedKey) || entry.Returning);
				if (!ready || !village.HasOwner())
				{
					continue;
				}
				if (!entry.Foraged)
				{
					NoteTrip(entry, TripForage.Give(entry), now);
					changed = true;
				}
				queue.RemoveAt(i);
				changed = true;
				Plugin.Log.LogInfo("Travel: " + entry.Vikings.Count + " vikings " + (entry.OnQuest ? "out on a quest " + (QuestKind)entry.QuestKind + " " : "") + "are due at the village at " + entry.Destination.ToString("F0") + ((entry.Name.Length > 0) ? " (" + entry.Name + ")" : "") + ", " + ((now - entry.Due) / (float)NetTime.Ticks(1f)).ToString("F0") + "s late, after " + (entry.Spent + entry.Remaining).ToString("F0") + "s on the road");
				TestLog.Count("Travel", (entry.OnQuest ? "quest groups due home" : (entry.Returning ? "groups due back at a village started again" : "groups due at a village")));
				ZRoutedRpc.instance.InvokeRoutedRPC(village.GetOwner(), DeliverRpc, new ZPackage(entry.Encode()));
			}
			if (changed)
			{
				Save();
			}
		}

		/// <summary>
		/// Checks whether the world was planned with a farm at a spot
		/// </summary>
		/// <param name="spot">The spot</param>
		/// <returns>True when it was</returns>
		private static bool IsPlannedFarm(Vector3 spot)
		{
			return(FarmSpawns.PlannedFarms().Exists((Vector3 farm) => Utils.DistanceXZ(farm, spot) <= FarmMatch));
		}

		/// <summary>
		/// Finds a group on the road another village, from the one it found no one in - one picked at random for the
		/// vikings of a disbanded village, or else the one they get to soonest - due there once it has run the road from
		/// there, or trying again after LostRetry when there's none
		/// </summary>
		/// <param name="entry">The group, kept in the queue meanwhile</param>
		private static void Reroute(TravelEntry entry)
		{
			entry.Excluded.Add(entry.Destination);
			entry.Due = long.MaxValue;
			Vector3 from = entry.Destination;
			bool disbanded = (entry.OriginFire != Vector3.zero || entry.FromName.Length > 0);
			TravelPlanner.Goal goal = (disbanded ? TravelPlanner.Goal.Random : TravelPlanner.Goal.Soonest);
			TravelPlanner.PlanHere(from, PartyOf(entry), entry.Excluded, entry.Vikings.Count + " vikings on the road that found no one at their village", (TravelPlanner.Answer answer) =>
			{
				long now = NetTime.Now();
				if (!answer.Found)
				{
					entry.Due = now + NetTime.Ticks(LostRetry);
					entry.Excluded.Remove(from);
				}
				else
				{
					entry.Events |= TravelEntry.VillageGoneEvent;
					NoteTrip(entry, null, 0L);
					entry.Destination = answer.Destination;
					entry.Name = answer.Name;
					entry.Restart(TravelJourney.Along(answer.Road, TravelTime.BoatSpeed(entry.BoatBiome), entry.Unteleportable), now, NetTime.Ticks(1f), false);
					Plugin.Log.LogInfo("Travel: " + entry.Vikings.Count + " vikings that found no one at the village at " + from.ToString("F0") + " go on to " + (disbanded ? "a village picked at random" : "the nearest village") + " at " + answer.Destination.ToString("F0") + ", " + answer.Seconds.ToString("F0") + "s away");
					TestLog.Count("Travel", "groups rerouted on the road");
				}
				Save();
			}, goal);
		}

		/// <summary>
		/// Turns a group of a disbanded village around for home, now that they've got where they were going and their
		/// village was started again at its bonfire - due there once they've run the road back, coming to its beds
		/// </summary>
		/// <param name="entry">The group, kept in the queue meanwhile</param>
		/// <param name="anchor">Where the village started again stands</param>
		private static void TurnHome(TravelEntry entry, Vector3 anchor)
		{
			entry.Due = long.MaxValue;
			Vector3 from = entry.Destination;
			TravelPlanner.PlanTo(from, PartyOf(entry), new List<Vector3> { anchor }, entry.Vikings.Count + " vikings turning home", (TravelPlanner.Answer answer) =>
			{
				long now = NetTime.Now();
				entry.Events |= TravelEntry.TurnedHomeEvent;
				NoteTrip(entry, null, 0L);
				entry.Returning = true;
				entry.Destination = anchor;
				entry.Excluded.Clear();
				TravelRoad road = answer.Road;
				if (!answer.Found)
				{
					road = new TravelRoad();
					road.Add(from, TravelLeg.Foot);
					road.Add(anchor, TravelLeg.Foot);
				}
				entry.Restart(TravelJourney.Along(road, TravelTime.BoatSpeed(entry.BoatBiome), entry.Unteleportable), now, NetTime.Ticks(1f), false);
				Plugin.Log.LogInfo("Travel: " + entry.Vikings.Count + " vikings got to the village at " + from.ToString("F0") + " and turn around for home at " + anchor.ToString("F0") + ", started again at their bonfire, " + entry.Remaining.ToString("F0") + "s away");
				TestLog.Count("Travel", "groups turned around for home");
				Save();
			});
		}

		/// <summary>
		/// Notes on each viking of a group what happened on the part of its trip off the road (see TripStory) - the portals
		/// it went through, the boat it sailed in, the biomes it walked through, what it picked and what happened to the
		/// group - and that it got where it was going, when it has
		/// </summary>
		/// <param name="entry">The group</param>
		/// <param name="foraged">How many things each viking picked on the way, or null</param>
		/// <param name="arrivedAt">When the group got there, in ticks, or 0 when it's still on its way</param>
		private static void NoteTrip(TravelEntry entry, int[] foraged, long arrivedAt)
		{
			int portals = 0;
			bool sailed = false;
			List<int> biomes = new List<int>();
			if (entry.Journey != null)
			{
				foreach (TravelJourney.Stage stage in entry.Journey.Stages)
				{
					if (stage.Road != null)
					{
						portals += stage.Road.PortalCount();
						sailed |= stage.Road.Legs.Contains(TravelLeg.Sea);
					}
				}
				if (WorldGenerator.instance != null)
				{
					entry.Journey.FootLegs(0, (Vector3 middle, float seconds) =>
					{
						int biome = VikingGear.IndexOf(WorldGenerator.instance.GetBiome(middle));
						if (!biomes.Contains(biome))
						{
							biomes.Add(biome);
						}
					});
				}
			}
			string boat = TravelTime.BoatName(entry.BoatBiome);
			for (int i = 0; i < entry.Vikings.Count; i++)
			{
				int picked = ((foraged != null && i < foraged.Length) ? foraged[i] : 0);
				entry.Vikings[i] = TripStory.Edit(entry.Vikings[i], (TripStory story) =>
				{
					story.Portals += portals;
					if (sailed && story.Boat.Length == 0)
					{
						story.Boat = boat;
					}
					biomes.ForEach((int biome) => story.AddBiome(biome));
					story.Foraged += picked;
					story.Rerouted |= ((entry.Events & TravelEntry.ReroutedEvent) != 0);
					story.TurnedHome |= ((entry.Events & TravelEntry.TurnedHomeEvent) != 0);
					story.VillageGone |= ((entry.Events & TravelEntry.VillageGoneEvent) != 0);
					if (story.To.Length == 0)
					{
						story.To = entry.Name;
					}
					if (arrivedAt > 0L)
					{
						story.Arrived = true;
					}
				}, arrivedAt);
			}
		}

		/// <summary>
		/// Gets what a group on the road can do on it
		/// </summary>
		/// <param name="entry">The group</param>
		/// <returns>The party</returns>
		private static TravelParty PartyOf(TravelEntry entry)
		{
			return(new TravelParty { Easiest = entry.Biome, Hardest = entry.BoatBiome, Unteleportable = entry.Unteleportable });
		}

		/// <summary>
		/// Takes a group handed to this client to bring into the village it runs
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="pkg">The group</param>
		private static void OnDeliver(long sender, ZPackage pkg)
		{
			TravelEntry entry = TravelEntry.Decode(pkg.GetArray());
			if (entry != null)
			{
				held.Add(new KeyValuePair<TravelEntry, float>(entry, Time.time));
				VillageRespawn.CheckSoon();
			}
		}

		/// <summary>
		/// Takes back on the server a group a client couldn't bring in, due again at once
		/// </summary>
		/// <param name="sender">The client</param>
		/// <param name="pkg">The group</param>
		private static void OnHandBack(long sender, ZPackage pkg)
		{
			OnQueue(sender, pkg);
		}

		/// <summary>
		/// Takes a group onto the road on the server - a viking leaving on a quest joining the others of its quest that
		/// left before it, so they come back together
		/// </summary>
		/// <param name="sender">The client that handed it over</param>
		/// <param name="pkg">The group</param>
		private static void OnQueue(long sender, ZPackage pkg)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer())
			{
				return;
			}
			TravelEntry entry = TravelEntry.Decode(pkg.GetArray());
			if (entry == null)
			{
				return;
			}
			TravelEntry group = (entry.OnQuest ? queue.Find((TravelEntry other) => other.QuestId == entry.QuestId) : null);
			if (group != null)
			{
				group.Vikings.AddRange(entry.Vikings);
				group.QuestMembers = Mathf.Max(group.QuestMembers, entry.QuestMembers);
				// A group whose trip is planned is due when its trip is over, not when the village guessed
				if (group.Journey == null)
				{
					group.Due = System.Math.Max(group.Due, entry.Due);
				}
			}
			else
			{
				queue.Add(entry);
				if (entry.OnQuest && entry.Journey == null && !entry.Returning)
				{
					PlanQuest(entry);
				}
			}
			Save();
		}

		/// <summary>
		/// Plans the trip of a group out on a quest, on the server, as its first viking leaves - the road from where they
		/// left the village to whichever place the quest could go they get to soonest (see QuestTargets), the work there,
		/// and the road home, without the portals that won't take what they bring back - due back once it's done, rather
		/// than when the village guessed as it sent them, which it's told (see OnQuestDue). A trip that can't be planned
		/// leaves the group due when the village guessed
		/// </summary>
		/// <param name="entry">The group, kept in the queue meanwhile</param>
		private static void PlanQuest(TravelEntry entry)
		{
			QuestKind kind = (QuestKind)entry.QuestKind;
			Vector3 home = entry.Destination;
			List<Vector3> targets = QuestTargets.FindAll(kind, entry.QuestItem, home, entry.Biome);
			if (targets.Count == 0)
			{
				return;
			}
			string label = "the quest " + kind + " from the village at " + home.ToString("F0");
			TravelParty outward = PartyOf(entry);
			outward.Unteleportable = false;
			TravelPlanner.PlanTo(entry.From, outward, targets, label, (TravelPlanner.Answer there) =>
			{
				if (!there.Found)
				{
					return;
				}
				TravelParty homeward = PartyOf(entry);
				homeward.Unteleportable = !TravelPlanner.Teleportable(entry.QuestItem);
				TravelPlanner.PlanTo(there.Destination, homeward, new List<Vector3> { entry.From }, label + " coming home", (TravelPlanner.Answer back) =>
				{
					if (!back.Found || entry.Journey != null || !queue.Contains(entry))
					{
						return;
					}
					TravelJourney journey = new TravelJourney { BoatSpeed = TravelTime.BoatSpeed(entry.BoatBiome) };
					journey.AddRoad(there.Road, outward.Unteleportable, false);
					journey.AddStay(entry.QuestActive);
					journey.AddRoad(back.Road, homeward.Unteleportable, true);
					long guessed = entry.Due;
					entry.Restart(journey, entry.Unloaded, NetTime.Ticks(1f), true);
					Plugin.Log.LogInfo("Travel: " + label + " goes to " + there.Destination.ToString("F0") + ", " + there.Seconds.ToString("F0") + "s there by " + TravelPlanner.DescribeRoad(there.Road) + ", " + entry.QuestActive.ToString("F0") + "s of work and " + back.Seconds.ToString("F0") + "s back by " + TravelPlanner.DescribeRoad(back.Road) + " - due " + ((entry.Due - guessed) / (float)NetTime.Ticks(1f)).ToString("+0;-0") + "s from the village's guess");
					TestLog.Count("Travel", "quest trips planned");
					NoteQuestDue(entry);
					Save();
				});
			});
		}

		/// <summary>
		/// Tells every client when a group out on a quest is now due back at its village, for the one running the village
		/// to note on its quest (see VillageQuests.DueChanged)
		/// </summary>
		/// <param name="entry">The group</param>
		private static void NoteQuestDue(TravelEntry entry)
		{
			if (entry.OnQuest && !entry.Returning)
			{
				ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, QuestDueRpc, entry.Destination, entry.QuestId, entry.Due);
			}
		}

		/// <summary>
		/// Notes on a village this client runs when its quest is now due back, as the server worked it out
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="farm">Where the village stands</param>
		/// <param name="questId">The quest's ID</param>
		/// <param name="due">When it's due back, in ZNet time ticks</param>
		private static void OnQuestDue(long sender, Vector3 farm, string questId, long due)
		{
			VillageQuests.DueChanged(farm, questId, due);
		}

		/// <summary>
		/// Describes the groups the server keeps on the road and its road searches, a line each, for the test run's
		/// snapshot - nothing on a client, which keeps none
		/// </summary>
		/// <returns>The lines</returns>
		public static List<string> DescribeQueue()
		{
			List<string> lines = new List<string>();
			if (ZNet.instance == null || !ZNet.instance.IsServer() || record == null)
			{
				return(lines);
			}
			long now = NetTime.Now();
			float tick = NetTime.Ticks(1f);
			foreach (TravelEntry entry in queue)
			{
				string what = (entry.OnQuest ? "out on a quest " + (QuestKind)entry.QuestKind : (entry.Returning ? "coming back to a village started again" : "on the road"));
				string due = ((entry.Due == long.MaxValue) ? "looking for another village" : ((entry.Due > now) ? "due in " + ((entry.Due - now) / tick).ToString("F0") + "s" : "due " + ((now - entry.Due) / tick).ToString("F0") + "s ago"));
				string where = "";
				if (entry.Journey != null && entry.Due != long.MaxValue)
				{
					TravelJourney.Place place = entry.Journey.At(entry.Elapsed(now, tick), entry.From);
					where = ", at about " + place.Position.ToString("F0") + ((place.Stage < entry.Journey.Stages.Count) ? " on stage " + (place.Stage + 1) + " of " + entry.Journey.Stages.Count + ((entry.Journey.Stages[place.Stage].Road == null) ? " (at work)" : " (" + place.Leg.ToString().ToLowerInvariant() + ")") : "");
				}
				lines.Add(entry.Vikings.Count + " vikings " + what + " to " + entry.Destination.ToString("F0") + ((entry.Name.Length > 0) ? " (" + entry.Name + ")" : "") + ", " + due + where + ", " + PartyOf(entry).Describe() + ((entry.FromName.Length > 0) ? ", from " + entry.FromName : "") + ((entry.OriginFire != Vector3.zero) ? ", left the bonfire at " + entry.OriginFire.ToString("F0") : "") + ((entry.Excluded.Count > 0) ? ", " + entry.Excluded.Count + " villages found empty" : "") + (replanning.Contains(entry) ? ", planning its road again" : ""));
			}
			string searching = TravelPlanner.DescribeSearches();
			if (searching.Length > 0)
			{
				lines.Add(searching);
			}
			return(lines);
		}

		/// <summary>
		/// Makes the vikings out on a quest due back now, on the server, for the kri_vv_testlog command
		/// </summary>
		/// <param name="questId">The quest's ID</param>
		/// <returns>How many vikings are due back</returns>
		public static int DueNow(string questId)
		{
			if (record == null)
			{
				Load();
			}
			int count = 0;
			long now = NetTime.Now();
			foreach (TravelEntry entry in queue)
			{
				if (entry.QuestId == questId)
				{
					entry.Due = now;
					entry.QuestDue = now;
					count += entry.Vikings.Count;
				}
			}
			if (count > 0)
			{
				Save();
			}
			return(count);
		}

		/// <summary>
		/// Tells the server a village was disbanded, so the vikings out on its quest come back to the village its vikings
		/// went to instead, later by the road from it there (see OnQuestsDisbanded)
		/// </summary>
		/// <param name="farm">Where the village stood - its farm's spot or its anchor</param>
		/// <param name="fire">Where its bonfire stands, which a village started again there calls them back by</param>
		/// <param name="answer">The village its vikings went to, not found when there's none</param>
		public static void QuestsDisbanded(Vector3 farm, Vector3 fire, TravelPlanner.Answer answer)
		{
			if (ZRoutedRpc.instance == null)
			{
				return;
			}
			ZPackage pkg = new ZPackage();
			pkg.Write(farm);
			pkg.Write(fire);
			answer.Write(pkg);
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), QuestsDisbandedRpc, pkg);
		}

		/// <summary>
		/// Sends the vikings out on a disbanded village's quests on to the village its vikings went to - due there as much
		/// later as the road from the disbanded village takes - noting its bonfire on them, so a village started there
		/// again calls them back. With no village found, they come back to the one disbanded, and find another then
		/// (see Deliver)
		/// </summary>
		/// <param name="sender">The client that disbanded it</param>
		/// <param name="pkg">Where the village stood, where its bonfire stands, and the village its vikings went to</param>
		private static void OnQuestsDisbanded(long sender, ZPackage pkg)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer())
			{
				return;
			}
			Vector3 farm = pkg.ReadVector3();
			Vector3 fire = pkg.ReadVector3();
			TravelPlanner.Answer answer = TravelPlanner.Answer.Read(pkg);
			long now = NetTime.Now();
			int count = 0;
			foreach (TravelEntry entry in queue)
			{
				if (!entry.OnQuest || entry.Returning || Utils.DistanceXZ(entry.Destination, farm) > FarmMatch)
				{
					continue;
				}
				entry.OriginFire = fire;
				if (answer.Found)
				{
					entry.Destination = answer.Destination;
					entry.Name = answer.Name;
					entry.From = ((answer.Road.Count > 0) ? answer.Road.Approach() : entry.From);
					if (entry.Journey != null && entry.Due != long.MaxValue)
					{
						// The road on from the disbanded village follows the road home to it
						entry.Journey.AddRoad(answer.Road, entry.Unteleportable, true);
						entry.Remaining = entry.Journey.TotalSeconds();
						entry.Due = entry.Unloaded + NetTime.Ticks(entry.Remaining);
					}
					else
					{
						entry.Due = System.Math.Max(entry.Due, now) + NetTime.Ticks(answer.Seconds);
					}
				}
				count += entry.Vikings.Count;
			}
			if (count > 0)
			{
				Save();
				Plugin.Log.LogInfo("Travel: " + count + " vikings out on a quest of the village disbanded at " + fire.ToString("F0") + (answer.Found ? " go on to the village at " + answer.Destination.ToString("F0") + " instead, " + answer.Seconds.ToString("F0") + "s later" : " have no village to go to yet"));
			}
		}

		/// <summary>
		/// Counts on the server the groups on the road that left the village disbanded at a bonfire, now that a village
		/// was started there - they go on to where they were going and turn around for home once they get there (see
		/// Deliver and TurnHome) - and tells the client that started it how many, and roughly when the last are due back,
		/// as though the road back takes as long as the road there
		/// </summary>
		/// <param name="sender">The client that started the village</param>
		/// <param name="fire">Where the bonfire stands</param>
		/// <param name="anchor">Where the new village's anchor stands</param>
		private static void OnRecall(long sender, Vector3 fire, Vector3 anchor)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer())
			{
				return;
			}
			long now = NetTime.Now();
			int count = 0;
			long latest = 0L;
			foreach (TravelEntry entry in queue)
			{
				if (entry.Returning || entry.OriginFire == Vector3.zero || Utils.DistanceXZ(entry.OriginFire, fire) > 1f)
				{
					continue;
				}
				long there = ((entry.Due == long.MaxValue) ? now : System.Math.Max(now, entry.Due));
				count += entry.Vikings.Count;
				latest = System.Math.Max(latest, there + NetTime.Ticks(entry.Spent + entry.Remaining));
			}
			if (count > 0)
			{
				Plugin.Log.LogInfo("Travel: " + count + " vikings on the road that left the village started again at " + fire.ToString("F0") + " turn around for home once they get where they're going");
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(sender, RecalledRpc, count, latest);
		}

		/// <summary>
		/// Tells the local player how many vikings on the road were called back to the village they started, and roughly
		/// when they're due back
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="count">How many</param>
		/// <param name="due">When the last are due, in ZNet time ticks</param>
		private static void OnRecalled(long sender, int count, long due)
		{
			if (count <= 0 || Player.m_localPlayer == null || EnvMan.instance == null)
			{
				return;
			}
			double now = ZNet.instance.GetTimeSeconds();
			double at = now + (due - NetTime.Now()) / (double)NetTime.Ticks(1f);
			Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, RecalledMessage(count, TravelTime.Phrase(now, at, EnvMan.instance.m_dayLengthSec)));
		}

		/// <summary>
		/// Says how many vikings on the road were called back and roughly when they're due
		/// </summary>
		/// <param name="count">How many</param>
		/// <param name="when">When, like "tomorrow" (see TravelTime.Phrase), or "now"</param>
		/// <returns>The message</returns>
		internal static string RecalledMessage(int count, string when)
		{
			string who = ((count == 1) ? "1 Viking is" : count + " Vikings are");
			if (when == "now")
			{
				return(who + " coming back now");
			}
			return(who + " on the way back - expect them " + when);
		}
	}
}
