using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Runs everything in and around a village on the game of a player inside it, wherever in the village it is - its
	/// vikings, the monsters they fight, its animals, fires and dropped items - as long as it's loaded, rather than only
	/// what's in the area the game runs around the player, so no viking freezes at an outpost past that area's edge
	/// with the monster it's fighting
	/// </summary>
	/// <remarks>
	/// The game hands each object to a player's game to run when it's within the area around that player, and takes it
	/// back when it's not (see ZDOMan.ReleaseNearbyZDOS) - a player's game does that for itself, and the server for each
	/// player. While the game decides, a point within CoverMargin of a village whose circle a player stands in counts as
	/// in that player's area, when it's in a zone loaded around them. Every game - the server and each player's - works
	/// it out the same way, from the village's middle and radius noted on its generator (see Remember), so they agree
	/// on who runs what. Nothing outside the loaded zones can be run, since the ground and buildings there aren't there
	/// </remarks>
	internal static class VillageSimulation
	{
		/// <summary>
		/// On a village's generator, or a player-started village's anchor, the village's middle and the radius around all
		/// of it, noted by the game that owns it for every game to read
		/// </summary>
		public static readonly int MiddleKey = "kriona.VikingVillages.villageMiddle".GetStableHashCode();
		public static readonly int RadiusKey = "kriona.VikingVillages.villageRadius".GetStableHashCode();

		/// <summary>
		/// How far past a village's circle everything is run by a player inside it, in meters - as far as its vikings go
		/// on their own, hunting prey or straying after an enemy
		/// </summary>
		public const float CoverMargin = 80f;

		/// <summary>
		/// How far from a player to look for the generators of villages they might be in, in meters - a player inside a
		/// village is within its radius of its middle, which is within its radius of its generator
		/// </summary>
		private const float FindReach = 200f;

		/// <summary>
		/// How long the village a player is in is kept before it's looked for again, and how often the loaded villages'
		/// middles and radii are noted, in seconds
		/// </summary>
		private const float CoverLife = 1f;
		private const float RememberInterval = 5f;

		/// <summary>
		/// How much a village's middle or radius has to change to be noted again, in meters
		/// </summary>
		private const float RememberChange = 0.5f;

		/// <summary>
		/// A village's circle a player is in, by the player's session, and when it was found
		/// </summary>
		private class Cover
		{
			public bool Found;
			public Vector3 Middle;
			public float Radius;
			public float FoundAt;
		}

		private static readonly Dictionary<long, Cover> covers = new Dictionary<long, Cover>();

		/// <summary>
		/// While the game decides who runs what around a player (see ReleaseNearbyZDOS), the player's reference position
		/// and session
		/// </summary>
		private static bool deciding;
		private static Vector3 decidingReference;
		private static Vector2s decidingZone;
		private static long decidingSession;

		/// <summary>
		/// When the loaded villages' middles and radii are next noted, by Time.time
		/// </summary>
		private static float nextRemember;

		/// <summary>
		/// Whether this client's player was covering a village at the last update, for the test log
		/// </summary>
		private static bool wasCovering;

		/// <summary>
		/// Notes the loaded villages' middles and radii on those whose generators this client owns, every
		/// RememberInterval seconds, and writes this client's player starting and stopping covering a village to the test
		/// log
		/// </summary>
		public static void Update()
		{
			if (ZNet.instance == null || ZDOMan.instance == null)
			{
				return;
			}
			if (TestLog.On)
			{
				LogCovering();
			}
			if (Time.time < nextRemember)
			{
				return;
			}
			nextRemember = Time.time + RememberInterval;
			foreach (Village village in Village.Loaded())
			{
				Remember(village);
			}
		}

		/// <summary>
		/// Notes a village's middle and radius on its generator when this client owns it and they changed
		/// </summary>
		/// <param name="village">The village</param>
		private static void Remember(Village village)
		{
			ZDO zdo = village.Zdo;
			if (zdo == null || !zdo.IsOwner() || village.Radius <= 0f)
			{
				return;
			}
			Vector3 middle = zdo.GetVec3(MiddleKey, Vector3.zero);
			float radius = zdo.GetFloat(RadiusKey, 0f);
			if (Vector3.Distance(middle, village.Middle) < RememberChange && Mathf.Abs(radius - village.Radius) < RememberChange)
			{
				return;
			}
			zdo.Set(MiddleKey, village.Middle);
			zdo.Set(RadiusKey, village.Radius);
		}

		/// <summary>
		/// Notes that the game is deciding who runs what around a player, for InActiveArea
		/// </summary>
		/// <param name="reference">The player's reference position</param>
		/// <param name="session">The player's session</param>
		public static void BeginDeciding(Vector3 reference, long session)
		{
			deciding = true;
			decidingReference = reference;
			decidingZone = ZoneSystem.GetZone(reference);
			decidingSession = session;
		}

		/// <summary>
		/// Notes that the game is done deciding who runs what around a player
		/// </summary>
		public static void EndDeciding()
		{
			deciding = false;
		}

		/// <summary>
		/// Checks whether a point the game found outside the area around the player it's deciding for counts as in it,
		/// since it's in the village the player is in
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="centerZone">The zone the area is around</param>
		/// <returns>True when it counts as in the area</returns>
		public static bool ExtendsDeciding(Vector3 point, Vector2s centerZone)
		{
			return(deciding && centerZone == decidingZone && Covers(point, decidingReference, decidingSession));
		}

		/// <summary>
		/// Checks whether a point counts as in a player's area since it's in or near the village they're in, and in a
		/// zone loaded around them
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="reference">The player's reference position</param>
		/// <param name="session">The player's session, for which village they're in</param>
		/// <returns>True when it does</returns>
		public static bool Covers(Vector3 point, Vector3 reference, long session)
		{
			Cover cover = CoverFor(reference, session);
			if (!cover.Found || !InReach(cover.Middle, cover.Radius + CoverMargin, point))
			{
				return(false);
			}
			return(Loaded(point, reference));
		}

		/// <summary>
		/// Checks whether this client's player is in a village, so everything in and around it is run here
		/// </summary>
		/// <param name="middle">Set to the village's middle</param>
		/// <param name="radius">Set to its radius</param>
		/// <returns>True when they are</returns>
		public static bool LocalCover(out Vector3 middle, out float radius)
		{
			middle = Vector3.zero;
			radius = 0f;
			if (ZNet.instance == null || ZDOMan.instance == null)
			{
				return(false);
			}
			Cover cover = CoverFor(ZNet.instance.GetReferencePosition(), ZDOMan.GetSessionID());
			middle = cover.Middle;
			radius = cover.Radius;
			return(cover.Found);
		}

		/// <summary>
		/// Finds the village a player is in, from the middles and radii noted on the generators around them, at most once
		/// a CoverLife for each player
		/// </summary>
		/// <param name="reference">The player's reference position</param>
		/// <param name="session">The player's session</param>
		/// <returns>The village's circle, not found when they're in none</returns>
		private static Cover CoverFor(Vector3 reference, long session)
		{
			if (covers.TryGetValue(session, out Cover cover) && Time.time - cover.FoundAt < CoverLife)
			{
				return(cover);
			}
			if (cover == null)
			{
				cover = new Cover();
				covers[session] = cover;
			}
			cover.FoundAt = Time.time;
			List<ZDO> zdos = new List<ZDO>();
			VillageRespawn.ZdosNear(reference, FindReach, zdos);
			List<KeyValuePair<Vector3, float>> circles = new List<KeyValuePair<Vector3, float>>();
			foreach (ZDO zdo in zdos)
			{
				int prefab = zdo.GetPrefab();
				if (prefab != VillagePaths.GeneratorHash && !VillageFounding.IsAnchor(prefab))
				{
					continue;
				}
				float radius = zdo.GetFloat(RadiusKey, 0f);
				if (radius > 0f)
				{
					circles.Add(new KeyValuePair<Vector3, float>(zdo.GetVec3(MiddleKey, zdo.GetPosition()), radius));
				}
			}
			int picked = PickCircle(circles, reference);
			cover.Found = (picked >= 0);
			cover.Middle = (cover.Found ? circles[picked].Key : Vector3.zero);
			cover.Radius = (cover.Found ? circles[picked].Value : 0f);
			return(cover);
		}

		/// <summary>
		/// Picks the village circle a player is in - among those reaching them, the one whose middle is nearest
		/// </summary>
		/// <param name="circles">Each village's middle and radius</param>
		/// <param name="reference">Where the player is</param>
		/// <returns>The circle's index, or -1 when none reaches them</returns>
		internal static int PickCircle(List<KeyValuePair<Vector3, float>> circles, Vector3 reference)
		{
			int picked = -1;
			float nearest = float.MaxValue;
			for (int i = 0; i < circles.Count; i++)
			{
				float distance = DistanceXZ(circles[i].Key, reference);
				if (distance <= circles[i].Value && distance < nearest)
				{
					picked = i;
					nearest = distance;
				}
			}
			return(picked);
		}

		/// <summary>
		/// Checks whether a point is within a distance of a middle, seen from above
		/// </summary>
		/// <param name="middle">The middle</param>
		/// <param name="reach">The distance, in meters</param>
		/// <param name="point">The point</param>
		/// <returns>True when it is</returns>
		internal static bool InReach(Vector3 middle, float reach, Vector3 point)
		{
			return(DistanceXZ(middle, point) <= reach);
		}

		/// <summary>
		/// Measures the distance between two points seen from above
		/// </summary>
		/// <param name="a">One point</param>
		/// <param name="b">The other</param>
		/// <returns>The distance, in meters</returns>
		private static float DistanceXZ(Vector3 a, Vector3 b)
		{
			float x = a.x - b.x;
			float z = a.z - b.z;
			return(Mathf.Sqrt(x * x + z * z));
		}

		/// <summary>
		/// Checks whether a point is in a zone loaded around a player, the way ZDOMan.FindSectorObjects picks the zones
		/// whose objects are loaded
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="reference">The player's reference position</param>
		/// <returns>True when it is</returns>
		private static bool Loaded(Vector3 point, Vector3 reference)
		{
			Vector2s center = ZoneSystem.GetZone(reference);
			Vector2s zone = ZoneSystem.GetZone(point);
			SimulationDistance distance = ZNet.instance.GetSyncedSimulationDistance();
			if (!ZonesWithin(center, zone, distance.NearSimulationDistance))
			{
				return(false);
			}
			return(distance.IsClassic || ZoneSystem.instance.ZonesWithinRadius(center, zone, distance.NearSimulationDistance));
		}

		/// <summary>
		/// Checks whether a zone is within a number of zones of another, either way along the ground
		/// </summary>
		/// <param name="center">The zone in the middle</param>
		/// <param name="zone">The zone</param>
		/// <param name="zones">How many zones away it can be</param>
		/// <returns>True when it is</returns>
		internal static bool ZonesWithin(Vector2s center, Vector2s zone, int zones)
		{
			return(Mathf.Abs(zone.x - center.x) <= zones && Mathf.Abs(zone.y - center.y) <= zones);
		}

		/// <summary>
		/// Writes this client's player starting and stopping covering a village to the test log
		/// </summary>
		private static void LogCovering()
		{
			bool covering = LocalCover(out Vector3 middle, out float radius);
			if (covering == wasCovering)
			{
				return;
			}
			wasCovering = covering;
			if (covering)
			{
				Plugin.Log.LogInfo("Covering: the player is in the village around " + middle.ToString("F0") + ", so this client runs everything loaded within " + (radius + CoverMargin).ToString("F0") + "m of its middle");
			}
			else
			{
				Plugin.Log.LogInfo("Covering: the player left the village, so this client runs only the area the game runs around them");
			}
		}
	}
}
