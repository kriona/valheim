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
		/// The easiest biome among them, by VikingGear's order, for finding another village they can go to
		/// </summary>
		public int Biome;

		/// <summary>
		/// The villages they found no one in on the way, to leave out when another is looked for
		/// </summary>
		public List<Vector3> Excluded = new List<Vector3>();

		/// <summary>
		/// The version of how an entry is written, which a reader skips entries written another way by
		/// </summary>
		private const int FormatVersion = 2;

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
			double seconds = System.Math.Max(0.0, (now - Unloaded) / ticksPerSecond);
			return(Spent + Mathf.Min(Remaining, (float)seconds));
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
		}

		/// <summary>
		/// Reads an entry from a package
		/// </summary>
		/// <param name="pkg">The package</param>
		/// <returns>The entry, or null when it was written another way</returns>
		internal static TravelEntry Read(ZPackage pkg)
		{
			if (pkg.ReadInt() != FormatVersion)
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
