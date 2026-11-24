using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Where a village's vikings could go on a quest - the nearest few places of the kind the quest goes to, from the
	/// world's locations, like a burial chamber or Haldor, or else the nearest ground of the quest's biome each way out
	/// from the village - for the road search to pick the one it gets to soonest (see TravelPlanner), on the server,
	/// which has the world's locations
	/// </summary>
	internal static class QuestTargets
	{
		/// <summary>
		/// How far out the nearest ground of a biome is looked for, how far apart the rings of the search are, how far
		/// apart the spots on each ring, and how far from the village the nearest ground of its own biome is, all in
		/// meters
		/// </summary>
		private const float SearchMax = 6000f;
		private const float RingStep = 64f;
		private const float SpotStep = 64f;
		private const float OwnBiomeMin = 150f;

		/// <summary>
		/// How many of the nearest locations of a kind a quest could go to, how many ways out from the village the nearest
		/// ground of a biome is looked for, and how much farther than the nearest a spot can be to still be tried
		/// </summary>
		private const int LocationChoices = 6;
		private const int BiomeSectors = 8;
		private const float BiomeFarther = 2f;

		/// <summary>
		/// Finds the places a quest could go
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <param name="from">Where the village stands</param>
		/// <param name="villageBiome">The village's biome, by VikingGear's order</param>
		/// <returns>The spots, nearest first, or none when there are none</returns>
		public static List<Vector3> FindAll(QuestKind kind, string item, Vector3 from, int villageBiome)
		{
			string location = LocationOf(kind);
			if (location != null)
			{
				List<Vector3> locations = NearestLocations(location, from);
				if (locations.Count > 0)
				{
					return(locations);
				}
			}
			int biome = BiomeOf(kind, item, villageBiome);
			return(NearestBiome(VikingGear.Get(biome).Biome, from, ((biome == villageBiome) ? OwnBiomeMin : 0f)));
		}

		/// <summary>
		/// Gets the kind of location a quest goes to, as part of the location's lowercase name
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>The name, or null for a quest that goes to ground of a biome</returns>
		internal static string LocationOf(QuestKind kind)
		{
			switch (kind)
			{
				case QuestKind.RaidCrypts:
					return("sunkencrypt");
				case QuestKind.RaidBurialChambers:
					return("crypt");
				case QuestKind.HuntTrolls:
					return("trollcave");
				case QuestKind.HuntGoblins:
					return("goblincamp");
				case QuestKind.SellTreasure:
				case QuestKind.BuyCirclet:
					return("vendor_blackforest");
				case QuestKind.KillEikthyr:
					return("eikthyrnir");
				default:
					return(null);
			}
		}

		/// <summary>
		/// Gets the biome a quest goes to, by VikingGear's order - the ore's, the tree's, or the village's own for hunting
		/// and foraging
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <param name="villageBiome">The village's biome</param>
		/// <returns>The biome</returns>
		internal static int BiomeOf(QuestKind kind, string item, int villageBiome)
		{
			switch (kind)
			{
				case QuestKind.MineCopper:
				case QuestKind.MineTin:
				case QuestKind.RaidBurialChambers:
				case QuestKind.HuntTrolls:
				case QuestKind.SellTreasure:
				case QuestKind.BuyCirclet:
					return(ResourceBook.BlackForest);
				case QuestKind.MineSilver:
					return(ResourceBook.Mountain);
				case QuestKind.RaidCrypts:
					return(ResourceBook.Swamp);
				case QuestKind.HuntGoblins:
					return(ResourceBook.Plains);
				case QuestKind.KillEikthyr:
				case QuestKind.FindBees:
					return(ResourceBook.Meadows);
				case QuestKind.ChopTrees:
					return(TreeBiome(item, villageBiome));
				default:
					return(villageBiome);
			}
		}

		/// <summary>
		/// Gets the biome the trees a kind of wood comes from grow in
		/// </summary>
		/// <param name="item">The wood's prefab name</param>
		/// <param name="villageBiome">The village's biome, for plain wood, which grows everywhere</param>
		/// <returns>The biome, by VikingGear's order</returns>
		internal static int TreeBiome(string item, int villageBiome)
		{
			switch (item)
			{
				case "FineWood":
					return(ResourceBook.Meadows);
				case "RoundLog":
					return(ResourceBook.BlackForest);
				case "ElderBark":
					return(ResourceBook.Swamp);
				default:
					return(villageBiome);
			}
		}

		/// <summary>
		/// Finds the nearest LocationChoices of the world's locations of a kind
		/// </summary>
		/// <param name="kind">Part of the location's lowercase name - at its start for "crypt", so a sunken crypt isn't
		/// taken for a burial chamber</param>
		/// <param name="from">Where to look from</param>
		/// <returns>Where they stand, nearest first</returns>
		private static List<Vector3> NearestLocations(string kind, Vector3 from)
		{
			List<Vector3> found = new List<Vector3>();
			if (ZoneSystem.instance == null)
			{
				return(found);
			}
			foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.m_locationInstances.Values)
			{
				ZoneSystem.ZoneLocation location = instance.m_location;
				string name = ((location != null) ? (!string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefabName : location.m_name) : null);
				if (name != null && Matches(name.ToLowerInvariant(), kind))
				{
					found.Add(instance.m_position);
				}
			}
			return(Nearest(found, from, LocationChoices));
		}

		/// <summary>
		/// Picks the nearest of a list of spots
		/// </summary>
		/// <param name="spots">The spots</param>
		/// <param name="from">Where to measure from</param>
		/// <param name="count">How many to pick</param>
		/// <returns>The nearest, nearest first</returns>
		internal static List<Vector3> Nearest(List<Vector3> spots, Vector3 from, int count)
		{
			List<Vector3> sorted = new List<Vector3>(spots);
			sorted.Sort((Vector3 a, Vector3 b) => Flat(a, from).CompareTo(Flat(b, from)));
			if (sorted.Count > count)
			{
				sorted.RemoveRange(count, sorted.Count - count);
			}
			return(sorted);
		}

		/// <summary>
		/// Measures the distance between two points across the ground, leaving out height
		/// </summary>
		/// <param name="a">One point</param>
		/// <param name="b">The other</param>
		/// <returns>The distance, in meters</returns>
		private static float Flat(Vector3 a, Vector3 b)
		{
			float dx = a.x - b.x;
			float dz = a.z - b.z;
			return(Mathf.Sqrt(dx * dx + dz * dz));
		}

		/// <summary>
		/// Checks whether a location's name is of a kind
		/// </summary>
		/// <param name="name">The location's lowercase name</param>
		/// <param name="kind">Part of the name - at its start for "crypt"</param>
		/// <returns>True when it is</returns>
		internal static bool Matches(string name, string kind)
		{
			return((kind == "crypt") ? name.StartsWith(kind, System.StringComparison.Ordinal) : name.Contains(kind));
		}

		/// <summary>
		/// Finds the nearest ground of a biome each of BiomeSectors ways out from a spot, in rings out from it - stopping
		/// once the rings are BiomeFarther times as far out as the nearest found
		/// </summary>
		/// <param name="biome">The biome</param>
		/// <param name="from">Where to look from</param>
		/// <param name="min">How far out to start looking, in meters</param>
		/// <returns>The spots, nearest first, or none within SearchMax</returns>
		private static List<Vector3> NearestBiome(Heightmap.Biome biome, Vector3 from, float min)
		{
			List<Vector3> found = new List<Vector3>();
			if (WorldGenerator.instance == null)
			{
				return(found);
			}
			bool[] sectors = new bool[BiomeSectors];
			float stopAt = SearchMax;
			for (float radius = Mathf.Max(RingStep, min); radius <= stopAt; radius += RingStep)
			{
				int spots = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * radius / SpotStep));
				for (int i = 0; i < spots; i++)
				{
					int sector = SectorOf(i, spots, BiomeSectors);
					if (sectors[sector])
					{
						continue;
					}
					float angle = 2f * Mathf.PI * i / spots;
					Vector3 spot = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
					if (WorldGenerator.instance.GetBiome(spot.x, spot.z) != biome)
					{
						continue;
					}
					sectors[sector] = true;
					found.Add(spot);
					if (found.Count == 1)
					{
						stopAt = Mathf.Min(SearchMax, radius * BiomeFarther);
					}
				}
			}
			return(found);
		}

		/// <summary>
		/// Gets which of a number of equal slices of a circle a spot on a ring falls in
		/// </summary>
		/// <param name="index">The spot's index on the ring</param>
		/// <param name="spots">How many spots the ring has</param>
		/// <param name="sectors">How many slices the circle is cut into</param>
		/// <returns>The slice's index</returns>
		internal static int SectorOf(int index, int spots, int sectors)
		{
			return(Mathf.Clamp(index * sectors / Mathf.Max(1, spots), 0, sectors - 1));
		}
	}
}
