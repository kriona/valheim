using System.Collections.Generic;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a quest's fight is worked out - how long it takes, how often the enemies attack and for how long, and
	/// what their hits deal the vikings through their shields and armor
	/// </summary>
	public class QuestFightTests
	{
		/// <summary>
		/// Makes a viking for a fight against one kind of enemy
		/// </summary>
		/// <param name="swing">What a swing of its deals the enemy</param>
		/// <param name="health">Its most health</param>
		/// <param name="armor">Its armor</param>
		/// <param name="blockPower">Its shield's block power, or 0 without one</param>
		/// <param name="hits">What each of the enemy's attacks deals it at once, before its shield and armor</param>
		/// <returns>The viking</returns>
		private static Fighter Viking(float swing, float health, float armor, float blockPower, params float[] hits)
		{
			Fighter fighter = new Fighter { Health = health, Armor = armor, BlockPower = blockPower };
			fighter.Swings.Add(swing);
			fighter.Blows.Add(new List<float>(hits).ConvertAll((float hit) => new Blow { Direct = hit }));
			return(fighter);
		}

		/// <summary>
		/// Makes a number of vikings alike
		/// </summary>
		/// <param name="count">How many</param>
		/// <param name="make">Makes one</param>
		/// <returns>The vikings</returns>
		private static List<Fighter> Alike(int count, System.Func<Fighter> make)
		{
			List<Fighter> fighters = new List<Fighter>();
			for (int i = 0; i < count; i++)
			{
				fighters.Add(make());
			}
			return(fighters);
		}

		/// <summary>
		/// Makes Eikthyr, as his prefab gives him - 500 health, a gore every 5 seconds, a charge every 25 and a stomp
		/// that strikes all around every 40, and 2 seconds at least between them
		/// </summary>
		/// <returns>Eikthyr</returns>
		private static Enemy Eikthyr()
		{
			Enemy enemy = new Enemy { Health = 500f, MinInterval = 2f };
			enemy.Attacks.Add(new EnemyAttack { Interval = 5f });
			enemy.Attacks.Add(new EnemyAttack { Interval = 25f });
			enemy.Attacks.Add(new EnemyAttack { Interval = 40f, Area = true });
			return(enemy);
		}

		/// <summary>
		/// Makes an enemy with one attack
		/// </summary>
		/// <param name="health">Its health</param>
		/// <param name="interval">How long it waits before attacking again, in seconds</param>
		/// <param name="area">Whether the attack strikes all around</param>
		/// <returns>The enemy</returns>
		private static Enemy OneAttack(float health, float interval, bool area)
		{
			Enemy enemy = new Enemy { Health = health };
			enemy.Attacks.Add(new EnemyAttack { Interval = interval, Area = area });
			return(enemy);
		}

		/// <summary>
		/// Fights one enemy of one kind
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <param name="fighters">The vikings</param>
		/// <returns>How it goes</returns>
		private static FightResult FightOne(Enemy enemy, List<Fighter> fighters)
		{
			return(QuestFight.Fight(new List<Enemy> { enemy }, new List<int> { 0 }, 1, fighters));
		}

		[Fact]
		public void AFightLastsTheEnemysHealthOverTheDamageDealtEachSecond()
		{
			Assert.Equal(50f, QuestFight.FightSeconds(500f, 10f), 3);
			Assert.Equal(QuestFight.MostSeconds, QuestFight.FightSeconds(500f, 0f), 3);
			Assert.Equal(QuestFight.MostSeconds, QuestFight.FightSeconds(1000000f, 1f), 3);
		}

		[Fact]
		public void EachVikingSwingsEverySwingSecondsLandingSomeOfThem()
		{
			FightResult result = FightOne(new Enemy { Health = 500f }, Alike(2, () => Viking(16f, 60f, 0f, 0f)));
			float perSecond = 2f * 16f * QuestFight.LandShare / QuestSim.SwingSeconds;
			Assert.Equal(500f / perSecond, result.Seconds, 3);
		}

		[Fact]
		public void AnEnemyUsesEachAttackAsOftenAsItsIntervalLetsIt()
		{
			List<float> uses = QuestFight.Uses(Eikthyr().Attacks, 2f, 100f);
			Assert.Equal(20f, uses[0], 3);
			Assert.Equal(4f, uses[1], 3);
			Assert.Equal(2.5f, uses[2], 3);
		}

		[Fact]
		public void AnEnemyNeverAttacksMoreOftenThanItsLeastInterval()
		{
			List<EnemyAttack> quick = new List<EnemyAttack> { new EnemyAttack { Interval = 0f }, new EnemyAttack { Interval = 1f } };
			List<float> uses = QuestFight.Uses(quick, 4f, 100f);
			Assert.Equal(25f, uses[0] + uses[1], 3);
			Assert.Equal(uses[0], uses[1], 3);
			List<float> unhurried = QuestFight.Uses(quick, 0f, 100f);
			Assert.Equal(100f / QuestFight.AttackSeconds, unhurried[0] + unhurried[1], 3);
		}

		[Fact]
		public void AWeaponAnEnemyMayHaveIsUsedAsOftenAsItsChanceOfHavingIt()
		{
			// A draugr has an axe 3 times in 4 and a bow otherwise
			List<EnemyAttack> draugr = new List<EnemyAttack> { new EnemyAttack { Interval = 3f, Share = 0.75f }, new EnemyAttack { Interval = 4f, Share = 0.25f } };
			List<float> uses = QuestFight.Uses(draugr, 0f, 60f);
			Assert.Equal(15f, uses[0], 3);
			Assert.Equal(3.75f, uses[1], 3);
		}

		[Fact]
		public void ArmorTakesOffHitsTheWayTheGamesDoes()
		{
			Assert.Equal(12f, QuestFight.Taken(20f, 8f, 0f), 3);
			Assert.Equal(5f, QuestFight.Taken(20f, 20f, 0f), 3);
			Assert.Equal(20f, QuestFight.Taken(20f, 0f, 0f), 3);
			Assert.Equal(0f, QuestFight.Taken(0f, 8f, 0f), 3);
		}

		[Fact]
		public void AShieldBlocksSomeOfTheHitsLikeArmorFirst()
		{
			// Blocked, 20 through a block power of 10 leaves 10, and unblocked it's all 20
			float expected = QuestFight.BlockShare * 10f + (1f - QuestFight.BlockShare) * 20f;
			Assert.Equal(expected, QuestFight.Taken(20f, 0f, 10f), 3);
			Assert.True(QuestFight.Taken(20f, 6f, 10f) < QuestFight.Taken(20f, 6f, 0f));
		}

		[Fact]
		public void AnAttackThatStrikesAllAroundHitsEveryViking()
		{
			float areaHarm = FightOne(OneAttack(500f, 10f, true), Alike(2, () => Viking(10f, 100f, 0f, 0f, 20f))).HarmShares[0];
			float aimedHarm = FightOne(OneAttack(500f, 10f, false), Alike(2, () => Viking(10f, 100f, 0f, 0f, 20f))).HarmShares[0];
			Assert.Equal(areaHarm, aimedHarm * 2f, 2);
		}

		[Fact]
		public void PoisonFromOneAttackOfAnEnemyCountsOnceAtMost()
		{
			// Attacking every 2 seconds for 60, 18 of its 30 attacks land, but its poison only takes once
			Enemy blob = OneAttack(50f, 2f, true);
			List<Blow> poison = new List<Blow> { new Blow { OverTime = 90f } };
			List<Blow> direct = new List<Blow> { new Blow { Direct = 90f } };
			Assert.Equal(90f, QuestFight.HarmFrom(blob, poison, 60f, 3, 0f, 0f), 3);
			Assert.Equal(90f * 18f, QuestFight.HarmFrom(blob, direct, 60f, 3, 0f, 0f), 3);
			// Barely attacking, the poison counts for as much of a dose as lands
			Assert.Equal(90f * 0.6f * 0.5f, QuestFight.HarmFrom(blob, poison, 1f, 3, 0f, 0f), 3);
		}

		[Fact]
		public void MoreVikingsEndAFightSoonerAndEachTakesLess()
		{
			FightResult byTwo = FightOne(Eikthyr(), Alike(2, () => Viking(16f, 60f, 6f, 0f, 20f, 15f, 15f)));
			FightResult byThree = FightOne(Eikthyr(), Alike(3, () => Viking(16f, 60f, 6f, 0f, 20f, 15f, 15f)));
			Assert.True(byThree.Seconds < byTwo.Seconds);
			Assert.True(byThree.HarmShares[0] < byTwo.HarmShares[0]);
		}

		[Fact]
		public void BetterGearHurtsLess()
		{
			float rags = FightOne(Eikthyr(), Alike(2, () => Viking(10f, 50f, 2f, 0f, 20f, 15f, 15f))).HarmShares[0];
			float leather = FightOne(Eikthyr(), Alike(2, () => Viking(20f, 50f, 8f, 10f, 20f, 15f, 15f))).HarmShares[0];
			Assert.True(leather < rags);
		}

		[Fact]
		public void TwoVikingsAgainstEikthyr()
		{
			// Two vikings each swinging for 16, with 6 armor and 60 health, bring 500 health down in 33.3 seconds, in
			// which each takes 2 gores of 20 through its armor - 14 - 0.4 charges and 0.5 stomps of 15 - 9 - for 36.1
			FightResult result = FightOne(Eikthyr(), Alike(2, () => Viking(16f, 60f, 6f, 0f, 20f, 15f, 15f)));
			Assert.Equal(33.33f, result.Seconds, 1);
			Assert.Equal(0.6f, result.HarmShares[0], 3);
			Assert.Equal(0.6f, result.HarmShares[1], 3);
			Assert.Equal(36.1f, result.Harms[0], 0);
			Assert.Equal(36.1f, result.Harms[1], 0);
		}

		[Fact]
		public void VikingsWhoCantHurtTheirEnemyFightTheLongest()
		{
			FightResult result = FightOne(OneAttack(500f, 5f, false), new List<Fighter> { Viking(0f, 60f, 0f, 0f, 20f) });
			Assert.Equal(QuestFight.MostSeconds, result.Seconds, 3);
			Assert.True(result.HarmShares[0] >= 1f);
		}

		[Fact]
		public void EnemiesOfAPackKeepAttackingUntilTheirTurnToGoDown()
		{
			// Three that take 10 seconds each to bring down, in packs of 2: the first attacks for 10, the second for 20,
			// and the third, coming after, for 10 - while the fight takes 30, the time to bring all three down
			List<float> alive = QuestFight.AliveSeconds(new List<int> { 0, 0, 0 }, new List<float> { 10f }, 2, out float seconds);
			Assert.Equal(new List<float> { 10f, 20f, 10f }, alive);
			Assert.Equal(30f, seconds, 3);
			List<float> together = QuestFight.AliveSeconds(new List<int> { 0, 0, 0 }, new List<float> { 10f }, 0, out float allSeconds);
			Assert.Equal(new List<float> { 10f, 20f, 30f }, together);
			Assert.Equal(30f, allSeconds, 3);
		}

		[Fact]
		public void BiggerPacksHurtMore()
		{
			List<Enemy> kinds = new List<Enemy> { OneAttack(100f, 3f, false) };
			List<int> foes = new List<int> { 0, 0, 0, 0 };
			float alone = QuestFight.Fight(kinds, foes, 1, Alike(2, () => Viking(20f, 100f, 0f, 0f, 20f))).HarmShares[0];
			float paired = QuestFight.Fight(kinds, foes, 2, Alike(2, () => Viking(20f, 100f, 0f, 0f, 20f))).HarmShares[0];
			Assert.True(paired > alone);
		}

		[Fact]
		public void EachKindIsWeighedOnItsOwn()
		{
			// A viking who can't hurt the second kind fights it the longest, so it's the second that harms it most
			List<Enemy> kinds = new List<Enemy> { OneAttack(100f, 3f, false), OneAttack(100f, 3f, false) };
			Fighter fighter = Viking(20f, 100f, 0f, 0f, 20f);
			fighter.Swings.Add(0f);
			fighter.Blows.Add(new List<Blow> { new Blow { Direct = 20f } });
			FightResult result = QuestFight.Fight(kinds, new List<int> { 0, 1 }, 1, new List<Fighter> { fighter });
			Assert.Equal(QuestFight.MostSeconds, result.Seconds, 3);
			Assert.True(result.HarmShares[0] > 10f);
		}

		[Fact]
		public void EnemiesComeOneOfEachKindInTurn()
		{
			Assert.Equal(new List<int> { 0, 1, 2, 0, 1, 0, 0 }, QuestFight.Order(new List<int> { 4, 2, 1 }));
			Assert.Equal(new List<int> { 1, 1 }, QuestFight.Order(new List<int> { 0, 2 }));
			Assert.Empty(QuestFight.Order(new List<int>()));
		}

		/// <summary>
		/// Makes a list of creatures and how many of each
		/// </summary>
		/// <param name="pairs">Each creature's prefab name followed by how many</param>
		/// <returns>The list</returns>
		private static List<KeyValuePair<string, int>> Creatures(params object[] pairs)
		{
			List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
			for (int i = 0; i + 1 < pairs.Length; i += 2)
			{
				list.Add(new KeyValuePair<string, int>((string)pairs[i], (int)pairs[i + 1]));
			}
			return(list);
		}

		[Fact]
		public void RaidsFightEveryCreatureOfThePlace()
		{
			Assert.Equal(Creatures("Skeleton", 8, "Skeleton_Poison", 2, "Ghost", 1), QuestFightGame.Fought(QuestKind.RaidBurialChambers, 2));
			Assert.Equal(Creatures("Ghost", 6, "Skeleton", 9, "Skeleton_Poison", 3), QuestFightGame.Fought(QuestKind.HuntGhosts, 3));
			Assert.Equal(Creatures("Draugr", 9, "Draugr_Elite", 2, "Blob", 3), QuestFightGame.Fought(QuestKind.RaidCrypts, 3));
			Assert.Equal(Creatures("Ulv", 9, "Fenring_Cultist", 3, "Bat", 6), QuestFightGame.Fought(QuestKind.RaidFrostCaves, 3));
		}

		[Fact]
		public void QuestsThatKillNothingFightNothing()
		{
			Assert.Equal(Creatures("Eikthyr", 1), QuestFightGame.Fought(QuestKind.KillEikthyr, 2));
			Assert.Empty(QuestFightGame.Fought(QuestKind.MineCopper, 2));
			Assert.Empty(QuestFightGame.Fought(QuestKind.SellTreasure, 2));
		}

		[Theory]
		[InlineData(55.5f, 100f, 0.55f)]
		[InlineData(100f, 100f, 1f)]
		[InlineData(150f, 100f, 1.5f)]
		[InlineData(0.5f, 100f, 0f)]
		[InlineData(10f, 0f, 0f)]
		public void HarmRoundsDownToWholeHundredths(float harm, float health, float expected)
		{
			Assert.Equal(expected, QuestFight.Share(harm, health), 4);
		}
	}
}
