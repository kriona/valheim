using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What happened to a viking on its last trip on the road - where it went, the portals it stepped through, the boat it
	/// sailed in, whether it was wrecked, found its village gone, went the long way round a broken portal or turned
	/// around for home, the biomes it walked through and what it picked on the way - kept on its ZDO, added to as it
	/// goes in the world (see VillageLife) and off it (see VillageTravel), for it to talk about for TalkHours game hours
	/// once it gets there (see TripSpeech)
	/// </summary>
	internal class TripStory
	{
		/// <summary>
		/// On a PC viking, its last trip, and when it got where it was going, in ZNet time ticks, or 0 while it's still
		/// on the road
		/// </summary>
		public static readonly int TripKey = "kriona.VikingVillages.vikingTrip".GetStableHashCode();
		public static readonly int TripAtKey = "kriona.VikingVillages.vikingTripAt".GetStableHashCode();

		/// <summary>
		/// How many game hours a viking talks about its last trip once it gets there, and how many a game day has
		/// </summary>
		public const float TalkHours = 12f;
		private const float DayHours = 24f;

		/// <summary>
		/// The version of how a trip is written, which a reader turns down a trip written another way by
		/// </summary>
		private const int FormatVersion = 1;

		/// <summary>
		/// What happened on a trip worth telling, the most worth first
		/// </summary>
		internal enum Fact
		{
			None,
			Wrecked,
			TurnedHome,
			VillageGone,
			Rerouted,
			Portal,
			Sailed,
			Biome,
			Foraged
		}

		/// <summary>
		/// The names of the village it left, or "", and the village it went to, and where that stands
		/// </summary>
		public string From = "";
		public string To = "";
		public Vector3 ToSpot;

		/// <summary>
		/// How many portals it stepped through, the boat it sailed in, or "", and whether it was wrecked, found its
		/// village gone, went the long way round a broken portal, or turned around for home
		/// </summary>
		public int Portals;
		public string Boat = "";
		public bool Wrecked;
		public bool VillageGone;
		public bool Rerouted;
		public bool TurnedHome;

		/// <summary>
		/// The biomes it walked through, by VikingGear's order, and how many things it picked on the way
		/// </summary>
		public List<int> Biomes = new List<int>();
		public int Foraged;

		/// <summary>
		/// Whether it got where it was going
		/// </summary>
		public bool Arrived;

		/// <summary>
		/// Notes a biome it walked through, once
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <returns>True when it wasn't noted already</returns>
		public bool AddBiome(int biome)
		{
			if (biome < 0 || Biomes.Contains(biome))
			{
				return(false);
			}
			Biomes.Add(biome);
			return(true);
		}

		/// <summary>
		/// Gets the hardest biome it walked through that's harder than its own, by VikingGear's order
		/// </summary>
		/// <param name="own">Its own biome</param>
		/// <returns>The biome, or -1 when it walked through none harder</returns>
		public int HardestBiome(int own)
		{
			int hardest = -1;
			foreach (int biome in Biomes)
			{
				if (biome > own && biome > hardest)
				{
					hardest = biome;
				}
			}
			return(hardest);
		}

		/// <summary>
		/// Picks the thing most worth telling about the trip
		/// </summary>
		/// <param name="own">The viking's own biome, by VikingGear's order</param>
		/// <returns>The thing, or None when there's nothing worth telling</returns>
		public Fact Notable(int own)
		{
			if (Wrecked)
			{
				return(Fact.Wrecked);
			}
			if (TurnedHome)
			{
				return(Fact.TurnedHome);
			}
			if (VillageGone)
			{
				return(Fact.VillageGone);
			}
			if (Rerouted)
			{
				return(Fact.Rerouted);
			}
			if (Portals > 0)
			{
				return(Fact.Portal);
			}
			if (Boat.Length > 0)
			{
				return(Fact.Sailed);
			}
			if (HardestBiome(own) >= 0)
			{
				return(Fact.Biome);
			}
			return((Foraged > 0) ? Fact.Foraged : Fact.None);
		}

		/// <summary>
		/// Checks whether a viking still talks about a trip it finished at a time
		/// </summary>
		/// <param name="arrived">When it got there, in ticks, or 0 while it's still on the road</param>
		/// <param name="now">The time now, in ticks</param>
		/// <param name="dayLength">How long a game day is, in seconds</param>
		/// <param name="ticksPerSecond">How many ticks a second has</param>
		/// <returns>True while it does</returns>
		internal static bool StillTalked(long arrived, long now, float dayLength, double ticksPerSecond)
		{
			if (arrived <= 0L || now < arrived)
			{
				return(false);
			}
			return((now - arrived) / ticksPerSecond <= dayLength * TalkHours / DayHours);
		}

		/// <summary>
		/// Writes the trip as bytes
		/// </summary>
		/// <returns>The bytes</returns>
		public byte[] Encode()
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(FormatVersion);
			pkg.Write(From);
			pkg.Write(To);
			pkg.Write(ToSpot);
			pkg.Write(Portals);
			pkg.Write(Boat);
			pkg.Write(Wrecked);
			pkg.Write(VillageGone);
			pkg.Write(Rerouted);
			pkg.Write(TurnedHome);
			pkg.Write(Biomes.Count);
			foreach (int biome in Biomes)
			{
				pkg.Write(biome);
			}
			pkg.Write(Foraged);
			pkg.Write(Arrived);
			return(pkg.GetArray());
		}

		/// <summary>
		/// Reads a trip from the bytes it's written as
		/// </summary>
		/// <param name="bytes">The bytes, or null</param>
		/// <returns>The trip, or null when there's none or it can't be read</returns>
		public static TripStory Decode(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0)
			{
				return(null);
			}
			try
			{
				ZPackage pkg = new ZPackage(bytes);
				if (pkg.ReadInt() != FormatVersion)
				{
					return(null);
				}
				TripStory story = new TripStory();
				story.From = pkg.ReadString();
				story.To = pkg.ReadString();
				story.ToSpot = pkg.ReadVector3();
				story.Portals = pkg.ReadInt();
				story.Boat = pkg.ReadString();
				story.Wrecked = pkg.ReadBool();
				story.VillageGone = pkg.ReadBool();
				story.Rerouted = pkg.ReadBool();
				story.TurnedHome = pkg.ReadBool();
				int count = pkg.ReadInt();
				for (int i = 0; i < count; i++)
				{
					story.Biomes.Add(pkg.ReadInt());
				}
				story.Foraged = pkg.ReadInt();
				story.Arrived = pkg.ReadBool();
				return(story);
			}
			catch (System.Exception)
			{
				return(null);
			}
		}

		/// <summary>
		/// Reads a viking's last trip from its ZDO
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>The trip, or null when it has made none</returns>
		public static TripStory Of(ZDO viking)
		{
			return((viking != null) ? Decode(viking.GetByteArray(TripKey)) : null);
		}

		/// <summary>
		/// Changes a viking's trip on its ZDO
		/// </summary>
		/// <param name="viking">The viking's ZDO, owned by this client</param>
		/// <param name="change">What to change</param>
		public static void Note(ZDO viking, System.Action<TripStory> change)
		{
			TripStory story = (Of(viking) ?? new TripStory());
			change(story);
			viking.Set(TripKey, story.Encode());
		}

		/// <summary>
		/// Starts a viking off on a new trip, when its last one is over or went somewhere else - one going on somewhere
		/// else partway keeps what happened so far
		/// </summary>
		/// <param name="viking">The viking's ZDO, owned by this client</param>
		/// <param name="to">The name of the village it's going to</param>
		/// <param name="spot">Where that stands</param>
		/// <param name="from">The name of the village it left, or ""</param>
		public static void Begin(ZDO viking, string to, Vector3 spot, string from)
		{
			TripStory story = Of(viking);
			if (story == null || story.Arrived)
			{
				story = new TripStory { From = from };
				viking.Set(TripAtKey, 0L);
			}
			story.To = to;
			story.ToSpot = spot;
			viking.Set(TripKey, story.Encode());
		}

		/// <summary>
		/// Notes that a viking got where it was going, now
		/// </summary>
		/// <param name="viking">The viking's ZDO, owned by this client</param>
		public static void Arrive(ZDO viking)
		{
			TripStory story = Of(viking);
			if (story == null || story.Arrived)
			{
				return;
			}
			story.Arrived = true;
			viking.Set(TripKey, story.Encode());
			viking.Set(TripAtKey, NetTime.Now());
		}

		/// <summary>
		/// Changes the trip a viking kept on the road is kept with, and notes when it got there when it has
		/// </summary>
		/// <param name="snapshot">The viking's values (see VillageDeparted.SnapshotOf)</param>
		/// <param name="change">What to change</param>
		/// <param name="arrivedAt">When it got there, in ticks, or 0 when it hasn't yet</param>
		/// <returns>The viking's values with the trip changed, or the values as they were when they can't be read</returns>
		public static byte[] Edit(byte[] snapshot, System.Action<TripStory> change, long arrivedAt)
		{
			VillageDeparted.VikingValues values = VillageDeparted.Decode(snapshot);
			if (values == null)
			{
				return(snapshot);
			}
			int at = values.Bytes.FindIndex((KeyValuePair<int, byte[]> value) => value.Key == TripKey);
			TripStory story = (((at >= 0) ? Decode(values.Bytes[at].Value) : null) ?? new TripStory());
			change(story);
			KeyValuePair<int, byte[]> written = new KeyValuePair<int, byte[]>(TripKey, story.Encode());
			if (at >= 0)
			{
				values.Bytes[at] = written;
			}
			else
			{
				values.Bytes.Add(written);
			}
			if (arrivedAt > 0L)
			{
				values.Longs.RemoveAll((KeyValuePair<int, long> value) => value.Key == TripAtKey);
				values.Longs.Add(new KeyValuePair<int, long>(TripAtKey, arrivedAt));
			}
			return(VillageDeparted.Encode(values, new HashSet<int>()));
		}
	}
}
