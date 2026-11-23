using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// What the vikings a client runs have all given up on reaching - loot, a bush, a chest or a piece - once Tries
	/// tries by at least Vikings of them have failed within Window, so every viking leaves it alone for AvoidTime,
	/// longer each time they give up on it again, up to AvoidMax, rather than each of them walking to it and failing
	/// again as soon as it's forgotten its own try
	/// </summary>
	/// <remarks>
	/// The times are passed in, so the counting can be tested outside the game
	/// </remarks>
	internal class VillageGiveUps
	{
		/// <summary>
		/// How many failed tries, by how many different vikings at least, within how many seconds, make them all give up
		/// on something
		/// </summary>
		public const int Tries = 3;
		public const int Vikings = 2;
		public const float Window = 600f;

		/// <summary>
		/// How long the vikings leave something alone the first time they all give up on it, and the longest they leave
		/// it alone after giving up on it again and again, in seconds
		/// </summary>
		public const float AvoidTime = 1800f;
		public const float AvoidMax = 7200f;

		/// <summary>
		/// The failed tries at something since the count began, by whom, when the count began, until when it's left
		/// alone, and how many times the vikings have given up on it
		/// </summary>
		private class Record
		{
			public float Since;
			public int Tries;
			public readonly HashSet<ZDOID> Vikings = new HashSet<ZDOID>();
			public float Until;
			public int Strikes;
		}

		private readonly Dictionary<ZDOID, Record> records = new Dictionary<ZDOID, Record>();

		/// <summary>
		/// Notes a viking's failed try at something, giving up on it for every viking when it's the last of Tries by at
		/// least Vikings of them within Window
		/// </summary>
		/// <param name="thing">What it couldn't reach</param>
		/// <param name="viking">The viking</param>
		/// <param name="now">The time now, in seconds</param>
		/// <returns>How long every viking now leaves it alone, in seconds, or 0 when they haven't given up on it</returns>
		public float Fail(ZDOID thing, ZDOID viking, float now)
		{
			if (!records.TryGetValue(thing, out Record record))
			{
				record = new Record { Since = now };
				records[thing] = record;
			}
			if (now - record.Since > Window)
			{
				record.Since = now;
				record.Tries = 0;
				record.Vikings.Clear();
			}
			record.Tries++;
			record.Vikings.Add(viking);
			if (record.Tries < Tries || record.Vikings.Count < Vikings)
			{
				return(0f);
			}
			record.Strikes++;
			float seconds = LeftAlone(record.Strikes);
			record.Until = now + seconds;
			record.Since = now;
			record.Tries = 0;
			record.Vikings.Clear();
			return(seconds);
		}

		/// <summary>
		/// Checks whether every viking leaves something alone, having given up on it
		/// </summary>
		/// <param name="thing">The thing</param>
		/// <param name="now">The time now, in seconds</param>
		/// <returns>True while they do</returns>
		public bool GivenUp(ZDOID thing, float now)
		{
			return(records.TryGetValue(thing, out Record record) && now < record.Until);
		}

		/// <summary>
		/// Forgets what has had no failed try for a whole Window and hasn't been left alone for AvoidMax - until then it's
		/// kept, so giving up on it again leaves it alone longer
		/// </summary>
		/// <param name="now">The time now, in seconds</param>
		public void Prune(float now)
		{
			List<ZDOID> done = new List<ZDOID>();
			foreach (KeyValuePair<ZDOID, Record> entry in records)
			{
				if (now - entry.Value.Since > Window && now >= entry.Value.Until + AvoidMax)
				{
					done.Add(entry.Key);
				}
			}
			foreach (ZDOID thing in done)
			{
				records.Remove(thing);
			}
		}

		/// <summary>
		/// Works out how long the vikings leave something alone the times they've given up on it - AvoidTime for each
		/// time, up to AvoidMax
		/// </summary>
		/// <param name="strikes">How many times they've given up on it</param>
		/// <returns>How long, in seconds</returns>
		internal static float LeftAlone(int strikes)
		{
			float seconds = AvoidTime * strikes;
			return((seconds < AvoidMax) ? seconds : AvoidMax);
		}
	}
}
