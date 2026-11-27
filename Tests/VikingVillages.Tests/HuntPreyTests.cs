using System.Collections.Generic;
using Kriona.Vikings;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the prey PC vikings hunt in each biome, and how far past its own biome a viking hunts
	/// </summary>
	public class HuntPreyTests
	{
		/// <summary>
		/// Finds a biome's place in the gear tables
		/// </summary>
		/// <param name="biome">The biome</param>
		/// <returns>Its index</returns>
		private static int Index(Heightmap.Biome biome)
		{
			return(VikingGear.IndexOf(biome));
		}

		/// <summary>
		/// Checks that a Black Forest viking hunts the Meadows' and the Black Forest's prey, but not the Swamp's or
		/// anything after
		/// </summary>
		[Fact]
		public void VikingHuntsNoBiomeAhead()
		{
			int forest = Index(Heightmap.Biome.BlackForest);
			Assert.True(HuntPrey.InReach(Index(Heightmap.Biome.Meadows), forest));
			Assert.True(HuntPrey.InReach(Index(Heightmap.Biome.BlackForest), forest));
			Assert.False(HuntPrey.InReach(Index(Heightmap.Biome.Swamp), forest));
			Assert.False(HuntPrey.InReach(Index(Heightmap.Biome.Mountain), forest));
		}

		/// <summary>
		/// Checks that a viking still hunts the prey of every biome before its own
		/// </summary>
		[Fact]
		public void VikingHuntsEarlierBiomes()
		{
			int deepNorth = Index(Heightmap.Biome.DeepNorth);
			for (int i = 0; i <= deepNorth; i++)
			{
				Assert.True(HuntPrey.InReach(i, deepNorth));
			}
		}

		/// <summary>
		/// Checks that a Meadows viking hunts boar, a Meadows archer deer too, and neither goes after the Black Forest's
		/// greylings, wolves or lox
		/// </summary>
		[Fact]
		public void MeadowsHuntersKeepToTheirPrey()
		{
			int meadows = Index(Heightmap.Biome.Meadows);
			Assert.True(HuntPrey.Hunts("Boar", false, meadows));
			Assert.False(HuntPrey.Hunts("Greyling", false, meadows));
			Assert.True(HuntPrey.Hunts("Greyling", false, Index(Heightmap.Biome.BlackForest)));
			Assert.False(HuntPrey.Hunts("Deer", false, meadows));
			Assert.True(HuntPrey.Hunts("Deer", true, meadows));
			Assert.False(HuntPrey.Hunts("Wolf", true, meadows));
			Assert.False(HuntPrey.Hunts("Lox", true, meadows));
		}

		/// <summary>
		/// Checks that a Mountains viking hunts wolves, but not lox from the Plains, while a Plains viking does
		/// </summary>
		[Fact]
		public void MountainsVikingHuntsWolvesNotLox()
		{
			int mountains = Index(Heightmap.Biome.Mountain);
			Assert.True(HuntPrey.Hunts("Wolf", false, mountains));
			Assert.False(HuntPrey.Hunts("Lox", false, mountains));
			Assert.True(HuntPrey.Hunts("Lox", false, Index(Heightmap.Biome.Plains)));
		}

		/// <summary>
		/// Checks that what isn't on the list - a monster, or the young of a herd - isn't hunted at all
		/// </summary>
		/// <param name="prefab">The creature's prefab name</param>
		[Theory]
		[InlineData("Troll")]
		[InlineData("Lox_Calf")]
		[InlineData("Wolf_cub")]
		[InlineData("Draugr_Elite")]
		[InlineData("Draugr_Ranged")]
		public void OthersAreNotPrey(string prefab)
		{
			Assert.False(HuntPrey.Hunts(prefab, true, Index(Heightmap.Biome.DeepNorth)));
		}

		/// <summary>
		/// Checks that a Swamp viking hunts a plain draugr, only one alone - another draugr near it rules the hunt out -
		/// while a Black Forest viking doesn't
		/// </summary>
		[Fact]
		public void SwampVikingHuntsLoneDraugr()
		{
			Assert.True(HuntPrey.Hunts("Draugr", false, Index(Heightmap.Biome.Swamp)));
			Assert.False(HuntPrey.Hunts("Draugr", true, Index(Heightmap.Biome.BlackForest)));
			Assert.False(HuntPrey.IsHarmless("Draugr"));
			Assert.False(HuntPrey.Ignores("Draugr", "Draugr"));
		}

		/// <summary>
		/// Checks that lox and wolves are dangerous, so they're only hunted alone, while boar and hares aren't
		/// </summary>
		[Fact]
		public void HerdsThatFightBackAreDangerous()
		{
			Assert.False(HuntPrey.IsHarmless("Lox"));
			Assert.False(HuntPrey.IsHarmless("Wolf"));
			Assert.True(HuntPrey.IsHarmless("Boar"));
			Assert.True(HuntPrey.IsHarmless("Hare"));
			Assert.False(HuntPrey.IsHarmless("Troll"));
		}

		/// <summary>
		/// Checks that every kind of prey is listed once, and every biome has some
		/// </summary>
		[Fact]
		public void EveryBiomeHasPrey()
		{
			HashSet<string> seen = new HashSet<string>();
			HashSet<Heightmap.Biome> biomes = new HashSet<Heightmap.Biome>();
			foreach (PreyKind kind in HuntPrey.Kinds)
			{
				Assert.True(seen.Add(kind.Prefab));
				biomes.Add(kind.Biome);
			}
			foreach (BiomeGear gear in VikingGear.Biomes)
			{
				Assert.Contains(gear.Biome, biomes);
			}
		}

		/// <summary>
		/// Checks that leeches are dangerous Swamp prey only archers hunt - so a Swamp archer goes after one, and a Black
		/// Forest archer or a Swamp viking without a bow doesn't
		/// </summary>
		[Fact]
		public void LeechesAreForArchersFromTheSwamp()
		{
			Assert.False(HuntPrey.IsHarmless("Leech"));
			Assert.True(HuntPrey.Hunts("Leech", true, Index(Heightmap.Biome.Swamp)));
			Assert.False(HuntPrey.Hunts("Leech", false, Index(Heightmap.Biome.Swamp)));
			Assert.False(HuntPrey.Hunts("Leech", true, Index(Heightmap.Biome.BlackForest)));
		}

		/// <summary>
		/// Checks that a hunter after a leech doesn't mind other leeches, which keep to themselves, while one after a lox
		/// minds other lox, and every hunter minds a draugr and none minds a boar
		/// </summary>
		[Fact]
		public void OnlyHerdsMindTheirOwnKind()
		{
			Assert.True(HuntPrey.Ignores("Leech", "Leech"));
			Assert.False(HuntPrey.Ignores("Lox", "Lox"));
			Assert.False(HuntPrey.Ignores("Leech", "Draugr"));
			Assert.False(HuntPrey.Ignores("Leech", "Lox"));
			Assert.True(HuntPrey.Ignores("Lox", "Boar"));
			Assert.False(HuntPrey.Ignores(null, "Leech"));
		}

		/// <summary>
		/// Checks that leeches, in and out of caves, are left alone until they hurt someone, and other monsters aren't
		/// </summary>
		[Fact]
		public void OnlyLeechesAreQuiet()
		{
			Assert.True(QuietMonsters.IsQuiet("Leech"));
			Assert.True(QuietMonsters.IsQuiet("Leech_cave"));
			Assert.False(QuietMonsters.IsQuiet("Draugr"));
			Assert.False(QuietMonsters.IsQuiet("Blob"));
		}
	}
}
