using System.Collections.Generic;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests the time conversions zombies are timed with, which creatures are kept from rising and how zombies take
	/// damage
	/// </summary>
	public class ZombiesTests
	{
		/// <summary>
		/// Checks that seconds convert to the ticks the game's clock counts in
		/// </summary>
		/// <param name="seconds">The number of seconds</param>
		/// <param name="expected">The number of ticks</param>
		[Theory]
		[InlineData(0f, 0L)]
		[InlineData(0.5f, 5000000L)]
		[InlineData(1f, 10000000L)]
		[InlineData(90f, 900000000L)]
		[InlineData(-2f, -20000000L)]
		public void TicksConvertsSeconds(float seconds, long expected)
		{
			Assert.Equal(expected, Zombies.Ticks(seconds));
		}

		/// <summary>
		/// Checks a prefab name against an ExcludedCreatures list
		/// </summary>
		/// <param name="text">The list</param>
		/// <param name="prefabName">The creature's prefab name</param>
		/// <returns>True when it's excluded</returns>
		private static bool Excluded(string text, string prefabName)
		{
			Zombies.ParseExcluded(text, out HashSet<string> names, out List<string> prefixes);
			return(Zombies.Matches(prefabName, names, prefixes));
		}

		/// <summary>
		/// Checks that a name in the list excludes only that prefab, whatever its case
		/// </summary>
		/// <param name="prefabName">The creature's prefab name</param>
		/// <param name="expected">Whether it's excluded</param>
		[Theory]
		[InlineData("StoneGolem", true)]
		[InlineData("stonegolem", true)]
		[InlineData("STONEGOLEM", true)]
		[InlineData("StoneGolem2", false)]
		[InlineData("Stone", false)]
		[InlineData("Mistile", true)]
		[InlineData("Troll", false)]
		public void ExcludesWholeNamesWhateverTheirCase(string prefabName, bool expected)
		{
			Assert.Equal(expected, Excluded("StoneGolem, Mistile", prefabName));
		}

		/// <summary>
		/// Checks that a name ending in * excludes every prefab starting with it, whatever its case, and the start alone
		/// </summary>
		/// <param name="prefabName">The creature's prefab name</param>
		/// <param name="expected">Whether it's excluded</param>
		[Theory]
		[InlineData("Troll", true)]
		[InlineData("Troll_Summoned", true)]
		[InlineData("troll_ashlands", true)]
		[InlineData("Charred_Melee", true)]
		[InlineData("Charred", false)]
		[InlineData("Greydwarf", false)]
		[InlineData("MountainTroll", false)]
		public void ExcludesPrefixes(string prefabName, bool expected)
		{
			Assert.Equal(expected, Excluded("Troll*, Charred_*", prefabName));
		}

		/// <summary>
		/// Checks that a space before the * isn't part of the start it matches
		/// </summary>
		/// <param name="prefabName">The creature's prefab name</param>
		/// <param name="expected">Whether it's excluded</param>
		[Theory]
		[InlineData("Goblin", true)]
		[InlineData("GoblinShaman", true)]
		[InlineData("GoblinBrute", true)]
		[InlineData("Greydwarf", false)]
		public void PrefixIsTrimmed(string prefabName, bool expected)
		{
			Assert.Equal(expected, Excluded("Goblin *", prefabName));
		}

		/// <summary>
		/// Checks that empty and blank entries are skipped rather than excluding anything
		/// </summary>
		[Fact]
		public void SkipsEmptyEntries()
		{
			Zombies.ParseExcluded(" , ,,Boar,   ,", out HashSet<string> names, out List<string> prefixes);
			Assert.Equal(new HashSet<string> { "Boar" }, names);
			Assert.Empty(prefixes);
			Assert.True(Zombies.Matches("Boar", names, prefixes));
			Assert.False(Zombies.Matches("Neck", names, prefixes));
			Assert.False(Excluded("", "Neck"));
			Assert.False(Excluded("   ", "Neck"));
		}

		/// <summary>
		/// Checks that a lone *, which every name starts with, excludes every creature, as the setting describes
		/// </summary>
		/// <param name="text">The list</param>
		[Theory]
		[InlineData("*")]
		[InlineData(" * ")]
		[InlineData("Boar, *")]
		public void LoneStarExcludesEverything(string text)
		{
			Assert.True(Excluded(text, "Neck"));
			Assert.True(Excluded(text, "Troll"));
			Assert.True(Excluded(text, "Greydwarf_Elite"));
		}

		/// <summary>
		/// Makes a creature's resistances, normal to everything but those given
		/// </summary>
		/// <param name="spirit">To spirit damage</param>
		/// <param name="fire">To fire damage</param>
		/// <param name="pierce">To pierce damage</param>
		/// <returns>The resistances</returns>
		private static HitData.DamageModifiers Creature(HitData.DamageModifier spirit, HitData.DamageModifier fire, HitData.DamageModifier pierce)
		{
			return(new HitData.DamageModifiers { m_spirit = spirit, m_fire = fire, m_pierce = pierce, m_poison = HitData.DamageModifier.VeryResistant, m_blunt = HitData.DamageModifier.Resistant, m_frost = HitData.DamageModifier.Weak });
		}

		/// <summary>
		/// Checks that a zombie is immune to poison, and leaves its creature's other resistances, like blunt and frost,
		/// as they were
		/// </summary>
		[Fact]
		public void UndeadAreImmuneToPoison()
		{
			HitData.DamageModifiers zombie = Zombies.UndeadModifiers(Creature(HitData.DamageModifier.Normal, HitData.DamageModifier.Normal, HitData.DamageModifier.Normal));
			Assert.Equal(HitData.DamageModifier.Immune, zombie.m_poison);
			Assert.Equal(HitData.DamageModifier.Resistant, zombie.m_blunt);
			Assert.Equal(HitData.DamageModifier.Weak, zombie.m_frost);
			Assert.Equal(HitData.DamageModifier.Normal, zombie.m_slash);
			Assert.Equal(HitData.DamageModifier.Normal, zombie.m_lightning);
		}

		/// <summary>
		/// Checks that a zombie is weak to spirit damage, even when its creature was immune to it, or very weak when it
		/// already was
		/// </summary>
		/// <param name="creature">The creature's resistance to spirit</param>
		/// <param name="expected">The zombie's</param>
		[Theory]
		[InlineData(HitData.DamageModifier.Normal, HitData.DamageModifier.Weak)]
		[InlineData(HitData.DamageModifier.Immune, HitData.DamageModifier.Weak)]
		[InlineData(HitData.DamageModifier.Resistant, HitData.DamageModifier.Weak)]
		[InlineData(HitData.DamageModifier.Weak, HitData.DamageModifier.Weak)]
		[InlineData(HitData.DamageModifier.VeryWeak, HitData.DamageModifier.VeryWeak)]
		public void UndeadAreWeakToSpirit(HitData.DamageModifier creature, HitData.DamageModifier expected)
		{
			Assert.Equal(expected, Zombies.UndeadModifiers(Creature(creature, HitData.DamageModifier.Normal, HitData.DamageModifier.Normal)).m_spirit);
		}

		/// <summary>
		/// Checks that a zombie is weak to fire, unless its creature resisted fire or was already weak or very weak to
		/// it
		/// </summary>
		/// <param name="creature">The creature's resistance to fire</param>
		/// <param name="expected">The zombie's</param>
		[Theory]
		[InlineData(HitData.DamageModifier.Normal, HitData.DamageModifier.Weak)]
		[InlineData(HitData.DamageModifier.SlightlyWeak, HitData.DamageModifier.Weak)]
		[InlineData(HitData.DamageModifier.SlightlyResistant, HitData.DamageModifier.Weak)]
		[InlineData(HitData.DamageModifier.Weak, HitData.DamageModifier.Weak)]
		[InlineData(HitData.DamageModifier.VeryWeak, HitData.DamageModifier.VeryWeak)]
		[InlineData(HitData.DamageModifier.Resistant, HitData.DamageModifier.Resistant)]
		[InlineData(HitData.DamageModifier.VeryResistant, HitData.DamageModifier.VeryResistant)]
		[InlineData(HitData.DamageModifier.Immune, HitData.DamageModifier.Immune)]
		public void UndeadAreWeakToFireUnlessItWasResisted(HitData.DamageModifier creature, HitData.DamageModifier expected)
		{
			Assert.Equal(expected, Zombies.UndeadModifiers(Creature(HitData.DamageModifier.Normal, creature, HitData.DamageModifier.Normal)).m_fire);
		}

		/// <summary>
		/// Checks that a zombie is a step more resistant to pierce damage than its creature, from very weak up to
		/// resistant, and left as it was when the creature already resisted it
		/// </summary>
		/// <param name="creature">The creature's resistance to pierce</param>
		/// <param name="expected">The zombie's</param>
		[Theory]
		[InlineData(HitData.DamageModifier.VeryWeak, HitData.DamageModifier.Weak)]
		[InlineData(HitData.DamageModifier.Weak, HitData.DamageModifier.Normal)]
		[InlineData(HitData.DamageModifier.SlightlyWeak, HitData.DamageModifier.Normal)]
		[InlineData(HitData.DamageModifier.Normal, HitData.DamageModifier.Resistant)]
		[InlineData(HitData.DamageModifier.SlightlyResistant, HitData.DamageModifier.SlightlyResistant)]
		[InlineData(HitData.DamageModifier.Resistant, HitData.DamageModifier.Resistant)]
		[InlineData(HitData.DamageModifier.VeryResistant, HitData.DamageModifier.VeryResistant)]
		[InlineData(HitData.DamageModifier.Immune, HitData.DamageModifier.Immune)]
		public void UndeadShrugOffArrows(HitData.DamageModifier creature, HitData.DamageModifier expected)
		{
			Assert.Equal(expected, Zombies.UndeadModifiers(Creature(HitData.DamageModifier.Normal, HitData.DamageModifier.Normal, creature)).m_pierce);
		}
	}
}
