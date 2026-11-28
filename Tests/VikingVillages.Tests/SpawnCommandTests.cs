using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how the spawn commands read the biome and color they're given
	/// </summary>
	public class SpawnCommandTests
	{
		[Fact]
		public void BiomesAreReadByTheirNamesHoweverWritten()
		{
			Assert.Equal(Heightmap.Biome.Plains, VikingSpawnCommand.ParseBiome("plains"));
			Assert.Equal(Heightmap.Biome.BlackForest, VikingSpawnCommand.ParseBiome("Black_Forest"));
			Assert.Equal(Heightmap.Biome.Mountain, VikingSpawnCommand.ParseBiome("mountains"));
			Assert.Equal(Heightmap.Biome.AshLands, VikingSpawnCommand.ParseBiome("AshLands"));
			Assert.Equal(Heightmap.Biome.DeepNorth, VikingSpawnCommand.ParseBiome("deep-north"));
			Assert.Equal(Heightmap.Biome.None, VikingSpawnCommand.ParseBiome("ocean"));
			Assert.Equal(Heightmap.Biome.None, VikingSpawnCommand.ParseBiome(null));
		}

		[Fact]
		public void ColorsAreReadByTheirNamesHoweverWritten()
		{
			string[] names = { "red", "white", "sky blue", "pink" };
			Assert.Equal(0, ValkyrieSpawnCommand.ParseColor("Red", names));
			Assert.Equal(2, ValkyrieSpawnCommand.ParseColor("skyblue", names));
			Assert.Equal(2, ValkyrieSpawnCommand.ParseColor("sky_blue", names));
			Assert.Equal(-1, ValkyrieSpawnCommand.ParseColor("green", names));
		}
	}
}
