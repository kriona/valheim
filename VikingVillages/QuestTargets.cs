using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Where a village's vikings go on a quest - the nearest place of the kind the quest goes to, from the world's
	/// locations, like a burial chamber or Haldor, or else the nearest ground of the quest's biome - on the server, which
	/// has the world's locations
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
		/// Finds where a quest goes
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="item">What it's for, or ""</param>
		/// <param name="from">Where the village stands</param>
		/// <param name="villageBiome">The village's biome, by VikingGear's order</param>
		/// <returns>The spot, or zero when there's none</returns>
		public static Vector3 Find(QuestKind kind, string item, Vector3 from, int villageBiome)
		{
			string location = LocationOf(kind);
			if (location != null && Nearest(location, from, out Vector3 spot))
			{
				return(spot);
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
		/// Finds the nearest of the world's locations of a kind
		/// </summary>
		/// <param name="kind">Part of the location's lowercase name - at its start for "crypt", so a sunken crypt isn't
		/// taken for a burial chamber</param>
		/// <param name="from">Where to look from</param>
		/// <param name="spot">Set to where the nearest stands</param>
		/// <returns>True when there's one</returns>
		private static bool Nearest(string kind, Vector3 from, out Vector3 spot)
		{
			spot = Vector3.zero;
			if (ZoneSystem.instance == null)
			{
				return(false);
			}
			float best = float.MaxValue;
			foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.m_locationInstances.Values)
			{
				ZoneSystem.ZoneLocation location = instance.m_location;
				string name = ((location != null) ? (!string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefabName : location.m_name) : null);
				if (name == null || !Matches(name.ToLowerInvariant(), kind))
				{
					continue;
				}
				float distance = Utils.DistanceXZ(instance.m_position, from);
				if (distance < best)
				{
					best = distance;
					spot = instance.m_position;
				}
			}
			return(best < float.MaxValue);
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
		/// Finds the nearest ground of a biome, in rings out from a spot
		/// </summary>
		/// <param name="biome">The biome</param>
		/// <param name="from">Where to look from</param>
		/// <param name="min">How far out to start looking, in meters</param>
		/// <returns>The spot, or zero when there's none within SearchMax</returns>
		private static Vector3 NearestBiome(Heightmap.Biome biome, Vector3 from, float min)
		{
			if (WorldGenerator.instance == null)
			{
				return(Vector3.zero);
			}
			for (float radius = Mathf.Max(RingStep, min); radius <= SearchMax; radius += RingStep)
			{
				int spots = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * radius / SpotStep));
				for (int i = 0; i < spots; i++)
				{
					float angle = 2f * Mathf.PI * i / spots;
					Vector3 spot = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
					if (WorldGenerator.instance.GetBiome(spot.x, spot.z) == biome)
					{
						return(spot);
					}
				}
			}
			return(Vector3.zero);
		}
	}
}
