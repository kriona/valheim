using System.Collections.Generic;
using Kriona.Vikings;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests a living viking's forsaken powers - which it knows from its biome, which it picks for a fight or its work,
	/// and how an attack's damage is read
	/// </summary>
	public class VikingPowersTests
	{
		/// <summary>
		/// Checks that a viking knows the powers of every biome up to its own - none in the Meadows, Eikthyr's in the
		/// Black Forest, up to Moder's in the Plains and all of them in the Deep North
		/// </summary>
		/// <param name="biome">The biome's index in VikingGear.Biomes</param>
		/// <param name="expected">The powers it knows, one bit for each</param>
		[Theory]
		[InlineData(0, 0)]
		[InlineData(1, 0b1)]
		[InlineData(2, 0b11)]
		[InlineData(4, 0b1111)]
		[InlineData(7, 0b1111111)]
		[InlineData(-1, 0)]
		[InlineData(12, 0b1111111)]
		public void KnowsPowersUpToItsBiome(int biome, int expected)
		{
			Assert.Equal(expected, VikingPowers.BiomeMask(biome));
		}

		/// <summary>
		/// Checks that each biome's power in the gear tables is the one the biome's mask adds, so a viking of a biome
		/// always knows its own biome's power
		/// </summary>
		[Fact]
		public void BiomeMaskAddsTheBiomesOwnPower()
		{
			for (int biome = 1; biome < VikingGear.Biomes.Length; biome++)
			{
				int added = VikingPowers.BiomeMask(biome) & ~VikingPowers.BiomeMask(biome - 1);
				Assert.Equal(VikingGear.Biomes[biome].Power, VikingPowers.Names[VikingPowers.Highest(added)]);
			}
		}

		/// <summary>
		/// Checks that a power is found by its status effect's name hash, and anything else isn't
		/// </summary>
		[Fact]
		public void FindsPowersByHash()
		{
			Assert.Equal(VikingPowers.Moder, VikingPowers.IndexOf("GP_Moder".GetStableHashCode()));
			Assert.Equal(VikingPowers.Eikthyr, VikingPowers.IndexOf("GP_Eikthyr".GetStableHashCode()));
			Assert.Equal(-1, VikingPowers.IndexOf("Rested".GetStableHashCode()));
		}

		/// <summary>
		/// Checks that the latest power in a set is the one picked, and none for an empty set
		/// </summary>
		/// <param name="powers">The powers, one bit for each</param>
		/// <param name="expected">The latest's index, or -1</param>
		[Theory]
		[InlineData(0, -1)]
		[InlineData(0b1, 0)]
		[InlineData(0b101, 2)]
		[InlineData(0b1000001, 6)]
		public void PicksTheLatestPower(int powers, int expected)
		{
			Assert.Equal(expected, VikingPowers.Highest(powers));
		}

		/// <summary>
		/// Checks that no power is used against a single enemy, or against only the Meadows' creatures, however many
		/// </summary>
		[Fact]
		public void NoPowerForOneEnemyOrTheMeadows()
		{
			VikingPowers.Elements all = VikingPowers.Elements.Poison | VikingPowers.Elements.Fire | VikingPowers.Elements.Frost;
			Assert.Empty(VikingPowers.FightPowers(1, false, all, true));
			Assert.Empty(VikingPowers.FightPowers(0, false, all, true));
			Assert.Empty(VikingPowers.FightPowers(8, true, all, true));
		}

		/// <summary>
		/// Checks that a fight's powers come in the order of the rules - the Queen's for poison, Fader's for fire, Moder's
		/// for frost, then Yagluth's, Bonemass's and the Elder's for many enemies, and the Elder's when hurt
		/// </summary>
		[Fact]
		public void FightPowersFollowTheRules()
		{
			VikingPowers.Elements all = VikingPowers.Elements.Poison | VikingPowers.Elements.Fire | VikingPowers.Elements.Frost;
			Assert.Equal(new List<int> { VikingPowers.Queen, VikingPowers.Fader, VikingPowers.Moder, VikingPowers.Yagluth, VikingPowers.Bonemass, VikingPowers.Elder }, VikingPowers.FightPowers(VikingPowers.ManyEnemies, false, all, true));
			Assert.Equal(new List<int> { VikingPowers.Fader }, VikingPowers.FightPowers(2, false, VikingPowers.Elements.Fire, false));
			Assert.Equal(new List<int> { VikingPowers.Elder }, VikingPowers.FightPowers(2, false, VikingPowers.Elements.None, true));
			Assert.Empty(VikingPowers.FightPowers(VikingPowers.ManyEnemies - 1, false, VikingPowers.Elements.None, false));
			Assert.Equal(new List<int> { VikingPowers.Yagluth, VikingPowers.Bonemass, VikingPowers.Elder }, VikingPowers.FightPowers(VikingPowers.ManyEnemies, false, VikingPowers.Elements.None, false));
		}

		/// <summary>
		/// Checks that work's powers are Yagluth's for farming, Eikthyr's for heading out and the Elder's when hurt, in
		/// that order
		/// </summary>
		[Fact]
		public void WorkPowersFollowTheRules()
		{
			Assert.Equal(new List<int> { VikingPowers.Yagluth, VikingPowers.Eikthyr, VikingPowers.Elder }, VikingPowers.WorkPowers(true, true, true));
			Assert.Equal(new List<int> { VikingPowers.Eikthyr }, VikingPowers.WorkPowers(false, true, false));
			Assert.Empty(VikingPowers.WorkPowers(false, false, false));
		}

		/// <summary>
		/// Checks that poison, fire and frost in an attack's damage are read, and the rest isn't
		/// </summary>
		[Fact]
		public void ReadsAttackElements()
		{
			HitData.DamageTypes damage = new HitData.DamageTypes();
			damage.m_slash = 20f;
			Assert.Equal(VikingPowers.Elements.None, VikingPowers.DamageElements(damage));
			damage.m_poison = 5f;
			damage.m_frost = 3f;
			Assert.Equal(VikingPowers.Elements.Poison | VikingPowers.Elements.Frost, VikingPowers.DamageElements(damage));
			damage.m_fire = 1f;
			Assert.Equal(VikingPowers.Elements.Poison | VikingPowers.Elements.Frost | VikingPowers.Elements.Fire, VikingPowers.DamageElements(damage));
		}
	}
}
