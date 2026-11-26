using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeps a living viking from stepping off a ledge it would be hurt falling from - more than SafeDrop down onto
	/// the ground or into water too shallow to swim in, or into water it shouldn't go in: the boiling sea around the
	/// Ashlands, water in freezing weather it doesn't resist, or water with no way out on the navmesh. A move that would
	/// step off is turned aside, up to 90 degrees either way, or stopped (see VikingDangerStepPatch)
	/// </summary>
	/// <remarks>
	/// Water deep enough to swim in breaks a fall from any height, as it does a player's (see Character.UpdateSwimming).
	/// In water, the game never makes anyone freeze for being wet - but a viking in the water is away from any fire's
	/// warmth and out of shelter, so in freezing weather it freezes there
	/// </remarks>
	internal static class Ledges
	{
		/// <summary>
		/// What a step lands on - ground it can step down to, a fall onto the ground, or water deep enough to swim in
		/// </summary>
		internal enum Landing
		{
			Footing,
			Fall,
			Water
		}

		/// <summary>
		/// How far ahead of a viking a step is looked at, and the most it steps down there, in meters - half the height
		/// a fall starts to hurt from (see Character.c_MinFallHeight), so a drop that goes on past the step is still
		/// caught
		/// </summary>
		private const float StepAhead = 1f;
		internal const float SafeDrop = 2f;

		/// <summary>
		/// How far above the viking's feet the ground ahead is looked for from, and how far down, in meters
		/// </summary>
		private const float FootingRise = 1f;
		private const float FootingDrop = 100f;

		/// <summary>
		/// How much deeper than the game's swimming depth water has to be to break a fall, in meters, since a fast fall
		/// goes deeper before the game counts the viking as swimming
		/// </summary>
		private const float DeepMargin = 1f;

		/// <summary>
		/// The ways a step off a ledge is turned to, in degrees from the way it was going, tried in order
		/// </summary>
		private static readonly float[] Turns = { 45f, -45f, 90f, -90f };

		/// <summary>
		/// How long whether there's a way out of the water at a spot is remembered, in seconds, and the side of the
		/// squares it's remembered for, in meters, since finding a path builds the navmesh around both ends
		/// </summary>
		private const float WayOutMemory = 5f;
		private const float WayOutCell = 2f;
		private const int WayOutPrune = 128;

		/// <summary>
		/// Whether there's a way out of the water from a square, and until when that's remembered
		/// </summary>
		private struct WayOut
		{
			public bool Found;
			public float Until;
		}

		private static readonly Dictionary<Vector2Int, WayOut> wayOuts = new Dictionary<Vector2Int, WayOut>();
		private static readonly List<Vector2Int> expired = new List<Vector2Int>();

		/// <summary>
		/// The layers of what can be stood on - the game's own for its AI's solid ground, ships' decks among them -
		/// looked up on first use
		/// </summary>
		private static int groundMask;

		/// <summary>
		/// Picks the way a living viking's move goes - its own, or turned aside when it would step off a ledge, onto
		/// neither a ledge nor the lava or the boiling sea (see DangerousGround)
		/// </summary>
		/// <remarks>
		/// A viking in the air, in the water, sitting or lying down is left to move as it likes
		/// </remarks>
		/// <param name="viking">The viking</param>
		/// <param name="dir">The way it's moving</param>
		/// <param name="way">The way it moves, or dir when it can't move safely</param>
		/// <returns>False when every way would step off a ledge, so it stops</returns>
		public static bool SafeWay(LivingVikingBody viking, Vector3 dir, out Vector3 way)
		{
			way = dir;
			Vector3 heading = new Vector3(dir.x, 0f, dir.z);
			if (heading.sqrMagnitude < 0.01f || !viking.TakesFallDamage() || !viking.IsOnGround() || viking.InWater() || viking.IsAttached())
			{
				return(true);
			}
			heading.Normalize();
			if (!StepsOff(viking, heading))
			{
				return(true);
			}
			foreach (float turn in Turns)
			{
				Vector3 turned = Turn(heading, turn);
				if (!StepsOff(viking, turned) && !DangerousGround.StepsIntoDanger(viking, turned))
				{
					way = new Vector3(turned.x, dir.y, turned.z);
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Turns a flat heading about the vertical, the way a positive turn of Quaternion.Euler(0, degrees, 0) does -
		/// from forward toward the right
		/// </summary>
		/// <param name="heading">The heading, flat on the ground</param>
		/// <param name="degrees">How far to turn it</param>
		/// <returns>The heading turned</returns>
		internal static Vector3 Turn(Vector3 heading, float degrees)
		{
			float radians = degrees * Mathf.Deg2Rad;
			float cos = Mathf.Cos(radians);
			float sin = Mathf.Sin(radians);
			return(new Vector3(heading.x * cos + heading.z * sin, 0f, heading.z * cos - heading.x * sin));
		}

		/// <summary>
		/// Works out what a step lands on - footing when the ground there is no more than SafeDrop below where the
		/// viking stands, else water when it's deep enough to swim in, else a fall
		/// </summary>
		/// <param name="drop">How far below where the viking stands the ground there is, in meters</param>
		/// <param name="deepWater">Whether the water there is deep enough to break a fall</param>
		/// <returns>What it lands on</returns>
		internal static Landing LandingOf(float drop, bool deepWater)
		{
			if (drop <= SafeDrop)
			{
				return(Landing.Footing);
			}
			return(deepWater ? Landing.Water : Landing.Fall);
		}

		/// <summary>
		/// Checks whether a step a heading takes the viking would hurt it - a fall, or water it shouldn't go in
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <param name="heading">The way it steps, flat on the ground and of length 1</param>
		/// <returns>True when it would</returns>
		private static bool StepsOff(LivingVikingBody viking, Vector3 heading)
		{
			if (groundMask == 0)
			{
				groundMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
			}
			Vector3 here = viking.transform.position;
			Vector3 ahead = here + heading * StepAhead;
			if (!Physics.Raycast(ahead + Vector3.up * FootingRise, Vector3.down, out RaycastHit hit, FootingDrop + FootingRise, groundMask, QueryTriggerInteraction.Ignore))
			{
				return(true);
			}
			float ground = hit.point.y;
			float water = Floating.GetLiquidLevel(new Vector3(ahead.x, ground + 0.05f, ahead.z), 1f, LiquidType.Water);
			bool deep = (water > ground && viking.InLiquidSwimDepth(water - ground - DeepMargin));
			switch (LandingOf(here.y - ground, deep))
			{
				case Landing.Footing:
					return(false);
				case Landing.Water:
					return(!SafeWater(viking, new Vector3(ahead.x, water, ahead.z)));
				default:
					return(true);
			}
		}

		/// <summary>
		/// Checks whether water the viking would fall into is safe to land in - not the boiling sea around the Ashlands,
		/// as Character.UpdateAshlandsWater has it, not in freezing weather it doesn't resist, and with a way out of it
		/// on the navmesh back to where it stands
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <param name="landing">Where it would land, on the water's surface</param>
		/// <returns>True when it's safe</returns>
		private static bool SafeWater(LivingVikingBody viking, Vector3 landing)
		{
			if (WorldGenerator.GetAshlandsOceanGradient(landing) >= 0f || VikingWeather.FreezingFor(viking))
			{
				return(false);
			}
			return(HasWayOut(landing, viking.transform.position));
		}

		/// <summary>
		/// Checks whether a swimming viking can get from a spot in the water back to where it stands, by a whole path on
		/// the navmesh - remembered for the spot's square for WayOutMemory seconds
		/// </summary>
		/// <param name="landing">The spot in the water</param>
		/// <param name="here">Where the viking stands</param>
		/// <returns>True when there's a way out</returns>
		private static bool HasWayOut(Vector3 landing, Vector3 here)
		{
			if (Pathfinding.instance == null)
			{
				return(false);
			}
			Vector2Int cell = new Vector2Int(Mathf.FloorToInt(landing.x / WayOutCell), Mathf.FloorToInt(landing.z / WayOutCell));
			if (wayOuts.TryGetValue(cell, out WayOut known) && Time.time < known.Until)
			{
				return(known.Found);
			}
			PruneWayOuts();
			bool found = Pathfinding.instance.GetPath(landing, here, null, Pathfinding.AgentType.Humanoid, true);
			wayOuts[cell] = new WayOut { Found = found, Until = Time.time + WayOutMemory };
			return(found);
		}

		/// <summary>
		/// Forgets the ways out of the water that are no longer remembered, once there are WayOutPrune of them
		/// </summary>
		private static void PruneWayOuts()
		{
			if (wayOuts.Count < WayOutPrune)
			{
				return;
			}
			foreach (KeyValuePair<Vector2Int, WayOut> entry in wayOuts)
			{
				if (Time.time >= entry.Value.Until)
				{
					expired.Add(entry.Key);
				}
			}
			foreach (Vector2Int key in expired)
			{
				wayOuts.Remove(key);
			}
			expired.Clear();
		}
	}
}
