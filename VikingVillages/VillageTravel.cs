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
	/// A viking on the road that's out of every player's reach, or has reached the sea and set sail, or stepped into a
	/// portal on its road, is taken out of the world together with the rest of its group and kept in a record of the
	/// server's, a ZDO far past the edge of the world, until they're due at their village - the rest of the road on foot
	/// at TravelTime.Speed, by sea at their boat's speed and through portals in TravelTime.PortalSeconds - and then handed
	/// to the client that runs the village, which brings them in from the side they came from (see Arrive). A village
	/// that's never loaded keeps them waiting. A village started again at the bonfire of a village its vikings left calls
	/// back those still on the road, due back as long as they've been gone (see Recall). Vikings out on a quest are kept
	/// the same way, due back at their own village when the quest is (see VillageQuests), or at the village its vikings
	/// went to when it was disbanded meanwhile (see QuestsDisbanded)
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
			TravelPlanner.Register();
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
		/// none of them yet, when there's no spot for them, so they come home together - ending the village's quest
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="vikings">Its living vikings' ZDOs, added to</param>
		/// <param name="entry">The group, emptied as they come</param>
		private static void ArriveTogether(Village village, List<ZDO> vikings, TravelEntry entry)
		{
			if (entry.Vikings.Count == 0 || !VillageRespawn.ArrivalSpotFor(village, entry.Vikings[0], entry.From, out Vector3 spot, out bool outside))
			{
				return;
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
			if (!ZNet.instance.IsServer() || WorldGenerator.instance == null)
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
				queue.Add(Pack(members));
				foreach (ZDO zdo in members)
				{
					outSince.Remove(zdo.m_uid);
					zdo.SetOwner(ZDOMan.GetSessionID());
					ZDOMan.instance.DestroyZDO(zdo);
				}
				changed = true;
			}
			if (changed)
			{
				Save();
			}
		}

		/// <summary>
		/// Checks whether every viking of a group has set sail, stepped into a portal or been out of every player's reach
		/// for OutOfReachTime
		/// </summary>
		/// <param name="members">The group's vikings</param>
		/// <returns>True when they all have</returns>
		private static bool OutOfReach(List<ZDO> members)
		{
			bool all = true;
			foreach (ZDO zdo in members)
			{
				if (Leaving(zdo))
				{
					continue;
				}
				if (InAnyReach(zdo.GetPosition()))
				{
					outSince.Remove(zdo.m_uid);
					all = false;
					continue;
				}
				if (!outSince.TryGetValue(zdo.m_uid, out float since))
				{
					outSince[zdo.m_uid] = Time.time;
					all = false;
				}
				else if (Time.time - since < OutOfReachTime)
				{
					all = false;
				}
			}
			return(all);
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
			foreach (ZDO zdo in members)
			{
				TravelRoad road = TravelRoad.Decode(zdo.GetByteArray(RouteKey));
				float left = road.Seconds(zdo.GetInt(LegKey, 1), zdo.GetPosition(), boat);
				if (left >= entry.Remaining)
				{
					entry.Remaining = left;
					total = road.TotalSeconds(boat);
					entry.From = ((road.Count > 0) ? road.Approach() : zdo.GetPosition());
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
			entry.Due = entry.Unloaded + NetTime.Ticks(entry.Remaining);
			Plugin.Log.LogInfo("Travel: " + members.Count + " vikings (" + party.Describe() + ") " + how + " on their way to the village at " + entry.Destination.ToString("F0") + ", " + entry.Spent.ToString("F0") + "s into the road, due there in " + entry.Remaining.ToString("F0") + "s");
			TestLog.Count("Travel", "groups taken off the road, " + counted);
			return(entry);
		}

		/// <summary>
		/// Hands each group that's due to the client running its village, once the village has had every viking it starts
		/// with - or finds it another village when it turned out to be a farm that stayed abandoned or a village since
		/// disbanded. A group due at a farm not generated or loaded yet waits for it, and a group out on a quest waits for
		/// the rest of its vikings to leave the village and join it, so they all come home together (see
		/// TravelEntry.QuestWaiting)
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
				if (village == null && !IsPlannedFarm(entry.Destination))
				{
					Reroute(entry);
					continue;
				}
				if (village == null)
				{
					continue;
				}
				if (village.GetPrefab() == VillagePaths.GeneratorHash && village.GetBool(VillagePaths.AbandonedKey))
				{
					Reroute(entry);
					continue;
				}
				bool ready = (VillageFounding.IsAnchor(village.GetPrefab()) || village.GetBool(VillageRespawn.PopulatedKey) || entry.Returning);
				if (!ready || !village.HasOwner())
				{
					continue;
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
		/// Finds a group on the road another village, from the one it found no one in - due there once it has run the
		/// road from there, or trying again after LostRetry when there's none
		/// </summary>
		/// <param name="entry">The group, kept in the queue meanwhile</param>
		private static void Reroute(TravelEntry entry)
		{
			entry.Excluded.Add(entry.Destination);
			entry.Due = long.MaxValue;
			Vector3 from = entry.Destination;
			TravelParty party = new TravelParty { Easiest = entry.Biome, Hardest = entry.BoatBiome, Unteleportable = entry.Unteleportable };
			TravelPlanner.PlanHere(from, party, entry.Excluded, entry.Vikings.Count + " vikings on the road that found no one at their village", (TravelPlanner.Answer answer) =>
			{
				long now = NetTime.Now();
				if (!answer.Found)
				{
					entry.Due = now + NetTime.Ticks(LostRetry);
					entry.Excluded.Remove(from);
				}
				else
				{
					entry.Destination = answer.Destination;
					entry.Name = answer.Name;
					entry.From = ((answer.Road.Count > 0) ? answer.Road.Approach() : from);
					entry.Unloaded = now;
					entry.Spent += entry.Remaining;
					entry.Remaining = answer.Seconds;
					entry.Due = now + NetTime.Ticks(answer.Seconds);
					TestLog.Count("Travel", "groups rerouted on the road");
				}
				Save();
			});
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
				group.Due = System.Math.Max(group.Due, entry.Due);
			}
			else
			{
				queue.Add(entry);
			}
			Save();
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
				TravelParty party = new TravelParty { Easiest = entry.Biome, Hardest = entry.BoatBiome, Unteleportable = entry.Unteleportable };
				lines.Add(entry.Vikings.Count + " vikings " + what + " to " + entry.Destination.ToString("F0") + ((entry.Name.Length > 0) ? " (" + entry.Name + ")" : "") + ", " + due + ", " + party.Describe() + ((entry.FromName.Length > 0) ? ", from " + entry.FromName : "") + ((entry.Excluded.Count > 0) ? ", " + entry.Excluded.Count + " villages found empty" : ""));
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
					entry.Due = System.Math.Max(entry.Due, now) + NetTime.Ticks(answer.Seconds);
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
		/// Calls back on the server the groups on the road that left the village disbanded at a bonfire, to the village
		/// just started there - each due back after as long as it had been on the road, coming back the way it went, or
		/// for vikings out on a quest, when the quest was due back, and coming to the village's beds - and tells the client
		/// that started it how many and when the last are due
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
			double ticksPerSecond = NetTime.Ticks(1f);
			foreach (TravelEntry entry in queue)
			{
				if (entry.Returning || entry.OriginFire == Vector3.zero || Utils.DistanceXZ(entry.OriginFire, fire) > 1f)
				{
					continue;
				}
				entry.Returning = true;
				entry.Destination = anchor;
				// Vikings out on a quest come back when the quest was due, as though the village had never been disbanded
				if (entry.OnQuest)
				{
					entry.Due = System.Math.Max(now, entry.QuestDue);
				}
				else
				{
					entry.Due = now + NetTime.Ticks(entry.Progress(now, ticksPerSecond));
				}
				count += entry.Vikings.Count;
				latest = System.Math.Max(latest, entry.Due);
			}
			if (count > 0)
			{
				Save();
				Plugin.Log.LogInfo("Travel: " + count + " vikings on the road were called back to the village started at " + fire.ToString("F0"));
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
