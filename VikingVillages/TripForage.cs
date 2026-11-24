using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What vikings out of every player's reach pick along their road - a few of what grows in the biomes they walk
	/// through, more the longer they walk through them, less what they eat on the way - put in their inventory as they
	/// arrive, on the server, which keeps them on the road (see VillageTravel)
	/// </summary>
	/// <remarks>
	/// A viking finds one thing for every SecondsPerFind seconds on foot through a biome where something grows, up to
	/// MostFound, picked by how long it spent in each biome, and eats one of what it found that it can eat for every
	/// EatEvery seconds of the trip, before any food of its own - so a long trip often brings back only one or two. Vikings
	/// out to mine or raid only pick on the way home, once they have what they went for. The numbers are small on
	/// purpose: a trip brings back a handful, never stacks
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
		/// Picks what a viking finds, each thing from a biome picked by how long it spent there
		/// </summary>
		/// <param name="seconds">How many seconds it spent on foot in each biome, by VikingGear's order</param>
		/// <param name="roll">Gives a random number from 0 to 1</param>
		/// <returns>What it found, a prefab name for each thing</returns>
		internal static List<string> Pick(Dictionary<int, float> seconds, System.Func<float> roll)
		{
			float growing = 0f;
			foreach (KeyValuePair<int, float> entry in seconds)
			{
				if (GrowsIn(entry.Key).Length > 0)
				{
					growing += entry.Value;
				}
			}
			List<string> found = new List<string>();
			int count = Found(growing);
			for (int i = 0; i < count; i++)
			{
				float at = roll() * growing;
				foreach (KeyValuePair<int, float> entry in seconds)
				{
					string[] grows = GrowsIn(entry.Key);
					if (grows.Length == 0)
					{
						continue;
					}
					if (at < entry.Value)
					{
						found.Add(grows[Mathf.Clamp(Mathf.FloorToInt(roll() * grows.Length), 0, grows.Length - 1)]);
						break;
					}
					at -= entry.Value;
				}
			}
			// A roll of exactly 1 falls past the end, so the last biome takes it
			while (found.Count < count)
			{
				foreach (KeyValuePair<int, float> entry in seconds)
				{
					if (GrowsIn(entry.Key).Length > 0)
					{
						found.Add(GrowsIn(entry.Key)[0]);
						break;
					}
				}
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
			int total = 0;
			for (int i = 0; i < entry.Vikings.Count; i++)
			{
				List<KeyValuePair<string, int>> left = Left(Pick(seconds, () => Random.value), Edible, tripSeconds);
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
		/// Checks whether an item can be eaten
		/// </summary>
		/// <param name="item">Its prefab name</param>
		/// <returns>True when it can</returns>
		private static bool Edible(string item)
		{
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
