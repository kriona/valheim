using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Plans a viking's road over the world's map to whichever of a list of places it can get to soonest - on foot
	/// over land, swimming across rivers and lakes no wider than it can swim, by boat across the sea and through the
	/// portals it may use, never through lava, past the edge of the world or across a sea that would burn its boat -
	/// keeping out of biomes harder than its own where it can
	/// </summary>
	/// <remarks>
	/// The map is a grid of CellSize squares, each sampled once from the world's plan, so a road can be planned over
	/// ground no player has been near. The quickest road out from the start is followed until it reaches a village
	/// (Dijkstra's search), so the first village reached is the soonest there, not the nearest in a straight line. A step
	/// costs the time it takes - on foot at TravelTime.Speed, swimming at TravelTime.SwimSpeed, longer for each biome a
	/// square is harder than the viking's own (see HarderCost), across the sea at its boat's speed, and from a portal's
	/// square to its other end's in TravelTime.PortalSeconds. The search goes by square and how many squares of water in
	/// a row the road has swum to get there, so a road never swims more of them in a row than the viking can (see
	/// SwimSquares). It runs a little at a time (see Step), so a long search doesn't hold up the game
	/// </remarks>
	internal class TravelRoute
	{
		/// <summary>
		/// What a square of the map holds - whether a viking can cross it, whether it's sea, crossed by boat, whether that
		/// sea boils and burns a boat not made for it (see Ship.m_ashlandsReady), whether it's a river, lake or water off
		/// a shore too deep to wade, swum across on foot, whether that water is deep enough for a boat too, so it can be
		/// sailed as well as swum, and how hard its biome is, by VikingGear's order
		/// </summary>
		internal struct Cell
		{
			public bool Open;
			public bool Sea;
			public bool Burning;
			public bool Water;
			public bool Deep;
			public int Biome;
		}

		/// <summary>
		/// The swim count of a search's state for a viking in a boat, rather than on foot with that many squares of water
		/// in a row swum
		/// </summary>
		private const int Sailing = -1;

		/// <summary>
		/// How much longer the search counts getting into a boat, in seconds - finding a spot to launch it and building
		/// it - so a river the viking can swim across is swum, and a boat is only taken where swimming won't do or the
		/// sea is long enough to be worth it
		/// </summary>
		private const float LaunchSeconds = 30f;

		/// <summary>
		/// One way through a portal a viking may use - the spot in front of the portal it steps into, and the spot in
		/// front of the other end it comes out at
		/// </summary>
		internal struct Portal
		{
			public Vector3 Entry;
			public Vector3 Exit;
		}

		/// <summary>
		/// How wide a square of the map is, in meters
		/// </summary>
		internal const float CellSize = 32f;

		/// <summary>
		/// How many times as long a step on foot takes for each biome a square is harder than the viking's own
		/// </summary>
		private const float HarderCost = 4f;

		/// <summary>
		/// The most squares a search looks at - more than the whole world holds, so it only stops a search that has gone
		/// wrong
		/// </summary>
		private const int MaxCells = 500000;

		/// <summary>
		/// How near a square's middle a village has to be for the road to reach it there, in meters
		/// </summary>
		private const float TargetReach = CellSize * 1.5f;

		/// <summary>
		/// The longest straight stretch between two points of the road, in meters, so each point is within reach of the
		/// ground loaded around a player when the viking sets out for it
		/// </summary>
		private const float LegMax = 60f;

		/// <summary>
		/// The eight steps to a square's neighbors
		/// </summary>
		private static readonly int[] StepX = { 1, -1, 0, 0, 1, 1, -1, -1 };
		private static readonly int[] StepZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

		/// <summary>
		/// A square reached with a number of squares of water in a row swum to get there, with the quickest time of the
		/// road to it, the square and swim it was reached from and whether it's done with
		/// </summary>
		private class Node
		{
			public float Cost;
			public (long Cell, int Swum) From;
			public bool Closed;

			/// <summary>
			/// The portal the square was reached through from the one before, or -1 when it was reached on foot or by sea
			/// </summary>
			public int Portal = -1;
		}

		/// <summary>
		/// Reads a square of the map at a point - its x and z in world space
		/// </summary>
		private readonly System.Func<float, float, Cell> sample;

		/// <summary>
		/// The viking's own biome, by VikingGear's order, how fast its boat goes, in meters a second, and whether its boat
		/// holds up in a sea that burns
		/// </summary>
		private readonly int biome;
		private readonly float boatSpeed;
		private readonly bool fireproof;

		/// <summary>
		/// The most squares of water in a row the road may swim across (see SwimSquares)
		/// </summary>
		private readonly int swimSquares;

		/// <summary>
		/// The portals the viking may use, and the squares each can be stepped into from, by the square
		/// </summary>
		private readonly List<Portal> portals;
		private readonly Dictionary<long, List<int>> portalsAt = new Dictionary<long, List<int>>();
		private readonly HashSet<long> portalCells = new HashSet<long>();

		/// <summary>
		/// Where the road starts and the villages it could lead to
		/// </summary>
		private readonly Vector3 start;
		private readonly List<Vector3> targets;

		/// <summary>
		/// The squares sampled, the squares reached by how much water was swum to get there, the squares to look at next
		/// by time, the squares a village can be reached from, with which village, and the square the road starts in
		/// </summary>
		private readonly Dictionary<long, Cell> cells = new Dictionary<long, Cell>();
		private readonly Dictionary<(long Cell, int Swum), Node> nodes = new Dictionary<(long Cell, int Swum), Node>();
		private readonly CostHeap open = new CostHeap();
		private readonly Dictionary<long, int> goals = new Dictionary<long, int>();
		private readonly long startKey;

		/// <summary>
		/// Whether the search is over, and the index of the village it found, or -1 when none can be reached
		/// </summary>
		public bool Done { get; private set; }
		public int Found { get; private set; } = -1;

		/// <summary>
		/// The road to the village found - from the start to the village, a point at most LegMax apart - how long it is,
		/// in meters, and how long it takes, in seconds
		/// </summary>
		public TravelRoad Road { get; private set; } = new TravelRoad();
		public float Length { get; private set; }
		public float Seconds { get; private set; }

		/// <summary>
		/// Sets up a search for the road to the village a viking can get to soonest, in a boat that burns, using no portal
		/// </summary>
		/// <param name="from">Where the road starts</param>
		/// <param name="own">The viking's own biome, by VikingGear's order</param>
		/// <param name="boat">How fast its boat goes, in meters a second</param>
		/// <param name="villages">Where each village stands</param>
		/// <param name="map">Reads a square of the map at a point's x and z</param>
		public TravelRoute(Vector3 from, int own, float boat, List<Vector3> villages, System.Func<float, float, Cell> map) : this(from, own, boat, false, 0f, villages, map, null)
		{
		}

		/// <summary>
		/// Sets up a search for the road to the place a viking can get to soonest
		/// </summary>
		/// <param name="from">Where the road starts</param>
		/// <param name="own">The biome it keeps to, by VikingGear's order</param>
		/// <param name="boat">How fast its boat goes, in meters a second</param>
		/// <param name="boatFireproof">Whether its boat holds up in a sea that burns</param>
		/// <param name="swimMeters">How far it swims, in meters, or 0 when it isn't known (see SwimSquares)</param>
		/// <param name="places">Where each place stands</param>
		/// <param name="map">Reads a square of the map at a point's x and z</param>
		/// <param name="usable">The portals it may use, one way each, or null for none</param>
		public TravelRoute(Vector3 from, int own, float boat, bool boatFireproof, float swimMeters, List<Vector3> places, System.Func<float, float, Cell> map, List<Portal> usable)
		{
			start = from;
			biome = own;
			boatSpeed = Mathf.Max(0.1f, boat);
			fireproof = boatFireproof;
			swimSquares = SwimSquares(swimMeters);
			targets = places;
			sample = map;
			portals = (usable ?? new List<Portal>());
			startKey = Key(Index(from.x), Index(from.z));
			for (int i = 0; i < portals.Count; i++)
			{
				long entry = Key(Index(portals[i].Entry.x), Index(portals[i].Entry.z));
				if (!portalsAt.TryGetValue(entry, out List<int> here))
				{
					here = new List<int>();
					portalsAt[entry] = here;
				}
				here.Add(i);
				portalCells.Add(entry);
				portalCells.Add(Key(Index(portals[i].Exit.x), Index(portals[i].Exit.z)));
			}
			for (int i = 0; i < targets.Count; i++)
			{
				AddGoal(i);
			}
			nodes[(startKey, 0)] = new Node { Cost = 0f, From = (startKey, 0) };
			open.Push(0f, (startKey, 0));
			if (goals.Count == 0)
			{
				Done = true;
			}
		}

		/// <summary>
		/// Works out how many squares of water in a row a viking may swim across on its road - as many as it swims across
		/// before its stamina runs out, but always at least one, so a river never cuts a viking off from a village
		/// </summary>
		/// <param name="swimMeters">How far it swims, in meters, or 0 when it isn't known</param>
		/// <returns>How many squares</returns>
		internal static int SwimSquares(float swimMeters)
		{
			return(Mathf.Max(1, Mathf.FloorToInt(swimMeters / CellSize)));
		}

		/// <summary>
		/// Notes the squares a village can be reached from - each whose middle is within TargetReach of it, the nearest
		/// village to it when several are
		/// </summary>
		/// <param name="index">The village's index</param>
		private void AddGoal(int index)
		{
			Vector3 target = targets[index];
			int cx = Index(target.x);
			int cz = Index(target.z);
			for (int dx = -2; dx <= 2; dx++)
			{
				for (int dz = -2; dz <= 2; dz++)
				{
					Vector3 middle = Middle(cx + dx, cz + dz);
					float distance = Flat(middle, target);
					if (distance > TargetReach)
					{
						continue;
					}
					long key = Key(cx + dx, cz + dz);
					if (!goals.TryGetValue(key, out int other) || Flat(middle, targets[other]) > distance)
					{
						goals[key] = index;
					}
				}
			}
		}

		/// <summary>
		/// Looks at up to a number of squares, reaching out from the quickest road so far, until a village is reached or
		/// there's nowhere left to go
		/// </summary>
		/// <param name="budget">The most squares to look at</param>
		/// <returns>True once the search is over</returns>
		public bool Step(int budget)
		{
			for (int i = 0; i < budget && !Done; i++)
			{
				if (open.Count == 0 || nodes.Count > MaxCells)
				{
					Done = true;
					break;
				}
				open.Pop(out float cost, out (long Cell, int Swum) state);
				Node node = nodes[state];
				if (node.Closed || cost > node.Cost)
				{
					continue;
				}
				node.Closed = true;
				if (goals.TryGetValue(state.Cell, out int goal))
				{
					Finish(state, goal);
					break;
				}
				Expand(state, node);
			}
			return(Done);
		}

		/// <summary>
		/// Reaches out from a square to each neighbor that can be crossed - stepping diagonally only past two neighbors
		/// that can, so the road doesn't cut a corner of lava or the edge of the world. On foot it steps onto land, and
		/// into water only while the squares of it swum in a row stay within swimSquares. By boat it steps onto the sea
		/// and water deep enough for a boat, getting into the boat taking LaunchSeconds more - so a strip of deep water
		/// off a shore too wide to swim, which the world's plan doesn't count as the sea, is sailed across
		/// </summary>
		/// <param name="state">The square, and how many squares of water in a row were swum to get there, or Sailing</param>
		/// <param name="node">What's known of it</param>
		private void Expand((long Cell, int Swum) state, Node node)
		{
			long key = state.Cell;
			int x = KeyX(key);
			int z = KeyZ(key);
			bool sailing = (state.Swum == Sailing);
			float here = PerMeter(CellAt(x, z), sailing);
			for (int i = 0; i < StepX.Length; i++)
			{
				int nx = x + StepX[i];
				int nz = z + StepZ[i];
				long next = Key(nx, nz);
				if (!Crossable(next, nx, nz))
				{
					continue;
				}
				bool diagonal = (StepX[i] != 0 && StepZ[i] != 0);
				if (diagonal && (!Crossable(Key(nx, z), nx, z) || !Crossable(Key(x, nz), x, nz)))
				{
					continue;
				}
				float length = (diagonal ? CellSize * 1.41421356f : CellSize);
				Cell cell = CellAt(nx, nz);
				if (!cell.Sea)
				{
					int swum = (cell.Water ? Mathf.Max(0, state.Swum) + 1 : 0);
					if (swum <= swimSquares)
					{
						Reach((next, swum), node.Cost + length * (here + PerMeter(cell, false)) / 2f, state, -1);
					}
				}
				if (cell.Sea || cell.Deep)
				{
					float launch = (sailing ? 0f : LaunchSeconds);
					Reach((next, Sailing), node.Cost + launch + length * (here + PerMeter(cell, true)) / 2f, state, -1);
				}
			}
			if (!portalsAt.TryGetValue(key, out List<int> jumps))
			{
				return;
			}
			foreach (int portal in jumps)
			{
				long exit = Key(Index(portals[portal].Exit.x), Index(portals[portal].Exit.z));
				if (exit != key)
				{
					Reach((exit, 0), node.Cost + TravelTime.PortalSeconds, state, portal);
				}
			}
		}

		/// <summary>
		/// Notes a road to a square when it's quicker than any found to it so far with as much water swum in a row
		/// </summary>
		/// <param name="next">The square's key, and how many squares of water in a row were swum to get there</param>
		/// <param name="cost">The time of the road to it</param>
		/// <param name="from">The square it's reached from, and the water swum to get there</param>
		/// <param name="portal">The portal it's reached through, or -1</param>
		private void Reach((long Cell, int Swum) next, float cost, (long Cell, int Swum) from, int portal)
		{
			if (nodes.TryGetValue(next, out Node known) && (known.Closed || known.Cost <= cost))
			{
				return;
			}
			nodes[next] = new Node { Cost = cost, From = from, Portal = portal };
			open.Push(cost, next);
		}

		/// <summary>
		/// Checks whether a square can be crossed - land, a river or a sea the boat holds up in, or one a village can be
		/// reached from, or a portal stands in
		/// </summary>
		/// <param name="key">The square's key</param>
		/// <param name="x">Its x</param>
		/// <param name="z">Its z</param>
		/// <returns>True when it can</returns>
		private bool Crossable(long key, int x, int z)
		{
			return(key == startKey || goals.ContainsKey(key) || portalCells.Contains(key) || Passable(CellAt(x, z)));
		}

		/// <summary>
		/// Checks whether a square of the map can be crossed - open, and not a sea that would burn the boat
		/// </summary>
		/// <param name="cell">The square</param>
		/// <returns>True when it can</returns>
		private bool Passable(Cell cell)
		{
			return(cell.Open && !(cell.Sea && cell.Burning && !fireproof));
		}

		/// <summary>
		/// Whether the vikings pull a cart along the road, so it keeps off rough ground where it can (see
		/// CartRules.Rough)
		/// </summary>
		public bool Cart;

		/// <summary>
		/// Works out how long a meter of a square takes - by boat, on foot over land and swimming across a river or lake,
		/// longer for each biome it's harder than the viking's own, and longer on rough ground for vikings pulling a cart
		/// </summary>
		/// <param name="cell">The square</param>
		/// <param name="sailing">Whether it's crossed by boat</param>
		/// <returns>The seconds a meter takes</returns>
		private float PerMeter(Cell cell, bool sailing)
		{
			if (sailing)
			{
				return(1f / boatSpeed);
			}
			float rough = ((Cart && !cell.Water && CartRules.Rough(cell.Biome)) ? CartRules.RoughCost : 0f);
			return((1f + HarderCost * Mathf.Max(0, cell.Biome - biome) + rough) / (cell.Water ? TravelTime.SwimSpeed : TravelTime.Speed));
		}

		/// <summary>
		/// Ends the search at a square a village is reached from - laying the road out from the start to the village
		/// </summary>
		/// <param name="state">The square, and how many squares of water in a row were swum to get there</param>
		/// <param name="goal">The village's index</param>
		private void Finish((long Cell, int Swum) state, int goal)
		{
			Done = true;
			Found = goal;
			List<(long Cell, int Swum)> states = new List<(long Cell, int Swum)>();
			for ((long Cell, int Swum) at = state; ; at = nodes[at].From)
			{
				states.Add(at);
				if (at.Cell == startKey && at.Swum == 0)
				{
					break;
				}
			}
			states.Reverse();
			List<long> path = states.ConvertAll(((long Cell, int Swum) at) => at.Cell);
			List<Vector3> points = new List<Vector3> { start };
			List<TravelLeg> legs = new List<TravelLeg> { TravelLeg.Foot };
			for (int i = 1; i < path.Count; i++)
			{
				int portal = nodes[states[i]].Portal;
				if (portal < 0)
				{
					points.Add(Middle(KeyX(path[i]), KeyZ(path[i])));
					legs.Add((states[i].Swum == Sailing) ? TravelLeg.Sea : TravelLeg.Foot);
					continue;
				}
				// The square stepped into the portal from becomes the spot in front of the portal itself, unless the road
				// came out of another portal there, which it walks on from
				if (points.Count > 1 && legs[legs.Count - 1] != TravelLeg.Portal)
				{
					points[points.Count - 1] = portals[portal].Entry;
				}
				else
				{
					points.Add(portals[portal].Entry);
					legs.Add(TravelLeg.Foot);
				}
				points.Add(portals[portal].Exit);
				legs.Add(TravelLeg.Portal);
			}
			points.Add(targets[goal]);
			legs.Add((states[states.Count - 1].Swum == Sailing) ? TravelLeg.Sea : TravelLeg.Foot);
			Road = Straighten(points, legs);
			Length = Road.Meters(0, start);
			Seconds = Road.TotalSeconds(boatSpeed);
		}

		/// <summary>
		/// Straightens a road of square middles - from each point, on to the farthest point after it that can be reached in
		/// a straight line within LegMax, the same way, on foot or by sea, over squares that can be crossed that way and
		/// take no longer than the road it skips - and breaks any longer stretch into legs of at most LegMax. A leg through
		/// a portal is kept as it is, so the road always stops at the portal
		/// </summary>
		/// <param name="points">The road, a point for each square</param>
		/// <param name="legs">How the leg to each point is crossed</param>
		/// <returns>The straightened road</returns>
		private TravelRoad Straighten(List<Vector3> points, List<TravelLeg> legs)
		{
			TravelRoad road = new TravelRoad();
			road.Add(points[0], TravelLeg.Foot);
			int at = 0;
			while (at < points.Count - 1)
			{
				int next = at + 1;
				TravelLeg leg = legs[next];
				if (leg == TravelLeg.Portal)
				{
					road.Add(points[next], TravelLeg.Portal);
					at = next;
					continue;
				}
				bool bySea = (leg == TravelLeg.Sea);
				float slowest = PerMeter(CellAtPoint(points[next]), bySea);
				for (int j = at + 2; j < points.Count; j++)
				{
					if (legs[j] != leg)
					{
						break;
					}
					slowest = Mathf.Max(slowest, PerMeter(CellAtPoint(points[j - 1]), bySea));
					if (Flat(points[at], points[j]) > LegMax || !Clear(points[at], points[j], bySea, slowest))
					{
						break;
					}
					next = j;
				}
				AddLeg(road, points[at], points[next], leg);
				at = next;
			}
			return(road);
		}

		/// <summary>
		/// Adds a straight stretch to a road, in legs of at most LegMax
		/// </summary>
		/// <param name="road">The road so far, ending where the stretch starts</param>
		/// <param name="from">Where the stretch starts</param>
		/// <param name="to">Where it ends</param>
		/// <param name="leg">How it's crossed, on foot or by sea</param>
		private static void AddLeg(TravelRoad road, Vector3 from, Vector3 to, TravelLeg leg)
		{
			int legs = Mathf.Max(1, Mathf.CeilToInt(Flat(from, to) / LegMax));
			for (int i = 1; i <= legs; i++)
			{
				road.Add(Vector3.Lerp(from, to, (float)i / legs), leg);
			}
		}

		/// <summary>
		/// Checks whether a straight line crosses only squares that can be crossed the same way, on foot or by boat, and
		/// take no longer than a limit - on foot, no water, so a straightened road never swims more than the road it
		/// skips, and by boat, only the sea and water deep enough for a boat
		/// </summary>
		/// <param name="from">Where it starts</param>
		/// <param name="to">Where it ends</param>
		/// <param name="sea">Whether it's by sea</param>
		/// <param name="limit">The most seconds a meter of a square on it may take (see PerMeter)</param>
		/// <returns>True when it does</returns>
		private bool Clear(Vector3 from, Vector3 to, bool sea, float limit)
		{
			int samples = Mathf.CeilToInt(Flat(from, to) / (CellSize / 2f));
			for (int i = 1; i < samples; i++)
			{
				Vector3 point = Vector3.Lerp(from, to, (float)i / samples);
				int x = Index(point.x);
				int z = Index(point.z);
				long key = Key(x, z);
				Cell cell = CellAt(x, z);
				bool sailable = (cell.Sea || cell.Deep);
				bool walkable = (!cell.Sea && !cell.Water);
				if (!Crossable(key, x, z) || (!(sea ? sailable : walkable) && !goals.ContainsKey(key)) || PerMeter(cell, sea) > limit)
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Reads the square a point stands in
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>The square</returns>
		private Cell CellAtPoint(Vector3 point)
		{
			return(CellAt(Index(point.x), Index(point.z)));
		}

		/// <summary>
		/// Reads a square, sampling the map at its middle the first time
		/// </summary>
		/// <param name="x">Its x</param>
		/// <param name="z">Its z</param>
		/// <returns>The square</returns>
		private Cell CellAt(int x, int z)
		{
			long key = Key(x, z);
			if (!cells.TryGetValue(key, out Cell cell))
			{
				Vector3 middle = Middle(x, z);
				cell = sample(middle.x, middle.z);
				cells[key] = cell;
			}
			return(cell);
		}

		/// <summary>
		/// Finds the square a coordinate falls in, along one axis
		/// </summary>
		/// <param name="coordinate">The coordinate, in meters</param>
		/// <returns>The square's index along that axis</returns>
		private static int Index(float coordinate)
		{
			return(Mathf.FloorToInt(coordinate / CellSize));
		}

		/// <summary>
		/// Finds the middle of a square, at height 0
		/// </summary>
		/// <param name="x">Its x</param>
		/// <param name="z">Its z</param>
		/// <returns>The middle</returns>
		private static Vector3 Middle(int x, int z)
		{
			return(new Vector3((x + 0.5f) * CellSize, 0f, (z + 0.5f) * CellSize));
		}

		/// <summary>
		/// Packs a square's x and z into one key
		/// </summary>
		/// <param name="x">Its x</param>
		/// <param name="z">Its z</param>
		/// <returns>The key</returns>
		private static long Key(int x, int z)
		{
			return(((long)x << 32) | (uint)z);
		}

		/// <summary>
		/// Unpacks a square's x from its key
		/// </summary>
		/// <param name="key">The key</param>
		/// <returns>Its x</returns>
		private static int KeyX(long key)
		{
			return((int)(key >> 32));
		}

		/// <summary>
		/// Unpacks a square's z from its key
		/// </summary>
		/// <param name="key">The key</param>
		/// <returns>Its z</returns>
		private static int KeyZ(long key)
		{
			return((int)(key & 0xFFFFFFFFL));
		}

		/// <summary>
		/// Measures the distance between two points across the ground, leaving out height
		/// </summary>
		/// <param name="a">One point</param>
		/// <param name="b">The other</param>
		/// <returns>The distance, in meters</returns>
		private static float Flat(Vector3 a, Vector3 b)
		{
			float dx = a.x - b.x;
			float dz = a.z - b.z;
			return(Mathf.Sqrt(dx * dx + dz * dz));
		}

		/// <summary>
		/// The squares still to look at, quickest first - a binary heap
		/// </summary>
		private class CostHeap
		{
			private readonly List<float> costs = new List<float>();
			private readonly List<(long Cell, int Swum)> keys = new List<(long Cell, int Swum)>();

			/// <summary>
			/// How many squares it holds
			/// </summary>
			public int Count
			{
				get
				{
					return(costs.Count);
				}
			}

			/// <summary>
			/// Adds a square
			/// </summary>
			/// <param name="cost">The time of the road to it</param>
			/// <param name="key">The square's key, and how many squares of water in a row were swum to get there</param>
			public void Push(float cost, (long Cell, int Swum) key)
			{
				costs.Add(cost);
				keys.Add(key);
				int at = costs.Count - 1;
				while (at > 0)
				{
					int parent = (at - 1) / 2;
					if (costs[parent] <= costs[at])
					{
						break;
					}
					Swap(at, parent);
					at = parent;
				}
			}

			/// <summary>
			/// Takes out the quickest square
			/// </summary>
			/// <param name="cost">The time of the road to it</param>
			/// <param name="key">The square's key, and how many squares of water in a row were swum to get there</param>
			public void Pop(out float cost, out (long Cell, int Swum) key)
			{
				cost = costs[0];
				key = keys[0];
				int last = costs.Count - 1;
				Swap(0, last);
				costs.RemoveAt(last);
				keys.RemoveAt(last);
				int at = 0;
				while (true)
				{
					int left = at * 2 + 1;
					int right = left + 1;
					int least = at;
					if (left < costs.Count && costs[left] < costs[least])
					{
						least = left;
					}
					if (right < costs.Count && costs[right] < costs[least])
					{
						least = right;
					}
					if (least == at)
					{
						break;
					}
					Swap(at, least);
					at = least;
				}
			}

			/// <summary>
			/// Swaps two entries
			/// </summary>
			/// <param name="a">One entry's index</param>
			/// <param name="b">The other's</param>
			private void Swap(int a, int b)
			{
				float cost = costs[a];
				costs[a] = costs[b];
				costs[b] = cost;
				(long Cell, int Swum) key = keys[a];
				keys[a] = keys[b];
				keys[b] = key;
			}
		}
	}
}
