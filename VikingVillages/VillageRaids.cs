using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The raids villages are caught in - the game's raids, the zombie viking raid among them, whose area takes in some
	/// of a village while the raid is running - which its awake vikings get ready for, and which its night watch wakes
	/// the village to
	/// </summary>
	/// <remarks>
	/// The world runs one raid at a time, which every client knows of - where it is, how far it reaches and how long
	/// it's been going - so each client tells for itself which villages it's on. A raid that pauses with no player in its
	/// area, the way most do, waits there for as long as no one comes, so it only counts while a player is in its area
	/// and it's running, the way the game itself judges (see RandEventSystem.IsAnyPlayerInEventArea)
	/// </remarks>
	internal static class VillageRaids
	{
		/// <summary>
		/// How high a player has to be to count as out of every raid's area, the way the game judges - in a dungeon or
		/// some other place out of the world above it, in meters
		/// </summary>
		private const float OutOfWorldHeight = 3000f;

		/// <summary>
		/// Checks whether a raid is on at a village - the world's raid is running, and reaches over some of it
		/// </summary>
		/// <param name="village">The village, or null</param>
		/// <returns>True while one is</returns>
		public static bool On(Village village)
		{
			RandomEvent raid = Current();
			return(raid != null && village != null && Reaches(raid, village) && Running(raid));
		}

		/// <summary>
		/// Describes the world's raid as a village sees it, for the debug hover text
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The line, ending in a line break</returns>
		public static string Describe(Village village)
		{
			RandomEvent raid = Current();
			if (raid == null)
			{
				return("Raid: none\n");
			}
			float outside = village.DistanceOutside(raid.m_pos);
			string state = (Running(raid) ? "running" : "paused, with no player within " + raid.m_eventRange.ToString("F0") + "m of it");
			string reach = (Reaches(raid, village) ? "on this village" : "not on this village");
			return("Raid: " + raid.m_name + " at " + raid.m_pos.ToString("F0") + ", " + outside.ToString("F0") + "m past the village's edge, reaching " + raid.m_eventRange.ToString("F0") + "m, " + raid.m_time.ToString("F0") + "s of " + raid.m_duration.ToString("F0") + "s in, " + state + " - " + reach + (On(village) ? "" : ", so the village pays it no mind") + "\n");
		}

		/// <summary>
		/// Checks whether a raid's area reaches over some of a village
		/// </summary>
		/// <param name="raid">The raid</param>
		/// <param name="village">The village</param>
		/// <returns>True when it does</returns>
		private static bool Reaches(RandomEvent raid, Village village)
		{
			return(village.DistanceOutside(raid.m_pos) <= raid.m_eventRange);
		}

		/// <summary>
		/// Checks whether a raid is running - it doesn't pause with no player in its area, or a player loaded on this
		/// client is in it
		/// </summary>
		/// <param name="raid">The raid</param>
		/// <returns>True when it is</returns>
		private static bool Running(RandomEvent raid)
		{
			if (!raid.m_pauseIfNoPlayerInArea)
			{
				return(true);
			}
			foreach (Player player in Player.GetAllPlayers())
			{
				if (player != null && InArea(player.transform.position, raid.m_pos, raid.m_eventRange))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a point is in a raid's area - nearer its middle than its range, seen from above, and no higher
		/// than OutOfWorldHeight, the way the game judges (see RandEventSystem.IsInsideRandomEventArea)
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="middle">The raid's middle</param>
		/// <param name="range">How far the raid reaches from its middle, in meters</param>
		/// <returns>True when it is</returns>
		internal static bool InArea(Vector3 point, Vector3 middle, float range)
		{
			return(point.y <= OutOfWorldHeight && Utils.DistanceXZ(point, middle) < range);
		}

		/// <summary>
		/// Finds the raid the world is running
		/// </summary>
		/// <returns>The raid, or null when there's none</returns>
		private static RandomEvent Current()
		{
			return((RandEventSystem.instance != null) ? RandEventSystem.instance.GetCurrentRandomEvent() : null);
		}
	}
}
