using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What vikings out of every player's reach pick along their road - a few of what grows in the biomes they walk
	/// through, more the longer they walk through them, less what they eat on the way, and for hunters, a prey or two -
	/// put in their inventory as they arrive, on the server, which keeps them on the road (see VillageTravel)
	/// </summary>
	/// <remarks>
	/// A viking finds one thing for every SecondsPerFind seconds on foot through a biome where something grows, up to
	/// MostFound, picked by how long it spent in each biome, and eats one of what it found that it can eat for every
	/// EatEvery seconds of the trip, before any food of its own - so a long trip often brings back only one or two. A
	/// viking out hunting also takes a prey for every SecondsPerPrey seconds on foot through a biome with prey, up to
	/// MostPrey. Only the biomes no harder than the viking's own count - a Meadows viking crossing the Plains finds
	/// nothing there. Vikings out to mine or raid only pick on the way home, once they have what they went for. The
	/// numbers are small on purpose: a trip brings back a handful, never stacks
	/// </remarks>
	internal static class TripForage
	{
		/// <summary>
		/// How many seconds on foot through a biome where something grows a viking takes to find one thing, the most it
		/// finds on one trip, and how many seconds of the trip it eats one of what it found in
		/// </summary>
		internal const float SecondsPerFind = 180f;
		internal const int MostFound = 5;
		internal const float EatEvery = 900f;

		/// <summary>
		/// How many seconds on foot through a biome with prey a hunter takes to take one on its road, and the most it takes
		/// on one trip
		/// </summary>
		internal const float SecondsPerPrey = 300f;
		internal const int MostPrey = 2;

		/// <summary>
		/// What grows in each biome that a viking picks on its way, by VikingGear's order
		/// </summary>
		private static readonly string[][] Grows =
		{
			new[] { "Raspberry", "Mushroom" },
			new[] { "Blueberries", "Mushroom", "MushroomYellow", "Thistle" },
			new string[0],
			new string[0],
			new[] { "Cloudberry" },
			new[] { "MushroomJotunPuffs", "MushroomMagecap" },
			new[] { "Fiddleheadfern", "MushroomSmokePuff" },
			new string[0]
		};

		/// <summary>
		/// Gets what grows in a biome that a viking picks on its way
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <returns>The items' prefab names, none for a biome where nothing grows</returns>
		internal static string[] GrowsIn(int biome)
		{
			return((biome >= 0 && biome < Grows.Length) ? Grows[biome] : new string[0]);
		}

		/// <summary>
		/// Works out how many things a viking finds on its way
		/// </summary>
		/// <param name="seconds">How many seconds it spent on foot in biomes where something grows</param>
		/// <returns>How many</returns>
		internal static int Found(float seconds)
		{
			return(Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, seconds) / SecondsPerFind), 0, MostFound));
		}

		/// <summary>
		/// Works out how many of what a viking found it eats on the way
		/// </summary>
		/// <param name="edible">How many of what it found it can eat</param>
		/// <param name="tripSeconds">How long the trip took, in seconds</param>
		/// <returns>How many it eats</returns>
		internal static int Eaten(int edible, float tripSeconds)
		{
			return(Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, tripSeconds) / EatEvery), 0, Mathf.Max(0, edible)));
		}

		/// <summary>
		/// Works out how many prey a hunter takes on its way
		/// </summary>
		/// <param name="seconds">How many seconds it spent on foot in biomes with prey</param>
		/// <returns>How many</returns>
		internal static int PreyFound(float seconds)
		{
			return(Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, seconds) / SecondsPerPrey), 0, MostPrey));
		}

		/// <summary>
		/// Picks what a viking finds growing, each thing from a biome no harder than its own, picked by how long it spent
		/// there
		/// </summary>
		/// <param name="seconds">How many seconds it spent on foot in each biome, by VikingGear's order</param>
		/// <param name="ownBiome">The viking's own biome, by VikingGear's order</param>
		/// <param name="roll">Gives a random number from 0 to 1</param>
		/// <returns>What it found, a prefab name for each thing</returns>
		internal static List<string> Pick(Dictionary<int, float> seconds, int ownBiome, System.Func<float> roll)
		{
			return(PickFrom(seconds, ownBiome, GrowsIn, Found, roll));
		}

		/// <summary>
		/// Picks the prey a hunter takes on its way, each from a biome no harder than its own, picked by how long it spent
		/// there
		/// </summary>
		/// <param name="seconds">How many seconds it spent on foot in each biome, by VikingGear's order</param>
		/// <param name="ownBiome">The viking's own biome, by VikingGear's order</param>
		/// <param name="preyIn">Gets the prey of a biome, by prefab name</param>
		/// <param name="roll">Gives a random number from 0 to 1</param>
		/// <returns>The prey it took, a prefab name for each</returns>
		internal static List<string> PickPrey(Dictionary<int, float> seconds, int ownBiome, System.Func<int, string[]> preyIn, System.Func<float> roll)
		{
			return(PickFrom(seconds, ownBiome, preyIn, PreyFound, roll));
		}

		/// <summary>
		/// Picks what a viking finds on its way, each from a biome no harder than its own that has any, picked by how long
		/// it spent there, and then at random among what that biome has
		/// </summary>
		/// <param name="seconds">How many seconds it spent on foot in each biome, by VikingGear's order</param>
		/// <param name="ownBiome">The viking's own biome, by VikingGear's order</param>
		/// <param name="has">Gets what a biome has, by prefab name</param>
		/// <param name="count">Works out how many it finds from the seconds it spent in biomes that have any</param>
		/// <param name="roll">Gives a random number from 0 to 1</param>
		/// <returns>What it found, a prefab name for each</returns>
		private static List<string> PickFrom(Dictionary<int, float> seconds, int ownBiome, System.Func<int, string[]> has, System.Func<float, int> count, System.Func<float> roll)
		{
			List<KeyValuePair<string[], float>> biomes = new List<KeyValuePair<string[], float>>();
			float total = 0f;
			foreach (KeyValuePair<int, float> entry in seconds)
			{
				string[] there = ((entry.Key <= ownBiome) ? has(entry.Key) : new string[0]);
				if (there.Length > 0 && entry.Value > 0f)
				{
					biomes.Add(new KeyValuePair<string[], float>(there, entry.Value));
					total += entry.Value;
				}
			}
			List<string> found = new List<string>();
			int wanted = count(total);
			for (int i = 0; i < wanted; i++)
			{
				float at = roll() * total;
				// A roll of exactly 1 falls past the end, so the last biome takes it
				KeyValuePair<string[], float> picked = biomes[biomes.Count - 1];
				foreach (KeyValuePair<string[], float> biome in biomes)
				{
					if (at < biome.Value)
					{
						picked = biome;
						break;
					}
					at -= biome.Value;
				}
				found.Add(picked.Key[Mathf.Clamp(Mathf.FloorToInt(roll() * picked.Key.Length), 0, picked.Key.Length - 1)]);
			}
			return(found);
		}

		/// <summary>
		/// Takes away what a viking eats of what it found - the things it can eat, in the order it found them
		/// </summary>
		/// <param name="found">What it found, a prefab name for each thing</param>
		/// <param name="edible">Checks whether a thing can be eaten</param>
		/// <param name="tripSeconds">How long the trip took, in seconds</param>
		/// <returns>What it has left, by prefab name, with how many of each</returns>
		internal static List<KeyValuePair<string, int>> Left(List<string> found, System.Func<string, bool> edible, float tripSeconds)
		{
			int eat = Eaten(found.FindAll((string item) => edible(item)).Count, tripSeconds);
			Dictionary<string, int> left = new Dictionary<string, int>();
			List<string> order = new List<string>();
			foreach (string item in found)
			{
				if (eat > 0 && edible(item))
				{
					eat--;
					continue;
				}
				if (!left.ContainsKey(item))
				{
					left[item] = 0;
					order.Add(item);
				}
				left[item]++;
			}
			return(order.ConvertAll((string item) => new KeyValuePair<string, int>(item, left[item])));
		}

		/// <summary>
		/// Checks whether a quest only picks on the way home - mining and raiding, which go out for what they bring back
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>True when it does</returns>
		internal static bool OnlyHomeward(QuestKind kind)
		{
			switch (kind)
			{
				case QuestKind.MineCopper:
				case QuestKind.MineTin:
				case QuestKind.MineSilver:
				case QuestKind.RaidCrypts:
				case QuestKind.RaidBurialChambers:
				case QuestKind.HuntGhosts:
				case QuestKind.RaidFrostCaves:
					return(true);
				default:
					return(false);
			}
		}

		/// <summary>
		/// Gives each viking of a group what it found on its trip, in its inventory as it's kept on the road - once, as the
		/// group is due at the end of its trip - on the server, which has the world's plan and its items
		/// </summary>
		/// <param name="entry">The group</param>
		/// <returns>How many things each viking was given, in the order of the group's vikings</returns>
		public static int[] Give(TravelEntry entry)
		{
			int[] given = new int[entry.Vikings.Count];
			TravelJourney journey = entry.Journey;
			if (entry.Foraged || journey == null || WorldGenerator.instance == null || ObjectDB.instance == null)
			{
				return(given);
			}
			entry.Foraged = true;
			int first = ((entry.OnQuest && OnlyHomeward((QuestKind)entry.QuestKind)) ? journey.FirstHomeward() : 0);
			Dictionary<int, float> seconds = new Dictionary<int, float>();
			journey.FootLegs(first, (Vector3 middle, float legSeconds) =>
			{
				int biome = VikingGear.IndexOf(WorldGenerator.instance.GetBiome(middle));
				seconds[biome] = (seconds.TryGetValue(biome, out float before) ? before : 0f) + legSeconds;
			});
			float tripSeconds = entry.Spent + entry.Remaining;
			bool hunting = (entry.OnQuest && (QuestKind)entry.QuestKind == QuestKind.Hunt);
			System.Random rng = new System.Random();
			int total = 0;
			for (int i = 0; i < entry.Vikings.Count; i++)
			{
				int own = OwnBiome(entry.Vikings[i], entry.Biome);
				List<KeyValuePair<string, int>> left = Left(Pick(seconds, own, () => Random.value), Edible, tripSeconds);
				if (hunting)
				{
					Dictionary<string, int> drops = new Dictionary<string, int>();
					foreach (string prey in PickPrey(seconds, own, (int biome) => QuestLoot.PreyFor(biome, null).ToArray(), () => Random.value))
					{
						// A prey that takes longer to hunt is taken that much less often along the way
						if (rng.NextDouble() < 1.0 / System.Math.Max(1f, QuestLoot.EffortOf(prey)))
						{
							QuestLoot.RollKill(prey, rng, drops);
						}
					}
					foreach (KeyValuePair<string, int> drop in drops)
					{
						left.Add(new KeyValuePair<string, int>(drop.Key, QuestGame.Scale(drop.Key, drop.Value)));
					}
				}
				if (left.Count == 0)
				{
					continue;
				}
				byte[] added = AddTo(entry.Vikings[i], left);
				if (added != null)
				{
					entry.Vikings[i] = added;
					left.ForEach((KeyValuePair<string, int> item) => given[i] += item.Value);
					total += given[i];
				}
			}
			if (total > 0)
			{
				Plugin.Log.LogInfo("Travel: " + entry.Vikings.Count + " vikings on their way to " + entry.Destination.ToString("F0") + " picked " + total + " things along the road");
				TestLog.Count("Travel", "things picked on the road", total);
			}
			return(given);
		}

		/// <summary>
		/// Gets a viking's own biome from the values it's kept with on the road
		/// </summary>
		/// <param name="snapshot">The viking's values (see VillageDeparted.SnapshotOf)</param>
		/// <param name="fallback">The biome when the values don't say, by VikingGear's order</param>
		/// <returns>The biome, by VikingGear's order</returns>
		private static int OwnBiome(byte[] snapshot, int fallback)
		{
			VillageDeparted.VikingValues values = VillageDeparted.Decode(snapshot);
			int at = ((values != null) ? values.Bytes.FindIndex((KeyValuePair<int, byte[]> value) => value.Key == VikingCore.DataKey) : -1);
			VikingData data = ((at >= 0) ? VikingData.FromBytes(values.Bytes[at].Value) : null);
			return((data != null && data.Biome >= 0) ? data.Biome : fallback);
		}

		/// <summary>
		/// Checks whether an item can be eaten
		/// </summary>
		/// <param name="item">Its prefab name</param>
		/// <returns>True when it can, false when it can't or the items can't be read</returns>
		internal static bool Edible(string item)
		{
			if (ObjectDB.instance == null)
			{
				return(false);
			}
			GameObject prefab = ObjectDB.instance.GetItemPrefab(item);
			ItemDrop drop = ((prefab != null) ? prefab.GetComponent<ItemDrop>() : null);
			return(drop != null && drop.m_itemData.m_shared.m_food > 0f);
		}

		/// <summary>
		/// Adds items to the inventory a viking is kept with on the road
		/// </summary>
		/// <param name="snapshot">The viking's values (see VillageDeparted.SnapshotOf)</param>
		/// <param name="items">The items, by prefab name, with how many of each</param>
		/// <returns>The viking's values with the items added, or null when they couldn't be read</returns>
		private static byte[] AddTo(byte[] snapshot, List<KeyValuePair<string, int>> items)
		{
			VillageDeparted.VikingValues values = VillageDeparted.Decode(snapshot);
			if (values == null)
			{
				return(null);
			}
			int at = values.Bytes.FindIndex((KeyValuePair<int, byte[]> value) => value.Key == VikingCore.DataKey);
			VikingData data = ((at >= 0) ? VikingData.FromBytes(values.Bytes[at].Value) : null);
			if (data == null)
			{
				return(null);
			}
			Inventory inventory = new Inventory("trip", null, data.Width, data.Height);
			int held = values.Bytes.FindIndex((KeyValuePair<int, byte[]> value) => value.Key == VikingCore.InventoryKey);
			if (held >= 0 && values.Bytes[held].Value != null)
			{
				inventory.Load(new ZPackage(values.Bytes[held].Value));
			}
			foreach (KeyValuePair<string, int> item in items)
			{
				inventory.AddItem(item.Key, item.Value, 1, 0, 0L, "", false);
			}
			ZPackage pkg = new ZPackage();
			inventory.Save(pkg);
			KeyValuePair<int, byte[]> saved = new KeyValuePair<int, byte[]>(VikingCore.InventoryKey, pkg.GetArray());
			if (held >= 0)
			{
				values.Bytes[held] = saved;
			}
			else
			{
				values.Bytes.Add(saved);
			}
			return(VillageDeparted.Encode(values, new HashSet<int>()));
		}
	}
}
