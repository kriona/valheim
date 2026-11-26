using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The grid each path is found on, steering around what blocks it (see Grid)
	/// </summary>
	internal static partial class VillagePaths
	{
		/// <summary>
		/// The ground across a village, in cells, with what's blocked and what costs more to walk through
		/// </summary>
		internal class Grid
		{
			private readonly Vector3 origin;
			private readonly float waterLevel;
			private readonly int width;
			private readonly int depth;
			private readonly float[] heights;
			private readonly bool[] blocked;
			private readonly bool[] wet;
			private readonly float[] extra;

			/// <summary>
			/// The cells under something a path steers around, the water a bridge can cross, and the water under a
			/// bridge already found
			/// </summary>
			private readonly bool[] solid;
			private readonly bool[] bridgeable;
			private readonly bool[] bridged;
			private readonly bool[] onPath;

			/// <summary>
			/// How far the ground at each cell was moved by flattening, in meters
			/// </summary>
			private readonly float[] changes;

			/// <summary>
			/// The widest water a bridge crosses, from bank to bank, in meters
			/// </summary>
			private readonly float spanMax;

			/// <summary>
			/// Reads the world's ground across an area, blocking the water, and the Ashlands' lava and the boiling sea
			/// around it as water too, for the bridges of a village's floor piece to cross (see BridgePieceAt)
			/// </summary>
			/// <param name="min">The area's lowest corner</param>
			/// <param name="max">The area's highest corner</param>
			/// <param name="farm">Where the village's farm stands</param>
			public Grid(Vector3 min, Vector3 max, Vector3 farm) : this(min, max, ZoneSystem.instance.m_waterLevel, (Vector3 point) => ZoneSystem.instance.GetGroundHeight(point), DangerousGround.IsDangerous, BridgeSpanOf(BridgePieceAt(farm)))
			{
			}

			/// <summary>
			/// Reads the ground across an area from the height of the ground at each cell, blocking the water, for wood
			/// bridges to cross
			/// </summary>
			/// <param name="min">The area's lowest corner</param>
			/// <param name="max">The area's highest corner</param>
			/// <param name="waterLevel">The water's height</param>
			/// <param name="groundHeight">The ground's height at a point</param>
			internal Grid(Vector3 min, Vector3 max, float waterLevel, System.Func<Vector3, float> groundHeight) : this(min, max, waterLevel, groundHeight, null, BridgeSpanMax)
			{
			}

			/// <summary>
			/// Reads the ground across an area from the height of the ground at each cell, blocking the water and the
			/// ground that burns whoever stands on it, which paths cross and bridge the way they do the water
			/// </summary>
			/// <param name="min">The area's lowest corner</param>
			/// <param name="max">The area's highest corner</param>
			/// <param name="waterLevel">The water's height</param>
			/// <param name="groundHeight">The ground's height at a point</param>
			/// <param name="dangerous">Whether standing on the ground at a point burns, or null when none does</param>
			/// <param name="spanMax">The widest water a bridge crosses, from bank to bank, in meters</param>
			internal Grid(Vector3 min, Vector3 max, float waterLevel, System.Func<Vector3, float> groundHeight, System.Func<Vector3, bool> dangerous, float spanMax)
			{
				this.waterLevel = waterLevel;
				this.spanMax = spanMax;
				// The cells sit on whole meters, where the terrain's vertices are
				origin = new Vector3(Mathf.Floor(min.x), 0f, Mathf.Floor(min.z));
				width = Mathf.CeilToInt((max.x - origin.x) / CellSize) + 1;
				depth = Mathf.CeilToInt((max.z - origin.z) / CellSize) + 1;
				heights = new float[width * depth];
				blocked = new bool[width * depth];
				wet = new bool[width * depth];
				solid = new bool[width * depth];
				bridgeable = new bool[width * depth];
				bridged = new bool[width * depth];
				extra = new float[width * depth];
				onPath = new bool[width * depth];
				changes = new float[width * depth];
				for (int z = 0; z < depth; z++)
				{
					for (int x = 0; x < width; x++)
					{
						int index = z * width + x;
						float height = groundHeight(Position(x, z, 0f));
						heights[index] = height;
						wet[index] = (height < waterLevel + WetHeight || (dangerous != null && dangerous(Position(x, z, height))));
						blocked[index] = wet[index];
					}
				}
			}

			/// <summary>
			/// Blocks the cells under what a path steers around and makes the cells close around it cost more, and makes
			/// the cells under clutter cost more
			/// </summary>
			/// <param name="blockers">The objects around the village</param>
			public void Mark(List<Blocker> blockers)
			{
				foreach (Blocker blocker in blockers)
				{
					if (blocker.Kind == Kind.Avoid && blocker.Colliders != null && blocker.Colliders.Count > 0)
					{
						MarkShape(blocker);
						continue;
					}
					float reach = blocker.Radius + ((blocker.Kind == Kind.Avoid) ? PathClearance : 0f);
					int minX = Mathf.Max(0, Mathf.FloorToInt((blocker.Center.x - reach - origin.x) / CellSize));
					int maxX = Mathf.Min(width - 1, Mathf.CeilToInt((blocker.Center.x + reach - origin.x) / CellSize));
					int minZ = Mathf.Max(0, Mathf.FloorToInt((blocker.Center.z - reach - origin.z) / CellSize));
					int maxZ = Mathf.Min(depth - 1, Mathf.CeilToInt((blocker.Center.z + reach - origin.z) / CellSize));
					for (int z = minZ; z <= maxZ; z++)
					{
						for (int x = minX; x <= maxX; x++)
						{
							if (Utils.DistanceXZ(Position(x, z, 0f), blocker.Center) > reach)
							{
								continue;
							}
							int index = z * width + x;
							if (blocker.Kind == Kind.Clear)
							{
								extra[index] = Mathf.Max(extra[index], ClutterCost);
							}
							else if (Utils.DistanceXZ(Position(x, z, 0f), blocker.Center) <= blocker.Radius)
							{
								blocked[index] = true;
								solid[index] = true;
							}
							else
							{
								extra[index] = Mathf.Max(extra[index], ClearanceCost);
							}
						}
					}
				}
			}

			/// <summary>
			/// Works out flatter ground for the village - each cell moved toward the blurred ground around it, pulled
			/// part of the way toward the village's average height, fully near the farm's middle and its houses and
			/// fading out away from them, and left alone right around building pieces and under water
			/// </summary>
			/// <param name="farm">The farm's middle</param>
			/// <param name="houses">The village's houses</param>
			/// <param name="blockers">The objects around the village</param>
			public void Flatten(Vector3 farm, List<House> houses, List<Blocker> blockers)
			{
				int count = width * depth;
				float[] weight = new float[count];
				double sum = 0d;
				double total = 0d;
				for (int z = 0; z < depth; z++)
				{
					for (int x = 0; x < width; x++)
					{
						int index = z * width + x;
						Vector3 point = Position(x, z, 0f);
						float w = Falloff(Utils.DistanceXZ(point, farm), FarmFlatCore, FarmFlatFade);
						foreach (House house in houses)
						{
							w = Mathf.Max(w, Falloff(Utils.DistanceXZ(point, house.Center), HouseFlatCore, HouseFlatFade));
						}
						float edge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(z, depth - 1 - z)) * CellSize;
						w *= Mathf.Clamp01(edge / EdgeFade);
						if (heights[index] < waterLevel)
						{
							w = 0f;
						}
						weight[index] = w;
						sum += heights[index] * w;
						total += w;
					}
				}
				if (total <= 0d)
				{
					return;
				}
				float mean = (float)(sum / total);
				float[] blurred = Blur(heights, Mathf.RoundToInt(FlatBlur / CellSize));
				float[] near = StructureDistance(blockers);
				for (int i = 0; i < count; i++)
				{
					float keep = Mathf.Clamp01((near[i] - StructureKeep) / StructureFade);
					float strength = weight[i] * keep * FlatStrength;
					if (strength <= 0f)
					{
						continue;
					}
					float target = Mathf.Lerp(blurred[i], mean, FlatMeanPull);
					float change = AboveWater(heights[i], Mathf.Clamp((target - heights[i]) * strength, -MaxFlatten, MaxFlatten), waterLevel);
					changes[i] = change;
					heights[i] += change;
				}
			}

			/// <summary>
			/// Raises a village's ground out of the water, as in the Swamp or the Plains by the sea - ground lower than
			/// WaterClearance above the water is raised to it, fully near the farm's middle and its houses and fading out
			/// away from them, so the raised ground slopes back down into the water, left alone right around building pieces
			/// the way flattening is, and by no more than MaxFlatten. A cell raised out of the water is no longer water for the paths, so they cross
			/// it rather than bridge it
			/// </summary>
			/// <param name="farm">The farm's middle</param>
			/// <param name="houses">The village's houses</param>
			/// <param name="blockers">The objects around the village</param>
			/// <returns>How many cells were raised</returns>
			public int RaiseOutOfWater(Vector3 farm, List<House> houses, List<Blocker> blockers)
			{
				float[] near = StructureDistance(blockers);
				int raised = 0;
				for (int z = 0; z < depth; z++)
				{
					for (int x = 0; x < width; x++)
					{
						int index = z * width + x;
						Vector3 point = Position(x, z, 0f);
						float w = Falloff(Utils.DistanceXZ(point, farm), FarmFlatCore, FarmFlatFade);
						foreach (House house in houses)
						{
							w = Mathf.Max(w, Falloff(Utils.DistanceXZ(point, house.Center), HouseFlatCore, HouseFlatFade));
						}
						float edge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(z, depth - 1 - z)) * CellSize;
						w *= Mathf.Clamp01(edge / EdgeFade) * Mathf.Clamp01((near[index] - StructureKeep) / StructureFade);
						float change = RaisedOutOfWater(heights[index], w, waterLevel);
						if (change <= 0f)
						{
							continue;
						}
						changes[index] += change;
						heights[index] += change;
						raised++;
						if (heights[index] >= waterLevel + WetHeight - RaisedSlack)
						{
							wet[index] = false;
							blocked[index] = false;
						}
					}
				}
				return(raised);
			}

			/// <summary>
			/// Works out how far a cell of a village's ground is raised out of the water (see RaiseOutOfWater) - up to
			/// WaterClearance above it, by the share it's raised by, and no more than MaxFlatten
			/// </summary>
			/// <param name="height">The ground's height</param>
			/// <param name="weight">How fully it's raised, from 1 near the farm and its houses to 0 away from them</param>
			/// <param name="waterLevel">The water's height</param>
			/// <returns>How far it's raised, or 0 when it's high enough already</returns>
			internal static float RaisedOutOfWater(float height, float weight, float waterLevel)
			{
				float target = waterLevel + WaterClearance;
				if (height >= target || weight <= 0f)
				{
					return(0f);
				}
				return(Mathf.Min((target - height) * Mathf.Clamp01(weight), MaxFlatten));
			}

			/// <summary>
			/// Keeps flattening from lowering dry ground into the water, as it could on low ground like the Plains' by the
			/// sea - ground lowered stops WaterClearance above the water, and ground already that low isn't lowered at all,
			/// though it can still be raised
			/// </summary>
			/// <param name="height">The ground's height</param>
			/// <param name="change">How far flattening would move it, in meters - lower for less than 0</param>
			/// <param name="waterLevel">The water's height</param>
			/// <returns>How far it's moved</returns>
			internal static float AboveWater(float height, float change, float waterLevel)
			{
				if (change >= 0f)
				{
					return(change);
				}
				return(Mathf.Max(change, Mathf.Min(0f, waterLevel + WaterClearance - height)));
			}

			/// <summary>
			/// Finds how far flattening moved the ground at a point
			/// </summary>
			/// <param name="point">The point</param>
			/// <param name="change">How far it moved, in meters</param>
			/// <returns>False when the point is off the grid</returns>
			public bool TryGetChange(Vector3 point, out float change)
			{
				int cell = Cell(point);
				change = ((cell >= 0) ? changes[cell] : 0f);
				return(cell >= 0);
			}

			/// <summary>
			/// Works out how strongly a spot is flattened by its distance from what it's flattened around
			/// </summary>
			/// <param name="distance">The distance, in meters</param>
			/// <param name="core">How far it's flattened fully</param>
			/// <param name="fade">How far it's flattened at all</param>
			/// <returns>From 1 for fully to 0 for not at all, easing between</returns>
			internal static float Falloff(float distance, float core, float fade)
			{
				if (distance <= core)
				{
					return(1f);
				}
				if (distance >= fade)
				{
					return(0f);
				}
				float t = 1f - (distance - core) / (fade - core);
				return(t * t * (3f - 2f * t));
			}

			/// <summary>
			/// Averages each cell's value with the cells around it, out to a reach
			/// </summary>
			/// <param name="values">The values</param>
			/// <param name="reach">How many cells out to average</param>
			/// <returns>The averaged values</returns>
			internal float[] Blur(float[] values, int reach)
			{
				// A running sum of every cell above and to the left of each, so any rectangle's sum takes four lookups
				double[] sums = new double[(width + 1) * (depth + 1)];
				for (int z = 0; z < depth; z++)
				{
					double row = 0d;
					for (int x = 0; x < width; x++)
					{
						row += values[z * width + x];
						sums[(z + 1) * (width + 1) + x + 1] = sums[z * (width + 1) + x + 1] + row;
					}
				}
				float[] blurred = new float[values.Length];
				for (int z = 0; z < depth; z++)
				{
					int z0 = Mathf.Max(0, z - reach);
					int z1 = Mathf.Min(depth - 1, z + reach) + 1;
					for (int x = 0; x < width; x++)
					{
						int x0 = Mathf.Max(0, x - reach);
						int x1 = Mathf.Min(width - 1, x + reach) + 1;
						double area = sums[z1 * (width + 1) + x1] - sums[z0 * (width + 1) + x1] - sums[z1 * (width + 1) + x0] + sums[z0 * (width + 1) + x0];
						blurred[z * width + x] = (float)(area / ((x1 - x0) * (z1 - z0)));
					}
				}
				return(blurred);
			}

			/// <summary>
			/// Measures how far each cell is from the nearest building piece, across the cells
			/// </summary>
			/// <param name="blockers">The objects around the village</param>
			/// <returns>The distances, in meters</returns>
			private float[] StructureDistance(List<Blocker> blockers)
			{
				float[] distance = new float[width * depth];
				for (int i = 0; i < distance.Length; i++)
				{
					distance[i] = float.MaxValue;
				}
				foreach (Blocker blocker in blockers)
				{
					if (!blocker.Structure)
					{
						continue;
					}
					int minX = Mathf.Max(0, Mathf.FloorToInt((blocker.Center.x - blocker.Radius - origin.x) / CellSize));
					int maxX = Mathf.Min(width - 1, Mathf.CeilToInt((blocker.Center.x + blocker.Radius - origin.x) / CellSize));
					int minZ = Mathf.Max(0, Mathf.FloorToInt((blocker.Center.z - blocker.Radius - origin.z) / CellSize));
					int maxZ = Mathf.Min(depth - 1, Mathf.CeilToInt((blocker.Center.z + blocker.Radius - origin.z) / CellSize));
					for (int z = minZ; z <= maxZ; z++)
					{
						for (int x = minX; x <= maxX; x++)
						{
							bool under = ((blocker.Colliders != null && blocker.Colliders.Count > 0) ? Gap(Position(x, z, 0f), blocker.Colliders) <= CellSize / 2f : Utils.DistanceXZ(Position(x, z, 0f), blocker.Center) <= blocker.Radius);
							if (under)
							{
								distance[z * width + x] = 0f;
							}
						}
					}
				}
				// Two sweeps, forward and back, carry each cell's distance to its neighbors
				float diagonal = 1.4142f * CellSize;
				for (int z = 0; z < depth; z++)
				{
					for (int x = 0; x < width; x++)
					{
						int index = z * width + x;
						if (x > 0)
						{
							distance[index] = Mathf.Min(distance[index], distance[index - 1] + CellSize);
						}
						if (z > 0)
						{
							distance[index] = Mathf.Min(distance[index], distance[index - width] + CellSize);
							if (x > 0)
							{
								distance[index] = Mathf.Min(distance[index], distance[index - width - 1] + diagonal);
							}
							if (x < width - 1)
							{
								distance[index] = Mathf.Min(distance[index], distance[index - width + 1] + diagonal);
							}
						}
					}
				}
				for (int z = depth - 1; z >= 0; z--)
				{
					for (int x = width - 1; x >= 0; x--)
					{
						int index = z * width + x;
						if (x < width - 1)
						{
							distance[index] = Mathf.Min(distance[index], distance[index + 1] + CellSize);
						}
						if (z < depth - 1)
						{
							distance[index] = Mathf.Min(distance[index], distance[index + width] + CellSize);
							if (x < width - 1)
							{
								distance[index] = Mathf.Min(distance[index], distance[index + width + 1] + diagonal);
							}
							if (x > 0)
							{
								distance[index] = Mathf.Min(distance[index], distance[index + width - 1] + diagonal);
							}
						}
					}
				}
				return(distance);
			}

			/// <summary>
			/// Blocks the cells under an object a path steers around by the true shape of its colliders, so a long thin
			/// piece like a fence or wall blocks only the cells it stands on and leaves the gaps between pieces open, and
			/// makes the cells close around it cost more
			/// </summary>
			/// <param name="blocker">The object</param>
			private void MarkShape(Blocker blocker)
			{
				float reach = blocker.Radius + PathClearance;
				int minX = Mathf.Max(0, Mathf.FloorToInt((blocker.Center.x - reach - origin.x) / CellSize));
				int maxX = Mathf.Min(width - 1, Mathf.CeilToInt((blocker.Center.x + reach - origin.x) / CellSize));
				int minZ = Mathf.Max(0, Mathf.FloorToInt((blocker.Center.z - reach - origin.z) / CellSize));
				int maxZ = Mathf.Min(depth - 1, Mathf.CeilToInt((blocker.Center.z + reach - origin.z) / CellSize));
				for (int z = minZ; z <= maxZ; z++)
				{
					for (int x = minX; x <= maxX; x++)
					{
						float gap = Gap(Position(x, z, 0f), blocker.Colliders);
						int index = z * width + x;
						if (gap <= CellSize / 2f)
						{
							blocked[index] = true;
							solid[index] = true;
						}
						else if (gap <= PathClearance)
						{
							extra[index] = Mathf.Max(extra[index], ClearanceCost);
						}
					}
				}
			}

			/// <summary>
			/// Measures how far a point is across the ground from the nearest of some colliders
			/// </summary>
			/// <remarks>
			/// The point is taken at each collider's middle height, so a wall or trunk is measured where it stands. A
			/// collider whose nearest point can't be found - a mesh that isn't convex - is measured by its bounds
			/// </remarks>
			/// <param name="point">The point</param>
			/// <param name="colliders">The colliders</param>
			/// <returns>The distance, 0 when the point is inside one</returns>
			private static float Gap(Vector3 point, List<Collider> colliders)
			{
				float nearest = float.MaxValue;
				foreach (Collider collider in colliders)
				{
					if (collider == null)
					{
						continue;
					}
					Vector3 probe = new Vector3(point.x, collider.bounds.center.y, point.z);
					MeshCollider mesh = collider as MeshCollider;
					Vector3 closest = ((mesh != null && !mesh.convex) ? collider.bounds.ClosestPoint(probe) : collider.ClosestPoint(probe));
					nearest = Mathf.Min(nearest, Utils.DistanceXZ(probe, closest));
				}
				return(nearest);
			}

			/// <summary>
			/// Notes the cells the paths were laid across, and the cells beside them the paint reaches
			/// </summary>
			/// <param name="path">Every path's points</param>
			public void MarkPath(List<Vector3> path)
			{
				int reach = Mathf.CeilToInt(PaintSpacing / CellSize);
				foreach (Vector3 point in path)
				{
					int cell = Cell(point);
					if (cell < 0)
					{
						continue;
					}
					int cx = cell % width;
					int cz = cell / width;
					for (int z = Mathf.Max(0, cz - reach); z <= Mathf.Min(depth - 1, cz + reach); z++)
					{
						for (int x = Mathf.Max(0, cx - reach); x <= Mathf.Min(width - 1, cx + reach); x++)
						{
							onPath[z * width + x] = true;
						}
					}
				}
			}

			/// <summary>
			/// Finds the open ground nearest a point with room all around it - nothing blocked, nothing too close to
			/// what paths steer around, no clutter, no path and no water within a radius, on ground that rises and falls
			/// no more than a limit
			/// </summary>
			/// <param name="near">The point</param>
			/// <param name="radius">How much room it needs around it, in meters</param>
			/// <param name="least">How far from the point it stands at the least, in meters</param>
			/// <param name="search">How far from the point to look, in meters</param>
			/// <param name="flatness">The most the ground there can rise and fall, in meters</param>
			/// <param name="spot">The spot found, on the ground</param>
			/// <param name="level">Halfway between the ground's highest and lowest points around the spot</param>
			/// <returns>False when there's no such spot that close</returns>
			public bool FindOpenSpot(Vector3 near, float radius, float least, float search, float flatness, out Vector3 spot, out float level)
			{
				spot = near;
				level = near.y;
				int center = Cell(near);
				if (center < 0)
				{
					return(false);
				}
				int reach = Mathf.CeilToInt(radius / CellSize);
				int limit = Mathf.CeilToInt(search / CellSize);
				int cx = center % width;
				int cz = center / width;
				List<int> candidates = new List<int>();
				for (int z = Mathf.Max(reach, cz - limit); z <= Mathf.Min(depth - 1 - reach, cz + limit); z++)
				{
					for (int x = Mathf.Max(reach, cx - limit); x <= Mathf.Min(width - 1 - reach, cx + limit); x++)
					{
						candidates.Add(z * width + x);
					}
				}
				candidates.Sort((a, b) => Distance(a, center).CompareTo(Distance(b, center)));
				foreach (int candidate in candidates)
				{
					if (Distance(candidate, center) * CellSize < least || !IsOpen(candidate, reach, flatness, out level))
					{
						continue;
					}
					// The open ground inside a pen is no place for a fire the whole village gathers at
					Vector3 found = Position(candidate % width, candidate / width, heights[candidate]);
					if (!Village.FencedIn(found))
					{
						spot = found;
						return(true);
					}
				}
				return(false);
			}

			/// <summary>
			/// Checks whether the ground around a cell is open and flat enough
			/// </summary>
			/// <param name="cell">The cell</param>
			/// <param name="reach">How many cells around it to check</param>
			/// <param name="flatness">The most the ground can rise and fall, in meters</param>
			/// <param name="level">Halfway between the ground's highest and lowest points</param>
			/// <returns>True when it is</returns>
			private bool IsOpen(int cell, int reach, float flatness, out float level)
			{
				int cx = cell % width;
				int cz = cell / width;
				float low = float.MaxValue;
				float high = float.MinValue;
				level = heights[cell];
				for (int z = cz - reach; z <= cz + reach; z++)
				{
					for (int x = cx - reach; x <= cx + reach; x++)
					{
						if ((x - cx) * (x - cx) + (z - cz) * (z - cz) > reach * reach)
						{
							continue;
						}
						int index = z * width + x;
						if (blocked[index] || extra[index] > 0f || onPath[index])
						{
							return(false);
						}
						low = Mathf.Min(low, heights[index]);
						high = Mathf.Max(high, heights[index]);
					}
				}
				level = (low + high) / 2f;
				return(high - low <= flatness);
			}

			/// <summary>
			/// Measures the distance between two cells
			/// </summary>
			/// <param name="a">One cell</param>
			/// <param name="b">The other</param>
			/// <returns>The distance, in cells</returns>
			private float Distance(int a, int b)
			{
				int dx = a % width - b % width;
				int dz = a / width - b / width;
				return(Mathf.Sqrt(dx * dx + dz * dz));
			}

			/// <summary>
			/// Finds the cheapest path between two houses, from the door of each nearest the other, or from the open
			/// ground nearest each toward the other when it has no door - through as little of what paths steer around
			/// and as little water as it can when there's no way around them
			/// </summary>
			/// <remarks>
			/// The path runs between the open cells just outside the doors, which are blocked like the rest of the
			/// house, then on up to each door's threshold so it's painted right to the door
			/// </remarks>
			/// <param name="from">The house it starts at</param>
			/// <param name="to">The house it ends at</param>
			/// <returns>The path's points on the ground, or null when there's no way through</returns>
			public List<Vector3> FindPath(House from, House to)
			{
				Vector3? fromDoor = NearestDoor(from, to.Center);
				Vector3? toDoor = NearestDoor(to, from.Center);
				int start = Doorstep(from, fromDoor, to.Center);
				int goal = Doorstep(to, toDoor, from.Center);
				if (start < 0 || goal < 0)
				{
					return(null);
				}
				List<Vector3> path = null;
				for (int i = 0; i < BridgeTries && path == null; i++)
				{
					path = Search(start, goal, false);
					if (path == null)
					{
						break;
					}
					if (ShutWideWater(path))
					{
						path = null;
					}
				}
				path = (path ?? Search(start, goal, true));
				if (path == null)
				{
					return(null);
				}
				if (fromDoor.HasValue)
				{
					path.Insert(0, OnGround(fromDoor.Value));
				}
				if (toDoor.HasValue)
				{
					path.Add(OnGround(toDoor.Value));
				}
				return(path);
			}

			/// <summary>
			/// Notes the water a bridge can cross - with nothing standing in it, and no farther from dry ground than half
			/// the widest a bridge spans
			/// </summary>
			public void MarkBridgeable()
			{
				int count = width * depth;
				float reach = spanMax / 2f;
				float[] distance = new float[count];
				Queue<int> queue = new Queue<int>();
				for (int i = 0; i < count; i++)
				{
					distance[i] = (wet[i] ? float.MaxValue : 0f);
					if (!wet[i])
					{
						queue.Enqueue(i);
					}
				}
				while (queue.Count > 0)
				{
					int current = queue.Dequeue();
					int cx = current % width;
					int cz = current / width;
					for (int dz = -1; dz <= 1; dz++)
					{
						for (int dx = -1; dx <= 1; dx++)
						{
							int nx = cx + dx;
							int nz = cz + dz;
							if ((dx == 0 && dz == 0) || nx < 0 || nz < 0 || nx >= width || nz >= depth)
							{
								continue;
							}
							int next = nz * width + nx;
							float through = distance[current] + ((dx != 0 && dz != 0) ? 1.4142f : 1f) * CellSize;
							if (wet[next] && through <= reach && through < distance[next])
							{
								distance[next] = through;
								queue.Enqueue(next);
							}
						}
					}
				}
				for (int i = 0; i < count; i++)
				{
					bridgeable[i] = (wet[i] && !solid[i] && distance[i] <= reach);
				}
			}

			/// <summary>
			/// Finds the bridges a path needs over the water it crosses - one from bank to bank over each stretch of
			/// water no bridge found before crosses, when its banks are from BridgeSpanMin to spanMax apart, it's no
			/// steeper than BridgeSlopeMax and has nothing standing in the way - and notes the water under them, so the
			/// paths found after cross on them
			/// </summary>
			/// <param name="path">The path's points on the ground</param>
			/// <returns>The bridges</returns>
			public List<Bridge> FindBridges(List<Vector3> path)
			{
				List<Bridge> bridges = new List<Bridge>();
				foreach (KeyValuePair<int, int> run in WaterRuns(path))
				{
					Vector3 start = path[run.Key - 1];
					Vector3 end = path[run.Value + 1];
					float span = Utils.DistanceXZ(start, end);
					if (span < BridgeSpanMin || span > spanMax || Mathf.Atan2(Mathf.Abs(end.y - start.y), span) * Mathf.Rad2Deg > BridgeSlopeMax)
					{
						continue;
					}
					bool crossed = true;
					for (int i = run.Key; i <= run.Value && crossed; i++)
					{
						int cell = Cell(path[i]);
						crossed = (cell < 0 || !wet[cell] || bridged[cell]);
					}
					if (crossed || Crosses(start, end, solid))
					{
						continue;
					}
					Bridge bridge = new Bridge();
					bridge.Start = start;
					bridge.End = end;
					bridges.Add(bridge);
					MarkBridged(start, end);
				}
				return(bridges);
			}

			/// <summary>
			/// Keeps paths off water too wide to bridge that a path crosses, and the water right beside it
			/// </summary>
			/// <param name="path">The path's points on the ground</param>
			/// <returns>True when the path crosses such water, so it's looked for again</returns>
			private bool ShutWideWater(List<Vector3> path)
			{
				bool shut = false;
				foreach (KeyValuePair<int, int> run in WaterRuns(path))
				{
					if (Utils.DistanceXZ(path[run.Key - 1], path[run.Value + 1]) <= spanMax)
					{
						continue;
					}
					for (int i = run.Key; i <= run.Value; i++)
					{
						int cell = Cell(path[i]);
						if (cell < 0)
						{
							continue;
						}
						int cx = cell % width;
						int cz = cell / width;
						for (int z = Mathf.Max(0, cz - 1); z <= Mathf.Min(depth - 1, cz + 1); z++)
						{
							for (int x = Mathf.Max(0, cx - 1); x <= Mathf.Min(width - 1, cx + 1); x++)
							{
								bridgeable[z * width + x] = false;
							}
						}
					}
					shut = true;
				}
				return(shut);
			}

			/// <summary>
			/// Finds the stretches of water a path crosses with dry ground on both sides
			/// </summary>
			/// <param name="path">The path's points on the ground</param>
			/// <returns>The first and last point in the water of each stretch</returns>
			internal List<KeyValuePair<int, int>> WaterRuns(List<Vector3> path)
			{
				List<KeyValuePair<int, int>> runs = new List<KeyValuePair<int, int>>();
				int first = -1;
				for (int i = 0; i < path.Count; i++)
				{
					int cell = Cell(path[i]);
					bool inWater = (cell >= 0 && wet[cell]);
					if (inWater && first < 0)
					{
						first = i;
					}
					else if (!inWater && first >= 0)
					{
						if (first > 0)
						{
							runs.Add(new KeyValuePair<int, int>(first, i - 1));
						}
						first = -1;
					}
				}
				return(runs);
			}

			/// <summary>
			/// Checks whether a straight line crosses any of a set of cells
			/// </summary>
			/// <param name="start">Where the line starts</param>
			/// <param name="end">Where it ends</param>
			/// <param name="cells">The cells</param>
			/// <returns>True when it does</returns>
			private bool Crosses(Vector3 start, Vector3 end, bool[] cells)
			{
				int steps = Mathf.CeilToInt(Utils.DistanceXZ(start, end) / (CellSize / 2f));
				for (int i = 0; i <= steps; i++)
				{
					int cell = Cell(Vector3.Lerp(start, end, (steps > 0) ? i / (float)steps : 0f));
					if (cell >= 0 && cells[cell])
					{
						return(true);
					}
				}
				return(false);
			}

			/// <summary>
			/// Notes the water under a bridge and beside it, as wide as the bridge, so the paths found after cross on it
			/// </summary>
			/// <param name="start">Where the bridge starts</param>
			/// <param name="end">Where it ends</param>
			private void MarkBridged(Vector3 start, Vector3 end)
			{
				int steps = Mathf.CeilToInt(Utils.DistanceXZ(start, end) / (CellSize / 2f));
				for (int i = 0; i <= steps; i++)
				{
					int cell = Cell(Vector3.Lerp(start, end, (steps > 0) ? i / (float)steps : 0f));
					if (cell < 0)
					{
						continue;
					}
					int cx = cell % width;
					int cz = cell / width;
					for (int z = Mathf.Max(0, cz - 1); z <= Mathf.Min(depth - 1, cz + 1); z++)
					{
						for (int x = Mathf.Max(0, cx - 1); x <= Mathf.Min(width - 1, cx + 1); x++)
						{
							int index = z * width + x;
							if (wet[index])
							{
								bridged[index] = true;
							}
						}
					}
				}
			}

			/// <summary>
			/// Checks whether a path can step into a cell without being forced - open ground, water a bridge can cross,
			/// or water under a bridge already found
			/// </summary>
			/// <param name="cell">The cell</param>
			/// <returns>True when it can</returns>
			private bool Passable(int cell)
			{
				return(!blocked[cell] || bridged[cell] || bridgeable[cell]);
			}

			/// <summary>
			/// Works out the extra cost of a step into a cell for what's there - nothing for open ground or a bridge
			/// already found, BridgeCost for water a bridge can cross, WetCost for other water and ForcedCost for
			/// something a path steers around
			/// </summary>
			/// <param name="cell">The cell</param>
			/// <returns>The extra cost, for each meter</returns>
			private float StepCost(int cell)
			{
				if (!blocked[cell] || bridged[cell])
				{
					return(0f);
				}
				if (bridgeable[cell])
				{
					return(BridgeCost);
				}
				return(wet[cell] ? WetCost : ForcedCost);
			}

			/// <summary>
			/// Finds a house's door nearest a point
			/// </summary>
			/// <param name="house">The house</param>
			/// <param name="toward">The point</param>
			/// <returns>Where the door stands, or null when the house has none</returns>
			private static Vector3? NearestDoor(House house, Vector3 toward)
			{
				if (house.Doors == null || house.Doors.Count == 0)
				{
					return(null);
				}
				Vector3 nearest = house.Doors[0];
				foreach (Vector3 door in house.Doors)
				{
					if (Utils.DistanceXZ(door, toward) < Utils.DistanceXZ(nearest, toward))
					{
						nearest = door;
					}
				}
				return(nearest);
			}

			/// <summary>
			/// Puts a point on the ground of the grid
			/// </summary>
			/// <param name="point">The point</param>
			/// <returns>The point at the ground's height</returns>
			private Vector3 OnGround(Vector3 point)
			{
				int cell = Cell(point);
				point.y = ((cell >= 0) ? heights[cell] : ZoneSystem.instance.GetGroundHeight(point));
				return(point);
			}

			/// <summary>
			/// Finds the cheapest way across the grid between two cells
			/// </summary>
			/// <param name="start">The cell it starts at</param>
			/// <param name="goal">The cell it ends at</param>
			/// <param name="forced">True to step through blocked cells, at ForcedCost, or WetCost in water no bridge
			/// crosses</param>
			/// <returns>The way's points on the ground, or null when there's no way through</returns>
			internal List<Vector3> Search(int start, int goal, bool forced)
			{
				float[] cost = new float[width * depth];
				int[] came = new int[width * depth];
				bool[] closed = new bool[width * depth];
				for (int i = 0; i < cost.Length; i++)
				{
					cost[i] = float.MaxValue;
					came[i] = -1;
				}
				cost[start] = 0f;
				SortedSet<KeyValuePair<float, int>> open = new SortedSet<KeyValuePair<float, int>>(Comparer<KeyValuePair<float, int>>.Create((a, b) => ((a.Key != b.Key) ? a.Key.CompareTo(b.Key) : a.Value.CompareTo(b.Value))));
				open.Add(new KeyValuePair<float, int>(Estimate(start, goal), start));
				while (open.Count > 0)
				{
					KeyValuePair<float, int> best = open.Min;
					open.Remove(best);
					int current = best.Value;
					if (current == goal)
					{
						return(Trace(came, goal));
					}
					if (closed[current])
					{
						continue;
					}
					closed[current] = true;
					int cx = current % width;
					int cz = current / width;
					for (int dz = -1; dz <= 1; dz++)
					{
						for (int dx = -1; dx <= 1; dx++)
						{
							int nx = cx + dx;
							int nz = cz + dz;
							if ((dx == 0 && dz == 0) || nx < 0 || nz < 0 || nx >= width || nz >= depth)
							{
								continue;
							}
							int next = nz * width + nx;
							if (closed[next] || (!forced && !Passable(next)))
							{
								continue;
							}
							// A diagonal step can't cut the corner of a blocked cell
							if (!forced && dx != 0 && dz != 0 && (!Passable(cz * width + nx) || !Passable(nz * width + cx)))
							{
								continue;
							}
							float through = StepCost(next);
							float step = ((dx != 0 && dz != 0) ? 1.4142f : 1f) * CellSize;
							float climb = Mathf.Abs(heights[next] - heights[current]);
							float total = cost[current] + step * (1f + extra[next] + through) + climb * SlopeCost;
							if (total < cost[next])
							{
								cost[next] = total;
								came[next] = current;
								open.Add(new KeyValuePair<float, int>(total + Estimate(next, goal), next));
							}
						}
					}
				}
				return(null);
			}

			/// <summary>
			/// Finds the open cell a path leaves a house from - the first one out from its door, or without a door the
			/// one just outside its walls toward where the path heads, or else the nearest open cell to the house
			/// </summary>
			/// <param name="house">The house</param>
			/// <param name="door">Where its door stands, or null</param>
			/// <param name="toward">Where the path heads</param>
			/// <returns>The cell's index, or -1 when there's none</returns>
			private int Doorstep(House house, Vector3? door, Vector3 toward)
			{
				if (door.HasValue)
				{
					Vector3 away = door.Value - house.Center;
					away.y = 0f;
					if (away.sqrMagnitude > 0.01f)
					{
						away.Normalize();
						for (float distance = 0f; distance <= DoorstepReach; distance += CellSize / 2f)
						{
							int cell = Cell(door.Value + away * distance);
							if (cell >= 0 && !blocked[cell])
							{
								return(cell);
							}
						}
					}
				}
				Vector3 outward = toward - house.Center;
				outward.y = 0f;
				if (outward.sqrMagnitude > 0.01f)
				{
					outward.Normalize();
					for (float distance = house.HalfSize; distance <= house.HalfSize + Margin; distance += CellSize)
					{
						int cell = Cell(house.Center + outward * distance);
						if (cell >= 0 && !blocked[cell])
						{
							return(cell);
						}
					}
				}
				int nearest = -1;
				float nearestDistance = float.MaxValue;
				for (int i = 0; i < blocked.Length; i++)
				{
					if (blocked[i])
					{
						continue;
					}
					float distance = Utils.DistanceXZ(Position(i % width, i / width, 0f), house.Center);
					if (distance < nearestDistance)
					{
						nearest = i;
						nearestDistance = distance;
					}
				}
				return(nearest);
			}

			/// <summary>
			/// Estimates the cost left from a cell to the goal, as the straight distance between them
			/// </summary>
			/// <param name="cell">The cell</param>
			/// <param name="goal">The goal's cell</param>
			/// <returns>The estimate</returns>
			private float Estimate(int cell, int goal)
			{
				int dx = Mathf.Abs(cell % width - goal % width);
				int dz = Mathf.Abs(cell / width - goal / width);
				return((Mathf.Max(dx, dz) + 0.4142f * Mathf.Min(dx, dz)) * CellSize);
			}

			/// <summary>
			/// Follows a found path back from its goal to its start
			/// </summary>
			/// <param name="came">The cell each cell was reached from</param>
			/// <param name="goal">The goal's cell</param>
			/// <returns>The path's points on the ground, from start to goal</returns>
			private List<Vector3> Trace(int[] came, int goal)
			{
				List<Vector3> path = new List<Vector3>();
				for (int cell = goal; cell >= 0; cell = came[cell])
				{
					path.Add(Position(cell % width, cell / width, heights[cell]));
				}
				path.Reverse();
				return(path);
			}

			/// <summary>
			/// Finds the cell a point is in
			/// </summary>
			/// <param name="point">The point</param>
			/// <returns>The cell's index, or -1 off the grid</returns>
			internal int Cell(Vector3 point)
			{
				int x = Mathf.RoundToInt((point.x - origin.x) / CellSize);
				int z = Mathf.RoundToInt((point.z - origin.z) / CellSize);
				return((x < 0 || z < 0 || x >= width || z >= depth) ? -1 : z * width + x);
			}

			/// <summary>
			/// Works out where a cell is in the world
			/// </summary>
			/// <param name="x">The cell's column</param>
			/// <param name="z">The cell's row</param>
			/// <param name="height">The height to give it</param>
			/// <returns>The cell's middle</returns>
			private Vector3 Position(int x, int z, float height)
			{
				return(new Vector3(origin.x + x * CellSize, height, origin.z + z * CellSize));
			}
		}
	}
}
