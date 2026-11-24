using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Vikings out of every player's reach on the road to a village, kept by the server until they're due there (see
	/// VillageTravel) - a group that set out together is one entry, so they arrive together
	/// </summary>
	internal class TravelEntry
	{
		/// <summary>
		/// The vikings, each as its values (see VillageDeparted.SnapshotOf)
		/// </summary>
		public List<byte[]> Vikings = new List<byte[]>();

		/// <summary>
		/// Where the village they're going to stands - its farm's planned spot or its anchor - and its name, or ""
		/// </summary>
		public Vector3 Destination;
		public string Name = "";

		/// <summary>
		/// When they're due there, in ZNet time ticks
		/// </summary>
		public long Due;

		/// <summary>
		/// Where they were when they went out of every player's reach, which they arrive from
		/// </summary>
		public Vector3 From;

		/// <summary>
		/// The bonfire of the village a player disbanded that they left, or zero when they didn't leave one - so a village
		/// started there again calls them back
		/// </summary>
		public Vector3 OriginFire;

		/// <summary>
		/// When they went out of every player's reach, in ticks, how long they had been on the road since they set out by
		/// then and how long the rest of it takes, on foot and by sea, in seconds
		/// </summary>
		public long Unloaded;
		public float Spent;
		public float Remaining;

		/// <summary>
		/// Whether they're coming back to a village started again at the bonfire they left, to come to its beds rather
		/// than walk in
		/// </summary>
		public bool Returning;

		/// <summary>
		/// The name of the village they left, or ""
		/// </summary>
		public string FromName = "";

		/// <summary>
		/// The easiest biome among them, by VikingGear's order, for finding another village they can go to, the hardest,
		/// whose best boat they sail in, and whether any of them carries something a portal won't take
		/// </summary>
		public int Biome;
		public int BoatBiome;
		public bool Unteleportable;

		/// <summary>
		/// The villages they found no one in on the way, to leave out when another is looked for
		/// </summary>
		public List<Vector3> Excluded = new List<Vector3>();

		/// <summary>
		/// For a group out on a quest (see VillageQuests), the quest's ID, or "" for a group on the road, its kind, and
		/// when it was due back at the village it set out from before that village was disbanded, in ticks - so a
		/// village started again where it stood calls them back for then
		/// </summary>
		public string QuestId = "";
		public int QuestKind;
		public long QuestDue;

		/// <summary>
		/// For a group out on a quest, how many vikings set out on it, so the group isn't brought home before the last of
		/// them has left the village (see QuestWaiting)
		/// </summary>
		public int QuestMembers;

		/// <summary>
		/// For a group out on a quest, what it's for, or "", and how long the work there takes, in seconds - for the
		/// server to plan the trip there and back (see VillageTravel.PlanQuest)
		/// </summary>
		public string QuestItem = "";
		public float QuestActive;

		/// <summary>
		/// The rest of the trip as it was when Unloaded, or null when it isn't known - an entry kept before trips were, or
		/// one whose road couldn't be planned - which tells where the group is and when it's due (see VillageTravel)
		/// </summary>
		public TravelJourney Journey;

		/// <summary>
		/// Whether the group has been given what it picked along its road (see TripForage)
		/// </summary>
		public bool Foraged;

		/// <summary>
		/// The version of how an entry is written, which a reader skips entries written another way by, and the earliest
		/// version it still reads, which has no quest
		/// </summary>
		private const int FormatVersion = 6;
		private const int OldestVersion = 2;

		/// <summary>
		/// Checks whether a group out on a quest waits for more of its vikings before it's brought home - while fewer of
		/// them are with it than set out, until a while past when it's due, so one that never leaves the village, killed
		/// on its way out, say, doesn't keep the rest away
		/// </summary>
		/// <param name="have">How many vikings the group has</param>
		/// <param name="expected">How many set out, or 0 when that isn't known</param>
		/// <param name="now">The time now, in ticks</param>
		/// <param name="due">When the group is due, in ticks</param>
		/// <param name="grace">How long past that it waits, in ticks</param>
		/// <returns>True while it waits</returns>
		internal static bool QuestWaiting(int have, int expected, long now, long due, long grace)
		{
			return(have < expected && now < due + grace);
		}

		/// <summary>
		/// Checks whether the group is out on a quest rather than on the road
		/// </summary>
		public bool OnQuest
		{
			get
			{
				return(QuestId.Length > 0);
			}
		}

		/// <summary>
		/// Works out how long a group has been on the road since it set out - what it had spent when it went out of every
		/// player's reach, and the time since, no longer than the whole road takes - which is how long it takes to come
		/// back the way it came
		/// </summary>
		/// <param name="now">The time now, in ticks</param>
		/// <param name="ticksPerSecond">How many ticks a second has</param>
		/// <returns>How long it has been on the road, in seconds</returns>
		internal float Progress(long now, double ticksPerSecond)
		{
			return(Spent + Elapsed(now, ticksPerSecond));
		}

		/// <summary>
		/// Works out how long a group has been on the rest of its trip since Unloaded, no longer than the rest takes
		/// </summary>
		/// <param name="now">The time now, in ticks</param>
		/// <param name="ticksPerSecond">How many ticks a second has</param>
		/// <returns>How long, in seconds</returns>
		internal float Elapsed(long now, double ticksPerSecond)
		{
			double seconds = System.Math.Max(0.0, (now - Unloaded) / ticksPerSecond);
			return(Mathf.Min(Remaining, (float)seconds));
		}

		/// <summary>
		/// Starts the rest of the group's trip over from now - due once it's done, arriving from the way its last road
		/// comes in - counting what it has spent on the road so far
		/// </summary>
		/// <param name="journey">The rest of the trip</param>
		/// <param name="now">The time now, in ticks</param>
		/// <param name="ticksPerSecond">How many ticks a second has</param>
		/// <param name="keepFrom">Whether to keep where it arrives from, as a group coming home from a quest to where it
		/// left does</param>
		internal void Restart(TravelJourney journey, long now, double ticksPerSecond, bool keepFrom)
		{
			if (Unloaded > 0L && now > Unloaded)
			{
				Spent += Elapsed(now, ticksPerSecond);
			}
			Journey = journey;
			Unloaded = now;
			Remaining = journey.TotalSeconds();
			Due = now + (long)(Remaining * ticksPerSecond);
			if (!keepFrom)
			{
				From = journey.Approach(From);
			}
		}

		/// <summary>
		/// Works out where the group is now - along its trip, or where it arrives from when its trip isn't known
		/// </summary>
		/// <param name="now">The time now, in ticks</param>
		/// <param name="ticksPerSecond">How many ticks a second has</param>
		/// <returns>Where it is</returns>
		internal Vector3 Position(long now, double ticksPerSecond)
		{
			if (Journey == null)
			{
				return(From);
			}
			return(Journey.At(Elapsed(now, ticksPerSecond), From).Position);
		}

		/// <summary>
		/// Writes an entry to a package
		/// </summary>
		/// <param name="pkg">The package</param>
		internal void Write(ZPackage pkg)
		{
			pkg.Write(FormatVersion);
			pkg.Write(Vikings.Count);
			foreach (byte[] viking in Vikings)
			{
				pkg.Write(viking);
			}
			pkg.Write(Destination);
			pkg.Write(Name);
			pkg.Write(Due);
			pkg.Write(From);
			pkg.Write(OriginFire);
			pkg.Write(Unloaded);
			pkg.Write(Spent);
			pkg.Write(Remaining);
			pkg.Write(Returning);
			pkg.Write(FromName);
			pkg.Write(Biome);
			pkg.Write(Excluded.Count);
			foreach (Vector3 excluded in Excluded)
			{
				pkg.Write(excluded);
			}
			pkg.Write(QuestId);
			pkg.Write(QuestKind);
			pkg.Write(QuestDue);
			pkg.Write(QuestMembers);
			pkg.Write(BoatBiome);
			pkg.Write(Unteleportable);
			pkg.Write(QuestItem);
			pkg.Write(QuestActive);
			pkg.Write((Journey != null) ? Journey.Encode() : new byte[0]);
			pkg.Write(Foraged);
		}

		/// <summary>
		/// Reads an entry from a package - one written before quests as a group on the road
		/// </summary>
		/// <param name="pkg">The package</param>
		/// <returns>The entry, or null when it was written another way</returns>
		internal static TravelEntry Read(ZPackage pkg)
		{
			int version = pkg.ReadInt();
			if (version < OldestVersion || version > FormatVersion)
			{
				return(null);
			}
			TravelEntry entry = new TravelEntry();
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				entry.Vikings.Add(pkg.ReadByteArray());
			}
			entry.Destination = pkg.ReadVector3();
			entry.Name = pkg.ReadString();
			entry.Due = pkg.ReadLong();
			entry.From = pkg.ReadVector3();
			entry.OriginFire = pkg.ReadVector3();
			entry.Unloaded = pkg.ReadLong();
			entry.Spent = pkg.ReadSingle();
			entry.Remaining = pkg.ReadSingle();
			entry.Returning = pkg.ReadBool();
			entry.FromName = pkg.ReadString();
			entry.Biome = pkg.ReadInt();
			int excluded = pkg.ReadInt();
			for (int i = 0; i < excluded; i++)
			{
				entry.Excluded.Add(pkg.ReadVector3());
			}
			if (version >= 3)
			{
				entry.QuestId = pkg.ReadString();
				entry.QuestKind = pkg.ReadInt();
				entry.QuestDue = pkg.ReadLong();
			}
			if (version >= 4)
			{
				entry.QuestMembers = pkg.ReadInt();
			}
			entry.BoatBiome = entry.Biome;
			if (version >= 5)
			{
				entry.BoatBiome = pkg.ReadInt();
				entry.Unteleportable = pkg.ReadBool();
			}
			if (version >= 6)
			{
				entry.QuestItem = pkg.ReadString();
				entry.QuestActive = pkg.ReadSingle();
				entry.Journey = TravelJourney.Decode(pkg.ReadByteArray());
				entry.Foraged = pkg.ReadBool();
			}
			return(entry);
		}

		/// <summary>
		/// Writes a list of entries as bytes, each in a package of its own so one written another way can be skipped
		/// </summary>
		/// <param name="entries">The entries</param>
		/// <returns>The bytes</returns>
		internal static byte[] EncodeAll(List<TravelEntry> entries)
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(entries.Count);
			foreach (TravelEntry entry in entries)
			{
				ZPackage one = new ZPackage();
				entry.Write(one);
				pkg.Write(one.GetArray());
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Reads a list of entries from the bytes they're written as, skipping any that can't be read
		/// </summary>
		/// <param name="bytes">The bytes, or null</param>
		/// <returns>The entries</returns>
		internal static List<TravelEntry> DecodeAll(byte[] bytes)
		{
			List<TravelEntry> entries = new List<TravelEntry>();
			if (bytes == null)
			{
				return(entries);
			}
			try
			{
				ZPackage pkg = new ZPackage(bytes);
				int count = pkg.ReadInt();
				for (int i = 0; i < count; i++)
				{
					TravelEntry entry = Decode(pkg.ReadByteArray());
					if (entry != null)
					{
						entries.Add(entry);
					}
				}
			}
			catch (System.Exception)
			{
				return(entries);
			}
			return(entries);
		}

		/// <summary>
		/// Reads one entry from the bytes it's written as
		/// </summary>
		/// <param name="bytes">The bytes</param>
		/// <returns>The entry, or null when it can't be read</returns>
		internal static TravelEntry Decode(byte[] bytes)
		{
			try
			{
				return(Read(new ZPackage(bytes)));
			}
			catch (System.Exception)
			{
				return(null);
			}
		}

		/// <summary>
		/// Writes one entry as bytes
		/// </summary>
		/// <returns>The bytes</returns>
		internal byte[] Encode()
		{
			ZPackage pkg = new ZPackage();
			Write(pkg);
			return(pkg.GetArray());
		}
	}
}
