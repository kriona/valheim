using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Puts a Home pin with a village's name on the local player's map, where its farm or anchor stands, once the village
	/// has a name and is loaded around them - moved over to the new name when it's renamed, and taken off when its name
	/// is taken away or it's disbanded
	/// </summary>
	/// <remarks>
	/// The pins put down are noted in the player's own data for each world, by where the village stands and its name,
	/// so a pin the player takes off themselves stays off until the village is renamed
	/// </remarks>
	internal static class VillageMapPins
	{
		/// <summary>
		/// The start of the key in the player's data that notes the pins put down in a world, followed by the world's ID
		/// </summary>
		private const string RecordPrefix = "kriona_ZombieApocalypse_villagePins_";

		/// <summary>
		/// Seconds between looks at the loaded villages' names
		/// </summary>
		private const float CheckInterval = 2f;

		/// <summary>
		/// How far from where a village stands a pin can be and still be its, in meters
		/// </summary>
		private const float PinMatch = 1f;

		private static float nextCheck;

		/// <summary>
		/// Puts down, moves over or takes off the pins of the loaded villages whose names have changed since they were
		/// last pinned - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			Player player = Player.m_localPlayer;
			if (Time.time < nextCheck || player == null || Minimap.instance == null || ZNet.instance == null || !ServerCheck.Active)
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			Dictionary<string, int> pinned = Read(player);
			bool changed = false;
			foreach (Village village in Village.Loaded())
			{
				string key = Key(village.Farm);
				string name = VillageNames.Get(village);
				bool had = pinned.TryGetValue(key, out int was);
				if (name.Length == 0)
				{
					if (had)
					{
						TakeOff(village.Farm, was);
						pinned.Remove(key);
						changed = true;
					}
					continue;
				}
				int hash = name.GetStableHashCode();
				if (had && was == hash)
				{
					continue;
				}
				if (had)
				{
					TakeOff(village.Farm, was);
				}
				Minimap.instance.AddPin(village.Farm, Minimap.PinType.Icon1, name, true, false);
				pinned[key] = hash;
				changed = true;
			}
			if (changed)
			{
				Write(player, pinned);
			}
		}

		/// <summary>
		/// Takes a village's pin off the local player's map as it's disbanded
		/// </summary>
		/// <param name="village">The village</param>
		public static void Forget(Village village)
		{
			Player player = Player.m_localPlayer;
			if (player == null || Minimap.instance == null || ZNet.instance == null)
			{
				return;
			}
			Dictionary<string, int> pinned = Read(player);
			string key = Key(village.Farm);
			if (!pinned.TryGetValue(key, out int was))
			{
				return;
			}
			TakeOff(village.Farm, was);
			pinned.Remove(key);
			Write(player, pinned);
		}

		/// <summary>
		/// Takes the Home pin put down for a village off the map, when it's still there
		/// </summary>
		/// <param name="farm">Where the village stands</param>
		/// <param name="name">The hash of the name it was put down with</param>
		private static void TakeOff(Vector3 farm, int name)
		{
			Minimap.PinData pin = Minimap.instance.m_pins.Find((Minimap.PinData candidate) => candidate.m_type == Minimap.PinType.Icon1 && Utils.DistanceXZ(candidate.m_pos, farm) <= PinMatch && candidate.m_name.GetStableHashCode() == name);
			if (pin != null)
			{
				Minimap.instance.RemovePin(pin);
			}
		}

		/// <summary>
		/// Makes the key a village's pin is noted by - where it stands, to the meter
		/// </summary>
		/// <param name="farm">Where the village stands</param>
		/// <returns>The key</returns>
		internal static string Key(Vector3 farm)
		{
			return(Mathf.RoundToInt(farm.x) + "," + Mathf.RoundToInt(farm.z));
		}

		/// <summary>
		/// Reads the pins put down in this world from the player's data
		/// </summary>
		/// <param name="player">The local player</param>
		/// <returns>The hash of the name each was put down with, by its village's key</returns>
		private static Dictionary<string, int> Read(Player player)
		{
			if (!player.m_customData.TryGetValue(RecordKey(), out string value))
			{
				return(new Dictionary<string, int>());
			}
			return(Parse(value));
		}

		/// <summary>
		/// Writes the pins put down in this world to the player's data
		/// </summary>
		/// <param name="player">The local player</param>
		/// <param name="pinned">The hash of the name each was put down with, by its village's key</param>
		private static void Write(Player player, Dictionary<string, int> pinned)
		{
			string value = Format(pinned);
			if (value.Length == 0)
			{
				player.m_customData.Remove(RecordKey());
				return;
			}
			player.m_customData[RecordKey()] = value;
		}

		/// <summary>
		/// Reads the pins put down from how they're noted in the player's data - "key=hash" entries split by ";",
		/// leaving out any entry with no key or no hash, the last of the same key kept
		/// </summary>
		/// <param name="value">The note (see Format)</param>
		/// <returns>The hash of the name each was put down with, by its village's key</returns>
		internal static Dictionary<string, int> Parse(string value)
		{
			Dictionary<string, int> pinned = new Dictionary<string, int>();
			foreach (string entry in value.Split(';'))
			{
				int split = entry.LastIndexOf('=');
				if (split > 0 && int.TryParse(entry.Substring(split + 1), out int hash))
				{
					pinned[entry.Substring(0, split)] = hash;
				}
			}
			return(pinned);
		}

		/// <summary>
		/// Writes the pins put down the way they're noted in the player's data
		/// </summary>
		/// <param name="pinned">The hash of the name each was put down with, by its village's key</param>
		/// <returns>The note, or "" when there are none</returns>
		internal static string Format(Dictionary<string, int> pinned)
		{
			List<string> entries = new List<string>();
			foreach (KeyValuePair<string, int> entry in pinned)
			{
				entries.Add(entry.Key + "=" + entry.Value);
			}
			return(string.Join(";", entries));
		}

		/// <summary>
		/// Makes the key in the player's data that notes the pins put down in this world
		/// </summary>
		/// <returns>The key</returns>
		private static string RecordKey()
		{
			return(RecordPrefix + ZNet.instance.GetWorldUID());
		}
	}
}
