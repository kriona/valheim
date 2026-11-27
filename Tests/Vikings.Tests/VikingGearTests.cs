using System.Collections.Generic;

namespace Kriona.Vikings.Tests
{
	/// <summary>
	/// Tests the table of what vikings carry, wear and are worth in each biome, and how a biome is looked up in it
	/// </summary>
	public class VikingGearTests
	{
		/// <summary>
		/// Checks that every PC viking wears a helmet from the Mountains on, and not before
		/// </summary>
		[Fact]
		public void HelmetsFromTheMountainsOn()
		{
			int mountains = VikingGear.IndexOf(Heightmap.Biome.Mountain);
			for (int i = 0; i < VikingGear.Biomes.Length; i++)
			{
				Assert.Equal(i >= mountains, VikingGear.AlwaysHelmeted(i));
			}
		}

		/// <summary>
		/// Checks that every armor set of a biome whose PC vikings always wear a helmet has one, since a PC
		/// viking wears its set's
		/// </summary>
		[Fact]
		public void HelmetedBiomesHaveHelmets()
		{
			for (int i = 0; i < VikingGear.Biomes.Length; i++)
			{
				if (VikingGear.AlwaysHelmeted(i))
				{
					Assert.All(VikingGear.Biomes[i].Armor, (ArmorSet set) => Assert.NotNull(set.Helmet));
				}
			}
		}

		/// <summary>
		/// Checks that every viking wears a cape from the Mountains on, and not before
		/// </summary>
		[Fact]
		public void CapesFromTheMountainsOn()
		{
			int mountains = VikingGear.IndexOf(Heightmap.Biome.Mountain);
			for (int i = 0; i < VikingGear.Biomes.Length; i++)
			{
				Assert.Equal(i >= mountains, VikingGear.AlwaysCaped(i));
			}
		}

		/// <summary>
		/// Checks that every biome whose vikings always wear a cape has one of its own, for a viking whose armor set has
		/// none or who wears no armor
		/// </summary>
		[Fact]
		public void CapedBiomesHaveCapes()
		{
			for (int i = 0; i < VikingGear.Biomes.Length; i++)
			{
				if (VikingGear.AlwaysCaped(i))
				{
					Assert.NotNull(VikingGear.Biomes[i].Cape);
				}
			}
		}

		/// <summary>
		/// Checks that every biome in the table is found at its own place
		/// </summary>
		[Fact]
		public void IndexOfFindsEachBiome()
		{
			for (int i = 0; i < VikingGear.Biomes.Length; i++)
			{
				Assert.Equal(i, VikingGear.IndexOf(VikingGear.Biomes[i].Biome));
			}
		}

		/// <summary>
		/// Checks that a biome with no gear of its own, like the ocean, counts as the Meadows
		/// </summary>
		[Fact]
		public void IndexOfFallsBackToMeadows()
		{
			Assert.Equal(0, VikingGear.IndexOf(Heightmap.Biome.Ocean));
			Assert.Equal(0, VikingGear.IndexOf(Heightmap.Biome.None));
			Assert.Equal(Heightmap.Biome.Meadows, VikingGear.Biomes[0].Biome);
		}

		/// <summary>
		/// Checks that an index outside the table is clamped to its nearest end
		/// </summary>
		[Fact]
		public void GetClampsTheIndex()
		{
			Assert.Same(VikingGear.Biomes[0], VikingGear.Get(-1));
			Assert.Same(VikingGear.Biomes[VikingGear.Biomes.Length - 1], VikingGear.Get(VikingGear.Biomes.Length));
		}

		/// <summary>
		/// Checks that only the Mountains and the Deep North freeze
		/// </summary>
		[Fact]
		public void OnlyColdBiomesFreeze()
		{
			for (int i = 0; i < VikingGear.Biomes.Length; i++)
			{
				Heightmap.Biome biome = VikingGear.Biomes[i].Biome;
				bool cold = (biome == Heightmap.Biome.Mountain || biome == Heightmap.Biome.DeepNorth);
				Assert.Equal(cold, VikingGear.Freezes(i));
			}
		}

		/// <summary>
		/// Checks that each biome is in the table once
		/// </summary>
		[Fact]
		public void BiomesAreListedOnce()
		{
			HashSet<Heightmap.Biome> seen = new HashSet<Heightmap.Biome>();
			foreach (BiomeGear gear in VikingGear.Biomes)
			{
				Assert.True(seen.Add(gear.Biome), gear.Biome + " is listed more than once");
			}
		}

