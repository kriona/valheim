namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the chance of silver gear for a viking of each biome, and how silver arrows are weighed against the undead
	/// </summary>
	public class VikingSilverTests
	{
		/// <summary>
		/// Makes the resistances Zombie Apocalypse gives a zombie of a creature that resists nothing - resistant to
		/// pierce, weak to spirit and fire, and immune to poison
		/// </summary>
		/// <returns>The resistances</returns>
		private static HitData.DamageModifiers Undead()
		{
			HitData.DamageModifiers undead = new HitData.DamageModifiers();
			undead.m_pierce = HitData.DamageModifier.Resistant;
			undead.m_spirit = HitData.DamageModifier.Weak;
			undead.m_fire = HitData.DamageModifier.Weak;
			undead.m_poison = HitData.DamageModifier.Immune;
			return(undead);
		}

		/// <summary>
		/// Makes arrow or bow damage
		/// </summary>
		/// <param name="pierce">Its pierce damage</param>
		/// <param name="frost">Its frost damage</param>
		/// <param name="spirit">Its spirit damage</param>
		/// <returns>The damage</returns>
		private static HitData.DamageTypes Damage(float pierce, float frost, float spirit)
		{
			HitData.DamageTypes damage = new HitData.DamageTypes();
			damage.m_pierce = pierce;
			damage.m_frost = frost;
			damage.m_spirit = spirit;
			return(damage);
		}

		/// <summary>
		/// Checks that vikings before the Mountains never start with silver gear, the Mountains, Plains and Mistlands
		/// sometimes do, more often the harder the biome, and the Ashlands and Deep North always do
		/// </summary>
		[Fact]
		public void ChanceRisesWithTheBiome()
		{
			Assert.Equal(0f, VikingSilver.LiningChance(0));
			Assert.Equal(0f, VikingSilver.LiningChance(1));
			Assert.Equal(0f, VikingSilver.LiningChance(2));
			Assert.Equal(0.25f, VikingSilver.LiningChance(3));
			Assert.Equal(0.5f, VikingSilver.LiningChance(4));
			Assert.Equal(0.75f, VikingSilver.LiningChance(5));
			Assert.Equal(1f, VikingSilver.LiningChance(6));
			Assert.Equal(1f, VikingSilver.LiningChance(7));
		}

		/// <summary>
		/// Checks that a hit's damage goes through resistances the way the game applies them
		/// </summary>
		[Fact]
		public void HurtAppliesResistances()
		{
			Assert.Equal(50f + 30f, VikingSilver.Hurt(Damage(100f, 0f, 20f), Undead()), 3);
			Assert.Equal(100f, VikingSilver.Hurt(Damage(100f, 0f, 0f), new HitData.DamageModifiers()), 3);
		}

		/// <summary>
		/// Checks that silver arrows beat obsidian arrows of the same pierce against the undead, but not frost arrows,
		/// whose frost the undead don't resist
		/// </summary>
		[Fact]
		public void SilverBeatsPierceButNotFrost()
		{
			HitData.DamageTypes bow = Damage(40f, 0f, 0f);
			HitData.DamageTypes silver = Damage(52f, 0f, 20f);
			Assert.True(VikingSilver.BeatsUndead(bow, Damage(52f, 0f, 0f), silver, Undead()));
			Assert.False(VikingSilver.BeatsUndead(bow, Damage(26f, 52f, 0f), silver, Undead()));
		}

		/// <summary>
		/// Checks that silver arrows only beat arrows that hurt as much when spirit damage hurts more than nothing - so a
		/// creature immune to spirit gets no silver arrows shot at it for nothing
		/// </summary>
		[Fact]
		public void SilverNeedsSpiritToCount()
		{
			HitData.DamageTypes bow = Damage(40f, 0f, 0f);
			HitData.DamageModifiers living = new HitData.DamageModifiers();
			living.m_spirit = HitData.DamageModifier.Immune;
			Assert.False(VikingSilver.BeatsUndead(bow, Damage(52f, 0f, 0f), Damage(52f, 0f, 20f), living));
		}
	}
}
