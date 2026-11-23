using System.Collections.Generic;
using Kriona.Vikings;

namespace VikingVillages
{
	/// <summary>
	/// The level each crafting station a village has is upgraded to, by the village's biome - high enough to repair the
	/// gear of that biome and every biome before it - and the upgrades that get it there
	/// </summary>
	/// <remarks>
	/// Repairing an item takes the station level its recipe needs to craft it at its first quality, whatever quality
	/// it's been upgraded to since. The levels follow the game's recipes - leather armor and the deer hide cape need a
	/// level 2 workbench, the bone tower shield level 3, bronze a level 1 forge, iron 2, silver 3 and black metal 4,
	/// Mistlands gear a level 1 black forge and galdr table, the Ashlands' flametal and its blood, storm and root
	/// versions a level 4 black forge and their mage gear a level 2 galdr table, and the Deep North's Nord gear a level 4
	/// black forge and the Cape of the Caller a level 4 galdr table. The game counts a station as no more than level 4
	/// for repairing, and no recipe needs more
	/// </remarks>
	internal static class StationUpgrades
	{
		/// <summary>
		/// A crafting station a village is given the upgrades of
		/// </summary>
		internal struct Station
		{
			/// <summary>
			/// The station's prefab
			/// </summary>
			public string Prefab;

			/// <summary>
			/// The level it's upgraded to in each biome, in the order of VikingGear.Biomes - Meadows, Black Forest,
			/// Swamp, Mountain, Plains, Mistlands, Ashlands and Deep North
			/// </summary>
			public int[] Levels;

			/// <summary>
			/// Its upgrades' prefabs in the order they're added, each adding a level - the ones that stand on the floor,
			/// leaving out those that hang on a wall, like the galdr table's feathery wreath
			/// </summary>
			public string[] Extensions;
		}

		/// <summary>
		/// The stations a village's are upgraded by
		/// </summary>
		internal static readonly Station[] Stations = new[]
		{
			new Station
			{
				Prefab = "piece_workbench",
				Levels = new[] { 2, 3, 3, 3, 3, 3, 3, 3 },
				Extensions = new[] { "piece_workbench_ext1", "piece_workbench_ext2" }
			},
			new Station
			{
				Prefab = "forge",
				Levels = new[] { 1, 1, 2, 3, 4, 4, 4, 4 },
				Extensions = new[] { "forge_ext1", "forge_ext2", "forge_ext3" }
			},
			new Station
			{
				Prefab = "blackforge",
				Levels = new[] { 1, 1, 1, 1, 1, 1, 4, 4 },
				Extensions = new[] { "blackforge_ext1", "blackforge_ext2_vise", "blackforge_ext3_metalcutter" }
			},
			new Station
			{
				Prefab = "piece_magetable",
				Levels = new[] { 1, 1, 1, 1, 1, 1, 2, 4 },
				Extensions = new[] { "piece_magetable_ext", "piece_magetable_ext2", "piece_magetable_ext4" }
			}
		};

		/// <summary>
		/// Finds the station a prefab is
		/// </summary>
		/// <param name="prefab">The prefab's name</param>
		/// <param name="station">The station</param>
		/// <returns>False when it isn't one a village's are upgraded by</returns>
		internal static bool Find(string prefab, out Station station)
		{
			foreach (Station candidate in Stations)
			{
				if (candidate.Prefab == prefab)
				{
					station = candidate;
					return(true);
				}
			}
			station = new Station();
			return(false);
		}

		/// <summary>
		/// Gets the level a station is upgraded to in a biome
		/// </summary>
		/// <param name="station">The station</param>
		/// <param name="biome">The biome's index into VikingGear.Biomes, clamped to the table</param>
		/// <returns>The level</returns>
		internal static int Level(Station station, int biome)
		{
			int index = ((biome < 0) ? 0 : ((biome >= station.Levels.Length) ? station.Levels.Length - 1 : biome));
			return(station.Levels[index]);
		}

		/// <summary>
		/// Lists the upgrades a station is still missing to reach its level in a biome - the first of its Extensions it
		/// doesn't have, as many as it's short of the level counting every upgrade it has, since each one the game counts
		/// adds a level whichever it is
		/// </summary>
		/// <param name="station">The station</param>
		/// <param name="biome">The biome's index into VikingGear.Biomes</param>
		/// <param name="present">The prefabs of the upgrades it has, each counted once</param>
		/// <returns>The upgrades' prefabs, in the order they're added</returns>
		internal static List<string> Missing(Station station, int biome, ICollection<string> present)
		{
			List<string> missing = new List<string>();
			int shortBy = Level(station, biome) - 1 - present.Count;
			foreach (string extension in station.Extensions)
			{
				if (missing.Count >= shortBy)
				{
					break;
				}
				if (!present.Contains(extension))
				{
					missing.Add(extension);
				}
			}
			return(missing);
		}
	}
}
