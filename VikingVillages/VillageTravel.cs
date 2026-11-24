using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Finds the village a living viking with none goes to, and keeps the vikings on the road while no player is near
	/// them - the server's side of the vikings' travels, and the calls each client makes to it
	/// </summary>
	/// <remarks>
	/// Only the server has the world's plan of its farms and every village's ZDO, so a client asks it for a viking's
	/// village (see Search) and it plans the road over the world's map (see TravelRoute), to the farm or village a player
	/// started that it can get to soonest, anywhere in the world, no harder than the viking's biome - generated or not,
	/// leaving out farms that stayed abandoned - crossing the sea in the best boat its biome has (see
	/// TravelTime.BoatSpeed). A viking on the road that's out of every player's reach, or has reached the sea and set
	/// sail, is taken out of the world and kept in a record of the server's, a ZDO far past the edge of the world, until
	/// it's due at its village - the rest of its road on foot at TravelTime.Speed and by sea at its boat's speed - and
	/// then handed to the client that runs the village, which brings it in from the side it came from (see Arrive). A
	/// village that's never loaded keeps it waiting. A village started again at the bonfire of a village its vikings left
	/// calls back those still on the road, due back as long as they've been gone (see Recall). Vikings out on a quest
	/// are kept the same way, due back at their own village when the quest is (see VillageQuests), or at the village
	/// its vikings went to when it was disbanded meanwhile (see QuestsDisbanded)
	/// </remarks>
	internal static class VillageTravel
	{
		/// <summary>
		/// On a living viking, that it was spawned out in the wild with a console command and is still lost
		/// </summary>
		public static readonly int ConfusedKey = "kriona.VikingVillages.vikingConfused".GetStableHashCode();

		/// <summary>
		/// On a living viking on the road, where the village it's going to stands, its name and who gave it, its road (see
		/// TravelRoad.Encode), the index of the waypoint it's heading for, the group it set out with, or "", its biome -
		/// the easiest of its group's, which picks the boat they cross the sea in - and that it has reached the sea and
		/// is setting sail, so the server takes it off the road at once
		/// </summary>
		public static readonly int DestinationKey = "kriona.VikingVillages.vikingDestination".GetStableHashCode();
		public static readonly int DestinationNameKey = "kriona.VikingVillages.vikingDestinationName".GetStableHashCode();
		public static readonly int DestinationAuthorKey = "kriona.VikingVillages.vikingDestinationAuthor".GetStableHashCode();
		public static readonly int RouteKey = "kriona.VikingVillages.vikingRoute".GetStableHashCode();
		public static readonly int LegKey = "kriona.VikingVillages.vikingRouteLeg".GetStableHashCode();
		public static readonly int GroupKey = "kriona.VikingVillages.vikingTravelGroup".GetStableHashCode();
		public static readonly int BiomeKey = "kriona.VikingVillages.vikingTravelBiome".GetStableHashCode();
		public static readonly int SailingKey = "kriona.VikingVillages.vikingSailing".GetStableHashCode();

		/// <summary>
		/// On a living viking that left a village a player disbanded, the village's name, kept until it joins another (see
		/// VillageNewcomers)
		/// </summary>
		public static readonly int FromNameKey = "kriona.VikingVillages.vikingFromVillage".GetStableHashCode();

		/// <summary>
		/// The routed RPCs - a client asking the server for a viking's village, and the server's answer; the server handing
		/// a group that's due to the client that runs its village, and that client handing it back when it can't bring it
		/// in; a client handing the server a group to keep on the road; and a client asking the server to call back the
		/// vikings that left a bonfire, and the server's answer
		/// </summary>
		private const string SearchRpc = Plugin.PluginGuid + ".TravelSearch";
		private const string FoundRpc = Plugin.PluginGuid + ".TravelFound";
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
		/// The routed RPCs that ask the server how long a village's vikings take to get to where a quest goes, and its
		/// answer
		/// </summary>
		private const string QuestRouteRpc = Plugin.PluginGuid + ".TravelQuestRoute";
		private const string QuestRoutedRpc = Plugin.PluginGuid + ".TravelQuestRouted";

		/// <summary>
		/// On a client, what to do with the server's answer to each quest's road it asked for, by the question's ID
		/// </summary>
		private static readonly Dictionary<long, System.Action<float>> askedRoutes = new Dictionary<long, System.Action<float>>();

		/// <summary>
		/// The prefab hash of the record the server keeps the vikings on the road in - one no prefab has - where it
		/// stands, and the key they're written to
		/// </summary>
		private const string RecordPrefab = "kriona.VikingVillages.travelRecord";
		private static readonly Vector3 RecordPosition = new Vector3(-15000f, -1000f, -15100f);
		private static readonly int QueueKey = "kriona.VikingVillages.travelQueue".GetStableHashCode();

		/// <summary>
		/// How far from the middle of the world the sea can be crossed, in meters - short of the edge, past which a boat
		/// is lost
		/// </summary>
		private const float WorldEdge = 10400f;

		/// <summary>
		/// How many squares of the map a search looks at each update (see TravelRoute.Step)
		/// </summary>
		private const int SearchBudget = 3000;

		/// <summary>
		/// Seconds the server keeps the list of villages to go to before looking through the world again
		/// </summary>
		private const float CandidateKeep = 60f;

		/// <summary>
		/// How near a farm's planned spot its generator, or the spot a village is noted at, has to be to be it, in meters
		/// </summary>
		private const float FarmMatch = 16f;

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
		/// Seconds a client holds a group handed to it for a village it no longer runs before handing it back
		/// </summary>
		private const float HoldTime = 30f;

		/// <summary>
		/// How far below a square of lava its paint mask is, from the Ashlands' terrain (see Heightmap.IsLava)
		/// </summary>
		private const float LavaMask = 0.6f;

		/// <summary>
		/// What the server answers about a viking's village - whether it found one, where it stands, its name and who gave
		/// it, the road there, how long it is, in meters, and how long it takes, in seconds
		/// </summary>
		internal class Answer
		{
			public bool Found;
			public Vector3 Destination;
			public string Name = "";
			public string Author = "";
			public TravelRoad Road = new TravelRoad();
			public float Length;
			public float Seconds;

			/// <summary>
			/// Gets the village's name as a viking says it, filtered for its author's words and without rich text
			/// </summary>
			/// <returns>The name, or ""</returns>
			public string Spoken()
			{
				return(VikingNames.Filter(Name, Author).RemoveRichTextTags().Trim());
			}

			/// <summary>
			/// Writes the answer to a package
			/// </summary>
			/// <param name="pkg">The package</param>
			public void Write(ZPackage pkg)
			{
				pkg.Write(Found);
				pkg.Write(Destination);
				pkg.Write(Name);
				pkg.Write(Author);
				pkg.Write(Road.Encode());
				pkg.Write(Length);
				pkg.Write(Seconds);
			}

			/// <summary>
			/// Reads an answer from a package
			/// </summary>
			/// <param name="pkg">The package</param>
			/// <returns>The answer</returns>
			public static Answer Read(ZPackage pkg)
			{
				Answer answer = new Answer();
				answer.Found = pkg.ReadBool();
				answer.Destination = pkg.ReadVector3();
				answer.Name = pkg.ReadString();
				answer.Author = pkg.ReadString();
				answer.Road = TravelRoad.Decode(pkg.ReadByteArray());
				answer.Length = pkg.ReadSingle();
				answer.Seconds = pkg.ReadSingle();
				return(answer);
			}
		}

		/// <summary>
		/// A village a viking could go to - where it stands, its name, or "", who gave it, and its biome by VikingGear's
		/// order
		/// </summary>
		private struct Candidate
		{
			public Vector3 Position;
			public string Name;
			public string Author;
			public int Biome;
		}

		/// <summary>
		/// A search the server runs - who asked, with what, and the road search once it's started
		/// </summary>
		private class Search
		{
			public long Id;
			public long Requester;
			public Vector3 From;
			public int Biome;
			public List<Vector3> Excluded = new List<Vector3>();
			public System.Action<Answer> Local;
			public List<Candidate> Candidates;
			public TravelRoute Route;

			/// <summary>
			/// The spots the road may lead to in place of the villages, for the road to a quest, or null
			/// </summary>
			public List<Vector3> Targets;
		}

		/// <summary>
		/// On the server, the searches waiting and the one running, the villages to go to and when they were listed, and
		/// the biome of each village's spot as worked out once
		/// </summary>
		private static readonly Queue<Search> searches = new Queue<Search>();
		private static Search running;
		private static List<Candidate> candidates;
		private static float candidatesAt;
		private static readonly Dictionary<Vector3, int> biomes = new Dictionary<Vector3, int>();

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
		/// On a client, what to do with the server's answer to each search it asked for, by the search's ID
		/// </summary>
		private static readonly Dictionary<long, System.Action<Answer>> asked = new Dictionary<long, System.Action<Answer>>();

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
			ZRoutedRpc.instance.Register<ZPackage>(SearchRpc, OnSearch);
			ZRoutedRpc.instance.Register<ZPackage>(FoundRpc, OnFound);
			ZRoutedRpc.instance.Register<ZPackage>(DeliverRpc, OnDeliver);
			ZRoutedRpc.instance.Register<ZPackage>(HandBackRpc, OnHandBack);
			ZRoutedRpc.instance.Register<ZPackage>(QueueRpc, OnQueue);
			ZRoutedRpc.instance.Register<Vector3, Vector3>(RecallRpc, OnRecall);
			ZRoutedRpc.instance.Register<int, long>(RecalledRpc, OnRecalled);
			ZRoutedRpc.instance.Register<ZPackage>(QuestsDisbandedRpc, OnQuestsDisbanded);
			ZRoutedRpc.instance.Register<ZPackage>(QuestRouteRpc, OnQuestRoute);
			ZRoutedRpc.instance.Register<long, float>(QuestRoutedRpc, OnQuestRouted);
			askedRoutes.Clear();
			searches.Clear();
			running = null;
			candidates = null;
			biomes.Clear();
			record = null;
			queue = new List<TravelEntry>();
			sweepIndex = -1;
			sweepFound.Clear();
			nextSweep = 0f;
			outSince.Clear();
			asked.Clear();
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
		}

		/// <summary>
		/// Sets a viking out on the road to a village
		/// </summary>
		/// <param name="viking">The viking's ZDO, owned by this client</param>
		/// <param name="answer">The server's answer, with a village found</param>
		/// <param name="group">The group it sets out with, or ""</param>
		/// <param name="biome">Its biome, by VikingGear's order - its group's easiest, so they all sail in the same boat</param>
		public static void SetOut(ZDO viking, Answer answer, string group, int biome)
		{
			viking.Set(ConfusedKey, false);
			viking.Set(DestinationKey, answer.Destination);
			viking.Set(DestinationNameKey, answer.Name);
			viking.Set(DestinationAuthorKey, answer.Author);
			viking.Set(RouteKey, answer.Road.Encode());
			viking.Set(LegKey, 1);
			viking.Set(GroupKey, group);
			viking.Set(BiomeKey, biome);
			viking.Set(SailingKey, false);
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
		/// Gets the biome a viking on the road picks its boat by
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>The biome, by VikingGear's order</returns>
		public static int Biome(ZDO viking)
		{
			return(viking.GetInt(BiomeKey, 0));
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
		/// Asks the server for the nearest village a viking can go to - the answer comes back later, to the action given
		/// </summary>
		/// <param name="from">Where the viking is</param>
		/// <param name="biome">Its biome, by VikingGear's order, the hardest a village can be</param>
		/// <param name="excluded">The villages to leave out</param>
		/// <param name="done">What to do with the answer</param>
		public static void Ask(Vector3 from, int biome, List<Vector3> excluded, System.Action<Answer> done)
		{
			if (ZRoutedRpc.instance == null)
			{
				return;
			}
			long id = ((long)Random.Range(int.MinValue, int.MaxValue) << 32) ^ Random.Range(int.MinValue, int.MaxValue);
			asked[id] = done;
			ZPackage pkg = new ZPackage();
			pkg.Write(id);
			pkg.Write(from);
			pkg.Write(biome);
			pkg.Write(excluded.Count);
			foreach (Vector3 point in excluded)
			{
				pkg.Write(point);
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), SearchRpc, pkg);
		}

		/// <summary>
		/// Forgets the answer to a search no one's waiting for any more
		/// </summary>
		/// <param name="done">The action that was to have it</param>
		public static void Forget(System.Action<Answer> done)
		{
			List<long> ids = new List<long>();
			foreach (KeyValuePair<long, System.Action<Answer>> entry in asked)
			{
				if (entry.Value == done)
				{
					ids.Add(entry.Key);
				}
			}
			foreach (long id in ids)
			{
				asked.Remove(id);
			}
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
		/// village as a newcomer, or coming home from a quest of the village's, which ends its quest (see
		/// VillageQuests.Returned)
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
				while (entry.Vikings.Count > 0)
				{
					ZDO zdo = VillageRespawn.SpawnArrival(village, vikings, entry.Vikings[0], entry.From);
					if (zdo == null)
					{
						break;
					}
					entry.Vikings.RemoveAt(0);
					if (entry.OnQuest)
					{
						VillageQuests.Returned(village, entry.QuestId);
					}
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
		/// Runs the server's side - its searches, its look for vikings out of reach and the groups due at their villages -
		/// and on a client hands back the groups it held too long - call it from the plugin's Update
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
			UpdateSearch();
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
		/// Takes a client's search on the server, to run after those before it
		/// </summary>
		/// <param name="sender">The client</param>
		/// <param name="pkg">The search's ID, where the viking is, its biome and the villages to leave out</param>
		private static void OnSearch(long sender, ZPackage pkg)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer())
			{
				return;
			}
			Search search = new Search();
			search.Id = pkg.ReadLong();
			search.Requester = sender;
			search.From = pkg.ReadVector3();
			search.Biome = pkg.ReadInt();
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				search.Excluded.Add(pkg.ReadVector3());
			}
			searches.Enqueue(search);
		}

		/// <summary>
		/// Gives a client the server's answer to its search
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="pkg">The search's ID and the answer</param>
		private static void OnFound(long sender, ZPackage pkg)
		{
			long id = pkg.ReadLong();
			Answer answer = Answer.Read(pkg);
			if (asked.TryGetValue(id, out System.Action<Answer> done))
			{
				asked.Remove(id);
				done(answer);
			}
		}

		/// <summary>
		/// Runs the server's searches a little at a time, one after another, answering each as it ends
		/// </summary>
		private static void UpdateSearch()
		{
			if (running == null)
			{
				if (searches.Count == 0)
				{
					return;
				}
				running = searches.Dequeue();
				Begin(running);
			}
			if (!running.Route.Step(SearchBudget))
			{
				return;
			}
			Search done = running;
			running = null;
			Answer answer = new Answer();
			if (done.Route.Found >= 0)
			{
				Candidate found = done.Candidates[done.Route.Found];
				answer.Found = true;
				answer.Destination = found.Position;
				answer.Name = found.Name;
				answer.Author = found.Author;
				answer.Road = done.Route.Road;
				answer.Length = done.Route.Length;
				answer.Seconds = done.Route.Seconds;
			}
			Plugin.Log.LogInfo("Travel: a viking at " + done.From.ToString("F0") + (answer.Found ? " is going to the village at " + answer.Destination.ToString("F0") + ((answer.Name.Length > 0) ? " (" + answer.Name + ")" : "") + ", " + answer.Length.ToString("F0") + "m away, " + answer.Seconds.ToString("F0") + "s by road and sea" : " found no village it can reach"));
			if (done.Local != null)
			{
				done.Local(answer);
				return;
			}
			ZPackage pkg = new ZPackage();
			pkg.Write(done.Id);
			answer.Write(pkg);
			ZRoutedRpc.instance.InvokeRoutedRPC(done.Requester, FoundRpc, pkg);
		}

		/// <summary>
		/// Starts a search - the road search out from the viking to every village in the world that's no harder than its
		/// biome and isn't left out, or to the spot a quest goes to, crossing the sea in the best boat its biome has
		/// </summary>
		/// <param name="search">The search</param>
		private static void Begin(Search search)
		{
			if (search.Targets != null)
			{
				search.Candidates = search.Targets.ConvertAll((Vector3 target) => new Candidate { Position = target, Name = "", Author = "", Biome = search.Biome });
			}
			else
			{
				List<Candidate> all = Candidates();
				search.Candidates = all.FindAll((Candidate candidate) => candidate.Biome <= search.Biome && !search.Excluded.Exists((Vector3 excluded) => Utils.DistanceXZ(excluded, candidate.Position) <= FarmMatch));
			}
			List<Vector3> targets = search.Candidates.ConvertAll((Candidate candidate) => candidate.Position);
			search.Route = new TravelRoute(search.From, search.Biome, TravelTime.BoatSpeed(search.Biome), targets, Sample);
		}

		/// <summary>
		/// Reads a square of the world's map from its plan - the sea is crossed by boat, and the Ashlands' lava and the
		/// water past WorldEdge can't be crossed at all
		/// </summary>
		/// <param name="x">The point's x</param>
		/// <param name="z">The point's z</param>
		/// <returns>The square</returns>
		private static TravelRoute.Cell Sample(float x, float z)
		{
			if (x * x + z * z > WorldEdge * WorldEdge)
			{
				return(new TravelRoute.Cell { Open = false });
			}
			Heightmap.Biome biome = WorldGenerator.instance.GetBiome(x, z);
			TravelRoute.Cell cell = new TravelRoute.Cell { Open = true, Sea = (biome == Heightmap.Biome.Ocean), Biome = VikingGear.IndexOf(biome) };
			if (biome == Heightmap.Biome.AshLands)
			{
				WorldGenerator.instance.GetBiomeHeight(biome, x, z, out Color mask);
				cell.Open = (mask.a <= LavaMask);
			}
			return(cell);
		}

		/// <summary>
		/// Lists the villages a viking could go to - every farm the world was planned with, but those generated that
		/// stayed abandoned, and every village a player started - named as they're named, or for a farm not set up yet by
		/// the name settled for it (see VillagePlannedNames), and kept for CandidateKeep seconds
		/// </summary>
		/// <returns>The villages</returns>
		private static List<Candidate> Candidates()
		{
			if (candidates != null && Time.time - candidatesAt < CandidateKeep)
			{
				return(candidates);
			}
			List<ZDO> generators = AllWithPrefab(VillagePaths.GeneratorPrefab);
			List<ZDO> anchors = AllWithPrefab(VillageFounding.PrefabName);
			List<Candidate> found = new List<Candidate>();
			foreach (Vector3 farm in FarmSpawns.PlannedFarms())
			{
				ZDO generator = generators.Find((ZDO zdo) => Utils.DistanceXZ(zdo.GetPosition(), farm) <= FarmMatch);
				if (generator != null && generator.GetBool(VillagePaths.AbandonedKey))
				{
					continue;
				}
				string name = "";
				string author = "";
				if (generator == null || !VillageNames.At(generator, out name, out author))
				{
					name = (VillagePlannedNames.For(farm) ?? "");
					author = "";
				}
				found.Add(new Candidate { Position = farm, Name = name, Author = (author ?? ""), Biome = BiomeAt(farm) });
			}
			foreach (ZDO anchor in anchors)
			{
				VillageNames.At(anchor, out string name, out string author);
				found.Add(new Candidate { Position = anchor.GetPosition(), Name = (name ?? ""), Author = (author ?? ""), Biome = BiomeAt(anchor.GetPosition()) });
			}
			candidates = found;
			candidatesAt = Time.time;
			return(candidates);
		}

		/// <summary>
		/// Works out the biome of a village's vikings at a spot once (see VillageFarmhouses.HardestBiomeNear)
		/// </summary>
		/// <param name="spot">The spot</param>
		/// <returns>The biome, by VikingGear's order</returns>
		private static int BiomeAt(Vector3 spot)
		{
			if (!biomes.TryGetValue(spot, out int biome))
			{
				biome = VillageFarmhouses.HardestBiomeNear(spot, VillageFarmhouses.HardBiomeVikingReach);
				biomes[spot] = biome;
			}
			return(biome);
		}

		/// <summary>
		/// Lists every ZDO of a prefab the server has
		/// </summary>
		/// <param name="prefab">The prefab's name</param>
		/// <returns>The ZDOs</returns>
		private static List<ZDO> AllWithPrefab(string prefab)
		{
			List<ZDO> found = new List<ZDO>();
			int index = 0;
			while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(prefab, found, ref index))
			{
			}
			return(found);
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
		/// Checks whether every viking of a group has set sail or been out of every player's reach for OutOfReachTime
		/// </summary>
		/// <param name="members">The group's vikings</param>
		/// <returns>True when they all have</returns>
		private static bool OutOfReach(List<ZDO> members)
		{
			bool all = true;
			foreach (ZDO zdo in members)
			{
				if (zdo.GetBool(SailingKey))
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
		/// covered the rest of its road, on foot and by sea in the group's boat, and arriving from the way the road comes
		/// in
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
			entry.Biome = int.MaxValue;
			foreach (ZDO zdo in members)
			{
				entry.Biome = Mathf.Min(entry.Biome, Biome(zdo));
			}
			float boat = TravelTime.BoatSpeed(entry.Biome);
			float total = 0f;
			bool sailing = false;
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
				sailing |= zdo.GetBool(SailingKey);
				entry.Vikings.Add(VillageDeparted.SnapshotOf(zdo));
			}
			entry.Spent = Mathf.Max(0f, total - entry.Remaining);
			entry.Due = entry.Unloaded + NetTime.Ticks(entry.Remaining);
			Plugin.Log.LogInfo("Travel: " + members.Count + " vikings " + (sailing ? "set sail in a " + TravelTime.BoatName(entry.Biome) : "went out of every player's reach") + " on their way to the village at " + entry.Destination.ToString("F0") + ", due there in " + entry.Remaining.ToString("F0") + "s");
			return(entry);
		}

		/// <summary>
		/// Hands each group that's due to the client running its village, once the village has had every viking it starts
		/// with - or finds it another village when it turned out to be a farm that stayed abandoned or a village since
		/// disbanded. A group due at a farm not generated or loaded yet waits for it
		/// </summary>
		private static void Deliver()
		{
			long now = NetTime.Now();
			bool changed = false;
			for (int i = queue.Count - 1; i >= 0; i--)
			{
				TravelEntry entry = queue[i];
				if (entry.Due > now)
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
			Search search = new Search();
			search.From = from;
			search.Biome = entry.Biome;
			search.Excluded = new List<Vector3>(entry.Excluded);
			search.Local = (Answer answer) =>
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
				}
				Save();
			};
			searches.Enqueue(search);
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
			}
			else
			{
				queue.Add(entry);
			}
			Save();
		}

		/// <summary>
		/// Asks the server how long a village's vikings take to get to where a quest goes (see QuestTargets), each way -
		/// the answer comes back later, to the action given
		/// </summary>
		/// <param name="from">Where the village stands</param>
		/// <param name="biome">The village's biome, by VikingGear's order</param>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <param name="done">What to do with the answer, in seconds</param>
		public static void AskQuestRoute(Vector3 from, int biome, QuestKind kind, string item, System.Action<float> done)
		{
			if (ZRoutedRpc.instance == null)
			{
				return;
			}
			long id = ((long)Random.Range(int.MinValue, int.MaxValue) << 32) ^ Random.Range(int.MinValue, int.MaxValue);
			askedRoutes[id] = done;
			ZPackage pkg = new ZPackage();
			pkg.Write(id);
			pkg.Write(from);
			pkg.Write(biome);
			pkg.Write((int)kind);
			pkg.Write(item);
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), QuestRouteRpc, pkg);
		}

		/// <summary>
		/// Works out on the server how long a village's vikings take to get to where a quest goes - the road there, over
		/// land and sea in the best boat the village's biome has, or straight there on foot when there's no road, or
		/// QuestRules.DefaultTravel when there's nowhere to go - and answers the client that asked
		/// </summary>
		/// <param name="sender">The client that asked</param>
		/// <param name="pkg">The question's ID, where the village stands, its biome, the quest and what it's for</param>
		private static void OnQuestRoute(long sender, ZPackage pkg)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer())
			{
				return;
			}
			long id = pkg.ReadLong();
			Vector3 from = pkg.ReadVector3();
			int biome = pkg.ReadInt();
			QuestKind kind = (QuestKind)pkg.ReadInt();
			string item = pkg.ReadString();
			Vector3 target = QuestTargets.Find(kind, item, from, biome);
			if (target == Vector3.zero)
			{
				ZRoutedRpc.instance.InvokeRoutedRPC(sender, QuestRoutedRpc, id, QuestRules.DefaultTravel);
				return;
			}
			Search search = new Search();
			search.From = from;
			search.Biome = biome;
			search.Targets = new List<Vector3> { target };
			search.Local = (Answer answer) =>
			{
				float seconds = (answer.Found ? answer.Seconds : TravelTime.Seconds(Utils.DistanceXZ(from, target)));
				Plugin.Log.LogInfo("Travel: a quest " + kind + " from the village at " + from.ToString("F0") + " goes to " + target.ToString("F0") + ", " + seconds.ToString("F0") + "s each way");
				ZRoutedRpc.instance.InvokeRoutedRPC(sender, QuestRoutedRpc, id, seconds);
			};
			searches.Enqueue(search);
		}

		/// <summary>
		/// Gives a client the server's answer to how long a quest's road takes
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="id">The question's ID</param>
		/// <param name="seconds">How long it takes each way, in seconds</param>
		private static void OnQuestRouted(long sender, long id, float seconds)
		{
			if (askedRoutes.TryGetValue(id, out System.Action<float> done))
			{
				askedRoutes.Remove(id);
				done(seconds);
			}
		}

		/// <summary>
		/// Tells the server a village was disbanded, so the vikings out on its quest come back to the village its vikings
		/// went to instead, later by the road from it there (see OnQuestsDisbanded)
		/// </summary>
		/// <param name="farm">Where the village stood - its farm's spot or its anchor</param>
		/// <param name="fire">Where its bonfire stands, which a village started again there calls them back by</param>
		/// <param name="answer">The village its vikings went to, not found when there's none</param>
		public static void QuestsDisbanded(Vector3 farm, Vector3 fire, Answer answer)
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
			Answer answer = Answer.Read(pkg);
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
