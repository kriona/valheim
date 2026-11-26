using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Laying the paths - joining the houses by the shortest set of paths, finding what blocks them, painting them
	/// with the hoe's path and clearing the clutter and wilds on them
	/// </summary>
	internal static partial class VillagePaths
	{
		/// <summary>
		/// Finds the clutter a path clears lying at a spot - branches, stones, small trees and small rocks
		/// </summary>
		/// <param name="blockers">The objects around the path</param>
		/// <param name="spot">The spot</param>
		/// <returns>The clutter</returns>
		private static List<ZNetView> ClutterAt(List<Blocker> blockers, Vector3 spot)
		{
			List<ZNetView> clutter = new List<ZNetView>();
			foreach (Blocker blocker in blockers)
			{
				if (blocker.Kind == Kind.Clear && blocker.View != null && Utils.DistanceXZ(spot, blocker.Center) <= blocker.Radius + ClearRadius)
				{
					clutter.Add(blocker.View);
				}
			}
			return(clutter);
		}

		/// <summary>
		/// Checks whether every zone across an area has loaded
		/// </summary>
		/// <param name="min">The area's lowest corner</param>
		/// <param name="max">The area's highest corner</param>
		/// <returns>True when they all have</returns>
		private static bool AreaLoaded(Vector3 min, Vector3 max)
		{
			for (float x = min.x; x <= max.x + ZoneCheckSpacing; x += ZoneCheckSpacing)
			{
				for (float z = min.z; z <= max.z + ZoneCheckSpacing; z += ZoneCheckSpacing)
				{
					if (!ZoneSystem.instance.IsZoneLoaded(new Vector3(Mathf.Min(x, max.x), 0f, Mathf.Min(z, max.z))))
					{
						return(false);
					}
				}
			}
			return(true);
		}

		/// <summary>
		/// Lists the straight lines a village's paths run between its houses, for how far the village reaches (see
		/// Village.Zones) until its paths have been traced on the ground (see VillageGroundPaths) - the houses with a
		/// path joined the way the paths are laid between them (see SpanningTree): a
		/// farm's own houses once its paths are laid, the houses added for players' beds once theirs is made, and a
		/// village a player started's fire, which its houses' paths lead back to
		/// </summary>
		/// <remarks>
		/// The paths themselves wind around what stands in the way and aren't kept, so their lines stand in for them
		/// </remarks>
		/// <param name="village">The village, its houses read</param>
		/// <returns>Each line's ends</returns>
		public static List<KeyValuePair<Vector3, Vector3>> PathLines(Village village)
		{
			List<KeyValuePair<Vector3, Vector3>> lines = new List<KeyValuePair<Vector3, Vector3>>();
			if (!Laid(village.Zdo))
			{
				return(lines);
			}
			bool founded = village.IsFounded();
			List<House> pathed = PathedHouses(village.Zdo);
			List<House> joined = village.Houses.FindAll((House house) => (VillageBeds.IsAdded(village, house) ? HasHouse(pathed, house) : !founded));
			if (founded && joined.Count > 0)
			{
				joined.Insert(0, new House { Center = village.Farm });
			}
			if (joined.Count < 2)
			{
				return(lines);
			}
			foreach (KeyValuePair<int, int> edge in SpanningTree(joined))
			{
				lines.Add(new KeyValuePair<Vector3, Vector3>(joined[edge.Key].Center, joined[edge.Value].Center));
			}
			return(lines);
		}

		/// <summary>
		/// Joins the houses with the shortest set of straight lines across the ground that connects them all, starting
		/// from the first
		/// </summary>
		/// <param name="houses">The houses</param>
		/// <returns>The pairs of houses to join, by index</returns>
		internal static List<KeyValuePair<int, int>> SpanningTree(List<House> houses)
		{
			List<KeyValuePair<int, int>> edges = new List<KeyValuePair<int, int>>();
			HashSet<int> joined = new HashSet<int> { 0 };
			while (joined.Count < houses.Count)
			{
				int bestFrom = -1;
				int bestTo = -1;
				float bestDistance = float.MaxValue;
				foreach (int from in joined)
				{
					for (int to = 0; to < houses.Count; to++)
					{
						if (joined.Contains(to))
						{
							continue;
						}
						float distance = Utils.DistanceXZ(houses[from].Center, houses[to].Center);
						if (distance < bestDistance)
						{
							bestFrom = from;
							bestTo = to;
							bestDistance = distance;
						}
					}
				}
				// The houses left have no distance to measure to them - a middle that isn't a number or is infinitely
				// far - so they're left out
				if (bestTo < 0)
				{
					break;
				}
				joined.Add(bestTo);
				edges.Add(new KeyValuePair<int, int>(bestFrom, bestTo));
			}
			return(edges);
		}

		/// <summary>
		/// Finds the loaded objects across an area that a path steers around or clears
		/// </summary>
		/// <param name="min">The area's lowest corner</param>
		/// <param name="max">The area's highest corner</param>
		/// <returns>The objects</returns>
		private static List<Blocker> FindBlockers(Vector3 min, Vector3 max)
		{
			List<Blocker> blockers = new List<Blocker>();
			foreach (ZNetView view in ZNetScene.instance.m_instances.Values)
			{
				if (view == null)
				{
					continue;
				}
				Vector3 position = view.transform.position;
				if (position.x < min.x || position.x > max.x || position.z < min.z || position.z > max.z)
				{
					continue;
				}
				Bounds bounds;
				List<Collider> colliders = new List<Collider>();
				if (!Measure(view.gameObject, out bounds, colliders))
				{
					continue;
				}
				// An oak is measured by its trunk alone - its branches spread far wider, and the vikings walk under them
				bool oak = VillageOaks.IsOak(view);
				Blocker blocker = new Blocker();
				blocker.View = view;
				blocker.Colliders = (oak ? new List<Collider>() : colliders);
				blocker.Center = (oak ? position : bounds.center);
				blocker.Radius = (oak ? VillageOaks.TrunkRadius * view.transform.localScale.x : Mathf.Max(bounds.extents.x, bounds.extents.z));
				blocker.Kind = Classify(view, blocker.Radius * 2f);
				blocker.Structure = (blocker.Kind == Kind.Avoid && (oak || view.GetComponent<WearNTear>() != null));
				if (blocker.Kind != Kind.Ignore)
				{
					blockers.Add(blocker);
				}
			}
			return(blockers);
		}

		/// <summary>
		/// Measures an object by its solid colliders, or by what's drawn of it when it has none
		/// </summary>
		/// <param name="target">The object</param>
		/// <param name="bounds">How far it reaches</param>
		/// <param name="colliders">Its solid colliders, filled in</param>
		/// <returns>False when it has nothing to measure</returns>
		private static bool Measure(GameObject target, out Bounds bounds, List<Collider> colliders)
		{
			bounds = new Bounds();
			bool found = false;
			foreach (Collider collider in target.GetComponentsInChildren<Collider>())
			{
				if (collider.isTrigger || !collider.enabled)
				{
					continue;
				}
				colliders.Add(collider);
				if (found)
				{
					bounds.Encapsulate(collider.bounds);
				}
				else
				{
					bounds = collider.bounds;
					found = true;
				}
			}
			if (found)
			{
				return(true);
			}
			foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
			{
				if (renderer is ParticleSystemRenderer)
				{
					continue;
				}
				if (found)
				{
					bounds.Encapsulate(renderer.bounds);
				}
				else
				{
					bounds = renderer.bounds;
					found = true;
				}
			}
			return(found);
		}

		/// <summary>
		/// Decides whether a path steers around an object, clears it or ignores it
		/// </summary>
		/// <param name="view">The object</param>
		/// <param name="size">How far across it is</param>
		/// <returns>What the path does about it</returns>
		private static Kind Classify(ZNetView view, float size)
		{
			GameObject target = view.gameObject;
			// A location's proxy and generator hold the whole location, which is measured piece by piece instead
			if (size > MaxSize || target.GetComponent<Character>() != null || target.GetComponent<ItemDrop>() != null || target.GetComponent<Ragdoll>() != null || target.GetComponent<DungeonGenerator>() != null || target.GetComponent<LocationProxy>() != null || target.GetComponent<TerrainComp>() != null)
			{
				return(Kind.Ignore);
			}
			string name = Utils.GetPrefabName(target);
			string lower = name.ToLowerInvariant();
			if (target.GetComponent<WearNTear>() != null)
			{
				return(Kind.Avoid);
			}
			if (Contains(lower, BushNames))
			{
				return(Kind.Avoid);
			}
			if (target.GetComponent<Pickable>() != null)
			{
				return(ClutterPickables.Contains(name) ? Kind.Clear : Kind.Ignore);
			}
			// Grown trees are TreeBase, while small trees, saplings and stumps are Destructible like rocks
			if (target.GetComponent<TreeBase>() != null)
			{
				return(Kind.Avoid);
			}
			if (target.GetComponent<MineRock>() != null || target.GetComponent<MineRock5>() != null)
			{
				return((size <= SmallSize) ? Kind.Clear : Kind.Avoid);
			}
			if (target.GetComponent<Destructible>() != null)
			{
				return((size <= SmallSize) ? Kind.Clear : Kind.Avoid);
			}
			return(Kind.Avoid);
		}

		/// <summary>
		/// Checks whether a name holds any of some parts
		/// </summary>
		/// <param name="name">The name, in lowercase</param>
		/// <param name="parts">The parts, in lowercase</param>
		/// <returns>True when it holds one</returns>
		private static bool Contains(string name, string[] parts)
		{
			foreach (string part in parts)
			{
				if (name.Contains(part))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Paints the hoe's path along a path, leaving out where it crosses water
		/// </summary>
		/// <param name="path">The path's points</param>
		private static void Paint(List<Vector3> path)
		{
			// The hoe's terrain pieces are kept in ObjectDB rather than among the networked prefabs
			if (ObjectDB.instance == null || !ObjectDB.instance.TryGetTerrainOp(PathPiece, out TerrainOp op))
			{
				Plugin.Log.LogWarning("Couldn't find the hoe's path to paint village paths with");
				return;
			}
			GameObject piece = op.gameObject;
			if (path.Count == 0)
			{
				return;
			}
			foreach (int i in PaintSteps(path, PaintSpacing, ZoneSystem.instance.m_waterLevel + WetHeight))
			{
				Object.Instantiate(piece, path[i], Quaternion.identity);
			}
		}

		/// <summary>
		/// Picks the points along a path the hoe's path is painted at - the first out of the water, then each at least a
		/// spacing across the ground from the last painted, and the last - leaving out those under water, though the
		/// distance across them still counts
		/// </summary>
		/// <param name="path">The path's points</param>
		/// <param name="spacing">How far apart the painted points are, in meters</param>
		/// <param name="water">The height a point under water is below</param>
		/// <returns>The painted points' places in the path, in order</returns>
		internal static List<int> PaintSteps(List<Vector3> path, float spacing, float water)
		{
			List<int> steps = new List<int>();
			float travelled = spacing;
			for (int i = 0; i < path.Count; i++)
			{
				if (i > 0)
				{
					travelled += Utils.DistanceXZ(path[i - 1], path[i]);
				}
				if (path[i].y < water)
				{
					continue;
				}
				if (travelled >= spacing || i == path.Count - 1)
				{
					travelled = 0f;
					steps.Add(i);
				}
			}
			return(steps);
		}

		/// <summary>
		/// Paints the ground under each floor piece of the village's houses the way the hoe's path does, so no grass
		/// grows through the floors - leaving the ground where it is
		/// </summary>
		/// <param name="houses">The village's houses</param>
		/// <param name="blockers">The objects around the village</param>
		/// <returns>How many floor pieces it painted under</returns>
		private static int PaintFloors(List<House> houses, List<Blocker> blockers)
		{
			if (floorOp == null)
			{
				Plugin.Log.LogWarning("Couldn't find the hoe's path to paint under the village's houses with");
				return(0);
			}
			int painted = 0;
			foreach (Blocker blocker in blockers)
			{
				if (!blocker.Structure || blocker.View == null || !blocker.View.IsValid())
				{
					continue;
				}
				string name = Utils.GetPrefabName(blocker.View.gameObject);
				if (name.IndexOf(FloorName, System.StringComparison.OrdinalIgnoreCase) < 0 || !houses.Exists((House house) => Village.InHouse(house, blocker.Center, 1f)))
				{
					continue;
				}
				// A floor piece is up to 2 meters square, which one paint just covers to its corners
				Vector3 point = blocker.Center;
				point.y = ZoneSystem.instance.GetGroundHeight(point);
				Object.Instantiate(floorOp, point, Quaternion.identity);
				painted++;
			}
			return(painted);
		}

		/// <summary>
		/// Works out how far from a farm's middle the trees, bushes, rocks and logs are cleared - FarmClearMargin past
		/// its fence, or past the largest fence a farm gets when its generator didn't note its own
		/// </summary>
		/// <param name="generator">The farm's generator's ZDO</param>
		/// <returns>The distance, in meters</returns>
		private static float FarmClearRadius(ZDO generator)
		{
			return(generator.GetFloat(VillageFarmhouses.FenceKey, VillageFarmhouses.FenceRadiusMax) + FarmClearMargin);
		}

		/// <summary>
		/// Removes the trees, bushes, rocks and logs the world grew within a distance of a farm's middle - in its own
		/// zone and the zones around it, which its widened fields, pens and houses reach into - leaving what's picked,
		/// like branches, stones and berries, and the oaks, which the village is built around (see VillageOaks)
		/// </summary>
		/// <remarks>
		/// A location only clears what grows in its own zone, as it's generated, so this is done once everything around
		/// the village has loaded, whichever zone was generated first
		/// </remarks>
		/// <param name="farm">The farm's generator</param>
		/// <param name="radius">How far from its middle to clear, in meters</param>
		/// <returns>How many things were cleared</returns>
		private static int ClearWilds(Vector3 farm, float radius)
		{
			HashSet<int> wilds = new HashSet<int>();
			foreach (ZoneSystem.ZoneVegetation vegetation in ZoneSystem.instance.m_vegetation)
			{
				if (vegetation.m_prefab != null)
				{
					wilds.Add(vegetation.m_prefab.name.GetStableHashCode());
				}
			}
			List<ZNetView> found = new List<ZNetView>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZNetView view = instance.Value;
				if (view == null || !view.IsValid() || !wilds.Contains(instance.Key.GetPrefab()) || view.GetComponent<Pickable>() != null || VillageOaks.IsOak(view))
				{
					continue;
				}
				if (Utils.DistanceXZ(view.transform.position, farm) <= radius)
				{
					found.Add(view);
				}
			}
			foreach (ZNetView view in found)
			{
				view.ClaimOwnership();
				ZNetScene.instance.Destroy(view.gameObject);
			}
			return(found.Count);
		}

		/// <summary>
		/// Removes the clutter lying on the paths - branches, stones, small trees and small rocks
		/// </summary>
		/// <param name="blockers">The objects around the village</param>
		/// <param name="path">Every path's points</param>
		/// <returns>How many things were cleared</returns>
		private static int Clear(List<Blocker> blockers, List<Vector3> path)
		{
			int cleared = 0;
			foreach (Blocker blocker in blockers)
			{
				if (blocker.Kind != Kind.Clear || blocker.View == null || !blocker.View.IsValid())
				{
					continue;
				}
				float reach = blocker.Radius + ClearRadius;
				foreach (Vector3 point in path)
				{
					if (Utils.DistanceXZ(point, blocker.Center) <= reach)
					{
						blocker.View.ClaimOwnership();
						ZNetScene.instance.Destroy(blocker.View.gameObject);
						cleared++;
						break;
					}
				}
			}
			return(cleared);
		}
	}
}
