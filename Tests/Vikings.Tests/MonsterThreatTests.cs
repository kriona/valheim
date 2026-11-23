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
