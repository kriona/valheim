using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A boat vikings on the road built to cross the sea in the world - a real ship of the game's, which drops nothing
	/// when it breaks, sails itself along the sea legs of their road with a fair wind of its own, and has a viking at
	/// the helm and the rest of its crew on board, a seat each (see VillageLife's crossing)
	/// </summary>
	/// <remarks>
	/// Everything about the boat is kept in its ZDO, so it sails on whichever client owns it - the one that built it,
	/// or a player's who boards it - and carries on after the world is saved and loaded again. It waits while its crew
	/// boards, and once they're all on, waits for a player near to come aboard too - asking after AskAfter seconds and
	/// again every AskEvery, setting off DepartAfter seconds after they're all on, or SwimmerWait for a player swimming
	/// out to it, with a farewell to whoever it leaves behind, or at once with no player near. It
	/// steers for each waypoint in turn, backing off when it's stuck, and once it's near the shore its crew jumps off and
	/// swims to land, and one of them breaks it up - waiting for any player on board to get off, for up to BreakWait
	/// seconds. A player can ride along, but not take the helm (see VikingBoatPatches)
	/// </remarks>
	internal class VikingBoat : MonoBehaviour
	{
		/// <summary>
		/// What the boat is doing - waiting for its crew to board, for a player to come aboard, sailing, or letting its
		/// crew off at the far shore
		/// </summary>
		internal enum Phase
		{
			Boarding,
			Waiting,
			Sailing,
			Landing
		}

		/// <summary>
		/// On a boat, the group of vikings it carries and which of their boats it is, its crew's IDs, helm first, its road
		/// - from where it was launched across the sea legs of their road - the waypoint it's heading for, the spot ashore
		/// it makes for after the last, the index of the first sea leg and of the waypoint ashore in the vikings' road,
		/// what it's doing and since when, in ZNet time ticks, and when one of its crew asked for it to be broken up, or 0
		/// </summary>
		public static readonly int GroupKey = "kriona.VikingVillages.boatGroup".GetStableHashCode();
		public static readonly int IndexKey = "kriona.VikingVillages.boatIndex".GetStableHashCode();
		public static readonly int CrewKey = "kriona.VikingVillages.boatCrew".GetStableHashCode();
		public static readonly int RouteKey = "kriona.VikingVillages.boatRoute".GetStableHashCode();
		public static readonly int LegKey = "kriona.VikingVillages.boatLeg".GetStableHashCode();
		public static readonly int LandKey = "kriona.VikingVillages.boatLand".GetStableHashCode();
		public static readonly int SeaStartKey = "kriona.VikingVillages.boatSeaStart".GetStableHashCode();
		public static readonly int AshoreKey = "kriona.VikingVillages.boatAshore".GetStableHashCode();
		public static readonly int PhaseKey = "kriona.VikingVillages.boatPhase".GetStableHashCode();
		public static readonly int PhaseAtKey = "kriona.VikingVillages.boatPhaseAt".GetStableHashCode();
		public static readonly int BreakAtKey = "kriona.VikingVillages.boatBreakAt".GetStableHashCode();

		/// <summary>
		/// On a boat, that its crew leaves it for the village they came to rather than breaking it up (see DecideKept)
		/// </summary>
		public static readonly int KeptKey = "kriona.VikingVillages.boatKept".GetStableHashCode();

		/// <summary>
		/// How far past a village's edge the far shore can be for the village to count as on the shore, and the boat to
		/// be left for it, in meters - as far out as a village's own boat is moored (see VillageBoatSite)
		/// </summary>
		private const float ShoreVillageReach = 80f;

		/// <summary>
		/// On a boat a quest sails in (see VillageQuests.PlanBoats), the quest's ID, or "", whether it's sailing home from
		/// it rather than setting out, and whether it's one of the village's own, which goes back to being a ship like any
		/// other if the quest never sails in it
		/// </summary>
		public static readonly int QuestKey = "kriona.VikingVillages.boatQuest".GetStableHashCode();
		public static readonly int HomewardKey = "kriona.VikingVillages.boatHomeward".GetStableHashCode();
		public static readonly int VillageOwnedKey = "kriona.VikingVillages.boatVillageOwned".GetStableHashCode();

		/// <summary>
		/// How long a quest's boat waits at the shore for its crew to get packed and on board before it gives up on them,
		/// in seconds, and how far out a quest's boat has to be with nobody left on it to count as gone with the quest,
		/// in meters - any nearer, it's left at the shore
		/// </summary>
		private const float QuestBoardTime = QuestRules.LeaveTimeout + QuestRules.SailOffSeconds + BoardTime;
		private const float QuestGoneOut = 40f;

		/// <summary>
		/// On a living viking, the boat it's crossing the sea in, as its group and the boat's index, or "", and how far
		/// along its crossing it is (see VillageLife's Crossing)
		/// </summary>
		public static readonly int BoatKey = "kriona.VikingVillages.vikingBoat".GetStableHashCode();
		public static readonly int BoardKey = "kriona.VikingVillages.vikingBoard".GetStableHashCode();

		/// <summary>
		/// The values of BoardKey for a viking sitting on board, and for one that went overboard and is getting back on
		/// </summary>
		public const int Aboard = 5;
		public const int Overboard = 8;

		/// <summary>
		/// The longest the boat comes about for any one person overboard before it sails on without them, in seconds - so
		/// one it can't reach, past land or rocks, doesn't hold it at sea for good
		/// </summary>
		private const float RescueMost = 180f;

		/// <summary>
		/// The RPC a viking asks the boat's owner to break it up with
		/// </summary>
		private const string BreakRpc = "kriona_VikingVillages_BoatBreak";

		/// <summary>
		/// How long the crew has to board before those still off it are put on board, how long after they're all on the
		/// boat asks a player standing near to come aboard and sets off without them, how near a player has to be to be
		/// waited for, in meters, and how long the boat waits on a player standing on it before it's broken up anyway
		/// </summary>
		public const float BoardTime = 30f;
		private const float AskAfter = 5f;
		private const float DepartAfter = 20f;

		/// <summary>
		/// How often the boat asks a player near it to come aboard again while it waits, and how long it waits in all for
		/// one swimming out to it, in seconds - longer than for one standing ashore, who can choose to stay
		/// </summary>
		private const float AskEvery = 15f;
		private const float SwimmerWait = 90f;

		/// <summary>
		/// How long the boat waits once a player is on board and no other is still off it, before it sets off, in seconds -
		/// so a player who just climbed aboard finds their feet first
		/// </summary>
		private const float BoardedDelay = 2f;
		private const float PlayerReach = 40f;
		private const float BreakWait = 60f;

		/// <summary>
		/// How near a waypoint the boat has to come to make for the next, and how near the spot ashore to let its crew off,
		/// in meters, and how near the spot ashore it sails at half sail
		/// </summary>
		private const float WaypointReach = 20f;
		private const float LandReach = 30f;
		private const float SlowReach = 60f;

		/// <summary>
		/// For a boat coming in to the far shore within LandReach, the nearest it has got to the spot ashore and since
		/// when it has been no nearer, in Time.time seconds (see BoatRules.Beached)
		/// </summary>
		private float beachBest = float.MaxValue;
		private float beachSince = -1f;

		/// <summary>
		/// How the boat gets itself unstuck - every StuckCheck seconds, if it hasn't got StuckGain meters nearer where it's
		/// heading, it backs off for BackTime seconds, and gives up and lets its crew off where it is after StuckLimit tries
		/// </summary>
		private const float StuckCheck = 10f;
		private const float StuckGain = 3f;
		private const float BackTime = 4f;
		private const int StuckLimit = 4;

		/// <summary>
		/// How long a boat loaded mid-voyage holds still for its crew and the player to load onto it, in seconds, and how
		/// long a sailing boat goes with no crew on board before it lets them off
		/// </summary>
		private const float SettleTime = 10f;
		private const float CrewlessTime = 20f;

		/// <summary>
		/// How the seats on deck are laid out on a ship not of the game's, with no plan in BoatSeats - rows along it and
		/// columns across it, as shares of its length and breadth, and how near the helm a seat can be, in meters
		/// </summary>
		private static readonly float[] SeatRows = { 0.3f, 0.15f, 0f, -0.15f, -0.3f };
		private static readonly float[] SeatColumns = { -0.22f, 0.22f };
		private const float HelmClearance = 1.5f;

		/// <summary>
		/// On a boat, that it's moored for the kri_vv_spawnboat command, so it never sets off and its crew stays on board
		/// until it's broken
		/// </summary>
		public static readonly int MooredKey = "kriona.VikingVillages.boatMoored".GetStableHashCode();

		/// <summary>
		/// Every boat loaded on this client
		/// </summary>
		public static readonly List<VikingBoat> All = new List<VikingBoat>();

		/// <summary>
		/// The ship, its view, its seats - the helm's first - each with the pose it's sat in (see
		/// VikingPoses.SetShipPose), and its ladders
		/// </summary>
		public Ship Ship { get; private set; }
		private ZNetView nview;
		private readonly List<Transform> seats = new List<Transform>();
		private readonly List<string> seatPoses = new List<string>();
		private Ladder[] ladders;

		/// <summary>
		/// Until when the boat holds still after loading, in Time.time seconds, when it next asks a player near it to come
		/// aboard, and how it's getting unstuck - when it last looked, how far it was from where it's heading then, until
		/// when it backs off and how many times it has
		/// </summary>
		private float settleUntil;
		private float nextAsk;

		/// <summary>
		/// Since when the boat has had a player on board and none still off it near it, in Time.time seconds, or -1
		/// </summary>
		private float boardedSince = -1f;

		/// <summary>
		/// How the crew talks as the boat sails (see UpdateTalk) - how often it looks at what's around it, in seconds, how
		/// far off a flying monster and a sea serpent can be to count as after them, how near one comes to count however
		/// it's minded, and how far off a leviathan is in sight, in meters
		/// </summary>
		private const float TalkLookInterval = 1f;
		private const float FlyerReach = 40f;
		private const float FlyerNear = 15f;
		private const float SerpentReach = 60f;
		private const float SerpentNear = 30f;
		private const float LeviathanSight = 120f;

		/// <summary>
		/// When the crew next looks at what's around it and next makes small talk, when it last said anything, what was
		/// around it on its last look, and which of the crew spoke last, in Time.time seconds
		/// </summary>
		private float nextTalkLook;
		private float nextTalk = -1f;
		private float lastTalkAt = -100f;
		private BoatTalk.Topic seenTopic = BoatTalk.Topic.Calm;
		private Character lastSpeaker;
		private float stuckLookAt;
		private float stuckDistance = float.MaxValue;
		private float backUntil;
		private int stuckTimes;
		private float crewlessSince = -1f;

		/// <summary>
		/// On its owner, the players who've stood on the boat while it sailed, since one of them in the water is overboard,
		/// and since when it has come about for each person overboard, in Time.time seconds - one it gave up on staying
		/// in it, so it isn't come back for
		/// </summary>
		private readonly HashSet<ZDOID> riders = new HashSet<ZDOID>();
		private readonly Dictionary<ZDOID, float> rescues = new Dictionary<ZDOID, float>();

		/// <summary>
		/// Turns a ship into a vikings' boat, when its ZDO says it's one - as it loads on any client
		/// </summary>
		/// <param name="ship">The ship</param>
		public static void Take(Ship ship)
		{
			ZNetView view = ship.GetComponent<ZNetView>();
			if (view == null || !view.IsValid() || view.GetZDO().GetString(GroupKey, "").Length == 0 || ship.GetComponent<VikingBoat>() != null)
			{
				return;
			}
			ship.gameObject.AddComponent<VikingBoat>();
		}

		/// <summary>
		/// Finds the vikings' boat a ship is, if it's one
		/// </summary>
		/// <param name="ship">The ship</param>
		/// <returns>The boat, or null for a ship of the players'</returns>
		public static VikingBoat Of(Ship ship)
		{
			return((ship != null) ? ship.GetComponent<VikingBoat>() : null);
		}

		/// <summary>
		/// Builds a boat for vikings in the water, facing the way they'll sail, and notes its crew and road on it
		/// </summary>
		/// <param name="prefab">The ship's prefab</param>
		/// <param name="spot">Where it goes, on the water</param>
		/// <param name="facing">Which way it faces</param>
		/// <param name="group">The group of vikings it carries</param>
		/// <param name="index">Which of their boats it is</param>
		/// <param name="crew">Its crew's IDs, helm first</param>
		/// <param name="route">Its road across the sea</param>
		/// <param name="land">Where it lets its crew off, ashore</param>
		/// <param name="seaStart">The index of the first sea leg in the vikings' road</param>
		/// <param name="ashore">The index of the waypoint ashore in the vikings' road</param>
		/// <returns>The boat, or null when it couldn't be built</returns>
		public static VikingBoat Build(GameObject prefab, Vector3 spot, Vector3 facing, string group, int index, List<string> crew, TravelRoad route, Vector3 land, int seaStart, int ashore)
		{
			facing.y = 0f;
			Quaternion rotation = ((facing.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(facing) : Quaternion.identity);
			GameObject instance = Object.Instantiate(prefab, spot, rotation);
			ZNetView view = instance.GetComponent<ZNetView>();
			Ship ship = instance.GetComponent<Ship>();
			if (view == null || !view.IsValid() || ship == null)
			{
				return(null);
			}
			ZDO zdo = view.GetZDO();
			zdo.Set(GroupKey, group);
			zdo.Set(IndexKey, index);
			zdo.Set(CrewKey, string.Join(",", crew));
			zdo.Set(RouteKey, route.Encode());
			zdo.Set(LegKey, 1);
			zdo.Set(LandKey, land);
			zdo.Set(SeaStartKey, seaStart);
			zdo.Set(AshoreKey, ashore);
			zdo.Set(PhaseKey, (int)Phase.Boarding);
			zdo.Set(PhaseAtKey, NetTime.Now());
			Piece piece = instance.GetComponent<Piece>();
			if (piece != null)
			{
				piece.m_placeEffect.Create(spot, rotation, instance.transform);
			}
			Take(ship);
			return(instance.GetComponent<VikingBoat>());
		}

		/// <summary>
		/// Takes over one of a village's own boats for a quest that sails from the village (see VillageQuests.PlanBoats)
		/// - noting its crew and road on it as Build does a new boat's, for this client to sail, and that it's the
		/// village's, so it's left as it was if the quest never sails in it
		/// </summary>
		/// <param name="ship">The ship</param>
		/// <param name="group">The quest's boats' group (see QuestRecord.BoatGroup)</param>
		/// <param name="index">Which of the quest's boats it is</param>
		/// <param name="crew">Its crew's IDs, helm first</param>
		/// <param name="route">Its road across the sea, from where it's moored</param>
		/// <param name="land">The shore past the sea</param>
		/// <param name="quest">The quest's ID</param>
		/// <returns>The boat, or null when the ship couldn't be taken over</returns>
		public static VikingBoat Enlist(Ship ship, string group, int index, List<string> crew, TravelRoad route, Vector3 land, string quest)
		{
			ZNetView view = ship.GetComponent<ZNetView>();
			if (view == null || !view.IsValid())
			{
				return(null);
			}
			if (!view.IsOwner())
			{
				view.ClaimOwnership();
			}
			ZDO zdo = view.GetZDO();
			zdo.Set(GroupKey, group);
			zdo.Set(IndexKey, index);
			zdo.Set(CrewKey, string.Join(",", crew));
			zdo.Set(RouteKey, route.Encode());
			zdo.Set(LegKey, 1);
			zdo.Set(LandKey, land);
			zdo.Set(SeaStartKey, 0);
			zdo.Set(AshoreKey, 0);
			zdo.Set(PhaseKey, (int)Phase.Boarding);
			zdo.Set(PhaseAtKey, NetTime.Now());
			zdo.Set(QuestKey, quest);
			zdo.Set(HomewardKey, false);
			zdo.Set(VillageOwnedKey, true);
			Take(ship);
			return(Of(ship));
		}

		/// <summary>
		/// Notes on a boat just built that a quest sails in it (see QuestKey)
		/// </summary>
		/// <param name="quest">The quest's ID</param>
		/// <param name="homeward">Whether it sails home from the quest</param>
		/// <param name="owned">Whether it's one of the village's own</param>
		public void MarkQuest(string quest, bool homeward, bool owned)
		{
			if (!Valid())
			{
				return;
			}
			ZDO zdo = nview.GetZDO();
			zdo.Set(QuestKey, quest);
			zdo.Set(HomewardKey, homeward);
			zdo.Set(VillageOwnedKey, owned);
		}

		/// <summary>
		/// Sets a boat just built for a quest's vikings coming home under way, its crew already on board, holding still a
		/// moment for them to load onto it
		/// </summary>
		public void StartHomeward()
		{
			if (!Valid())
			{
				return;
			}
			SetPhase(Phase.Sailing);
			settleUntil = Time.time + SettleTime;
		}

		/// <summary>
		/// Checks whether a quest sails in the boat, setting out from its village or coming home (see QuestKey)
		/// </summary>
		/// <returns>True when it does</returns>
		public bool OnQuest()
		{
			return(Valid() && nview.GetZDO().GetString(QuestKey, "").Length > 0);
		}

		/// <summary>
		/// Checks whether the boat sails home from a quest, rather than setting out on it
		/// </summary>
		/// <returns>True when it does</returns>
		public bool Homeward()
		{
			return(Valid() && nview.GetZDO().GetBool(HomewardKey));
		}

		/// <summary>
		/// Checks whether the boat is one of a village's own, rather than one its vikings built
		/// </summary>
		/// <returns>True when it is</returns>
		public bool VillageOwned()
		{
			return(Valid() && nview.GetZDO().GetBool(VillageOwnedKey));
		}

		/// <summary>
		/// Gets where the boat set sail from, the start of its road
		/// </summary>
		public Vector3 Launch
		{
			get
			{
				TravelRoad route = (Valid() ? TravelRoad.Decode(nview.GetZDO().GetByteArray(RouteKey)) : new TravelRoad());
				return((route.Count > 0) ? route.Points[0] : transform.position);
			}
		}

		/// <summary>
		/// Sets the boat up as it loads - its seats and ladders - holding a boat loaded mid-voyage still for a while
		/// </summary>
		private void Awake()
		{
			Ship = GetComponent<Ship>();
			nview = GetComponent<ZNetView>();
			ladders = GetComponentsInChildren<Ladder>(true);
			MakeSeats();
			All.Add(this);
			if (nview != null && nview.IsValid())
			{
				nview.Register(BreakRpc, RPC_Break);
				if (CurrentPhase == Phase.Sailing)
				{
					settleUntil = Time.time + SettleTime;
				}
			}
		}

		/// <summary>
		/// Forgets the boat as it unloads or breaks
		/// </summary>
		private void OnDestroy()
		{
			All.Remove(this);
		}

		/// <summary>
		/// Lays out the boat's seats by its plan (see BoatSeats) - the helm's first - each a point a viking sits at that
		/// moves with the boat, and the pose it sits in there. A ship with no plan has its helm, its chairs and spots along
		/// its deck
		/// </summary>
		private void MakeSeats()
		{
			ShipControlls controls = ((Ship != null) ? Ship.m_shipControlls : null);
			Transform helm = ((controls != null) ? controls.m_attachPoint : null);
			AddSeat(((helm != null) ? helm : transform), ((controls != null) ? controls.m_attachAnimation : VikingPoses.FloorPose));
			Chair[] chairs = GetComponentsInChildren<Chair>(true);
			BoatSeats.Plan[] plan = BoatSeats.For(Utils.GetPrefabName(gameObject));
			if (plan == null)
			{
				MakeOtherSeats(helm, chairs);
				return;
			}
			foreach (BoatSeats.Plan seat in plan)
			{
				if (seat.Chair == BoatSeats.Helm)
				{
					continue;
				}
				if (!seat.OnDeck)
				{
					Chair chair = System.Array.Find(chairs, (Chair found) => found.gameObject.name == seat.Chair && found.m_attachPoint != null);
					if (chair != null && seat.Beside)
					{
						AddBesideSeat(chair, seat.X);
						continue;
					}
					if (chair != null)
					{
						AddSeat(chair.m_attachPoint, chair.m_attachAnimation);
						continue;
					}
					Plugin.Log.LogWarning("Boat: the " + Utils.GetPrefabName(gameObject) + " has no chair called " + seat.Chair + " to seat its crew on");
					continue;
				}
				Vector3 guess = transform.TransformPoint(new Vector3(seat.X, seat.Y, seat.Z));
				AddDeckSeat(DeckBelow(transform.TransformPoint(new Vector3(seat.X, seat.Y + BoatSeats.ProbeAbove, seat.Z)), out Vector3 onDeck) ? onDeck : guess);
			}
		}

		/// <summary>
		/// Lays out the seats of a ship not of the game's - its active chairs but its figurehead's, then spots along its
		/// deck - after its helm
		/// </summary>
		/// <param name="helm">Where its helm is sat at, or null</param>
		/// <param name="chairs">Its chairs</param>
		private void MakeOtherSeats(Transform helm, Chair[] chairs)
		{
			foreach (Chair chair in chairs)
			{
				if (chair.m_attachPoint != null && chair.m_attachPoint != helm && chair.gameObject.activeInHierarchy && chair.m_attachAnimation != "attach_dragon")
				{
					AddSeat(chair.m_attachPoint, chair.m_attachAnimation);
				}
			}
			BoxCollider hull = ((Ship != null) ? Ship.m_floatCollider : null);
			if (hull == null)
			{
				return;
			}
			foreach (float row in SeatRows)
			{
				foreach (float column in SeatColumns)
				{
					Vector3 local = hull.center + new Vector3(hull.size.x * column, 0f, hull.size.z * row);
					Vector3 above = hull.transform.TransformPoint(local) + Vector3.up * 5f;
					if (DeckBelow(above, out Vector3 deck) && (helm == null || Vector3.Distance(deck, helm.position) >= HelmClearance))
					{
						AddDeckSeat(deck);
					}
				}
			}
		}

		/// <summary>
		/// Adds a seat
		/// </summary>
		/// <param name="point">The point a viking sits at</param>
		/// <param name="pose">The pose it sits in there (see VikingPoses.SetShipPose)</param>
		private void AddSeat(Transform point, string pose)
		{
			seats.Add(point);
			seatPoses.Add(pose);
		}

		/// <summary>
		/// Adds a seat in line with one of the ship's chairs, moved across the ship, sat in the same way and facing the
		/// same way
		/// </summary>
		/// <param name="chair">The chair</param>
		/// <param name="x">How far across the ship from its middle the seat is, in meters, to the right</param>
		private void AddBesideSeat(Chair chair, float x)
		{
			Vector3 local = transform.InverseTransformPoint(chair.m_attachPoint.position);
			local.x = x;
			GameObject seat = new GameObject("vikingSeat");
			seat.transform.SetParent(transform, false);
			seat.transform.position = transform.TransformPoint(local);
			seat.transform.rotation = chair.m_attachPoint.rotation;
			AddSeat(seat.transform, chair.m_attachAnimation);
		}

		/// <summary>
		/// Adds a spot on the deck to sit on the boards at, facing the bow
		/// </summary>
		/// <param name="deck">The deck's surface there</param>
		private void AddDeckSeat(Vector3 deck)
		{
			GameObject seat = new GameObject("vikingSeat");
			seat.transform.SetParent(transform, false);
			seat.transform.position = deck;
			seat.transform.rotation = Quaternion.LookRotation(transform.forward);
			AddSeat(seat.transform, VikingPoses.FloorPose);
		}

		/// <summary>
		/// How much higher or lower than a seat's own deck a spot on the boat can be for a viking sitting there to be pushed
		/// onto it - more is a rail, a step or the hull's edge - in meters, and how wide and how high above the deck the room
		/// a viking sitting there takes up is, in meters
		/// </summary>
		private const float PushStep = 0.3f;
		private const float SitRoom = 0.3f;
		private const float SitRoomHeight = 0.6f;

		/// <summary>
		/// Checks whether a viking sitting on the boat's deck can be pushed onto a spot - on the same deck as its seat,
		/// with nothing of the boat's there, like the mast, its chest, its cargo or a stool
		/// </summary>
		/// <param name="local">The spot, in the boat's space</param>
		/// <param name="deckHeight">The height of the seat's deck, in the boat's space</param>
		/// <param name="height">Set to the deck's height at the spot, in the boat's space</param>
		/// <returns>True when it can</returns>
		public bool Roomy(Vector3 local, float deckHeight, out float height)
		{
			height = deckHeight;
			Vector3 above = transform.TransformPoint(new Vector3(local.x, deckHeight + BoatSeats.ProbeAbove, local.z));
			if (!DeckBelow(above, out Vector3 deck))
			{
				return(false);
			}
			height = transform.InverseTransformPoint(deck).y;
			if (Mathf.Abs(height - deckHeight) > PushStep)
			{
				return(false);
			}
			foreach (Collider collider in Physics.OverlapSphere(deck + transform.up * SitRoomHeight, SitRoom))
			{
				if (collider != null && !collider.isTrigger && collider.transform.IsChildOf(transform))
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Finds the boat's deck below a point
		/// </summary>
		/// <param name="above">The point, above the boat</param>
		/// <param name="deck">Set to the deck's surface</param>
		/// <returns>False when the boat isn't below it</returns>
		private bool DeckBelow(Vector3 above, out Vector3 deck)
		{
			deck = Vector3.zero;
			float nearest = float.MaxValue;
			foreach (RaycastHit hit in Physics.RaycastAll(above, Vector3.down, 10f))
			{
				if (hit.collider == null || hit.collider.isTrigger || !hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest)
				{
					continue;
				}
				nearest = hit.distance;
				deck = hit.point;
			}
			return(nearest < float.MaxValue);
		}

		/// <summary>
		/// The boat's group of vikings, its key as a viking notes it, its crew's IDs, what it's doing and for how long, in
		/// seconds
		/// </summary>
		public string Group
		{
			get
			{
				return(Valid() ? nview.GetZDO().GetString(GroupKey, "") : "");
			}
		}
		public string Key
		{
			get
			{
				return(Valid() ? KeyOf(Group, nview.GetZDO().GetInt(IndexKey)) : "");
			}
		}
		public List<string> Crew
		{
			get
			{
				string crew = (Valid() ? nview.GetZDO().GetString(CrewKey, "") : "");
				return((crew.Length > 0) ? new List<string>(crew.Split(',')) : new List<string>());
			}
		}
		public Phase CurrentPhase
		{
			get
			{
				return(Valid() ? (Phase)nview.GetZDO().GetInt(PhaseKey) : Phase.Landing);
			}
		}
		public float PhaseSeconds
		{
			get
			{
				return(Valid() ? (NetTime.Now() - nview.GetZDO().GetLong(PhaseAtKey, 0L)) / (float)NetTime.Ticks(1f) : 0f);
			}
		}

		/// <summary>
		/// Where the boat lets its crew off, ashore, the index of its first sea leg and of the waypoint ashore in its
		/// vikings' road, and the waypoint of its own road it's heading for
		/// </summary>
		public Vector3 Land
		{
			get
			{
				return(Valid() ? nview.GetZDO().GetVec3(LandKey, transform.position) : transform.position);
			}
		}
		public int SeaStart
		{
			get
			{
				return(Valid() ? nview.GetZDO().GetInt(SeaStartKey) : 0);
			}
		}
		public int Ashore
		{
			get
			{
				return(Valid() ? nview.GetZDO().GetInt(AshoreKey) : 0);
			}
		}
		public int Leg
		{
			get
			{
				return(Valid() ? nview.GetZDO().GetInt(LegKey, 1) : 1);
			}
		}

		/// <summary>
		/// Makes the key a viking notes the boat it's crossing in by
		/// </summary>
		/// <param name="group">The boat's group of vikings</param>
		/// <param name="index">Which of their boats it is</param>
		/// <returns>The key</returns>
		public static string KeyOf(string group, int index)
		{
			return(group + "#" + index);
		}

		/// <summary>
		/// Finds the boat a viking crosses in, among those loaded, and its seat on it
		/// </summary>
		/// <param name="group">The viking's group</param>
		/// <param name="id">The viking's ID (see VikingIdentity)</param>
		/// <param name="seat">Set to the index of its seat, the helm being 0</param>
		/// <returns>The boat, or null when none loaded has it on its crew</returns>
		public static VikingBoat FindFor(string group, string id, out int seat)
		{
			seat = -1;
			foreach (VikingBoat boat in All)
			{
				if (boat == null || boat.Group != group)
				{
					continue;
				}
				seat = boat.Crew.IndexOf(id);
				if (seat >= 0)
				{
					return(boat);
				}
			}
			return(null);
		}

		/// <summary>
		/// Gets a seat of the boat, the helm's for seat 0 - those past the boat's last seat share its last
		/// </summary>
		/// <param name="index">The seat's index</param>
		/// <returns>The point a viking sits at</returns>
		public Transform Seat(int index)
		{
			return(seats[Mathf.Clamp(index, 0, seats.Count - 1)]);
		}

		/// <summary>
		/// Gets the pose a seat of the boat is sat in (see VikingPoses.SetShipPose)
		/// </summary>
		/// <param name="index">The seat's index</param>
		/// <returns>The pose</returns>
		public string SeatPose(int index)
		{
			return(seatPoses[Mathf.Clamp(index, 0, seatPoses.Count - 1)]);
		}

		/// <summary>
		/// Counts the boat's seats
		/// </summary>
		public int SeatCount
		{
			get
			{
				return(seats.Count);
			}
		}

		/// <summary>
		/// Notes the boat's crew, helm first, on its owner
		/// </summary>
		/// <param name="crew">The crew's IDs</param>
		public void SetCrew(List<string> crew)
		{
			if (Valid())
			{
				nview.GetZDO().Set(CrewKey, string.Join(",", crew));
			}
		}

		/// <summary>
		/// Moors the boat for the kri_vv_spawnboat command, so it never sets off, on its owner
		/// </summary>
		public void Moor()
		{
			if (Valid())
			{
				nview.GetZDO().Set(MooredKey, true);
			}
		}

		/// <summary>
		/// Checks whether the boat is moored for the kri_vv_spawnboat command
		/// </summary>
		/// <returns>True when it is</returns>
		public bool Moored()
		{
			return(Valid() && nview.GetZDO().GetBool(MooredKey));
		}

		/// <summary>
		/// Finds where a viking in the water climbs aboard - the ladder nearest it, or the boat's middle
		/// </summary>
		/// <param name="from">Where the viking is</param>
		/// <returns>The point</returns>
		public Vector3 Ladder(Vector3 from)
		{
			Vector3 best = transform.position;
			float nearest = float.MaxValue;
			foreach (Ladder ladder in ladders)
			{
				if (ladder != null && Vector3.Distance(ladder.transform.position, from) < nearest)
				{
					nearest = Vector3.Distance(ladder.transform.position, from);
					best = ladder.transform.position;
				}
			}
			return(best);
		}

		/// <summary>
		/// Checks whether the boat sails on its own - setting off, under way, with no player needed on board
		/// </summary>
		/// <returns>True when it does</returns>
		public bool Sails()
		{
			return(CurrentPhase == Phase.Sailing && Time.time >= settleUntil);
		}

		/// <summary>
		/// Checks whether the boat's view is there
		/// </summary>
		/// <returns>True when it is</returns>
		private bool Valid()
		{
			return(nview != null && nview.IsValid());
		}

		/// <summary>
		/// Moves the boat on to what it does next, on its owner
		/// </summary>
		/// <param name="phase">What it does next</param>
		private void SetPhase(Phase phase)
		{
			nview.GetZDO().Set(PhaseKey, (int)phase);
			nview.GetZDO().Set(PhaseAtKey, NetTime.Now());
			Plugin.Log.LogInfo("Boat: the " + Utils.GetPrefabName(gameObject) + " of " + Key + " at " + transform.position.ToString("F0") + " is now " + phase.ToString().ToLowerInvariant() + ", with " + CrewAboard() + " of its " + Crew.Count + " crew on board");
			TestLog.Count("Boats", phase.ToString().ToLowerInvariant());
		}

		/// <summary>
		/// Sails the boat on the client that owns it - waiting for its crew and any player near, steering along its road,
		/// and letting its crew off and breaking up at the far shore
		/// </summary>
		private void Update()
		{
			if (!Valid() || !nview.IsOwner() || Ship == null || (Game.instance != null && Game.instance.IsShuttingDown()))
			{
				return;
			}
			if (Moored())
			{
				Hold();
				return;
			}
			float dt = Time.deltaTime;
			switch (CurrentPhase)
			{
				case Phase.Boarding:
					Hold();
					// A quest's boat waits for its crew to pack, and sets off without asking a player along, since its crew
					// is soon gone from the world (see UpdateQuestOutbound)
					if (CrewAboard() >= Crew.Count || PhaseSeconds > (OnQuest() ? QuestBoardTime : BoardTime + 10f))
					{
						nextAsk = 0f;
						SetPhase(OnQuest() ? Phase.Sailing : Phase.Waiting);
					}
					break;
				case Phase.Waiting:
					Hold();
					UpdateWaiting();
					break;
				case Phase.Sailing:
					UpdateSailing(dt);
					break;
				case Phase.Landing:
					Hold();
					UpdateLanding();
					break;
			}
		}

		/// <summary>
		/// Keeps the boat where it is - sail down and rudder straight
		/// </summary>
		private void Hold()
		{
			Ship.m_speed = Ship.Speed.Stop;
			Ship.m_rudderValue = Mathf.MoveTowards(Ship.m_rudderValue, 0f, Time.deltaTime);
		}

		/// <summary>
		/// Waits for a player near the boat to come aboard - asking them after AskAfter seconds and again every AskEvery
		/// seconds, and setting off DepartAfter seconds after the crew got on, or SwimmerWait for a player swimming out to
		/// it, with the one at the helm calling out a farewell to whoever it leaves behind - or once no player near is
		/// still off it, BoardedDelay seconds after that with a player on board, and at once with none
		/// </summary>
		private void UpdateWaiting()
		{
			Player waiting = PlayerAshore();
			if (waiting == null)
			{
				if (Ship.m_players.Count > 0)
				{
					boardedSince = ((boardedSince < 0f) ? Time.time : boardedSince);
					if (Time.time - boardedSince < BoardedDelay)
					{
						return;
					}
				}
				boardedSince = -1f;
				SetPhase(Phase.Sailing);
				return;
			}
			boardedSince = -1f;
			Character helm = CrewMember(0);
			if (PhaseSeconds >= (waiting.IsSwimming() ? SwimmerWait : DepartAfter))
			{
				if (helm != null)
				{
					VikingSpeech.SayFirst(helm, VikingRoadSpeech.SailAway(waiting.GetPlayerName()), VikingSpeech.CallTime, VikingSpeech.CallCull);
				}
				Plugin.Log.LogInfo("Boat: " + Key + " sails without " + waiting.GetPlayerName() + ", " + (waiting.IsSwimming() ? "still swimming" : "still ashore") + " " + Vector3.Distance(waiting.transform.position, transform.position).ToString("F0") + "m away");
				TestLog.Count("Boats", "sailed without a player");
				SetPhase(Phase.Sailing);
				return;
			}
			if (PhaseSeconds >= AskAfter && Time.time >= nextAsk)
			{
				nextAsk = Time.time + AskEvery;
				if (helm != null)
				{
					VikingSpeech.Say(helm, VikingRoadSpeech.Coming(waiting.GetPlayerName()), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				}
			}
		}

		/// <summary>
		/// Finds a player near the boat who isn't on it - one swimming out to it first, since the boat waits longer for
		/// them (see UpdateWaiting)
		/// </summary>
		/// <returns>The player, or null when there's none</returns>
		private Player PlayerAshore()
		{
			Player found = null;
			foreach (Player player in Player.GetAllPlayers())
			{
				if (player == null || player.IsDead() || Vector3.Distance(player.transform.position, transform.position) > PlayerReach || Ship.IsPlayerInBoat(player))
				{
					continue;
				}
				if (player.IsSwimming())
				{
					return(player);
				}
				found = (found ?? player);
			}
			return(found);
		}

		/// <summary>
		/// Steers the boat along its road, a waypoint at a time, then for the spot ashore - backing off when it's stuck,
		/// and letting its crew off once it's there, or once it has been stuck StuckLimit times, or has had no crew on
		/// board for CrewlessTime seconds. While someone who was on it is overboard, it comes about for them instead (see
		/// ComeAbout), and sails on once they're back on board, ashore or dead
		/// </summary>
		/// <param name="dt">Seconds since the last update</param>
		private void UpdateSailing(float dt)
		{
			if (Time.time < settleUntil)
			{
				Hold();
				Rigidbody body = Ship.m_body;
				if (body != null)
				{
					body.linearVelocity = Vector3.zero;
					body.angularVelocity = Vector3.zero;
				}
				return;
			}
			foreach (Player player in Ship.m_players)
			{
				if (player != null)
				{
					riders.Add(player.GetZDOID());
				}
			}
			Character overboard = FindOverboard();
			if (overboard != null)
			{
				crewlessSince = -1f;
				ComeAbout(dt, overboard);
				return;
			}
			if (UpdateQuestOutbound())
			{
				return;
			}
			if (CrewAboard() == 0)
			{
				crewlessSince = ((crewlessSince < 0f) ? Time.time : crewlessSince);
				if (Time.time - crewlessSince > CrewlessTime)
				{
					SetPhase(Phase.Landing);
					return;
				}
			}
			else
			{
				crewlessSince = -1f;
			}
			TravelRoad route = TravelRoad.Decode(nview.GetZDO().GetByteArray(RouteKey));
			int leg = Leg;
			Vector3 target = ((leg < route.Count) ? route.Points[leg] : Land);
			float distance = Utils.DistanceXZ(transform.position, target);
			if (leg < route.Count && distance < WaypointReach)
			{
				nview.GetZDO().Set(LegKey, leg + 1);
				stuckDistance = float.MaxValue;
				return;
			}
			if (leg >= route.Count && distance < LandReach)
			{
				if (ComeIn(dt, distance, target))
				{
					DecideKept();
					LandHo();
					SetPhase(Phase.Landing);
				}
				return;
			}
			UpdateTalk();
			if (Time.time >= stuckLookAt)
			{
				stuckLookAt = Time.time + StuckCheck;
				if (distance > stuckDistance - StuckGain)
				{
					stuckTimes++;
					backUntil = Time.time + BackTime;
					if (stuckTimes >= StuckLimit)
					{
						Plugin.Log.LogInfo("Boat: " + Key + " is stuck at " + transform.position.ToString("F0") + ", and lets its crew off there");
						SetPhase(Phase.Landing);
						return;
					}
				}
				stuckDistance = distance;
			}
			float rudder = BoatRules.Rudder(transform.forward, target - transform.position);
			if (Time.time < backUntil)
			{
				Ship.m_speed = Ship.Speed.Back;
				rudder = -rudder;
			}
			else
			{
				Ship.m_speed = ((leg >= route.Count && distance < SlowReach) ? Ship.Speed.Half : Ship.Speed.Full);
			}
			Ship.m_rudderValue = Mathf.MoveTowards(Ship.m_rudderValue, rudder, Ship.m_rudderSpeed * dt * 2f);
		}

		/// <summary>
		/// Finds the nearest person overboard the boat comes about for - one of its crew getting back on (see
		/// VillageLife.SwimBack), or a player who stood on it while it sailed and is swimming now - while they're alive
		/// and for up to RescueMost seconds each. A player out of the water, ashore or back on board, is no longer
		/// overboard, and one who died or left is forgotten
		/// </summary>
		/// <returns>The person, or null when no one is overboard</returns>
		private Character FindOverboard()
		{
			Character nearest = null;
			float best = float.MaxValue;
			overboardNow.Clear();
			string key = Key;
			foreach (Character character in Character.GetAllCharacters())
			{
				ZDO zdo = ((character != null && character.m_nview != null && character.m_nview.IsValid()) ? character.m_nview.GetZDO() : null);
				if (zdo != null && !character.IsDead() && zdo.GetInt(BoardKey) == Overboard && zdo.GetString(BoatKey, "") == key)
				{
					Consider(character, ref nearest, ref best);
				}
			}
			List<ZDOID> gone = new List<ZDOID>();
			foreach (ZDOID id in riders)
			{
				Player player = Player.GetAllPlayers().Find((Player found) => found != null && found.GetZDOID() == id);
				if (player == null || player.IsDead())
				{
					gone.Add(id);
					continue;
				}
				if (!Ship.IsPlayerInBoat(player) && player.IsSwimming())
				{
					Consider(player, ref nearest, ref best);
				}
				else if (!Ship.IsPlayerInBoat(player))
				{
					// Ashore, or wherever else they went, they've left the boat
					gone.Add(id);
				}
			}
			foreach (ZDOID id in gone)
			{
				riders.Remove(id);
			}
			// One back on board, ashore or dead is no longer come about for, and is come back for if they go over again
			List<ZDOID> done = new List<ZDOID>();
			foreach (ZDOID id in rescues.Keys)
			{
				if (!overboardNow.Contains(id))
				{
					done.Add(id);
				}
			}
			foreach (ZDOID id in done)
			{
				rescues.Remove(id);
			}
			return(nearest);
		}

		/// <summary>
		/// Everyone FindOverboard found overboard on its last look, whether or not the boat still comes about for them
		/// </summary>
		private readonly HashSet<ZDOID> overboardNow = new HashSet<ZDOID>();

		/// <summary>
		/// Takes someone overboard as the one the boat comes about for when they're the nearest so far and it hasn't given
		/// up on them
		/// </summary>
		/// <param name="who">Who's overboard</param>
		/// <param name="nearest">The nearest so far, changed to them when they're nearer</param>
		/// <param name="best">How far the nearest is, in meters, changed along with it</param>
		private void Consider(Character who, ref Character nearest, ref float best)
		{
			ZDOID id = who.GetZDOID();
			overboardNow.Add(id);
			if (rescues.TryGetValue(id, out float since) && Time.time - since > RescueMost)
			{
				if (since >= 0f)
				{
					// Noted as given up on, so it's said once
					rescues[id] = float.NegativeInfinity;
					Plugin.Log.LogInfo("Boat: " + Key + " gives up coming about for " + TestLog.Name(who) + " after " + RescueMost.ToString("F0") + "s, and sails on");
					TestLog.Count("Boats", "gave up on someone overboard");
				}
				return;
			}
			float distance = Utils.DistanceXZ(transform.position, who.transform.position);
			if (distance < best)
			{
				best = distance;
				nearest = who;
			}
		}

		/// <summary>
		/// Comes about for someone overboard - the viking at the helm calling out their name the first time, the boat
		/// turning toward them at half sail, slowing as it closes in and stopping beside them (see BoatRules.RescueSpeed),
		/// so they can swim to its ladder - without counting any of it as being stuck. It gives up on them after
		/// RescueMost seconds (see Consider)
		/// </summary>
		/// <param name="dt">Seconds since the last update</param>
		/// <param name="who">Who's overboard</param>
		private void ComeAbout(float dt, Character who)
		{
			ZDOID id = who.GetZDOID();
			if (!rescues.ContainsKey(id))
			{
				rescues[id] = Time.time;
				string name = ((who is Player player) ? player.GetPlayerName() : VikingNames.Get(who));
				Character caller = SpeakerAboard();
				if (caller != null)
				{
					VikingSpeech.SayFirst(caller, VikingRoadSpeech.Overboard(name), VikingSpeech.CallTime, VikingSpeech.CallCull);
				}
				Plugin.Log.LogInfo("Boat: " + Key + " at " + transform.position.ToString("F0") + " comes about for " + TestLog.Name(who) + ", overboard " + Utils.DistanceXZ(transform.position, who.transform.position).ToString("F0") + "m away");
				TestLog.Count("Boats", "came about for someone overboard");
			}
			// Coming about isn't being stuck, so the road and coming in to shore pick up afresh after
			stuckLookAt = Time.time + StuckCheck;
			stuckDistance = float.MaxValue;
			backUntil = 0f;
			beachBest = float.MaxValue;
			beachSince = -1f;
			float distance = Utils.DistanceXZ(transform.position, who.transform.position);
			Ship.Speed speed = BoatRules.RescueSpeed(distance);
			if (speed == Ship.Speed.Stop)
			{
				Hold();
				return;
			}
			float rudder = BoatRules.Rudder(transform.forward, who.transform.position - transform.position);
			Ship.m_speed = speed;
			Ship.m_rudderValue = Mathf.MoveTowards(Ship.m_rudderValue, rudder, Ship.m_rudderSpeed * dt * 2f);
		}

		/// <summary>
		/// How long a quest's boat out at sea holds still with nobody left on it before it's gone with the quest, in
		/// seconds
		/// </summary>
		private const float QuestGoneWait = 3f;

		/// <summary>
		/// Looks after a quest's boat setting out once nobody of its crew is on it - out at sea, QuestGoneOut from the shore
		/// or farther, its crew gone from the world with the quest (see VillageLife.SailOffOnQuest), it's gone too, or left
		/// as a ship like any other when a player is still on it, and at the shore, its crew never having got on, the
		/// village's own is left as it was and a new one broken up, after CrewlessTime seconds
		/// </summary>
		/// <returns>True when it's crewless, so it holds still</returns>
		private bool UpdateQuestOutbound()
		{
			if (!OnQuest() || Homeward())
			{
				return(false);
			}
			if (CrewAboard() > 0)
			{
				crewlessSince = -1f;
				return(false);
			}
			crewlessSince = ((crewlessSince < 0f) ? Time.time : crewlessSince);
			float crewless = Time.time - crewlessSince;
			Hold();
			bool outAtSea = (Utils.DistanceXZ(transform.position, Launch) >= QuestGoneOut);
			if (outAtSea && crewless >= QuestGoneWait)
			{
				if (Ship.m_players.Count > 0)
				{
					LeaveBehind();
				}
				else
				{
					Vanish();
				}
			}
			else if (!outAtSea && crewless >= CrewlessTime)
			{
				if (VillageOwned())
				{
					LeaveBehind();
				}
				else
				{
					Break();
				}
			}
			return(true);
		}

		/// <summary>
		/// Takes a quest's boat out of the world, gone with its crew - noted on the quest, so it comes back with them (see
		/// QuestRecord.Boats)
		/// </summary>
		private void Vanish()
		{
			Plugin.Log.LogInfo("Boat: " + Key + " sailed away with its quest at " + transform.position.ToString("F0"));
			TestLog.Count("Boats", "sailed away on a quest");
			nview.Destroy();
		}

		/// <summary>
		/// Brings the boat in to the far shore at slow speed from within LandReach of the spot ashore, so its crew has as
		/// little as it can to swim - until it's near the spot, the water ahead of its bow is about to run it aground, or
		/// it's no longer getting nearer (see BoatRules.Beached)
		/// </summary>
		/// <param name="dt">Seconds since the last update</param>
		/// <param name="distance">How far the spot ashore is, in meters</param>
		/// <param name="land">The spot ashore</param>
		/// <returns>True once it has come as near as it can, and holds there</returns>
		private bool ComeIn(float dt, float distance, Vector3 land)
		{
			if (distance < beachBest - 1f || beachSince < 0f)
			{
				beachBest = distance;
				beachSince = Time.time;
			}
			if (BoatRules.Beached(distance, DepthAhead(), Time.time - beachSince))
			{
				Hold();
				beachBest = float.MaxValue;
				beachSince = -1f;
				return(true);
			}
			UpdateTalk();
			float rudder = BoatRules.Rudder(transform.forward, land - transform.position);
			Ship.m_speed = Ship.Speed.Slow;
			Ship.m_rudderValue = Mathf.MoveTowards(Ship.m_rudderValue, rudder, Ship.m_rudderSpeed * dt * 2f);
			return(false);
		}

		/// <summary>
		/// Finds how deep the water is just ahead of the boat's bow, by the ground's height there
		/// </summary>
		/// <returns>The depth, in meters, less than 0 over dry ground</returns>
		private float DepthAhead()
		{
			if (ZoneSystem.instance == null)
			{
				return(float.MaxValue);
			}
			BoxCollider hull = Ship.m_floatCollider;
			float half = ((hull != null) ? hull.size.z * hull.transform.lossyScale.z / 2f : 3f);
			Vector3 bow = transform.position + transform.forward * (half + 1f);
			return(ZoneSystem.instance.m_waterLevel - ZoneSystem.instance.GetGroundHeight(bow));
		}

		/// <summary>
		/// Has the crew talk as the boat sails - looking at what's around it every TalkLookInterval seconds (see
		/// BoatTalk.Pick), calling out something new to worry about or marvel at as soon as it's seen, and otherwise
		/// making small talk about it every TalkMin to TalkMax seconds, never two lines within TalkGap of each other
		/// </summary>
		private void UpdateTalk()
		{
			if (Time.time < nextTalkLook)
			{
				return;
			}
			nextTalkLook = Time.time + TalkLookInterval;
			if (nextTalk < 0f)
			{
				nextTalk = Time.time + Random.Range(BoatTalk.TalkMin, BoatTalk.TalkMax) / 2f;
			}
			Character speaker = TalkerAboard();
			if (speaker == null)
			{
				return;
			}
			BoatTalk.Topic topic = BoatTalk.Pick(FlyersAfterUs(speaker), SerpentAfterUs(), VikingNewsWatch.LeviathanInSight(transform.position, LeviathanSight), VikingWeather.Storming(), EnvMan.IsWet(), EnvMan.IsFreezing(), VikingWeather.Foggy(), EnvMan.IsNight());
			bool callOut = BoatTalk.CallsOut(topic, seenTopic);
			seenTopic = topic;
			if (Time.time - lastTalkAt < BoatTalk.TalkGap || (!callOut && Time.time < nextTalk))
			{
				return;
			}
			lastTalkAt = Time.time;
			lastSpeaker = speaker;
			nextTalk = Time.time + Random.Range(BoatTalk.TalkMin, BoatTalk.TalkMax);
			VikingSpeech.Say(speaker, VikingRoadSpeech.Sailing(topic), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
			TestLog.Count("Boats", "lines said at sea about " + topic.ToString().ToLowerInvariant());
		}

		/// <summary>
		/// Has one of the crew on board call out the far shore as the boat reaches it, with the name of the village
		/// they're going to
		/// </summary>
		private void LandHo()
		{
			Character speaker = TalkerAboard();
			if (speaker == null)
			{
				return;
			}
			VikingSpeech.SayFirst(speaker, VikingRoadSpeech.LandHo(VillageTravel.DestinationName(speaker.m_nview.GetZDO())), VikingSpeech.CallTime, VikingSpeech.CallCull);
		}

		/// <summary>
		/// Picks one of the crew sitting on board and loaded on this client to say something - at random, and not the one
		/// who spoke last while another is there to
		/// </summary>
		/// <returns>The viking, or null when none is</returns>
		private Character TalkerAboard()
		{
			List<Character> aboard = new List<Character>();
			for (int seat = 0; seat < Crew.Count; seat++)
			{
				Character member = CrewMember(seat);
				if (member != null && member.m_nview.GetZDO().GetInt(BoardKey) == Aboard)
				{
					aboard.Add(member);
				}
			}
			if (aboard.Count > 1)
			{
				aboard.Remove(lastSpeaker);
			}
			return((aboard.Count > 0) ? aboard[Random.Range(0, aboard.Count)] : null);
		}

		/// <summary>
		/// Checks whether flying monsters are after the boat - one of the speaker's enemies, flying within FlyerReach, after
		/// someone on board or come within FlyerNear
		/// </summary>
		/// <param name="speaker">One of the crew, whose enemies they are</param>
		/// <returns>True when they are</returns>
		private bool FlyersAfterUs(Character speaker)
		{
			foreach (Character other in Character.GetAllCharacters())
			{
				if (other != null && !other.IsDead() && other.IsFlying() && BaseAI.IsEnemy(speaker, other) && AfterUs(other, FlyerReach, FlyerNear))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a sea serpent is after the boat - within SerpentReach, after someone on board or come within
		/// SerpentNear
		/// </summary>
		/// <returns>True when one is</returns>
		private bool SerpentAfterUs()
		{
			foreach (Character other in Character.GetAllCharacters())
			{
				if (other != null && !other.IsDead() && VikingNewsLines.Serpents.Contains(Utils.GetPrefabName(other.gameObject)) && AfterUs(other, SerpentReach, SerpentNear))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a monster is after the boat - within reach of it and after someone on board, or come near it
		/// whatever it's after
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <param name="reach">How far off it can be, in meters</param>
		/// <param name="near">How near it counts however it's minded, in meters</param>
		/// <returns>True when it is</returns>
		private bool AfterUs(Character monster, float reach, float near)
		{
			float distance = Vector3.Distance(monster.transform.position, transform.position);
			if (distance > reach)
			{
				return(false);
			}
			if (distance <= near)
			{
				return(true);
			}
			Character target = ((monster.GetBaseAI() is MonsterAI theirs) ? theirs.GetTargetCreature() : null);
			if (target == null)
			{
				return(false);
			}
			if (target is Player player)
			{
				return(Ship.IsPlayerInBoat(player));
			}
			ZDO zdo = ((target.m_nview != null && target.m_nview.IsValid()) ? target.m_nview.GetZDO() : null);
			return(zdo != null && zdo.GetString(BoatKey, "") == Key && zdo.GetInt(BoardKey) == Aboard);
		}

		/// <summary>
		/// Finds one of the boat's crew sitting on board and loaded on this client to call out - the one at the helm first
		/// </summary>
		/// <returns>The viking, or null when none is</returns>
		private Character SpeakerAboard()
		{
			for (int seat = 0; seat < Crew.Count; seat++)
			{
				Character member = CrewMember(seat);
				if (member != null && member.m_nview.GetZDO().GetInt(BoardKey) == Aboard)
				{
					return(member);
				}
			}
			return(null);
		}

		/// <summary>
		/// Breaks the boat up once one of its crew has asked, or once its crew has all got off and BreakWait seconds have
		/// passed - waiting on a player standing on it for up to BreakWait seconds from then. One left for the village its
		/// crew came to is left there once they're all off it (see LeaveBehind)
		/// </summary>
		private void UpdateLanding()
		{
			if (Kept())
			{
				if (CrewAboard() == 0)
				{
					LeaveBehind();
				}
				return;
			}
			long breakAt = nview.GetZDO().GetLong(BreakAtKey, 0L);
			if (breakAt == 0L)
			{
				if (CrewAboard() == 0 && PhaseSeconds > BreakWait)
				{
					nview.GetZDO().Set(BreakAtKey, NetTime.Now());
				}
				return;
			}
			float since = (NetTime.Now() - breakAt) / (float)NetTime.Ticks(1f);
			if (Ship.m_players.Count > 0 && since < BreakWait)
			{
				return;
			}
			Break();
		}

		/// <summary>
		/// Checks whether the boat's crew leaves it for the village they came to rather than breaking it up
		/// </summary>
		/// <returns>True when they do</returns>
		public bool Kept()
		{
			return(Valid() && nview.GetZDO().GetBool(KeptKey));
		}

		/// <summary>
		/// Notes whether the boat's crew leaves it for the village they come to, on a quest's boat coming home, which is
		/// decided as it comes back (see VillageQuests.SailHome)
		/// </summary>
		/// <param name="kept">True to leave it</param>
		public void SetKept(bool kept)
		{
			if (Valid())
			{
				nview.GetZDO().Set(KeptKey, kept);
			}
		}

		/// <summary>
		/// How far apart a quest's boats come back side by side, in meters
		/// </summary>
		public const float SailInSpacing = 12f;

		/// <summary>
		/// Decides, as the boat comes in to the far shore, whether its crew leaves it for the village they're going to -
		/// the first of their group's boats, when the village stands on this shore, within ShoreVillageReach of where
		/// they go ashore, and has no ship of this kind by it already, its own moored one, a player's or one left before
		/// (see HasSuchBoat)
		/// </summary>
		private void DecideKept()
		{
			ZDO zdo = nview.GetZDO();
			// A quest's boat coming home was decided on as it came back (see VillageQuests.SailHome)
			if (zdo.GetInt(IndexKey) != 0 || OnQuest())
			{
				return;
			}
			Character member = TalkerAboard();
			Vector3 destination = ((member != null) ? member.m_nview.GetZDO().GetVec3(VillageTravel.DestinationKey, Vector3.zero) : Vector3.zero);
			Village village = ((destination != Vector3.zero) ? Village.Find(destination) : null);
			if (village == null || village.DistanceOutside(Land) > ShoreVillageReach)
			{
				return;
			}
			if (HasSuchBoat(village, zdo.GetPrefab(), zdo))
			{
				Plugin.Log.LogInfo("Boat: " + Key + " is broken up at " + transform.position.ToString("F0") + ", since the village at " + village.Farm.ToString("F0") + " has a " + Utils.GetPrefabName(gameObject) + " already");
				return;
			}
			zdo.Set(KeptKey, true);
		}

		/// <summary>
		/// Checks whether a ship of a kind is by a village already - within ShoreVillageReach of its edge, but for boats
		/// still out on the road with their vikings
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="prefab">The ship's prefab hash</param>
		/// <param name="except">A ship's ZDO not to count, or null</param>
		/// <returns>True when one is</returns>
		public static bool HasSuchBoat(Village village, int prefab, ZDO except)
		{
			List<ZDO> near = new List<ZDO>();
			VillageRespawn.ZdosNear(village.Middle, village.Radius + ShoreVillageReach, near);
			return(near.Exists((ZDO other) => other != except && other.GetPrefab() == prefab && other.GetString(GroupKey, "").Length == 0 && village.DistanceOutside(other.GetPosition()) <= ShoreVillageReach));
		}

		/// <summary>
		/// Leaves the boat for the village its crew came to, once they're all off it - a ship like any other from then on,
		/// as the village's own moored one is (see VillageShoreBoat): its sail down where it came in, no longer the
		/// vikings', so a player can take the helm, and dropping what it's built from when it breaks
		/// </summary>
		private void LeaveBehind()
		{
			Hold();
			Plugin.Log.LogInfo("Boat: " + Key + " was left at " + transform.position.ToString("F0") + " for the village its crew came to");
			TestLog.Count("Boats", "left for a village");
			ZDO zdo = nview.GetZDO();
			zdo.Set(GroupKey, "");
			zdo.Set(CrewKey, "");
			zdo.Set(KeptKey, false);
			zdo.Set(BreakAtKey, 0L);
			zdo.Set(QuestKey, "");
			zdo.Set(HomewardKey, false);
			zdo.Set(VillageOwnedKey, false);
			Destroy(this);
		}

		/// <summary>
		/// Asks the boat's owner to break it up, once no player is on it (see UpdateLanding)
		/// </summary>
		public void RequestBreak()
		{
			if (Valid())
			{
				nview.InvokeRPC(BreakRpc);
			}
		}

		/// <summary>
		/// Notes on the boat's owner that one of its crew asked for it to be broken up
		/// </summary>
		/// <param name="sender">Who asked</param>
		private void RPC_Break(long sender)
		{
			if (Valid() && nview.IsOwner() && nview.GetZDO().GetLong(BreakAtKey, 0L) == 0L)
			{
				nview.GetZDO().Set(BreakAtKey, NetTime.Now());
			}
		}

		/// <summary>
		/// Breaks the boat up, dropping nothing
		/// </summary>
		private void Break()
		{
			Plugin.Log.LogInfo("Boat: " + Key + " was broken up at " + transform.position.ToString("F0"));
			TestLog.Count("Boats", "broken up");
			WearNTear wear = GetComponent<WearNTear>();
			if (wear != null)
			{
				wear.Remove(true);
			}
			else
			{
				nview.Destroy();
			}
		}

		/// <summary>
		/// Counts the boat's crew sitting on board, on any client
		/// </summary>
		/// <returns>How many</returns>
		public int CrewAboard()
		{
			string key = Key;
			int count = 0;
			foreach (Character character in Character.GetAllCharacters())
			{
				ZDO zdo = ((character != null && character.m_nview != null && character.m_nview.IsValid()) ? character.m_nview.GetZDO() : null);
				if (zdo != null && !character.IsDead() && zdo.GetString(BoatKey, "") == key && zdo.GetInt(BoardKey) == Aboard)
				{
					count++;
				}
			}
			return(count);
		}

		/// <summary>
		/// Finds one of the boat's crew loaded on this client
		/// </summary>
		/// <param name="seat">The index of its seat on the crew, the helm being 0</param>
		/// <returns>The viking, or null when it isn't loaded or is dead</returns>
		public Character CrewMember(int seat)
		{
			List<string> crew = Crew;
			if (seat < 0 || seat >= crew.Count)
			{
				return(null);
			}
			foreach (Character character in Character.GetAllCharacters())
			{
				ZDO zdo = ((character != null && character.m_nview != null && character.m_nview.IsValid()) ? character.m_nview.GetZDO() : null);
				if (zdo != null && !character.IsDead() && VikingIdentity.Get(zdo) == crew[seat])
				{
					return(character);
				}
			}
			return(null);
		}

		/// <summary>
		/// Takes the boats of a group of vikings out of the world, as the group is taken off the road, on the server - the
		/// ships near any of them noted as the group's
		/// </summary>
		/// <param name="group">The group</param>
		/// <param name="near">Where its vikings are</param>
		/// <returns>How many boats were taken out</returns>
		public static int RemoveAll(string group, List<Vector3> near)
		{
			if (group.Length == 0)
			{
				return(0);
			}
			HashSet<ZDO> found = new HashSet<ZDO>();
			List<ZDO> zdos = new List<ZDO>();
			foreach (Vector3 point in near)
			{
				zdos.Clear();
				VillageRespawn.ZdosNear(point, 60f, zdos);
				foreach (ZDO zdo in zdos)
				{
					if (zdo.GetString(GroupKey, "") == group)
					{
						found.Add(zdo);
					}
				}
			}
			foreach (ZDO zdo in found)
			{
				zdo.SetOwner(ZDOMan.GetSessionID());
				ZDOMan.instance.DestroyZDO(zdo);
			}
			return(found.Count);
		}
	}
}
