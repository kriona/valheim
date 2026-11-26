using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Plans every long trip living vikings make - where they go and the road there - for a viking with no village, the
	/// vikings of a disbanded village and a village's quests alike, on the server, and the calls each client makes to it
	/// </summary>
	/// <remarks>
	/// Only the server has the world's plan of its farms, its locations, every village's ZDO and every portal, so a client
	/// asks it (see Ask and AskQuestRoute) and it plans the road over the world's map (see TravelRoute) to whichever
	/// place it can get to soonest - a village no harder than the easiest biome of the party (see TravelParty), or one of
	/// the places a quest could go (see QuestTargets). The road keeps out of biomes harder than the easiest of them where
	/// it can, crosses the sea in the best boat of the hardest of them, never across the Ashlands' boiling sea in a boat
	/// it would burn, and goes through any pair of connected portals that would take what they carry - by the world's
	/// rules and the portal's own (see TeleportWorld.m_allowAllItems). Searches run one at a time, a little each update
	/// (see TravelRoute.Step)
	/// </remarks>
	internal static class TravelPlanner
	{
		/// <summary>
		/// The routed RPCs - a client asking the server for a viking's village, and the server's answer; a client asking
		/// how long a village's vikings take to get to where a quest goes and back, and the server's answer
		/// </summary>
		private const string SearchRpc = Plugin.PluginGuid + ".TravelSearch";
		private const string FoundRpc = Plugin.PluginGuid + ".TravelFound";
		private const string QuestRouteRpc = Plugin.PluginGuid + ".TravelQuestRoute";
		private const string QuestRoutedRpc = Plugin.PluginGuid + ".TravelQuestRouted";

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
		public const float FarmMatch = 16f;

		/// <summary>
		/// How far below a square of lava its paint mask is, from the Ashlands' terrain (see Heightmap.IsLava)
		/// </summary>
		private const float LavaMask = 0.6f;

		/// <summary>
		/// How far below the sea's surface the Ashlands' ground has to be for its water to be crossed by boat rather than
		/// waded, in meters - the Ashlands' sea counting as its biome's ground in the world's plan, not the ocean's
		/// </summary>
		private const float AshlandsSeaDepth = 1.5f;

		/// <summary>
		/// How deep water has to be for a viking to swim in it rather than wade, in meters - the depth a character starts
		/// to swim at (see Character.InLiquidSwimDepth, with its m_swimDepth of 2)
		/// </summary>
		private const float SwimDepth = 1.6f;

		/// <summary>
		/// How deep water off the sea has to be for a boat to cross it, in meters - deeper than the vikings launch a boat
		/// in (see VillageLife.LaunchDepth), so a road by boat never runs aground. The world's plan only counts water as
		/// the ocean well out from the shore, leaving deep water off a beach, between an island and the mainland, to the
		/// land's biome
		/// </summary>
		private const float SailDepth = 2.5f;

		/// <summary>
		/// The sea's level when the zone system isn't there to say, in meters
		/// </summary>
		private const float DefaultWaterLevel = 30f;

		/// <summary>
		/// How far in front of a portal a viking steps into it and comes out of it when the portal's own distance can't be
		/// read, in meters (see TeleportWorld.m_exitDistance)
		/// </summary>
		private const float PortalFront = 1f;

		/// <summary>
		/// What the server answers about a trip - whether it found somewhere to go, where it stands, its name and who gave
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
		/// Where a search looks for a village to go to - the one the party gets to soonest, one picked at random, the
		/// village started again at the bonfire of the village they left, their home, or one by its name, whatever its
		/// biome, for the kri_vv_travel command
		/// </summary>
		internal enum Goal
		{
			Soonest,
			Random,
			Home,
			Named
		}

		/// <summary>
		/// How many villages picked at random a search tries before it gives up on finding one it can reach
		/// </summary>
		private const int RandomTries = 5;

		/// <summary>
		/// How near a bonfire the anchor of a village started at it stands, in meters
		/// </summary>
		private const float AnchorMatch = 2f;

		/// <summary>
		/// A search the server runs - who asked, with what, what it's for in the log, and the road search once it's started
		/// </summary>
		private class Search
		{
			public long Id;
			public long Requester;
			public Vector3 From;
			public TravelParty Party = TravelParty.Of(0);
			public List<Vector3> Excluded = new List<Vector3>();
			public System.Action<Answer> Local;
			public string Label = "a viking";
			public List<Candidate> Candidates;
			public TravelRoute Route;

			/// <summary>
			/// Where it looks for a village, the bonfire of the village they left for a search for home, and how many
			/// villages picked at random it has tried
			/// </summary>
			public Goal Goal = Goal.Soonest;
			public Vector3 HomeFire;
			public int Tries;

			/// <summary>
			/// For a search by name, the name, or part of it
			/// </summary>
			public string Named = "";

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
		/// On a client, what to do with the server's answer to each search it asked for, and to each quest's road, by the
		/// question's ID
		/// </summary>
		private static readonly Dictionary<long, System.Action<Answer>> asked = new Dictionary<long, System.Action<Answer>>();
		private static readonly Dictionary<long, System.Action<float, float, int, TravelRoad>> askedRoutes = new Dictionary<long, System.Action<float, float, int, TravelRoad>>();

		/// <summary>
		/// Registers the RPCs with a world's routed RPC, starting the world with no search running
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance.Register<ZPackage>(SearchRpc, OnSearch);
			ZRoutedRpc.instance.Register<ZPackage>(FoundRpc, OnFound);
			ZRoutedRpc.instance.Register<ZPackage>(QuestRouteRpc, OnQuestRoute);
			ZRoutedRpc.instance.Register<long, float, float, int, byte[]>(QuestRoutedRpc, OnQuestRouted);
			searches.Clear();
			running = null;
			candidates = null;
			biomes.Clear();
			asked.Clear();
			askedRoutes.Clear();
		}

		/// <summary>
		/// Runs the server's searches a little at a time - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (ZNet.instance == null || ZDOMan.instance == null || ZRoutedRpc.instance == null || !ServerCheck.Active)
			{
				return;
			}
			if (!ZNet.instance.IsServer() || WorldGenerator.instance == null)
			{
				return;
			}
			UpdateSearch();
		}

		/// <summary>
		/// Works out what a viking brings to its party - its biome, whether it carries something a portal won't take, and
		/// how far it swims before it would start to drown, by its food and Swim skill as it is now, the same as when it's
		/// wrecked at sea (see VillageLife.StartSwim)
		/// </summary>
		/// <param name="party">The party, added to</param>
		/// <param name="viking">The viking</param>
		/// <param name="first">Whether it's the first one added</param>
		public static void AddTo(TravelParty party, Humanoid viking, bool first)
		{
			VikingCore core = viking.GetComponent<VikingCore>();
			ZDO zdo = ((viking.m_nview != null && viking.m_nview.IsValid()) ? viking.m_nview.GetZDO() : null);
			bool cart = (zdo != null && RoadCarts.Has(zdo));
			party.Add(((core != null) ? core.BiomeIndex() : 0), Unteleportable(viking.GetInventory()) || (cart && RoadCarts.CarriesUnteleportable(viking)), first);
			party.Cart |= cart;
			LivingVikingBody body = viking as LivingVikingBody;
			float food = ((body != null && body.Food != null) ? body.Food.FoodStamina() : 0f);
			party.AddSwimmer(BoatRules.SwimMeters(food, viking.GetSkillFactor(Skills.SkillType.Swim), Game.m_moveStaminaRate, viking.m_swimSpeed), first);
		}

		/// <summary>
		/// Checks whether an inventory holds something a portal won't take, leaving out the world's own rules and the
		/// portal's, which the server weighs for each portal (see UsablePortals)
		/// </summary>
		/// <param name="inventory">The inventory, or null</param>
		/// <returns>True when it does</returns>
		public static bool Unteleportable(Inventory inventory)
		{
			if (inventory == null)
			{
				return(false);
			}
			foreach (ItemDrop.ItemData item in inventory.GetAllItems())
			{
				if (item != null && item.m_shared != null && !item.m_shared.m_teleportable)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a portal takes an item, by its prefab name, leaving out the world's own rules and the portal's
		/// </summary>
		/// <param name="item">The item's prefab name, or ""</param>
		/// <returns>True when it does, or when there's no item</returns>
		public static bool Teleportable(string item)
		{
			if (string.IsNullOrEmpty(item) || ObjectDB.instance == null)
			{
				return(true);
			}
			GameObject prefab = ObjectDB.instance.GetItemPrefab(item);
			ItemDrop drop = ((prefab != null) ? prefab.GetComponent<ItemDrop>() : null);
			return(drop == null || drop.m_itemData.m_shared.m_teleportable);
		}

		/// <summary>
		/// Asks the server for a village a party can go to - the answer comes back later, to the action given
		/// </summary>
		/// <param name="from">Where they are</param>
		/// <param name="party">What they can do on the road</param>
		/// <param name="excluded">The villages to leave out</param>
		/// <param name="done">What to do with the answer</param>
		/// <param name="goal">Where to look - the village they get to soonest, one at random, their home, or one by name</param>
		/// <param name="homeFire">For their home, the bonfire of the village they left</param>
		/// <param name="named">For one by name, the name, or part of it</param>
		public static void Ask(Vector3 from, TravelParty party, List<Vector3> excluded, System.Action<Answer> done, Goal goal = Goal.Soonest, Vector3 homeFire = default(Vector3), string named = "")
		{
			if (ZRoutedRpc.instance == null)
			{
				return;
			}
			long id = NewId();
			asked[id] = done;
			ZPackage pkg = new ZPackage();
			pkg.Write(id);
			pkg.Write(from);
			party.Write(pkg);
			pkg.Write(excluded.Count);
			foreach (Vector3 point in excluded)
			{
				pkg.Write(point);
			}
			pkg.Write((int)goal);
			pkg.Write(homeFire);
			pkg.Write(named ?? "");
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), SearchRpc, pkg);
		}

		/// <summary>
		/// Plans on the server the road to a village a party can go to - the answer comes back later, to the action given,
		/// on the server
		/// </summary>
		/// <param name="from">Where they are</param>
		/// <param name="party">What they can do on the road</param>
		/// <param name="excluded">The villages to leave out</param>
		/// <param name="label">Who they are, for the log</param>
		/// <param name="done">What to do with the answer</param>
		/// <param name="goal">Where to look - the village they get to soonest, one at random, or their home</param>
		/// <param name="homeFire">For their home, the bonfire of the village they left</param>
		public static void PlanHere(Vector3 from, TravelParty party, List<Vector3> excluded, string label, System.Action<Answer> done, Goal goal = Goal.Soonest, Vector3 homeFire = default(Vector3))
		{
			searches.Enqueue(new Search { From = from, Party = party, Excluded = new List<Vector3>(excluded), Label = label, Local = done, Goal = goal, HomeFire = homeFire });
		}

		/// <summary>
		/// Plans on the server the road to whichever of a list of places a party gets to soonest - the answer comes back
		/// later, to the action given, on the server
		/// </summary>
		/// <param name="from">Where they are</param>
		/// <param name="party">What they can do on the road</param>
		/// <param name="targets">The places</param>
		/// <param name="label">Who they are, for the log</param>
		/// <param name="done">What to do with the answer</param>
		public static void PlanTo(Vector3 from, TravelParty party, List<Vector3> targets, string label, System.Action<Answer> done)
		{
			searches.Enqueue(new Search { From = from, Party = party, Targets = new List<Vector3>(targets), Label = label, Local = done });
		}

		/// <summary>
		/// Finds the anchor of the village started at a bonfire, on the server
		/// </summary>
		/// <param name="fire">Where the bonfire stands</param>
		/// <param name="anchor">Set to the anchor's ZDO</param>
		/// <returns>True when a village was started there</returns>
		public static bool HomeAnchor(Vector3 fire, out ZDO anchor)
		{
			anchor = null;
			if (fire == Vector3.zero || ZDOMan.instance == null)
			{
				return(false);
			}
			anchor = AllWithPrefab(VillageFounding.PrefabName).Find((ZDO zdo) => Utils.DistanceXZ(zdo.GetPosition(), fire) <= AnchorMatch);
			return(anchor != null);
		}

		/// <summary>
		/// Makes an ID for a question to the server
		/// </summary>
		/// <returns>The ID</returns>
		private static long NewId()
		{
			return(((long)Random.Range(int.MinValue, int.MaxValue) << 32) ^ Random.Range(int.MinValue, int.MaxValue));
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
		/// Takes a client's search on the server, to run after those before it
		/// </summary>
		/// <param name="sender">The client</param>
		/// <param name="pkg">The search's ID, where the viking is, its party and the villages to leave out</param>
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
			search.Party = TravelParty.Read(pkg);
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				search.Excluded.Add(pkg.ReadVector3());
			}
			search.Goal = (Goal)pkg.ReadInt();
			search.HomeFire = pkg.ReadVector3();
			search.Named = pkg.ReadString();
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
			// A village picked at random that can't be reached is left out, and another picked
			if (done.Goal == Goal.Random && done.Route.Found < 0 && done.Candidates.Count > 0 && done.Tries < RandomTries)
			{
				done.Excluded.Add(done.Candidates[0].Position);
				done.Tries++;
				searches.Enqueue(done);
				return;
			}
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
			Plugin.Log.LogInfo("Travel: " + done.Label + " at " + done.From.ToString("F0") + " (" + done.Party.Describe() + ")" + (answer.Found ? " is going to " + ((done.Targets != null) ? "" : "the village at ") + answer.Destination.ToString("F0") + ((answer.Name.Length > 0) ? " (" + answer.Name + ")" : "") + ", " + answer.Length.ToString("F0") + "m away, " + answer.Seconds.ToString("F0") + "s by " + DescribeRoad(answer.Road) + ", the soonest of " + done.Candidates.Count : " found nowhere it can reach, of " + done.Candidates.Count));
			TestLog.Count("Travel", (answer.Found ? "roads planned" : "roads not found"));
			if (answer.Road.PortalCount() > 0)
			{
				TestLog.Count("Travel", "roads through portals");
			}
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
		/// Describes how a road is crossed, for the log
		/// </summary>
		/// <param name="road">The road</param>
		/// <returns>The description, like "road, sea and 2 portals"</returns>
		internal static string DescribeRoad(TravelRoad road)
		{
			bool foot = road.Legs.Contains(TravelLeg.Foot) && road.Count > 1;
			bool sea = road.Legs.Contains(TravelLeg.Sea);
			int portals = road.PortalCount();
			List<string> parts = new List<string>();
			if (foot || (!sea && portals == 0))
			{
				parts.Add("road");
			}
			if (sea)
			{
				parts.Add("sea");
			}
			if (portals > 0)
			{
				parts.Add(portals + ((portals == 1) ? " portal" : " portals"));
			}
			if (parts.Count == 1)
			{
				return(parts[0]);
			}
			return(string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[parts.Count - 1]);
		}

		/// <summary>
		/// Starts a search - the road search out from the party to every village in the world that's no harder than its
		/// easiest biome and isn't left out, or to the spots a quest could go to, crossing the sea in the best boat of its
		/// hardest biome and through the portals it can use
		/// </summary>
		/// <param name="search">The search</param>
		private static void Begin(Search search)
		{
			if (search.Targets != null)
			{
				search.Candidates = search.Targets.ConvertAll((Vector3 target) => new Candidate { Position = target, Name = "", Author = "", Biome = search.Party.Easiest });
			}
			else if (search.Goal == Goal.Home)
			{
				search.Candidates = new List<Candidate>();
				if (HomeAnchor(search.HomeFire, out ZDO anchor))
				{
					VillageNames.At(anchor, out string name, out string author);
					search.Candidates.Add(new Candidate { Position = anchor.GetPosition(), Name = (name ?? ""), Author = (author ?? ""), Biome = search.Party.Easiest });
				}
			}
			else if (search.Goal == Goal.Named)
			{
				search.Candidates = Candidates().FindAll((Candidate candidate) => candidate.Name.IndexOf(search.Named, System.StringComparison.OrdinalIgnoreCase) >= 0 && !search.Excluded.Exists((Vector3 excluded) => Utils.DistanceXZ(excluded, candidate.Position) <= FarmMatch));
			}
			else
			{
				List<Candidate> all = Candidates();
				search.Candidates = all.FindAll((Candidate candidate) => candidate.Biome <= search.Party.Easiest && !search.Excluded.Exists((Vector3 excluded) => Utils.DistanceXZ(excluded, candidate.Position) <= FarmMatch));
				if (search.Goal == Goal.Random && search.Candidates.Count > 0)
				{
					search.Candidates = new List<Candidate> { search.Candidates[Random.Range(0, search.Candidates.Count)] };
				}
			}
			List<Vector3> targets = search.Candidates.ConvertAll((Candidate candidate) => candidate.Position);
			int boat = search.Party.Hardest;
			search.Route = new TravelRoute(search.From, search.Party.Easiest, TravelTime.BoatSpeed(boat), BoatFireproof(boat), search.Party.SwimMeters, targets, Sample, UsablePortals(search.Party));
			search.Route.Cart = search.Party.Cart;
		}

		/// <summary>
		/// Checks whether the best boat of a biome holds up in the Ashlands' boiling sea - by its prefab, or by
		/// TravelTime's list when the prefab can't be read
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <returns>True when it does</returns>
		public static bool BoatFireproof(int biome)
		{
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(TravelTime.BoatPrefab(biome)) : null);
			Ship ship = ((prefab != null) ? prefab.GetComponent<Ship>() : null);
			return((ship != null) ? ship.m_ashlandsReady : TravelTime.BoatSurvivesAshlands(biome));
		}

		/// <summary>
		/// Lists the portals a party may use, one way each - every portal joined to another, none at all in a world with
		/// portals turned off, and for a party carrying something a portal won't take, only the portals that take
		/// everything, or every portal in a world that lets them
		/// </summary>
		/// <param name="party">The party</param>
		/// <returns>The portals</returns>
		public static List<TravelRoute.Portal> UsablePortals(TravelParty party)
		{
			List<TravelRoute.Portal> usable = new List<TravelRoute.Portal>();
			if (ZDOMan.instance == null || ZoneSystem.instance == null || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoPortals))
			{
				return(usable);
			}
			bool takesAll = ZoneSystem.instance.GetGlobalKey(GlobalKeys.TeleportAll);
			foreach (ZDO portal in ZDOMan.instance.GetPortalList())
			{
				ZDOID joined = portal.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal);
				ZDO other = (joined.IsNone() ? null : ZDOMan.instance.GetZDO(joined));
				if (other == null)
				{
					continue;
				}
				TeleportWorld kind = PortalKind(portal);
				if (party.Unteleportable && !takesAll && (kind == null || !kind.m_allowAllItems))
				{
					continue;
				}
				usable.Add(new TravelRoute.Portal { Entry = Front(portal, kind), Exit = Front(other, PortalKind(other)) });
			}
			return(usable);
		}

		/// <summary>
		/// How far from where a portal on a road was planned the portal can stand and still be it, in meters
		/// </summary>
		private const float PortalMatch = 3f;

		/// <summary>
		/// Finds the first stage of a trip whose road goes through a portal that's no longer there to use - broken, no
		/// longer joined to the other end, or one that won't take what they carry on that stage
		/// </summary>
		/// <param name="journey">The trip</param>
		/// <param name="any">The portals that can be used carrying anything a portal takes (see UsablePortals)</param>
		/// <param name="barred">The portals that can be used carrying something most portals won't take</param>
		/// <returns>The stage's index, or -1 when every portal on the trip is there</returns>
		internal static int BrokenStage(TravelJourney journey, List<TravelRoute.Portal> any, List<TravelRoute.Portal> barred)
		{
			for (int i = 0; i < journey.Stages.Count; i++)
			{
				TravelRoad road = journey.Stages[i].Road;
				if (road == null)
				{
					continue;
				}
				List<TravelRoute.Portal> usable = (journey.Stages[i].Unteleportable ? barred : any);
				for (int w = 1; w < road.Count; w++)
				{
					if (!road.PortalLeg(w))
					{
						continue;
					}
					Vector3 entry = road.Points[w - 1];
					Vector3 exit = road.Points[w];
					if (!usable.Exists((TravelRoute.Portal portal) => TravelRoad.Flat(portal.Entry, entry) <= PortalMatch && TravelRoad.Flat(portal.Exit, exit) <= PortalMatch))
					{
						return(i);
					}
				}
			}
			return(-1);
		}

		/// <summary>
		/// Gets the portal component of a portal's prefab
		/// </summary>
		/// <param name="portal">The portal's ZDO</param>
		/// <returns>The component, or null when its prefab can't be read</returns>
		private static TeleportWorld PortalKind(ZDO portal)
		{
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(portal.GetPrefab()) : null);
			return((prefab != null) ? prefab.GetComponent<TeleportWorld>() : null);
		}

		/// <summary>
		/// Finds the spot in front of a portal a player comes out at (see TeleportWorld.Teleport)
		/// </summary>
		/// <param name="portal">The portal's ZDO</param>
		/// <param name="kind">Its prefab's portal component, or null</param>
		/// <returns>The spot</returns>
		private static Vector3 Front(ZDO portal, TeleportWorld kind)
		{
			float distance = ((kind != null) ? kind.m_exitDistance : PortalFront);
			return(portal.GetPosition() + portal.GetRotation() * Vector3.forward * distance);
		}

		/// <summary>
		/// Reads a square of the world's map from its plan - the sea is crossed by boat, the Ashlands' deep water too, the
		/// Ashlands' lava and the water past WorldEdge can't be crossed at all, the sea out where the Ashlands' water
		/// boils burns a boat not made for it (see Ship.TakeAshlandsDamage), and a river, lake or water off a shore too
		/// deep to wade is swum, or sailed when it's deep enough for a boat (see SailDepth)
		/// </summary>
		/// <param name="x">The point's x</param>
		/// <param name="z">The point's z</param>
		/// <returns>The square</returns>
		internal static TravelRoute.Cell Sample(float x, float z)
		{
			if (x * x + z * z > WorldEdge * WorldEdge)
			{
				return(new TravelRoute.Cell { Open = false });
			}
			Heightmap.Biome biome = WorldGenerator.instance.GetBiome(x, z);
			TravelRoute.Cell cell = new TravelRoute.Cell { Open = true, Sea = (biome == Heightmap.Biome.Ocean), Biome = VikingGear.IndexOf(biome) };
			float water = ((ZoneSystem.instance != null) ? ZoneSystem.instance.m_waterLevel : DefaultWaterLevel);
			if (!cell.Sea && biome != Heightmap.Biome.AshLands)
			{
				float ground = WorldGenerator.instance.GetBiomeHeight(biome, x, z, out Color _);
				cell.Water = (ground < water - SwimDepth);
				cell.Deep = (ground < water - SailDepth);
			}
			if (biome == Heightmap.Biome.AshLands)
			{
				float height = WorldGenerator.instance.GetBiomeHeight(biome, x, z, out Color mask);
				if (height < water - AshlandsSeaDepth)
				{
					cell.Sea = true;
				}
				else
				{
					cell.Open = (mask.a <= LavaMask);
				}
			}
			if (cell.Sea)
			{
				cell.Burning = (WorldGenerator.GetAshlandsOceanGradient(x, z) > 0f);
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
		/// Asks the server how long a village's vikings take to get to where a quest goes (see QuestTargets) and back -
		/// the answer comes back later, to the action given
		/// </summary>
		/// <param name="from">Where the village stands</param>
		/// <param name="biome">The village's biome, by VikingGear's order</param>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <param name="done">What to do with the answer - the time there and the time back, in seconds, the biome the road
		/// goes to, by VikingGear's order, or -1 when it goes nowhere, and the sea it sets out across from the village (see
		/// BoatRules.QuestSea), with no waypoints when it goes on foot</param>
		public static void AskQuestRoute(Vector3 from, int biome, QuestKind kind, string item, System.Action<float, float, int, TravelRoad> done)
		{
			if (ZRoutedRpc.instance == null)
			{
				return;
			}
			long id = NewId();
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
		/// Works out on the server how long a village's vikings take to get to where a quest goes and back - the road to
		/// whichever place the quest could go they get to soonest, over land and sea in the best boat the village's biome
		/// has and through portals, and the road home again, without the portals that won't take what the quest brings
		/// back, like ore - or straight there on foot when there's no road, or QuestRules.DefaultTravel when there's
		/// nowhere to go - and answers the client that asked, with the sea the road sets out across from the village when
		/// it takes to the sea there (see BoatRules.QuestSea)
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
			List<Vector3> targets = QuestTargets.FindAll(kind, item, from, biome);
			if (targets.Count == 0)
			{
				Plugin.Log.LogInfo("Travel: a quest " + kind + " from the village at " + from.ToString("F0") + " has nowhere to go, so it takes " + QuestRules.DefaultTravel.ToString("F0") + "s each way");
				ZRoutedRpc.instance.InvokeRoutedRPC(sender, QuestRoutedRpc, id, QuestRules.DefaultTravel, QuestRules.DefaultTravel, -1, new byte[0]);
				return;
			}
			string label = "a quest " + kind + " from the village";
			Search there = new Search { From = from, Party = TravelParty.Of(biome), Targets = targets, Label = label };
			there.Local = (Answer outward) =>
			{
				Vector3 target = (outward.Found ? outward.Destination : targets[0]);
				float seconds = (outward.Found ? outward.Seconds : TravelTime.Seconds(Utils.DistanceXZ(from, target)));
				int reached = ((WorldGenerator.instance != null) ? VikingGear.IndexOf(WorldGenerator.instance.GetBiome(target)) : -1);
				TravelRoad seaOut = (outward.Found ? BoatRules.QuestSea(outward.Road, from) : null);
				byte[] sea = ((seaOut != null) ? seaOut.Encode() : new byte[0]);
				TravelParty home = TravelParty.Of(biome);
				home.Unteleportable = !Teleportable(item);
				if (!outward.Found || outward.Road.PortalCount() == 0 || !home.Unteleportable)
				{
					Plugin.Log.LogInfo("Travel: " + label + " at " + from.ToString("F0") + " goes to " + target.ToString("F0") + ", " + seconds.ToString("F0") + "s each way");
					ZRoutedRpc.instance.InvokeRoutedRPC(sender, QuestRoutedRpc, id, seconds, seconds, reached, sea);
					return;
				}
				Search back = new Search { From = target, Party = home, Targets = new List<Vector3> { from }, Label = label + " coming home with " + item };
				back.Local = (Answer homeward) =>
				{
					float returning = (homeward.Found ? homeward.Seconds : TravelTime.Seconds(Utils.DistanceXZ(from, target)));
					Plugin.Log.LogInfo("Travel: " + label + " at " + from.ToString("F0") + " goes to " + target.ToString("F0") + ", " + seconds.ToString("F0") + "s there and " + returning.ToString("F0") + "s back, as no portal on the way takes " + item);
					ZRoutedRpc.instance.InvokeRoutedRPC(sender, QuestRoutedRpc, id, seconds, returning, reached, sea);
				};
				searches.Enqueue(back);
			};
			searches.Enqueue(there);
		}

		/// <summary>
		/// Gives a client the server's answer to how long a quest's road takes
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="id">The question's ID</param>
		/// <param name="there">How long it takes there, in seconds</param>
		/// <param name="back">How long it takes back, in seconds</param>
		/// <param name="biome">The biome the road goes to, by VikingGear's order, or -1 when it goes nowhere</param>
		/// <param name="sea">The sea the road sets out across from the village, as written (see TravelRoad.Encode), or
		/// nothing when it goes on foot</param>
		private static void OnQuestRouted(long sender, long id, float there, float back, int biome, byte[] sea)
		{
			if (askedRoutes.TryGetValue(id, out System.Action<float, float, int, TravelRoad> done))
			{
				askedRoutes.Remove(id);
				done(there, back, biome, TravelRoad.Decode(sea));
			}
		}

		/// <summary>
		/// Describes the searches waiting on the server, for the test run's snapshot
		/// </summary>
		/// <returns>The description, or "" when there are none</returns>
		public static string DescribeSearches()
		{
			int waiting = searches.Count + ((running != null) ? 1 : 0);
			return((waiting > 0) ? waiting + " road searches waiting" + ((running != null) ? ", for " + running.Label + " at " + running.From.ToString("F0") + " running" : "") : "");
		}
	}
}
