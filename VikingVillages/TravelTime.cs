namespace VikingVillages
{
	/// <summary>
	/// How long vikings take on the road between villages, and how that's said to a player - roughly, in the game's own
	/// hours and days, like "in a couple of hours" or "tomorrow"
	/// </summary>
	internal static class TravelTime
	{
		/// <summary>
		/// How fast a viking covers ground on the road, running over hills, around trees and through rivers, in meters a
		/// second
		/// </summary>
		public const float Speed = 5f;

		/// <summary>
		/// The boats vikings cross the sea in - by the first biome, by VikingGear's order, each is the best boat of - how
		/// fast each goes on average with the wind as it comes, in meters a second, each one's prefab, and whether it
		/// holds up in the Ashlands' boiling sea (see Ship.m_ashlandsReady), for when its prefab can't be read
		/// </summary>
		private static readonly string[] Boats = { "raft", "karve", "longship", "drakkar" };
		private static readonly int[] BoatFrom = { 0, 1, 3, 6 };
		private static readonly float[] BoatSpeeds = { 3f, 6f, 7.5f, 8.5f };
		private static readonly string[] BoatPrefabs = { "Raft", "Karve", "VikingShip", "VikingShip_Ashlands" };
		private static readonly bool[] BoatFireproof = { false, false, false, true };

		/// <summary>
		/// How many vikings each boat carries, the one at the helm included
		/// </summary>
		private static readonly int[] BoatCrews = { 2, 4, 8, 10 };

		/// <summary>
		/// How long stepping through a portal takes, in seconds - the walk into it and out the other side
		/// </summary>
		public const float PortalSeconds = 5f;

		/// <summary>
		/// How many game hours a game day has
		/// </summary>
		private const double DayHours = 24.0;

		/// <summary>
		/// Works out how long a viking takes to cover a stretch of road
		/// </summary>
		/// <param name="meters">How long the road is, in meters</param>
		/// <returns>How long it takes, in seconds</returns>
		public static float Seconds(float meters)
		{
			return(System.Math.Max(0f, meters) / Speed);
		}

		/// <summary>
		/// Finds the best boat vikings of a biome have - a raft in the Meadows, a karve in the Black Forest and the Swamp,
		/// a longship from the Mountains to the Mistlands, and a drakkar from the Ashlands on
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <returns>The boat's index into Boats</returns>
		private static int Boat(int biome)
		{
			int boat = 0;
			for (int i = 0; i < BoatFrom.Length; i++)
			{
				if (biome >= BoatFrom[i])
				{
					boat = i;
				}
			}
			return(boat);
		}

		/// <summary>
		/// Gets how fast vikings of a biome cross the sea, in the best boat they have
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <returns>The speed, in meters a second</returns>
		public static float BoatSpeed(int biome)
		{
			return(BoatSpeeds[Boat(biome)]);
		}

		/// <summary>
		/// Gets the name of the best boat vikings of a biome have, as they say it
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <returns>The name, like "longship"</returns>
		public static string BoatName(int biome)
		{
			return(Boats[Boat(biome)]);
		}

		/// <summary>
		/// Gets the prefab of the best boat vikings of a biome have
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <returns>The prefab's name, like "VikingShip"</returns>
		public static string BoatPrefab(int biome)
		{
			return(BoatPrefabs[Boat(biome)]);
		}

		/// <summary>
		/// Gets how many vikings the best boat of a biome carries, the one at the helm included
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <returns>How many</returns>
		public static int BoatCrew(int biome)
		{
			return(BoatCrews[Boat(biome)]);
		}

		/// <summary>
		/// Checks whether the best boat vikings of a biome have holds up in the Ashlands' boiling sea, by the game's boats
		/// as they are - only the drakkar does
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <returns>True when it does</returns>
		public static bool BoatSurvivesAshlands(int biome)
		{
			return(BoatFireproof[Boat(biome)]);
		}

		/// <summary>
		/// Says roughly when something is due, in game time - within the next few hours by the hour, later on by the day
		/// it falls on (see EnvMan.GetDay)
		/// </summary>
		/// <param name="now">The time now, in seconds of world time</param>
		/// <param name="due">When it's due, in seconds of world time</param>
		/// <param name="dayLength">How long a game day is, in seconds</param>
		/// <returns>The phrase, like "in a couple of hours" or "tomorrow", or "now" when it's due already</returns>
		internal static string Phrase(double now, double due, double dayLength)
		{
			if (due <= now)
			{
				return("now");
			}
			double hours = (due - now) / dayLength * DayHours;
			if (hours < 0.75)
			{
				return("in roughly half an hour");
			}
			if (hours < 1.5)
			{
				return("in about an hour");
			}
			if (hours < 3.5)
			{
				return("in a couple of hours");
			}
			if (hours < 8.0)
			{
				return("in a few hours");
			}
			long days = (long)System.Math.Floor(due / dayLength) - (long)System.Math.Floor(now / dayLength);
			if (days <= 0)
			{
				return("later today");
			}
			if (days == 1)
			{
				return("tomorrow");
			}
			if (hours < 36.0)
			{
				return("in a day");
			}
			if (days <= 3)
			{
				return("in a couple of days");
			}
			if (days <= 6)
			{
				return("in a few days");
			}
			return((days <= 9) ? "in about a week" : "in more than a week");
		}
	}
}
