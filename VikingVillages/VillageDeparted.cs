using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The vikings that left a village a player disbanded, kept so they come back when a village is started at its
	/// bonfire again - each one as it was when it left, its gear, inventory, name, skills and the players it knows
	/// </summary>
	/// <remarks>
	/// A viking is kept as every value in its ZDO, but for the ones that tie it to the village it left, and kept in the
	/// bonfire's ZDO, as it's gone - at once for one no player is near, or once it has run out of every player's reach
	/// (see VillageLife.UpdateLeaving). A village started at the bonfire takes them over, and the next vikings to come
	/// to it - for the beds players put up, or in the place of one that died - are the ones that left, in the order
	/// they left, before any new one (see VillageRespawn.SpawnVillager)
	/// </remarks>
	internal static class VillageDeparted
	{
		/// <summary>
		/// On a bonfire, the vikings that left the village disbanded at it, and on a village's anchor, those still to come
		/// back to it - each written as its values (see Snapshot)
		/// </summary>
		private static readonly int DepartedKey = "kriona.VikingVillages.fireDeparted".GetStableHashCode();
		private static readonly int ReturningKey = "kriona.VikingVillages.villageReturning".GetStableHashCode();

		/// <summary>
		/// On a viking leaving a village that was disbanded, where the bonfire it's kept at once it's gone stands - noted
		/// by where it stands, since the game gives every ZDO a new ID each time the world loads - and how far from there
		/// the bonfire can stand and still be it, in meters
		/// </summary>
		private static readonly int FireKey = "kriona.VikingVillages.vikingLeftFirePosition".GetStableHashCode();
		private const float FireMatch = 1f;

		/// <summary>
		/// The bonfire a village a player started is kept at
		/// </summary>
		private static readonly int BonfireHash = "bonfire".GetStableHashCode();

		/// <summary>
		/// How far from the nearest player a leaving viking has to be to go, the same as the game's night monsters at
		/// dawn (see BaseAI.MoveAwayAndDespawn), in meters
		/// </summary>
		public const float GoneReach = 40f;

		/// <summary>
		/// The most vikings a bonfire or village keeps to come back
		/// </summary>
		private const int MaxKept = 64;

		/// <summary>
		/// The values a viking is kept without - those that tie it to the village it left, its bed and where it patrolled
		/// - by hash
		/// </summary>
		private static HashSet<int> skipped;

		/// <summary>
		/// Notes the bonfire a viking leaving a village is kept at once it's gone
		/// </summary>
		/// <param name="viking">The viking's ZDO, owned by this client</param>
		/// <param name="fire">The bonfire's ZDO</param>
		public static void NoteFire(ZDO viking, ZDO fire)
		{
			viking.Set(FireKey, fire.GetPosition());
		}

		/// <summary>
		/// Finds the bonfire a viking leaving a village is kept at once it's gone - the one standing where it was noted
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>The bonfire's ZDO, or null when none was noted or this client knows of none there</returns>
		public static ZDO LeftFire(ZDO viking)
		{
			Vector3 position = viking.GetVec3(FireKey, Vector3.zero);
			if (position == Vector3.zero)
			{
				return(null);
			}
			List<ZDO> zdos = new List<ZDO>();
			VillageRespawn.ZdosNear(position, FireMatch, zdos);
			return(zdos.Find((ZDO zdo) => zdo.GetPrefab() == BonfireHash && Vector3.Distance(zdo.GetPosition(), position) <= FireMatch));
		}

		/// <summary>
		/// Keeps a leaving viking at its bonfire as it goes, with its inventory as it is right now - nothing is kept when
		/// the bonfire is gone
		/// </summary>
		/// <param name="viking">The viking, owned by this client</param>
		public static void Depart(Humanoid viking)
		{
			ZDO zdo = viking.m_nview.GetZDO();
			VikingItems.RemoveProps(viking.GetInventory());
			ZPackage inventory = new ZPackage();
			viking.GetInventory().Save(inventory);
			zdo.Set(VikingCore.InventoryKey, inventory.GetArray());
			Keep(LeftFire(zdo), zdo);
		}

		/// <summary>
		/// Keeps a viking at a bonfire straight from its ZDO - or with the village started at the bonfire again since it
		/// left, to come back to it with the rest
		/// </summary>
		/// <param name="fire">The bonfire's ZDO, or null when there's none, when nothing is kept</param>
		/// <param name="viking">The viking's ZDO</param>
		public static void Keep(ZDO fire, ZDO viking)
		{
			if (fire == null)
			{
				return;
			}
			ZDO anchor = (fire.GetBool(VillagePaths.BonfireKey) ? VillageRespawn.FindGenerator(fire.GetPosition()) : null);
			bool started = (anchor != null && VillageFounding.IsAnchor(anchor.GetPrefab()));
			ZDO holder = (started ? anchor : fire);
			int key = (started ? ReturningKey : DepartedKey);
			if (!holder.IsOwner())
			{
				holder.SetOwner(ZDOMan.GetSessionID());
			}
			List<byte[]> kept = Read(holder, key);
			kept.Add(Snapshot(viking));
			Write(holder, key, kept);
		}

		/// <summary>
		/// Hands the vikings that left the village disbanded at a bonfire over to the village just started at it, to come
		/// back to it
		/// </summary>
		/// <param name="fire">The bonfire's ZDO, owned by this client</param>
		/// <param name="anchor">The village's anchor's ZDO, owned by this client</param>
		/// <returns>How many are coming back</returns>
		public static int HandOver(ZDO fire, ZDO anchor)
		{
			List<byte[]> kept = Read(fire, DepartedKey);
			if (kept.Count == 0)
			{
				return(0);
			}
			Write(anchor, ReturningKey, kept);
			// Emptied rather than removed, since a removal isn't sent to the other clients
			Write(fire, DepartedKey, new List<byte[]>());
			return(kept.Count);
		}

		/// <summary>
		/// Makes a viking just spawned for a village the next of those that left it coming back, when there are any -
		/// dropping any whose values can't be read, so they aren't tried again for every viking after them
		/// </summary>
		/// <param name="village">The village, whose anchor this client owns</param>
		/// <param name="viking">The new viking's ZDO, owned by this client, before its Start loads what it notes</param>
		/// <returns>True when it's one coming back</returns>
		public static bool TakeReturning(Village village, ZDO viking)
		{
			List<byte[]> returning = Read(village.Zdo, ReturningKey);
			if (returning.Count == 0)
			{
				return(false);
			}
			while (returning.Count > 0)
			{
				VikingValues values = Decode(returning[0]);
				returning.RemoveAt(0);
				if (values == null)
				{
					Plugin.Log.LogWarning("Couldn't read a viking that left the village at " + village.Farm.ToString("F0") + ", so it won't come back");
					continue;
				}
				Apply(values, viking);
				Write(village.Zdo, ReturningKey, returning);
				Plugin.Log.LogInfo("A viking that left came back to the village at " + village.Farm.ToString("F0") + ", " + returning.Count + " still to come");
				return(true);
			}
			Write(village.Zdo, ReturningKey, returning);
			return(false);
		}

		/// <summary>
		/// Counts the vikings still to come back to a village
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>How many</returns>
		public static int Returning(Village village)
		{
			return(Read(village.Zdo, ReturningKey).Count);
		}

		/// <summary>
		/// Writes every value in a viking's ZDO but the ones it's kept without (see Skipped), by type
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>The values</returns>
		private static byte[] Snapshot(ZDO viking)
		{
			VikingValues values = new VikingValues();
			ZDOExtraData.GetData(viking.m_uid, out values.Floats, out values.Vec3s, out values.Quats, out values.Ints, out values.Longs, out values.Strings, out values.Bytes, out ZDOConnection _);
			return(Encode(values, Skipped()));
		}

		/// <summary>
		/// Sets the values a viking was kept with on a new viking's ZDO
		/// </summary>
		/// <param name="values">The values (see Decode)</param>
		/// <param name="viking">The new viking's ZDO, owned by this client</param>
		private static void Apply(VikingValues values, ZDO viking)
		{
			foreach (KeyValuePair<int, float> entry in values.Floats)
			{
				viking.Set(entry.Key, entry.Value);
			}
			foreach (KeyValuePair<int, Vector3> entry in values.Vec3s)
			{
				viking.Set(entry.Key, entry.Value);
			}
			foreach (KeyValuePair<int, Quaternion> entry in values.Quats)
			{
				viking.Set(entry.Key, entry.Value);
			}
			foreach (KeyValuePair<int, int> entry in values.Ints)
			{
				viking.Set(entry.Key, entry.Value);
			}
			foreach (KeyValuePair<int, long> entry in values.Longs)
			{
				viking.Set(entry.Key, entry.Value);
			}
			foreach (KeyValuePair<int, string> entry in values.Strings)
			{
				viking.Set(entry.Key, entry.Value);
			}
			foreach (KeyValuePair<int, byte[]> entry in values.Bytes)
			{
				viking.Set(entry.Key, entry.Value);
			}
		}

		/// <summary>
		/// Every value in a viking's ZDO, by type, each with its hash
		/// </summary>
		internal class VikingValues
		{
			public List<KeyValuePair<int, float>> Floats = new List<KeyValuePair<int, float>>();
			public List<KeyValuePair<int, Vector3>> Vec3s = new List<KeyValuePair<int, Vector3>>();
			public List<KeyValuePair<int, Quaternion>> Quats = new List<KeyValuePair<int, Quaternion>>();
			public List<KeyValuePair<int, int>> Ints = new List<KeyValuePair<int, int>>();
			public List<KeyValuePair<int, long>> Longs = new List<KeyValuePair<int, long>>();
			public List<KeyValuePair<int, string>> Strings = new List<KeyValuePair<int, string>>();
			public List<KeyValuePair<int, byte[]>> Bytes = new List<KeyValuePair<int, byte[]>>();
		}

		/// <summary>
		/// Writes a viking's values as bytes, a section for each type in turn - floats, vectors, rotations, ints, longs,
		/// strings and byte arrays - leaving out the values to skip
		/// </summary>
		/// <param name="values">The values</param>
		/// <param name="skip">The hashes of the values to leave out</param>
		/// <returns>The bytes</returns>
		internal static byte[] Encode(VikingValues values, HashSet<int> skip)
		{
			ZPackage pkg = new ZPackage();
			WriteSection(pkg, values.Floats, skip, (float value) => pkg.Write(value));
			WriteSection(pkg, values.Vec3s, skip, (Vector3 value) => pkg.Write(value));
			WriteSection(pkg, values.Quats, skip, (Quaternion value) => pkg.Write(value));
			WriteSection(pkg, values.Ints, skip, (int value) => pkg.Write(value));
			WriteSection(pkg, values.Longs, skip, (long value) => pkg.Write(value));
			WriteSection(pkg, values.Strings, skip, (string value) => pkg.Write(value));
			WriteSection(pkg, values.Bytes, skip, (byte[] value) => pkg.Write(value));
			return(pkg.GetArray());
		}

		/// <summary>
		/// Reads a viking's values from the bytes they're written as, all of them before any is used
		/// </summary>
		/// <param name="snapshot">The bytes (see Encode)</param>
		/// <returns>The values, or null when the bytes can't be read</returns>
		internal static VikingValues Decode(byte[] snapshot)
		{
			try
			{
				ZPackage pkg = new ZPackage(snapshot);
				VikingValues values = new VikingValues();
				values.Floats = ReadSection(pkg, () => pkg.ReadSingle());
				values.Vec3s = ReadSection(pkg, () => pkg.ReadVector3());
				values.Quats = ReadSection(pkg, () => pkg.ReadQuaternion());
				values.Ints = ReadSection(pkg, () => pkg.ReadInt());
				values.Longs = ReadSection(pkg, () => pkg.ReadLong());
				values.Strings = ReadSection(pkg, () => pkg.ReadString());
				values.Bytes = ReadSection(pkg, () => ReadWholeByteArray(pkg));
				return(values);
			}
			catch (System.Exception)
			{
				return(null);
			}
		}

		/// <summary>
		/// Writes one type's values - how many there are, then each one's hash and value - leaving out the values to skip
		/// </summary>
		/// <typeparam name="T">The type</typeparam>
		/// <param name="pkg">The package written to</param>
		/// <param name="entries">The values, by hash</param>
		/// <param name="skip">The hashes of the values to leave out</param>
		/// <param name="write">Writes one value to the package</param>
		private static void WriteSection<T>(ZPackage pkg, List<KeyValuePair<int, T>> entries, HashSet<int> skip, System.Action<T> write)
		{
			List<KeyValuePair<int, T>> kept = entries.FindAll((KeyValuePair<int, T> entry) => !skip.Contains(entry.Key));
			pkg.Write(kept.Count);
			foreach (KeyValuePair<int, T> entry in kept)
			{
				pkg.Write(entry.Key);
				write(entry.Value);
			}
		}

		/// <summary>
		/// Reads one type's values (see WriteSection)
		/// </summary>
		/// <typeparam name="T">The type</typeparam>
		/// <param name="pkg">The package read from</param>
		/// <param name="read">Reads one value from the package</param>
		/// <returns>The values, by hash</returns>
		private static List<KeyValuePair<int, T>> ReadSection<T>(ZPackage pkg, System.Func<T> read)
		{
			List<KeyValuePair<int, T>> entries = new List<KeyValuePair<int, T>>();
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				int key = pkg.ReadInt();
				entries.Add(new KeyValuePair<int, T>(key, read()));
			}
			return(entries);
		}

		/// <summary>
		/// Reads a byte array written with its length, failing when the bytes end before it does rather than giving
		/// back the part there is, the way ZPackage.ReadByteArray does
		/// </summary>
		/// <param name="pkg">The package read from</param>
		/// <returns>The byte array</returns>
		private static byte[] ReadWholeByteArray(ZPackage pkg)
		{
			int length = pkg.ReadInt();
			byte[] array = pkg.ReadByteArray(length);
			if (array.Length != length)
			{
				throw new System.IO.EndOfStreamException("The bytes end partway through a byte array");
			}
			return(array);
		}

		/// <summary>
		/// Lists the values a viking is kept without - that it's leaving or turned back and the bonfire it's kept at, its
		/// village, home and bed, and where it patrolled - made the first time they're wanted
		/// </summary>
		/// <returns>Their hashes</returns>
		private static HashSet<int> Skipped()
		{
			if (skipped != null)
			{
				return(skipped);
			}
			skipped = new HashSet<int>
			{
				VillageFounding.LeavingKey,
				VillageFounding.TurnedBackKey,
				FireKey,
				Village.VillageKey,
				Village.HomeKey,
				VillageBeds.BedKey.Key,
				VillageBeds.BedKey.Value,
				VillageLife.ArrivingKey,
				ZDOVars.s_patrol,
				ZDOVars.s_patrolPoint,
				ZDOVars.s_spawnPoint
			};
			return(skipped);
		}

		/// <summary>
		/// Reads the vikings kept in a ZDO
		/// </summary>
		/// <param name="zdo">The bonfire's or anchor's ZDO</param>
		/// <param name="key">Where they're kept</param>
		/// <returns>Each one's values, the first to leave first</returns>
		private static List<byte[]> Read(ZDO zdo, int key)
		{
			byte[] bytes = zdo.GetByteArray(key);
			if (bytes == null)
			{
				return(new List<byte[]>());
			}
			List<byte[]> kept = DecodeKept(bytes);
			if (kept == null)
			{
				Plugin.Log.LogWarning("Couldn't read the vikings kept at " + zdo.GetPosition().ToString("F0"));
				return(new List<byte[]>());
			}
			return(kept);
		}

		/// <summary>
		/// Writes the vikings kept in a ZDO, keeping the last MaxKept to leave
		/// </summary>
		/// <param name="zdo">The bonfire's or anchor's ZDO, owned by this client</param>
		/// <param name="key">Where they're kept</param>
		/// <param name="kept">Each one's values, the first to leave first</param>
		private static void Write(ZDO zdo, int key, List<byte[]> kept)
		{
			zdo.Set(key, EncodeKept(kept));
		}

		/// <summary>
		/// Reads the vikings kept from the bytes they're written as
		/// </summary>
		/// <param name="bytes">The bytes (see EncodeKept)</param>
		/// <returns>Each one's values, the first to leave first, or null when the bytes can't be read</returns>
		internal static List<byte[]> DecodeKept(byte[] bytes)
		{
			List<byte[]> kept = new List<byte[]>();
			try
			{
				ZPackage pkg = new ZPackage(bytes);
				int count = pkg.ReadInt();
				for (int i = 0; i < count; i++)
				{
					kept.Add(ReadWholeByteArray(pkg));
				}
			}
			catch (System.Exception)
			{
				return(null);
			}
			return(kept);
		}

		/// <summary>
		/// Writes the vikings kept as bytes, keeping the last MaxKept to leave
		/// </summary>
		/// <param name="kept">Each one's values, the first to leave first</param>
		/// <returns>The bytes</returns>
		internal static byte[] EncodeKept(List<byte[]> kept)
		{
			int start = Mathf.Max(0, kept.Count - MaxKept);
			ZPackage pkg = new ZPackage();
			pkg.Write(kept.Count - start);
			for (int i = start; i < kept.Count; i++)
			{
				pkg.Write(kept[i]);
			}
			return(pkg.GetArray());
		}
	}
}
