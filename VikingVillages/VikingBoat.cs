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
	/// boards, and once they're all on, waits for a player standing near to come aboard too - asking after
	/// AskAfter seconds, setting off DepartAfter seconds after they're all on, or at once with no player near. It
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
		/// On a living viking, the boat it's crossing the sea in, as its group and the boat's index, or "", and how far
		/// along its crossing it is (see VillageLife's Crossing)
		/// </summary>
		public static readonly int BoatKey = "kriona.VikingVillages.vikingBoat".GetStableHashCode();
		public static readonly int BoardKey = "kriona.VikingVillages.vikingBoard".GetStableHashCode();

		/// <summary>
		/// The value of BoardKey for a viking sitting on board
		/// </summary>
		public const int Aboard = 5;

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
		/// Until when the boat holds still after loading, in Time.time seconds, whether it asked a player to come aboard,
		/// and how it's getting unstuck - when it last looked, how far it was from where it's heading then, until when it
		/// backs off and how many times it has
		/// </summary>
		private float settleUntil;
		private bool asked;
		private float stuckLookAt;
		private float stuckDistance = float.MaxValue;
		private float backUntil;
		private int stuckTimes;
		private float crewlessSince = -1f;

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
					if (CrewAboard() >= Crew.Count || PhaseSeconds > BoardTime + 10f)
					{
						asked = false;
						SetPhase(Phase.Waiting);
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
		/// Waits for a player standing near the boat to come aboard - asking them after AskAfter seconds, and setting off
		/// DepartAfter seconds after the crew got on, or at once when no player near is still ashore
		/// </summary>
		private void UpdateWaiting()
		{
			Player waiting = PlayerAshore();
			if (waiting == null)
			{
				SetPhase(Phase.Sailing);
				return;
			}
			if (!asked && PhaseSeconds >= AskAfter)
			{
				asked = true;
				Character helm = CrewMember(0);
				if (helm != null)
				{
					VikingSpeech.Say(helm, VikingRoadSpeech.Coming(waiting.GetPlayerName()), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				}
			}
			if (PhaseSeconds >= DepartAfter)
			{
				SetPhase(Phase.Sailing);
			}
		}

		/// <summary>
		/// Finds a player standing near the boat who isn't on it
		/// </summary>
		/// <returns>The player, or null when there's none</returns>
		private Player PlayerAshore()
		{
			foreach (Player player in Player.GetAllPlayers())
			{
				if (player != null && !player.IsDead() && Vector3.Distance(player.transform.position, transform.position) <= PlayerReach && !Ship.IsPlayerInBoat(player))
				{
					return(player);
				}
			}
			return(null);
		}

		/// <summary>
		/// Steers the boat along its road, a waypoint at a time, then for the spot ashore - backing off when it's stuck,
		/// and letting its crew off once it's there, or once it has been stuck StuckLimit times, or has had no crew on
		/// board for CrewlessTime seconds
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
				SetPhase(Phase.Landing);
				return;
			}
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
		/// Breaks the boat up once one of its crew has asked, or once its crew has all got off and BreakWait seconds have
		/// passed - waiting on a player standing on it for up to BreakWait seconds from then
		/// </summary>
		private void UpdateLanding()
		{
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
