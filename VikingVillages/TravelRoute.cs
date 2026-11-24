using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Plans a viking's road over the world's map to whichever of a list of villages it can get to soonest - on foot
	/// over land and rivers, and by boat across the sea, never through lava or past the edge of the world - keeping out
	/// of biomes harder than its own where it can
	/// </summary>
	/// <remarks>
	/// The map is a grid of CellSize squares, each sampled once from the world's plan, so a road can be planned over
	/// ground no player has been near. The quickest road out from the start is followed until it reaches a village
	/// (Dijkstra's search), so the first village reached is the soonest there, not the nearest in a straight line. A step
	/// costs the time it takes - on foot at TravelTime.Speed, longer for each biome a square is harder than the viking's
	/// own (see HarderCost), and across the sea at its boat's speed. It runs a little at a time (see Step), so a long
	/// search doesn't hold up the game
	/// </remarks>
	internal class TravelRoute
	{
		/// <summary>
		/// What a square of the map holds - whether a viking can cross it, whether it's sea, crossed by boat, and how hard
		/// its biome is, by VikingGear's order
		/// </summary>
		internal struct Cell
		{
			public bool Open;
			public bool Sea;
			public int Biome;
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
		/// A square reached, with the quickest time of the road to it, the square it was reached from and whether it's
		/// done with
		/// </summary>
		private class Node
		{
			public float Cost;
			public long From;
			public bool Closed;
		}

		/// <summary>
		/// Reads a square of the map at a point - its x and z in world space
		/// </summary>
		private readonly System.Func<float, float, Cell> sample;

		/// <summary>
		/// The viking's own biome, by VikingGear's order, and how fast its boat goes, in meters a second
		/// </summary>
		private readonly int biome;
		private readonly float boatSpeed;

		/// <summary>
		/// Where the road starts and the villages it could lead to
		/// </summary>
		private readonly Vector3 start;
		private readonly List<Vector3> targets;

		/// <summary>
		/// The squares sampled, the squares reached, the squares to look at next by time, and the squares a village can be
		/// reached from, with which village
		/// </summary>
		private readonly Dictionary<long, Cell> cells = new Dictionary<long, Cell>();
		private readonly Dictionary<long, Node> nodes = new Dictionary<long, Node>();
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
		/// Sets up a search for the road to the village a viking can get to soonest
		/// </summary>
		/// <param name="from">Where the road starts</param>
		/// <param name="own">The viking's own biome, by VikingGear's order</param>
		/// <param name="boat">How fast its boat goes, in meters a second</param>
		/// <param name="villages">Where each village stands</param>
		/// <param name="map">Reads a square of the map at a point's x and z</param>
		public TravelRoute(Vector3 from, int own, float boat, List<Vector3> villages, System.Func<float, float, Cell> map)
		{
			start = from;
			biome = own;
			boatSpeed = Mathf.Max(0.1f, boat);
			targets = villages;
			sample = map;
			startKey = Key(Index(from.x), Index(from.z));
			for (int i = 0; i < targets.Count; i++)
			{
				AddGoal(i);
			}
			nodes[startKey] = new Node { Cost = 0f, From = startKey };
			open.Push(0f, startKey);
			if (goals.Count == 0)
			{
				Done = true;
			}
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
				open.Pop(out float cost, out long key);
				Node node = nodes[key];
				if (node.Closed || cost > node.Cost)
				{
					continue;
				}
				node.Closed = true;
				if (goals.TryGetValue(key, out int goal))
				{
					Finish(key, goal);
					break;
				}
				Expand(key, node);
			}
			return(Done);
		}

		/// <summary>
		/// Reaches out from a square to each neighbor that can be crossed - stepping diagonally only past two neighbors
		/// that can, so the road doesn't cut a corner of lava or the edge of the world
		/// </summary>
		/// <param name="key">The square</param>
		/// <param name="node">What's known of it</param>
		private void Expand(long key, Node node)
		{
			int x = KeyX(key);
			int z = KeyZ(key);
			float here = PerMeter(CellAt(x, z));
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
				float step = (diagonal ? CellSize * 1.41421356f : CellSize) * (here + PerMeter(CellAt(nx, nz))) / 2f;
				float cost = node.Cost + step;
				if (nodes.TryGetValue(next, out Node known) && (known.Closed || known.Cost <= cost))
				{
					continue;
				}
				nodes[next] = new Node { Cost = cost, From = key };
				open.Push(cost, next);
			}
		}

		/// <summary>
		/// Checks whether a square can be crossed - land, a river or the sea, or one a village can be reached from
		/// </summary>
		/// <param name="key">The square's key</param>
		/// <param name="x">Its x</param>
		/// <param name="z">Its z</param>
		/// <returns>True when it can</returns>
		private bool Crossable(long key, int x, int z)
		{
			return(key == startKey || goals.ContainsKey(key) || CellAt(x, z).Open);
		}

		/// <summary>
		/// Works out how long a meter of a square takes - by boat across the sea, and on foot over land, longer for each
		/// biome it's harder than the viking's own
		/// </summary>
		/// <param name="cell">The square</param>
		/// <returns>The seconds a meter takes</returns>
		private float PerMeter(Cell cell)
		{
			if (cell.Sea)
			{
				return(1f / boatSpeed);
			}
			return((1f + HarderCost * Mathf.Max(0, cell.Biome - biome)) / TravelTime.Speed);
		}

		/// <summary>
		/// Ends the search at a square a village is reached from - laying the road out from the start to the village
		/// </summary>
		/// <param name="key">The square</param>
		/// <param name="goal">The village's index</param>
		private void Finish(long key, int goal)
		{
			Done = true;
			Found = goal;
			List<long> path = new List<long>();
			for (long at = key; ; at = nodes[at].From)
			{
				path.Add(at);
				if (at == startKey)
				{
					break;
				}
			}
			path.Reverse();
			List<Vector3> points = new List<Vector3> { start };
			List<bool> sea = new List<bool> { false };
			for (int i = 1; i < path.Count; i++)
			{
				points.Add(Middle(KeyX(path[i]), KeyZ(path[i])));
				sea.Add(IsSea(path[i]));
			}
			points.Add(targets[goal]);
			sea.Add(IsSea(path[path.Count - 1]));
			Road = Straighten(points, sea);
			Length = Road.Meters(0, start);
			Seconds = Road.TotalSeconds(boatSpeed);
		}

		/// <summary>
		/// Checks whether a square of the road is sea
		/// </summary>
		/// <param name="key">The square's key</param>
		/// <returns>True when it is</returns>
		private bool IsSea(long key)
		{
			return(CellAt(KeyX(key), KeyZ(key)).Sea);
		}

		/// <summary>
		/// Straightens a road of square middles - from each point, on to the farthest point after it that can be reached in
		/// a straight line within LegMax, the same way, on foot or by sea, over squares that can be crossed that way and
		/// take no longer than the road it skips - and breaks any longer stretch into legs of at most LegMax
		/// </summary>
		/// <param name="points">The road, a point for each square</param>
		/// <param name="sea">Whether the leg to each point is by sea</param>
		/// <returns>The straightened road</returns>
		private TravelRoad Straighten(List<Vector3> points, List<bool> sea)
		{
			TravelRoad road = new TravelRoad();
			road.Add(points[0], false);
			int at = 0;
			while (at < points.Count - 1)
			{
				int next = at + 1;
				bool bySea = sea[next];
				float slowest = PerMeter(CellAtPoint(points[next]));
				for (int j = at + 2; j < points.Count; j++)
				{
					if (sea[j] != bySea)
					{
						break;
					}
					slowest = Mathf.Max(slowest, PerMeter(CellAtPoint(points[j - 1])));
					if (Flat(points[at], points[j]) > LegMax || !Clear(points[at], points[j], bySea, slowest))
					{
						break;
					}
					next = j;
				}
				AddLeg(road, points[at], points[next], bySea);
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
		/// <param name="sea">Whether it's by sea</param>
		private static void AddLeg(TravelRoad road, Vector3 from, Vector3 to, bool sea)
		{
			int legs = Mathf.Max(1, Mathf.CeilToInt(Flat(from, to) / LegMax));
			for (int i = 1; i <= legs; i++)
			{
				road.Add(Vector3.Lerp(from, to, (float)i / legs), sea);
			}
		}

		/// <summary>
		/// Checks whether a straight line crosses only squares that can be crossed the same way, on foot or by sea, and
		/// take no longer than a limit
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
				if (!Crossable(key, x, z) || (cell.Sea != sea && !goals.ContainsKey(key)) || PerMeter(cell) > limit)
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
			private readonly List<long> keys = new List<long>();

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
			/// <param name="key">The square's key</param>
			public void Push(float cost, long key)
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
			/// <param name="key">The square's key</param>
			public void Pop(out float cost, out long key)
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
				long key = keys[a];
				keys[a] = keys[b];
				keys[b] = key;
			}
		}
	}
}
