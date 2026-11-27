using System.Collections.Generic;

namespace Kriona.Vikings.Tests
{
	/// <summary>
	/// Tests how hard a monster's attacks hit and how far they reach
	/// </summary>
	public class MonsterThreatTests
	{
		/// <summary>
		/// Checks that each star past the first makes a monster hit half as hard again, and a level below 1 counts as one
		/// star
		/// </summary>
		/// <param name="level">The monster's level</param>
		/// <param name="expected">The factor its hit is multiplied by</param>
		[Theory]
		[InlineData(0, 1f)]
		[InlineData(1, 1f)]
		[InlineData(2, 1.5f)]
		[InlineData(3, 2f)]
		[InlineData(5, 3f)]
		public void StarsMakeItHitHarder(int level, float expected)
		{
			Assert.Equal(expected, MonsterThreat.LevelFactor(level), 3);
		}

		/// <summary>
		/// Checks that an attack's hit counts every kind of damage that harms a creature, times the attack's multiplier,
		/// and leaves out chop and pickaxe damage
		/// </summary>
		[Fact]
		public void CountsDamageThatHarmsCreatures()
		{
			HitData.DamageTypes damage = new HitData.DamageTypes
			{
				m_damage = 1f,
				m_blunt = 2f,
				m_slash = 3f,
				m_pierce = 4f,
				m_fire = 5f,
				m_frost = 6f,
				m_lightning = 7f,
				m_poison = 8f,
				m_spirit = 9f,
				m_chop = 100f,
				m_pickaxe = 1000f
			};
			Assert.Equal(90f, MonsterThreat.SwingHit(damage, 2f), 3);
		}

		/// <summary>
		/// Checks that a shot hits with the weapon's damage and the ammo that adds the most to it, times the attack's
		/// multiplier
		/// </summary>
		[Fact]
		public void ShootsWithTheBestAmmo()
		{
			HitData.DamageTypes bow = new HitData.DamageTypes { m_pierce = 50f, m_poison = 10f };
			List<HitData.DamageTypes> ammo = new List<HitData.DamageTypes>
			{
				new HitData.DamageTypes { m_pierce = 42f },
				new HitData.DamageTypes { m_pierce = 52f }
			};
			Assert.Equal(112f, MonsterThreat.ShotHit(bow, ammo, 1f), 3);
			Assert.Equal(224f, MonsterThreat.ShotHit(bow, ammo, 2f), 3);
		}

		/// <summary>
		/// Checks that a weapon with no ammo for it hits with its own damage alone
		/// </summary>
		[Fact]
		public void ShootsWithTheWeaponAloneWithNoAmmo()
		{
			HitData.DamageTypes spear = new HitData.DamageTypes { m_pierce = 75f };
			Assert.Equal(75f, MonsterThreat.ShotHit(spear, new List<HitData.DamageTypes>(), 1f), 3);
		}

		/// <summary>
		/// Checks that a viking's hit hand to hand is scaled by its MeleeScale and its skill, the way it deals it - a
		/// Mountains zombie's Fang Spear at skill 44 hits about 15% harder than the spear alone
		/// </summary>
		[Fact]
		public void ScalesAVikingsMeleeHitBySkillAndMeleeScale()
		{
			// 0.4 + 0.6 * 0.44 = 0.664 of the damage, times 1.75
			Assert.Equal(75f * 0.664f * 1.75f, MonsterThreat.VikingHit(75f, false, 1.75f, 0.44f), 3);
			Assert.Equal(75f * 1.75f, MonsterThreat.VikingHit(75f, false, 1.75f, 1f), 3);
		}

		/// <summary>
		/// Checks that a viking's shot is scaled by its skill but not by its MeleeScale, and that a skill out of range
		/// counts as none or full
		/// </summary>
		[Fact]
		public void ScalesAVikingsShotBySkillAlone()
		{
			Assert.Equal(112f * 0.664f, MonsterThreat.VikingHit(112f, true, 1.75f, 0.44f), 3);
			Assert.Equal(112f * VikingCore.SkillDamageBase, MonsterThreat.VikingHit(112f, true, 1.75f, -1f), 3);
			Assert.Equal(112f, MonsterThreat.VikingHit(112f, true, 1.75f, 2f), 3);
		}

		/// <summary>
		/// Checks that a monster with no attacks hits for nothing and reaches DefaultReach
		/// </summary>
		[Fact]
		public void WeighsNoAttacks()
		{
			KeyValuePair<float, float> weighed = MonsterThreat.Weigh(new List<MonsterThreat.Swing>());
			Assert.Equal(0f, weighed.Key, 3);
			Assert.Equal(MonsterThreat.DefaultReach, weighed.Value, 3);
		}

		/// <summary>
		/// Checks that the hardest hit is taken from every attack, a thrown one too, while the reach comes only from those
		/// that don't shoot or throw
		/// </summary>
		[Fact]
		public void TakesTheHardestHitAndTheFarthestReachHandToHand()
		{
			List<MonsterThreat.Swing> swings = new List<MonsterThreat.Swing>
			{
				new MonsterThreat.Swing { Hit = 40f, Range = 2.5f },
				new MonsterThreat.Swing { Hit = 60f, Range = 30f, Projectile = true },
				new MonsterThreat.Swing { Hit = 25f, Range = 3.5f }
			};
			KeyValuePair<float, float> weighed = MonsterThreat.Weigh(swings);
			Assert.Equal(60f, weighed.Key, 3);
			Assert.Equal(3.5f, weighed.Value, 3);
		}

		/// <summary>
		/// Checks that a monster that only shoots or throws reaches DefaultReach hand to hand
		/// </summary>
		[Fact]
		public void FallsBackToTheDefaultReachWhenItOnlyShoots()
		{
			List<MonsterThreat.Swing> swings = new List<MonsterThreat.Swing>
			{
				new MonsterThreat.Swing { Hit = 30f, Range = 20f, Projectile = true }
			};
			KeyValuePair<float, float> weighed = MonsterThreat.Weigh(swings);
			Assert.Equal(30f, weighed.Key, 3);
			Assert.Equal(MonsterThreat.DefaultReach, weighed.Value, 3);
		}
	}
}
