using Kriona.Vikings;

namespace VikingVillages
{
	/// <summary>
	/// The Traveling Viking event - a Viking from a biome at least three harder than the village's comes by in the
	/// latter half of the day, stays the night by the fire and sets off again the next morning (see EVENTS.md)
	/// </summary>
	internal static class TravelerEvent
	{
		/// <summary>
		/// The event's ID, for the console
		/// </summary>
		internal const string Id = "traveler";

		/// <summary>
		/// The chance it happens on a day, and how many days its cooldown lasts
		/// </summary>
		internal const float Chance = 0.1f;
		internal const int CooldownDays = 6;

		/// <summary>
		/// The time of day the traveler arrives within - from noon to two game hours before dusk, so they come in daylight
		/// </summary>
		internal const float ArriveFrom = 0.5f;
		internal const float ArriveUntil = VillageLife.DuskStart - 2f / 24f;

		/// <summary>
		/// The event, for villages of the Meadows through the Plains
		/// </summary>
		public static readonly VillageEvent Event = new VillageEvent(Id, "Traveling Viking", Chance, CooldownDays, VikingGear.IndexOf(Heightmap.Biome.Meadows), VikingGear.IndexOf(Heightmap.Biome.Plains), ArriveFrom, ArriveUntil, CanStart, Start);

		/// <summary>
		/// Checks whether a traveler can arrive at a village now - not while a raid is on it
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>True when they can</returns>
		private static bool CanStart(Village village)
		{
			return(!VillageRaids.On(village));
		}

		/// <summary>
		/// Sends a traveler to a village
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		private static void Start(Village village)
		{
			Plugin.Log.LogInfo("A traveling Viking is due at " + VillageNames.Get(village));
		}
	}
}