		/// <summary>
		/// Checks that every biome's skill and coin ranges run from low to high
		/// </summary>
		[Fact]
		public void RangesRunLowToHigh()
		{
			foreach (BiomeGear gear in VikingGear.Biomes)
			{
				Assert.True(gear.SkillMin <= gear.SkillMax, gear.Biome + " has its skill range backwards");
				Assert.True(gear.CoinsMin <= gear.CoinsMax, gear.Biome + " has its coin range backwards");
				Assert.InRange(gear.SkillMin, 0f, 100f);
				Assert.InRange(gear.SkillMax, 0f, 100f);
			}
		}

		/// <summary>
		/// Checks that vikings get tougher, more skilled and richer biome by biome, in the order the table lists them
		/// </summary>
		[Fact]
		public void EachBiomeIsTougherThanTheLast()
		{
			for (int i = 1; i < VikingGear.Biomes.Length; i++)
			{
				BiomeGear last = VikingGear.Biomes[i - 1];
				BiomeGear gear = VikingGear.Biomes[i];
				Assert.True(gear.Health > last.Health, gear.Biome + " vikings have no more health than " + last.Biome + " vikings");
				Assert.True(gear.SkillMin >= last.SkillMin, gear.Biome + " vikings start less skilled than " + last.Biome + " vikings");
				Assert.True(gear.CoinsMin >= last.CoinsMin, gear.Biome + " vikings carry fewer coins than " + last.Biome + " vikings");
				Assert.True(gear.Unarmed > last.Unarmed, gear.Biome + " vikings hit no harder unarmed than " + last.Biome + " vikings");
			}
		}

		/// <summary>
		/// Checks that every biome has weapons, ammo, shields and armor to hand out, and that every armor set at least
		/// covers the chest and legs
		/// </summary>
		[Fact]
		public void EveryBiomeHasGear()
		{
			foreach (BiomeGear gear in VikingGear.Biomes)
			{
				Assert.NotEmpty(gear.Weapons);
				Assert.NotEmpty(gear.Ammo);
				Assert.NotEmpty(gear.Shields);
				Assert.NotEmpty(gear.Armor);
				Assert.True(gear.Health > 0f, gear.Biome + " vikings have no health");
				Assert.True(gear.MeleeScale > 0f, gear.Biome + " vikings deal no melee damage");
				foreach (ArmorSet armor in gear.Armor)
				{
					Assert.False(string.IsNullOrEmpty(armor.Chest), gear.Biome + " has an armor set with no chest");
					Assert.False(string.IsNullOrEmpty(armor.Legs), gear.Biome + " has an armor set with no legs");
				}
			}
		}

		/// <summary>
		/// Checks that no biome lists the same weapon, ammo or shield twice, which would make it more likely to be
		/// rolled than the rest
		/// </summary>
		[Fact]
		public void NoGearIsListedTwice()
		{
			foreach (BiomeGear gear in VikingGear.Biomes)
			{
				Assert.Distinct(gear.Weapons);
				Assert.Distinct(gear.Ammo);
				Assert.Distinct(gear.Shields);
			}
		}

		/// <summary>
		/// Checks that the weapon kept out of the Deep North is one the Ash Lands hand out, so the rule still has
		/// something to keep out
		/// </summary>
		[Fact]
		public void DeepNorthBanIsAnAshLandsWeapon()
		{
			Assert.Contains(VikingGear.DeepNorthBanned, VikingGear.Get(VikingGear.IndexOf(Heightmap.Biome.AshLands)).Weapons);
		}

		/// <summary>
		/// Checks that every PC viking from the Mountains on has both a bow and a melee weapon, marked or not
		/// </summary>
		[Fact]
		public void PCVikingsFromTheMountainsOnHaveBoth()
		{
			for (int i = VikingGear.IndexOf(Heightmap.Biome.Mountain); i < VikingGear.Biomes.Length; i++)
			{
				foreach (float roll in new[] { 0f, 0.2f, 0.5f, 0.8f, 0.999f })
				{
					Assert.Equal(Arms.Both, VikingGear.PCArms(i, false, roll));
					Assert.Equal(Arms.Both, VikingGear.PCArms(i, true, roll));
				}
			}
		}

		/// <summary>
		/// Counts how many of a spread of evenly spaced rolls give a PC viking of a biome each kind of weapons
		/// </summary>
		/// <param name="biome">The biome</param>
		/// <param name="marked">Whether the viking is marked as its village's archer</param>
		/// <param name="steps">How many rolls to spread from 0 to 1</param>
		/// <returns>How many rolls gave each kind</returns>
		private static Dictionary<Arms, int> CountArms(Heightmap.Biome biome, bool marked, int steps)
		{
			int index = VikingGear.IndexOf(biome);
			Dictionary<Arms, int> counts = new Dictionary<Arms, int>();
			for (int i = 0; i < steps; i++)
			{
				Arms arms = VikingGear.PCArms(index, marked, (i + 0.5f) / steps);
				counts[arms] = (counts.TryGetValue(arms, out int count) ? count + 1 : 1);
			}
			return(counts);
		}

