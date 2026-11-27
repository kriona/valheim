namespace VikingVillages
{
	/// <summary>
	/// One of the events a village can have on a day - how likely it is each day, how long until it can happen again
	/// once it has, the biomes it happens in, the time of day it starts within, and what it does (see VillageEvents)
	/// </summary>
	internal class VillageEvent
	{
		/// <summary>
		/// The event's ID, for the console and its key on the village, and its name, for the log and debug text
		/// </summary>
		public readonly string Id;
		public readonly string Name;

		/// <summary>
		/// The chance it happens on a day, from 0 to 1, and how many days its cooldown lasts, counting the day it starts
		/// </summary>
		public readonly float Chance;
		public readonly int CooldownDays;

		/// <summary>
		/// The easiest and hardest biome of a village it happens in, by their indexes in VikingGear's tables
		/// </summary>
		public readonly int EasiestBiome;
		public readonly int HardestBiome;

		/// <summary>
		/// The time of day its window opens and closes, from 0 at midnight to 1 - it starts at a time between them, and
		/// is missed when the window closes before it can start
		/// </summary>
		public readonly float WindowStart;
		public readonly float WindowEnd;

		/// <summary>
		/// On a farm's generator, the day the event last started
		/// </summary>
		public readonly int LastDayKey;

		/// <summary>
		/// Checks whether it can start at a village now, and starts it
		/// </summary>
		private readonly System.Func<Village, bool> canStart;
		private readonly System.Action<Village> start;

		/// <summary>
		/// Describes an event
		/// </summary>
		/// <param name="id">Its ID</param>
		/// <param name="name">Its name</param>
		/// <param name="chance">The chance it happens on a day, from 0 to 1</param>
		/// <param name="cooldownDays">How many days its cooldown lasts</param>
		/// <param name="easiestBiome">The easiest biome it happens in</param>
		/// <param name="hardestBiome">The hardest biome it happens in</param>
		/// <param name="windowStart">The time of day its window opens</param>
		/// <param name="windowEnd">The time of day its window closes</param>
		/// <param name="canStart">Checks whether it can start at a village now, or null when it always can</param>
		/// <param name="start">Starts it at a village</param>
		public VillageEvent(string id, string name, float chance, int cooldownDays, int easiestBiome, int hardestBiome, float windowStart, float windowEnd, System.Func<Village, bool> canStart, System.Action<Village> start)
		{
			Id = id;
			Name = name;
			Chance = chance;
			CooldownDays = cooldownDays;
			EasiestBiome = easiestBiome;
			HardestBiome = hardestBiome;
			WindowStart = windowStart;
			WindowEnd = windowEnd;
			LastDayKey = ("kriona.VikingVillages.eventLastDay." + id).GetStableHashCode();
			this.canStart = canStart;
			this.start = start;
		}

		/// <summary>
		/// Checks whether the event can start at a village now
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>True when it can</returns>
		public bool CanStart(Village village)
		{
			return(canStart == null || canStart(village));
		}

		/// <summary>
		/// Starts the event at a village
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		public void Start(Village village)
		{
			start?.Invoke(village);
		}

		/// <summary>
		/// Gets the day the event last started at a village
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The day, or -1 when it never has</returns>
		public long LastDay(Village village)
		{
			return(village.Zdo.GetLong(LastDayKey, -1L));
		}
	}
}
