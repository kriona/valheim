using System.Collections.Generic;
using Kriona.Vikings;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the levels a village's crafting stations are upgraded to in each biome, and which upgrades a station is
	/// given to get there
	/// </summary>
	public class StationUpgradesTests
	{
		/// <summary>
		/// Finds a station by its prefab, failing the test when there's none
		/// </summary>
		/// <param name="prefab">The station's prefab</param>
		/// <returns>The station</returns>
		private static StationUpgrades.Station Get(string prefab)
		{
			Assert.True(StationUpgrades.Find(prefab, out StationUpgrades.Station station));
			return(station);
		}

		/// <summary>
		/// Checks that every station has a level for each of VikingGear's biomes, never falling from one biome to the
		/// next, with enough upgrades listed to reach its highest level and none above the 4 the game repairs up to
		/// </summary>
		[Fact]
		public void EveryStationReachesItsLevels()
		{
			foreach (StationUpgrades.Station station in StationUpgrades.Stations)
			{
				Assert.Equal(8, station.Levels.Length);
				for (int i = 0; i < station.Levels.Length; i++)
				{
					Assert.InRange(station.Levels[i], 1, 4);
					if (i > 0)
					{
						Assert.True(station.Levels[i] >= station.Levels[i - 1], station.Prefab + " falls a level in biome " + i);
					}
				}
				Assert.True(station.Extensions.Length >= station.Levels[station.Levels.Length - 1] - 1, station.Prefab + " lists too few upgrades");
			}
		}

		/// <summary>
		/// Checks the levels the gear of each biome needs to be repaired - leather a level 2 workbench in the Meadows and
		/// the bone tower shield level 3 from the Black Forest on, iron a level 2 forge, silver 3 and black metal 4, the
		/// Ashlands a level 4 black forge and level 2 galdr table, and the Deep North a level 4 galdr table
		/// </summary>
		[Fact]
		public void LevelsFollowTheGear()
		{
			Assert.Equal(2, StationUpgrades.Level(Get("piece_workbench"), 0));
			Assert.Equal(3, StationUpgrades.Level(Get("piece_workbench"), 1));
			Assert.Equal(1, StationUpgrades.Level(Get("forge"), 1));
			Assert.Equal(2, StationUpgrades.Level(Get("forge"), 2));
			Assert.Equal(3, StationUpgrades.Level(Get("forge"), 3));
			Assert.Equal(4, StationUpgrades.Level(Get("forge"), 4));
			Assert.Equal(1, StationUpgrades.Level(Get("blackforge"), 5));
			Assert.Equal(4, StationUpgrades.Level(Get("blackforge"), 6));
			Assert.Equal(2, StationUpgrades.Level(Get("piece_magetable"), 6));
			Assert.Equal(4, StationUpgrades.Level(Get("piece_magetable"), 7));
		}

		/// <summary>
		/// Checks that a biome past the table's ends counts as the nearest one in it
		/// </summary>
		[Fact]
		public void BiomesOutsideTheTableAreClamped()
		{
			Assert.Equal(2, StationUpgrades.Level(Get("piece_workbench"), -1));
			Assert.Equal(4, StationUpgrades.Level(Get("forge"), 20));
		}

		/// <summary>
		/// Checks that a station with no upgrades gets the first of its upgrades, as many as its level needs
		/// </summary>
		[Fact]
		public void MissingUpgradesComeInOrder()
		{
			List<string> missing = StationUpgrades.Missing(Get("forge"), 4, new HashSet<string>());
			Assert.Equal(new List<string> { "forge_ext1", "forge_ext2", "forge_ext3" }, missing);
		}

		/// <summary>
		/// Checks that the upgrades a station already has are skipped, and count toward its level whichever they are
		/// </summary>
		[Fact]
		public void PresentUpgradesCount()
		{
			Assert.Equal(new List<string> { "forge_ext1", "forge_ext3" }, StationUpgrades.Missing(Get("forge"), 4, new HashSet<string> { "forge_ext2" }));
			Assert.Equal(new List<string> { "forge_ext1" }, StationUpgrades.Missing(Get("forge"), 4, new HashSet<string> { "forge_ext5", "forge_ext6" }));
		}

		/// <summary>
		/// Checks that a station already at its level, or past it, gets nothing
		/// </summary>
		[Fact]
		public void NothingPastTheLevel()
		{
			Assert.Empty(StationUpgrades.Missing(Get("forge"), 0, new HashSet<string>()));
			Assert.Empty(StationUpgrades.Missing(Get("piece_workbench"), 0, new HashSet<string> { "piece_workbench_ext3" }));
			Assert.Empty(StationUpgrades.Missing(Get("piece_workbench"), 0, new HashSet<string> { "piece_workbench_ext1", "piece_workbench_ext2", "piece_workbench_ext4" }));
		}

		/// <summary>
		/// Checks that a prefab that isn't one of the stations isn't found
		/// </summary>
		[Fact]
		public void OtherPrefabsAreNotStations()
		{
			Assert.False(StationUpgrades.Find("piece_cauldron", out StationUpgrades.Station _));
		}
	}
}
