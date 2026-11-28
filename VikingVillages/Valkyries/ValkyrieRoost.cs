using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Where a valkyrie roosts in a village - on a roof flat and wide enough for her to stand on - and where she lands on
	/// open ground clear of its buildings, for a rider to get on and off (see ValkyrieFlight and TravelerPet)
	/// </summary>
	/// <remarks>
	/// Both look straight down over a grid of points around where they're asked about, and take the nearest that does
	/// </remarks>
	internal static class ValkyrieRoost
	{
		/// <summary>
		/// How far around a point roofs and open ground are looked for, and how far apart the points looked down at are,
		/// in meters
		/// </summary>
		internal const float SearchRange = 30f;
		internal const float SearchStep = 2f;

		/// <summary>
		/// The steepest a roof can be for her to roost on, in degrees, and how far above the ground it has to be, in
		/// meters, so a floor isn't taken for one
		/// </summary>
		internal const float MaxRoofSlope = 30f;
		internal const float MinRoofHeight = 2.5f;

		/// <summary>
		/// How far out from a roost's middle roof has to reach all around at much the same height, how much it can rise or
		/// fall there, and how high above it has to be clear, in meters
		/// </summary>
		internal const float RoofReach = 1.2f;
		internal const float RoofEven = 1.2f;
		internal const float Headroom = 5f;

		/// <summary>
		/// How far from open ground anything built, a tree or a rock has to be for her to land there, and how high above
		/// it, in meters
		/// </summary>
		internal const float GroundClear = 4f;
		internal const float GroundHeadroom = 15f;

		/// <summary>
		/// The layers looked down at, and those that count as in her way
		/// </summary>
		private static int solidMask;
		private static int blockMask;

		/// <summary>
		/// Checks whether a surface is flat enough to roost on, by the up part of its normal
		/// </summary>
		/// <param name="normalUp">The up part of the surface's normal</param>
		/// <returns>True when it is</returns>
		internal static bool FlatEnough(float normalUp)
		{
			return(normalUp >= Mathf.Cos(MaxRoofSlope * Mathf.Deg2Rad));
		}

		/// <summary>
		/// Checks whether a surface is high enough above the ground to be a roof rather than a floor
		/// </summary>
		/// <param name="surface">The surface's height</param>
		/// <param name="ground">The ground's height there</param>
		/// <returns>True when it is</returns>
		internal static bool HighEnough(float surface, float ground)
		{
			return(surface - ground >= MinRoofHeight);
		}

		/// <summary>
		/// Finds the nearest roof spot to a point that she can roost on - flat enough, high enough above the ground, wide
		/// enough all around her and clear above - within a village
		/// </summary>
		/// <param name="near">The point</param>
		/// <param name="village">The village, whose roofs alone count</param>
		/// <param name="spot">The spot on the roof</param>
		/// <returns>True when there's one</returns>
		public static bool FindRoof(Vector3 near, Village village, out Vector3 spot)
		{
			Masks();
			spot = near;
			float best = float.MaxValue;
			int steps = Mathf.CeilToInt(SearchRange / SearchStep);
			for (int x = -steps; x <= steps; x++)
			{
				for (int z = -steps; z <= steps; z++)
				{
					Vector3 point = near + new Vector3(x * SearchStep, 0f, z * SearchStep);
					float distance = Utils.DistanceXZ(point, near);
					if (distance > SearchRange || distance >= best || (village != null && !village.Contains(point)))
					{
						continue;
					}
					if (Roof(point, out Vector3 found))
					{
						best = distance;
						spot = found;
					}
				}
			}
			return(best < float.MaxValue);
		}

		/// <summary>
		/// Finds the nearest open ground to a point she can land on - solid, out of the water, with nothing built, no tree
		/// and no rock within GroundClear of it and nothing above it
		/// </summary>
		/// <param name="near">The point</param>
		/// <param name="spot">The spot on the ground</param>
		/// <returns>True when there's one</returns>
		public static bool FindOpenGround(Vector3 near, out Vector3 spot)
		{
			Masks();
			spot = near;
			float best = float.MaxValue;
			int steps = Mathf.CeilToInt(SearchRange / SearchStep);
			for (int x = -steps; x <= steps; x++)
			{
				for (int z = -steps; z <= steps; z++)
				{
					Vector3 point = near + new Vector3(x * SearchStep, 0f, z * SearchStep);
					float distance = Utils.DistanceXZ(point, near);
					if (distance > SearchRange || distance >= best)
					{
						continue;
					}
					if (OpenGround(point, out Vector3 found))
					{
						best = distance;
						spot = found;
					}
				}
			}
			return(best < float.MaxValue);
		}

		/// <summary>
		/// Checks whether the first thing below a point is a roof she can roost on
		/// </summary>
		/// <param name="point">The point, looked straight down from high above</param>
		/// <param name="spot">Where on the roof</param>
		/// <returns>True when it is</returns>
		private static bool Roof(Vector3 point, out Vector3 spot)
		{
			spot = point;
			if (!Down(point, out RaycastHit hit) || hit.collider.GetComponentInParent<WearNTear>() == null || !FlatEnough(hit.normal.y))
			{
				return(false);
			}
			float ground = ((ZoneSystem.instance != null) ? ZoneSystem.instance.GetGroundHeight(hit.point) : hit.point.y);
			if (!HighEnough(hit.point.y, ground))
			{
				return(false);
			}
			// Roof all around her, at much the same height, so she isn't standing over an edge
			Vector3[] around = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
			foreach (Vector3 side in around)
			{
				if (!Down(hit.point + side * RoofReach, out RaycastHit near) || near.collider.GetComponentInParent<WearNTear>() == null || Mathf.Abs(near.point.y - hit.point.y) > RoofEven)
				{
					return(false);
				}
			}
			if (Physics.CheckCapsule(hit.point + Vector3.up * (RoofReach + 0.3f), hit.point + Vector3.up * Headroom, RoofReach, blockMask, QueryTriggerInteraction.Ignore))
			{
				return(false);
			}
			spot = hit.point;
			return(true);
		}

		/// <summary>
		/// Checks whether the ground below a point is open ground she can land on
		/// </summary>
		/// <param name="point">The point, looked straight down from high above</param>
		/// <param name="spot">Where on the ground</param>
		/// <returns>True when it is</returns>
		private static bool OpenGround(Vector3 point, out Vector3 spot)
		{
			spot = point;
			ZoneSystem zones = ZoneSystem.instance;
			if (zones == null || !Down(point, out RaycastHit hit) || hit.collider.GetComponent<Heightmap>() == null)
			{
				return(false);
			}
			if (hit.point.y < zones.m_waterLevel + 0.5f)
			{
				return(false);
			}
			if (Physics.CheckCapsule(hit.point + Vector3.up * (GroundClear + 0.5f), hit.point + Vector3.up * GroundHeadroom, GroundClear, blockMask, QueryTriggerInteraction.Ignore))
			{
				return(false);
			}
			spot = hit.point;
			return(true);
		}

		/// <summary>
		/// Looks straight down from high above a point for the first solid thing there
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="hit">What's hit</param>
		/// <returns>True when something is</returns>
		private static bool Down(Vector3 point, out RaycastHit hit)
		{
			Vector3 from = new Vector3(point.x, point.y + 200f, point.z);
			return(Physics.Raycast(from, Vector3.down, out hit, 400f, solidMask, QueryTriggerInteraction.Ignore));
		}

		/// <summary>
		/// Sets up the layers looked at, once
		/// </summary>
		private static void Masks()
		{
			if (solidMask != 0)
			{
				return;
			}
			solidMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
			blockMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
		}
	}
}