		/// <summary>
		/// Checks that an unmarked PC viking of the Black Forest has only a bow, only a melee weapon or both, a third
		/// of the rolls each
		/// </summary>
		[Fact]
		public void BlackForestVikingsSplitInThirds()
		{
			Dictionary<Arms, int> counts = CountArms(Heightmap.Biome.BlackForest, false, 3000);
			Assert.Equal(3, counts.Count);
			Assert.Equal(1000, counts[Arms.Ranged]);
			Assert.Equal(1000, counts[Arms.Melee]);
			Assert.Equal(1000, counts[Arms.Both]);
		}

		/// <summary>
		/// Checks that an unmarked PC viking of the Swamp has only a bow a fifth of the rolls, only a melee weapon a
		/// fifth and both the rest
		/// </summary>
		[Fact]
		public void SwampVikingsSplitMostlyBoth()
		{
			Dictionary<Arms, int> counts = CountArms(Heightmap.Biome.Swamp, false, 3000);
			Assert.Equal(3, counts.Count);
			Assert.Equal(600, counts[Arms.Ranged]);
			Assert.Equal(600, counts[Arms.Melee]);
			Assert.Equal(1800, counts[Arms.Both]);
		}

		/// <summary>
		/// Checks that a village's marked archer in a split biome always has a bow, alone or with a melee weapon in the
		/// same proportion as the rest of its biome - half and half in the Black Forest, a quarter alone in the Swamp
		/// </summary>
		/// <param name="biome">The biome</param>
		/// <param name="ranged">How many of 1000 rolls give it only a bow</param>
		[Theory]
		[InlineData(Heightmap.Biome.BlackForest, 500)]
		[InlineData(Heightmap.Biome.Swamp, 250)]
		public void MarkedArcherAlwaysHasABow(Heightmap.Biome biome, int ranged)
		{
			Dictionary<Arms, int> counts = CountArms(biome, true, 1000);
			Assert.Equal(2, counts.Count);
			Assert.Equal(ranged, counts[Arms.Ranged]);
			Assert.Equal(1000 - ranged, counts[Arms.Both]);
		}

		/// <summary>
		/// Checks that in the Meadows the marked archer has both and the rest one weapon of any kind
		/// </summary>
		[Fact]
		public void MeadowsKeepsOneArcher()
		{
			int index = VikingGear.IndexOf(Heightmap.Biome.Meadows);
			Assert.Equal(Arms.Both, VikingGear.PCArms(index, true, 0.5f));
			Assert.Equal(Arms.Any, VikingGear.PCArms(index, false, 0.5f));
		}

		/// <summary>
		/// Checks that every split leaves a share for vikings with both weapons, so no roll falls outside it
		/// </summary>
		[Fact]
		public void SplitsLeaveRoomForBoth()
		{
			foreach (VikingGear.ArmsSplit split in VikingGear.ArmsSplits.Values)
			{
				Assert.InRange(split.Ranged + split.Melee, 0f, 1f);
				Assert.True(split.Ranged >= 0f && split.Melee >= 0f);
			}
		}

		/// <summary>
		/// Checks how many of 1000 evenly spaced rolls give a PC viking of each biome a dverger circlet rather than
		/// a torch
		/// </summary>
		/// <param name="biome">The biome</param>
		/// <param name="circlets">How many rolls should give a circlet</param>
		[Theory]
		[InlineData(Heightmap.Biome.Meadows, 0)]
		[InlineData(Heightmap.Biome.BlackForest, 100)]
		[InlineData(Heightmap.Biome.Swamp, 300)]
		[InlineData(Heightmap.Biome.Mountain, 500)]
		[InlineData(Heightmap.Biome.Plains, 700)]
		[InlineData(Heightmap.Biome.Mistlands, 1000)]
		[InlineData(Heightmap.Biome.AshLands, 1000)]
		[InlineData(Heightmap.Biome.DeepNorth, 1000)]
		public void CircletChanceRisesByBiome(Heightmap.Biome biome, int circlets)
		{
			int index = VikingGear.IndexOf(biome);
			int count = 0;
			for (int i = 0; i < 1000; i++)
			{
				if (VikingGear.PCCirclet(index, (i + 0.5f) / 1000f))
				{
					count++;
				}
			}
			Assert.Equal(circlets, count);
		}

		/// <summary>
		/// Checks that only a PC viking of the Mistlands starts with a wisp light
		/// </summary>
		[Fact]
		public void OnlyTheMistlandsStartWithAWisp()
		{
			for (int i = 0; i < VikingGear.Biomes.Length; i++)
			{
				Assert.Equal(VikingGear.Biomes[i].Biome == Heightmap.Biome.Mistlands, VikingGear.PCWisp(i));
			}
		}
	}
}
